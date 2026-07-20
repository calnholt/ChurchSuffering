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
	/// Tracks current active EnemyAttackProgress from block assignment events and planned attacks,
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

			var planned = intent.Planned.FirstOrDefault();
			if (planned == null)
			{
				DestroyProgressForEnemy(enemy);
				return;
			}

			var progress = EnemyAttackFlowService.GetOrCreateCurrentProgress(EntityManager, enemy, intent, planned);
			if (progress == null) return;

			progress.AttackId = planned.AttackId;
			Recompute(progress);
		}

		private void DestroyProgressForEnemy(Entity enemy)
		{
			var all = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>().ToList();
			foreach (var e in all)
			{
				var p = e.GetComponent<EnemyAttackProgress>();
				if (p != null && p.Enemy == enemy)
					EntityManager.DestroyEntity(e.Id);
			}
		}

		private void OnLoadSceneEvent(LoadSceneEvent e)
		{
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
			LoggingService.Append("EnemyAttackProgressManagementSystem.PrintProgress", new System.Text.Json.Nodes.JsonObject { ["attackSequence"] = p.AttackSequence, ["playedCards"] = p.PlayedCards, ["playedRed"] = p.PlayedRed, ["playedWhite"] = p.PlayedWhite, ["playedBlack"] = p.PlayedBlack, ["assignedBlockTotal"] = p.AssignedBlockTotal, ["effectiveAssignedBlockTotal"] = p.EffectiveAssignedBlockTotal, ["additionalConditionalDamageTotal"] = p.AdditionalConditionalDamageTotal, ["isConditionMet"] = p.IsConditionMet, ["actualDamage"] = p.ActualDamage, ["preventedDamage"] = p.AegisTotal, ["baseDamage"] = p.BaseDamage, ["totalPreventedDamage"] = p.TotalPreventedDamage });
		}

		[DebugAction("Print Progress")]
		public void Debug_PrintProgress()
		{
			if (!EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress)) return;
			PrintProgress(progress);
		}

		private void OnBlockAssignmentAdded(BlockAssignmentAdded e)
		{
			if (e == null) return;
			LoggingService.Append("EnemyAttackProgressManagementSystem.OnBlockAssignmentAdded", new System.Text.Json.Nodes.JsonObject { ["colors"] = string.Join(",", e.Colors ?? Array.Empty<CardData.CardColor>()), ["deltaBlock"] = e.DeltaBlock });
			if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out var enemy, out var intent, out var planned)) return;

			var p = EnemyAttackFlowService.GetOrCreateCurrentProgress(EntityManager, enemy, intent, planned);
			if (p == null) return;

			p.PlayedCards = SafeInc(p.PlayedCards);
			if (e.DeltaBlock > 0)
			{
				p.AssignedBlockTotal = SafeInc(p.AssignedBlockTotal, e.DeltaBlock);
			}
			foreach (var color in (e.Colors ?? Array.Empty<CardData.CardColor>()).Distinct())
			{
				switch (color)
				{
					case CardData.CardColor.Red: p.PlayedRed = SafeInc(p.PlayedRed); break;
					case CardData.CardColor.White: p.PlayedWhite = SafeInc(p.PlayedWhite); break;
					case CardData.CardColor.Black: p.PlayedBlack = SafeInc(p.PlayedBlack); break;
				}
			}
			Recompute(p);
			PrintProgress(p);
		}

		private void OnBlockAssignmentRemoved(BlockAssignmentRemoved e)
		{
			if (e == null) return;
			LoggingService.Append("EnemyAttackProgressManagementSystem.OnBlockAssignmentRemoved", new System.Text.Json.Nodes.JsonObject { ["colors"] = string.Join(",", e.Colors ?? Array.Empty<CardData.CardColor>()), ["deltaBlock"] = e.DeltaBlock });
			if (!EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress)) return;

			long nextAssigned = (long)progress.AssignedBlockTotal + e.DeltaBlock;
			progress.AssignedBlockTotal = nextAssigned < 0 ? 0 : (int)nextAssigned;

			if (e.DeltaBlock < 0)
			{
				foreach (var color in (e.Colors ?? Array.Empty<CardData.CardColor>()).Distinct())
				{
					switch (color)
					{
						case CardData.CardColor.Red: progress.PlayedRed = SafeDec(progress.PlayedRed); break;
						case CardData.CardColor.White: progress.PlayedWhite = SafeDec(progress.PlayedWhite); break;
						case CardData.CardColor.Black: progress.PlayedBlack = SafeDec(progress.PlayedBlack); break;
					}
				}
			}
			progress.PlayedCards = SafeDec(progress.PlayedCards);

			Recompute(progress);
			PrintProgress(progress);
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
			var enemy = p.Enemy ?? EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			if (enemy == null) return;
			var attackIntent = enemy.GetComponent<AttackIntent>();
			if (attackIntent == null || attackIntent.Planned == null || attackIntent.Planned.Count == 0) return;

			var planned = attackIntent.Planned[0];
			var def = planned.AttackDefinition;
			if (def == null) return;

			int full = DamagePredictionService.ComputeFullDamage(def);
			p.IgnoresAegis = def.IgnoresAegis;
			int aegis = p.IgnoresAegis ? 0 : Math.Max(0, p.AegisTotal);
			p.DamageBeforePrevention = full;
			p.EffectiveAssignedBlockTotal = Math.Max(0, p.AssignedBlockTotal);

			bool specialEffectExecuted = def.ProgressOverride != null ? def.ProgressOverride(EntityManager) : false;
			if (specialEffectExecuted) return;
			p.PreventedDamageFromBlockCondition = 0;
			p.AdditionalConditionalDamageTotal = 0;
			p.BaseDamage = def.Damage;
			bool isConditionMet = ConditionService.Evaluate(def.ConditionType, EntityManager, p);
			int effectiveAssignedBlock = DamagePredictionService.GetEffectiveAssignedBlockTotal(p);
			int reduced = aegis + effectiveAssignedBlock;
			int actual = Math.Max(full - reduced, 0);

			if (def.BlockRequiredToPreventEffect is int blockRequired)
			{
				isConditionMet = isConditionMet
					&& ConditionService.EvaluateBlockRequiredToPreventEffect(blockRequired, p, actual);
			}

			p.IsConditionMet = isConditionMet;
			p.ActualDamage = actual;
			p.TotalPreventedDamage = aegis + effectiveAssignedBlock;
			p.FullyPreventedBySpecial = false;

			try
			{
				var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
				if (phase != null && phase.Sub == SubPhase.Block)
				{
					int liveAssigned = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
						.Select(e => e.GetComponent<AssignedBlockCard>())
						.Where(abc => abc != null)
						.Sum(abc => abc.BlockAmount);
					if (liveAssigned != p.AssignedBlockTotal)
					{
						LoggingService.Append("EnemyAttackProgressManagementSystem.Recompute.desync", new System.Text.Json.Nodes.JsonObject { ["attackSequence"] = p.AttackSequence, ["snapshot"] = p.AssignedBlockTotal, ["live"] = liveAssigned });
					}
				}
			}
			catch { }
		}

		private void OnApplyPassive(ApplyPassiveEvent e)
		{
			if (e == null || e.Type != AppliedPassiveType.Aegis) return;
			if (e.Target == null || !e.Target.HasComponent<Player>()) return;
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
