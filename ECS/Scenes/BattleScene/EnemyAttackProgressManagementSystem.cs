using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Ids;
using Microsoft.Xna.Framework;
using System;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Tracks per-context EnemyAttackProgress from block assignment events and planned attacks,
	/// and precomputes IsBlocked, ActualDamage, and PreventedDamage for UI/logic.
	/// </summary>
	[DebugTab("EnemyAttackProgress")]
	public class EnemyAttackProgressManagementSystem : Core.System
	{
		public EnemyAttackProgressManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<BlockAssignmentAdded>(OnBlockAssignmentAdded);
			EventManager.Subscribe<BlockAssignmentRemoved>(OnBlockAssignmentRemoved);
			EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
			EventManager.Subscribe<RemovePassive>(OnRemovePassive);
			EventManager.Subscribe<UpdatePassive>(OnUpdatePassive);
		EventManager.Subscribe<ChangeBattlePhaseEvent>(_ => { if (_.Current == SubPhase.Block || _.Current == SubPhase.EnemyAttack) RecomputeAll(); });
		EventManager.Subscribe<LoadSceneEvent>(OnLoadSceneEvent);

	}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<AttackIntent>();
		}

		protected override void UpdateEntity(Entity enemy, GameTime gameTime)
		{
			var intent = enemy.GetComponent<AttackIntent>();
			if (intent == null) return;

			// Ensure a progress entity exists for each planned attack context
			var activeContextIds = new HashSet<string>();
			foreach (var pa in intent.Planned)
			{
				if (string.IsNullOrEmpty(pa.ContextId)) continue;
				activeContextIds.Add(pa.ContextId);
				var progress = FindOrCreateProgress(pa.ContextId, enemy, pa.AttackId);
				// Keep AttackId in sync in case it changes
				progress.AttackId = pa.AttackId;
				// Recompute(progress);
			}

			// Remove any progress entities whose contextId is no longer present
			var all = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>().ToList();
			foreach (var e in all)
			{
				var p = e.GetComponent<EnemyAttackProgress>();
				if (p == null) continue;
				if (p.Enemy == enemy && !string.IsNullOrEmpty(p.ContextId) && !activeContextIds.Contains(p.ContextId))
				{
					EntityManager.DestroyEntity(e.Id);
				}
			}
		// RecomputeAll();
	}

	private void OnLoadSceneEvent(LoadSceneEvent e)
	{
		// Clean up all EnemyAttackProgress entities when loading/reloading battle scene
		// This ensures fresh state for each enemy in a quest
		if (e.Scene == SceneId.Battle)
		{
			LoggingService.Append("EnemyAttackProgressManagementSystem.OnLoadSceneEvent", new System.Text.Json.Nodes.JsonObject { ["message"] = "cleaning up all EnemyAttackProgress entities for new battle" });
			var allProgress = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>().ToList();
			foreach (var entity in allProgress)
			{
				EntityManager.DestroyEntity(entity.Id);
			}
		}
	}

	private void PrintProgress(EnemyAttackProgress p)
	{
		LoggingService.Append("EnemyAttackProgressManagementSystem.PrintProgress", new System.Text.Json.Nodes.JsonObject { ["contextId"] = p.ContextId, ["playedCards"] = p.PlayedCards, ["playedRed"] = p.PlayedRed, ["playedWhite"] = p.PlayedWhite, ["playedBlack"] = p.PlayedBlack, ["assignedBlockTotal"] = p.AssignedBlockTotal, ["additionalConditionalDamageTotal"] = p.AdditionalConditionalDamageTotal, ["isConditionMet"] = p.IsConditionMet, ["actualDamage"] = p.ActualDamage, ["preventedDamage"] = p.AegisTotal, ["baseDamage"] = p.BaseDamage, ["totalPreventedDamage"] = p.TotalPreventedDamage });
	}

		[DebugAction("Print Progress")]
		public void Debug_PrintProgress()
		{
			var e = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			var progress = FindProgressByContext(e.GetComponent<AttackIntent>().Planned.First().ContextId);
			PrintProgress(progress);
		}


		private void OnBlockAssignmentAdded(BlockAssignmentAdded e)
		{
			LoggingService.Append("EnemyAttackProgressManagementSystem.OnBlockAssignmentAdded", new System.Text.Json.Nodes.JsonObject { ["contextId"] = e.ContextId, ["color"] = e.Color, ["deltaBlock"] = e.DeltaBlock });
			if (e == null) return;
			string color = NormalizeColorKey(e.Color);
			if (string.IsNullOrEmpty(e.ContextId)) return;

			// Update only the specific context this assignment targets
			var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>()
				.FirstOrDefault(en => en.GetComponent<AttackIntent>()?.Planned?.Any(pa => pa.ContextId == e.ContextId) == true);
			if (enemy == null) return;
			var attackId = enemy.GetComponent<AttackIntent>().Planned.First(pa => pa.ContextId == e.ContextId).AttackId;
			var p = FindOrCreateProgress(e.ContextId, enemy, attackId);
			p.PlayedCards = SafeInc(p.PlayedCards);
			if (e.DeltaBlock > 0)
			{
				p.AssignedBlockTotal = SafeInc(p.AssignedBlockTotal, e.DeltaBlock);
			}
			switch (color)
			{
				case "Red": p.PlayedRed = SafeInc(p.PlayedRed); break;
				case "White": p.PlayedWhite = SafeInc(p.PlayedWhite); break;
				case "Black": p.PlayedBlack = SafeInc(p.PlayedBlack); break;
			}
			// if (e.DeltaBlock > 0) p.AssignedBlockTotal = SafeInc(p.AssignedBlockTotal, e.DeltaBlock);
			Recompute(p);
			PrintProgress(p);
		}

		private void OnBlockAssignmentRemoved(BlockAssignmentRemoved e)
		{
			LoggingService.Append("EnemyAttackProgressManagementSystem.OnBlockAssignmentRemoved", new System.Text.Json.Nodes.JsonObject { ["contextId"] = e.ContextId, ["color"] = e.Color, ["deltaBlock"] = e.DeltaBlock });
			if (e == null || string.IsNullOrEmpty(e.ContextId)) return;

			// Find the owning enemy and attack for this context
			var progress = FindProgressByContext(e.ContextId);
			if (progress == null) return;

			// Maintain running totals
			long nextAssigned = (long)progress.AssignedBlockTotal + e.DeltaBlock;
			progress.AssignedBlockTotal = nextAssigned < 0 ? 0 : (int)nextAssigned;

			// Adjust color play counters and played cards like previous system
			if (!string.IsNullOrWhiteSpace(e.Color) && e.DeltaBlock < 0)
			{
				string color = NormalizeColorKey(e.Color);
				switch (color)
				{
					case "Red": progress.PlayedRed = SafeDec(progress.PlayedRed); break;
					case "White": progress.PlayedWhite = SafeDec(progress.PlayedWhite); break;
					case "Black": progress.PlayedBlack = SafeDec(progress.PlayedBlack); break;
				}
			}
			progress.PlayedCards = SafeDec(progress.PlayedCards);

			Recompute(progress);
			PrintProgress(progress);
		}

		private EnemyAttackProgress FindOrCreateProgress(string contextId, Entity enemy, EnemyAttackId attackId)
		{
			var existing = FindProgressByContext(contextId);
			if (existing != null)
			{
				existing.Enemy = enemy;
				return existing;
			}
			var entity = EntityManager.CreateEntity($"EnemyAttackProgress[{contextId}]");
			var comp = new EnemyAttackProgress
			{
				ContextId = contextId,
				Enemy = enemy,
				AttackId = attackId,
				AssignedBlockTotal = 0,
				PlayedCards = 0,
				PlayedRed = 0,
				PlayedWhite = 0,
				PlayedBlack = 0,
				IsConditionMet = false,
				ActualDamage = 0,
				// Seed Aegis snapshot from current passives when the progress row is created
				AegisTotal = DamagePredictionService.GetAegisAmount(EntityManager)
			};
			EntityManager.AddComponent(entity, comp);
			return comp;
		}

		private EnemyAttackProgress FindProgressByContext(string contextId)
		{
			var list = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>().ToList();
			foreach (var e in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
			{
				var p = e.GetComponent<EnemyAttackProgress>();
				if (p != null && p.ContextId == contextId) return p;
			}
			return null;
		}

		private void RecomputeAll()
		{
			foreach (var e in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
			{
				var p = e.GetComponent<EnemyAttackProgress>();
				if (p != null) Recompute(p);
			}
		}

		private void Recompute(EnemyAttackProgress p)
		{
			if (p == null) return;
			// Resolve owning enemy and planned attack for this context
			var enemy = p.Enemy ?? EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			if (enemy == null) return;
			var attackIntent = enemy.GetComponent<AttackIntent>();
			if (attackIntent == null || attackIntent.Planned == null || attackIntent.Planned.Count == 0) return;

			PlannedAttack planned = null;
			if (!string.IsNullOrEmpty(p.ContextId))
			{
				planned = attackIntent.Planned.FirstOrDefault(pa => pa.ContextId == p.ContextId);
			}
			if (planned == null)
			{
				planned = attackIntent.Planned.FirstOrDefault(pa => pa.AttackId == p.AttackId);
			}
			planned ??= attackIntent.Planned[0];
			var def = planned.AttackDefinition;
			if (def == null) return;

			int full = DamagePredictionService.ComputeFullDamage(def);
			// Copy IgnoresAegis flag from attack definition
			p.IgnoresAegis = def.IgnoresAegis;
			// Use the snapshot value maintained from passive events rather than re-reading passives here
			// When attack ignores aegis, treat it as 0 for damage calculation
			int aegis = p.IgnoresAegis ? 0 : Math.Max(0, p.AegisTotal);
			p.DamageBeforePrevention = full;

			bool specialEffectExecuted = def.ProgressOverride != null ? def.ProgressOverride(EntityManager) : false;
			if (specialEffectExecuted) return;
			p.PreventedDamageFromBlockCondition = 0;
			p.AdditionalConditionalDamageTotal = 0;
			p.BaseDamage = def.Damage;
			bool isConditionMet = ConditionService.Evaluate(def.ConditionType, EntityManager, p);
			int reduced = aegis + p.AssignedBlockTotal;
			int actual = Math.Max(full - reduced, 0);

			if (def.BlockRequiredToPreventEffect is int blockRequired)
			{
				isConditionMet = isConditionMet
					&& ConditionService.EvaluateBlockRequiredToPreventEffect(blockRequired, p, actual);
			}

			p.IsConditionMet = isConditionMet;
			p.ActualDamage = actual;
			p.TotalPreventedDamage = aegis + p.AssignedBlockTotal;
			p.FullyPreventedBySpecial = false;

			// Optional: sanity check for desync between snapshot and live AssignedBlockCard state (debug only)
			try
			{
				var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
				if (phase != null && phase.Sub == SubPhase.Block && !string.IsNullOrEmpty(p.ContextId))
				{
					int liveAssigned = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
						.Select(e => e.GetComponent<AssignedBlockCard>())
						.Where(abc => abc != null && abc.ContextId == p.ContextId)
						.Sum(abc => abc.BlockAmount);
					if (liveAssigned != p.AssignedBlockTotal)
					{
						LoggingService.Append("EnemyAttackProgressManagementSystem.Recompute.desync", new System.Text.Json.Nodes.JsonObject { ["contextId"] = p.ContextId, ["snapshot"] = p.AssignedBlockTotal, ["live"] = liveAssigned });
					}
				}
			}
			catch { }
		}

		private void OnApplyPassive(ApplyPassiveEvent e)
		{
			if (e == null || e.Type != AppliedPassiveType.Aegis) return;
			if (e.Target == null || !e.Target.HasComponent<Player>()) return;
			// Increment snapshot Aegis for all contexts and recompute from the snapshot,
			// so we are not sensitive to the ordering of passive update systems.
			foreach (var ent in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
			{
				var p = ent.GetComponent<EnemyAttackProgress>();
				if (p == null) continue;
				p.AegisTotal = SafeInc(p.AegisTotal, e.Delta);
				Recompute(p);
			}
		}

		private void OnRemovePassive(RemovePassive e)
		{
			if (e == null || e.Type != AppliedPassiveType.Aegis) return;
			if (e.Owner == null || !e.Owner.HasComponent<Player>()) return;
			foreach (var ent in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
			{
				var p = ent.GetComponent<EnemyAttackProgress>();
				if (p == null) continue;
				p.AegisTotal = 0;
				Recompute(p);
			}
		}

		private void OnUpdatePassive(UpdatePassive e)
		{
			if (e == null || e.Type != AppliedPassiveType.Aegis) return;
			if (e.Owner == null || !e.Owner.HasComponent<Player>()) return;
			foreach (var ent in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>())
			{
				var p = ent.GetComponent<EnemyAttackProgress>();
				if (p == null) continue;
				p.AegisTotal = SafeInc(p.AegisTotal, e.Delta);
				Recompute(p);
			}
		}

		private static string NormalizeColorKey(string color)
		{
			if (string.IsNullOrWhiteSpace(color)) return string.Empty;
			string c = color.Trim().ToLowerInvariant();
			switch (c)
			{
				case "r":
				case "red": return "Red";
				case "w":
				case "white": return "White";
				case "b":
				case "black": return "Black";
				default: return char.ToUpperInvariant(color[0]) + color.Substring(1);
			}
		}

		private static int SafeInc(int value, int delta = 1)
		{
			long next = (long)value + delta;
			return next < 0 ? 0 : (int)next;
		}

		private static int SafeDec(int value, int delta = 1)
		{
			long next = (long)value - delta;
			return next < 0 ? 0 : (int)next;
		}
	}
}
