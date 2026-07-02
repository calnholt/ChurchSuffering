using System;
using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using System.IO;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// On StartEnemyTurn, selects attacks from EnemyArsenal and queues them into AttackIntent.
	/// Publishes IntentPlanned for UI/telegraphing.
	/// </summary>
	public class EnemyIntentPlanningSystem : Core.System
	{
		private int _lastPlannedTurnNumber = -1;

		public EnemyIntentPlanningSystem(EntityManager em) : base(em)
		{
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnStartEnemyTurn);
			EventManager.Subscribe<ApplyPassiveEvent>(OnFearApplied);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Operates on enemies with EnemyArsenal
			return EntityManager.GetEntitiesWithComponent<EnemyArsenal>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnStartEnemyTurn(ChangeBattlePhaseEvent evt)
		{
			LoggingService.Append("EnemyIntentPlanningSystem.OnStartEnemyTurn", new System.Text.Json.Nodes.JsonObject { ["event"] = "ChangeBattlePhaseEvent", ["current"] = evt.Current.ToString() });
			// Reset planning guard when a new battle starts so turn 1 can plan again
			if (evt.Current == SubPhase.StartBattle)
			{
				_lastPlannedTurnNumber = -1;
				return;
			}
			if (evt.Current == SubPhase.EnemyStart)
			{
				// Get the phase state to check previous phase before PhaseCoordinatorSystem updates it
				var phaseStateEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
				var phaseState = phaseStateEntity?.GetComponent<PhaseState>();
				var previousPhase = phaseState?.Sub ?? SubPhase.StartBattle;
				
				// Read current turn number (before PhaseCoordinatorSystem increments it)
				int currentTurnNumber = GetCurrentTurnNumber();
				
				// If coming from a player phase, PhaseCoordinatorSystem will increment the turn
				// So we need to plan for the incremented turn number
				bool isNewTurn = previousPhase == SubPhase.PlayerEnd || previousPhase == SubPhase.Action || previousPhase == SubPhase.PlayerStart;
				int turnNumber = isNewTurn ? currentTurnNumber + 1 : currentTurnNumber;
				
				LoggingService.Append("EnemyIntentPlanningSystem.OnStartEnemyTurn", new System.Text.Json.Nodes.JsonObject { ["currentTurn"] = currentTurnNumber, ["planningTurn"] = turnNumber, ["lastPlannedTurn"] = _lastPlannedTurnNumber, ["previousPhase"] = previousPhase.ToString(), ["isNewTurn"] = isNewTurn });

				// Guard: prevent multiple plans for the same EnemyStart turn
				if (_lastPlannedTurnNumber == turnNumber)
				{
					bool needsReplan = GetRelevantEntities().Any(enemy =>
						(enemy.GetComponent<AttackIntent>()?.Planned.Count ?? 0) == 0);
					if (!needsReplan)
					{
						LoggingService.Append("EnemyIntentPlanningSystem.OnStartEnemyTurn", new System.Text.Json.Nodes.JsonObject { ["action"] = "Skipping - already planned", ["turn"] = turnNumber });
						return;
					}
				}
				LoggingService.Append("EnemyIntentPlanningSystem.OnStartEnemyTurn", new System.Text.Json.Nodes.JsonObject { ["action"] = "Planning intents" });
				foreach (var enemy in GetRelevantEntities())
				{
					var enemyCmp = enemy.GetComponent<Enemy>();
					var arsenal = enemy.GetComponent<EnemyArsenal>();
					if (arsenal == null || arsenal.AttackIds.Count == 0) continue;
					string enemyId = enemyCmp?.EnemyBase?.Id.ToKey() ?? EnemyId.Demon.ToKey();
					var intent = enemy.GetComponent<AttackIntent>();
					if (intent == null)
					{
						intent = new AttackIntent();
						EntityManager.AddComponent(enemy, intent);
					}
					var next = enemy.GetComponent<NextTurnAttackIntent>();
					if (next == null)
					{
						next = new NextTurnAttackIntent();
						EntityManager.AddComponent(enemy, next);
					}

					// If this is a new turn (different from last planned), clear old attacks
					if (_lastPlannedTurnNumber != -1 && turnNumber != _lastPlannedTurnNumber)
					{
						LoggingService.Append("EnemyIntentPlanningSystem.OnStartEnemyTurn", new System.Text.Json.Nodes.JsonObject { ["action"] = "New turn detected, clearing old attacks", ["from"] = _lastPlannedTurnNumber, ["to"] = turnNumber });
						intent.Planned.Clear();
					}

					// Promote next -> current if current is empty
					if (intent.Planned.Count == 0 && next.Planned.Count > 0)
					{
						intent.Planned = next.Planned;
						next.Planned = new List<PlannedAttack>();
					}

					next.Planned.Clear();
					// If current (this turn) is empty, fill it using the current turn's selection
					if (intent.Planned.Count == 0)
					{
						var currentIds = enemyCmp?.EnemyBase?.GetAttackIds(EntityManager, turnNumber) ?? [];
						LoggingService.Append("EnemyIntentPlanningSystem.OnStartEnemyTurn", new System.Text.Json.Nodes.JsonObject { ["action"] = "Planning current turn", ["currentTurnIds"] = string.Join(", ", currentIds.Select(id => id.ToKey())) });
						AddPlanned(currentIds, intent, enemyId, turnNumber);
					}
					// Plan next-turn preview using next turn's selection
					var nextIds = enemyCmp?.EnemyBase?.GetAttackIds(EntityManager, turnNumber + 1) ?? [];
					{
						AddPlanned(nextIds, next, enemyId, turnNumber + 1);
					}
				}
				_lastPlannedTurnNumber = turnNumber;
			}
			else if (evt.Current == SubPhase.PreBlock)
			{
				var intent = EntityManager.GetEntity("Enemy").GetComponent<AttackIntent>();
				var planned = intent.Planned.FirstOrDefault();
				if (planned == null) return;
				EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(EntityManager, planned);
				if (planned.AttackDefinition.OnAttackReveal != null)
				{
					planned.AttackDefinition.OnAttackReveal(EntityManager);
				}
			}
		}

		private void AddPlanned(IEnumerable<EnemyAttackId> attackIds, dynamic target, string enemyId, int plannedTurn)
		{
			int index = (target.Planned is List<PlannedAttack> l) ? l.Count : 0;
			foreach (var id in attackIds)
			{
				var attackDef = EnemyAttackFactory.Create(id);
				attackDef.Initialize(EntityManager);
				string ctx = Guid.NewGuid().ToString("N");
				var passives = EntityManager.GetEntity("Player").GetComponent<AppliedPassives>().Passives;
				bool hasFear = passives.TryGetValue(AppliedPassiveType.Fear, out int fear) && fear > 0;
				bool isAmbush = hasFear
					|| (attackDef.AmbushPercentage > 0 && Random.Shared.Next(0, 100) < attackDef.AmbushPercentage);
				target.Planned.Add(new PlannedAttack
				{
					AttackId = id,
					ResolveStep = Math.Max(1, index + 1),
					ContextId = ctx,
					WasBlocked = false,
					IsAmbush = isAmbush,
					AttackDefinition = attackDef
				});
				EventManager.Publish(new IntentPlanned
				{
					AttackId = id.ToKey(),
					ContextId = ctx,
					Step = Math.Max(1, index + 1),
					TelegraphText = attackDef.Name
				});
				index++;
			}
		}

		private int GetCurrentTurnNumber()
		{
			var infoEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (infoEntity == null) return 1;
			var info = infoEntity.GetComponent<PhaseState>();
			return info.TurnNumber;
		}

		private void OnFearApplied(ApplyPassiveEvent evt)
		{
			if (evt.Type != AppliedPassiveType.Fear || evt.Delta <= 0) return;

			var enemyEntity = EntityManager.GetEntity("Enemy");
			if (enemyEntity == null) return;

			var intent = enemyEntity.GetComponent<AttackIntent>();
			if (intent?.Planned != null)
			{
				foreach (var attack in intent.Planned)
					attack.IsAmbush = true;
			}

			var nextIntent = enemyEntity.GetComponent<NextTurnAttackIntent>();
			if (nextIntent?.Planned != null)
			{
				foreach (var attack in nextIntent.Planned)
					attack.IsAmbush = true;
			}
		}

	}
}
