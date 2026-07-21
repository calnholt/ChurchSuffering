using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Services
{
	public static class VisualEffectRequestFactory
	{
		public static IReadOnlyList<VisualEffectRequested> ForCardSequence(
			EntityManager entityManager,
			Entity cardEntity,
			VisualEffectSequence sequence,
			bool isPreview = false)
		{
			if (entityManager == null || cardEntity == null) return Array.Empty<VisualEffectRequested>();
			var card = cardEntity.GetComponent<CardData>()?.Card;
			return BuildSequence(
				entityManager,
				sequence,
				FindPlayer(entityManager),
				VisualEffectSourceKind.Card,
				card?.CardId,
				card?.DisplayName,
				isPreview);
		}

		public static IReadOnlyList<VisualEffectRequested> ForEnemyAttackSequence(
			EntityManager entityManager,
			Entity enemyEntity,
			EnemyAttackBase attack,
			VisualEffectSequence sequence,
			bool isPreview = false)
		{
			if (entityManager == null || enemyEntity == null || attack == null) return Array.Empty<VisualEffectRequested>();
			return BuildSequence(
				entityManager,
				sequence,
				enemyEntity,
				VisualEffectSourceKind.EnemyAttack,
				attack.Id.ToKey(),
				attack.Name,
				isPreview);
		}

		public static IReadOnlyList<VisualEffectRequested> ForDebugPreviewSequence(
			EntityManager entityManager,
			VisualEffectSourceKind sourceKind,
			string sourceId,
			string displayName,
			VisualEffectSequence sequence)
		{
			if (entityManager == null) return Array.Empty<VisualEffectRequested>();
			var source = sourceKind == VisualEffectSourceKind.EnemyAttack ? FindEnemy(entityManager) : FindPlayer(entityManager);
			return BuildSequence(entityManager, sequence, source, sourceKind, sourceId, displayName, true);
		}
		public static VisualEffectRequested ForCard(
			EntityManager entityManager,
			Entity cardEntity,
			VisualEffectRecipe recipe,
			bool isPreview = false)
		{
			if (entityManager == null || cardEntity == null || recipe == null) return null;
			var card = cardEntity.GetComponent<CardData>()?.Card;
			var source = FindPlayer(entityManager);
			var target = ResolveTarget(entityManager, VisualEffectSourceKind.Card, recipe.TargetRole);
			return Build(recipe, source, target, VisualEffectSourceKind.Card, card?.CardId, card?.DisplayName, string.Empty, isPreview);
		}

		public static VisualEffectRequested ForEquipment(
			EntityManager entityManager,
			Entity equipmentEntity,
			VisualEffectRecipe recipe,
			bool isPreview = false)
		{
			if (entityManager == null || equipmentEntity == null || recipe == null) return null;
			var equipment = equipmentEntity.GetComponent<EquippedEquipment>()?.Equipment;
			var source = FindPlayer(entityManager);
			var target = ResolveTarget(entityManager, VisualEffectSourceKind.Equipment, recipe.TargetRole);
			return Build(recipe, source, target, VisualEffectSourceKind.Equipment, equipment?.Id, equipment?.Name, string.Empty, isPreview);
		}

		public static VisualEffectRequested ForMedal(
			EntityManager entityManager,
			Entity medalEntity,
			VisualEffectRecipe recipe,
			bool isPreview = false)
		{
			if (entityManager == null || medalEntity == null || recipe == null) return null;
			var medal = medalEntity.GetComponent<EquippedMedal>()?.Medal;
			var source = FindPlayer(entityManager);
			var target = ResolveTarget(entityManager, VisualEffectSourceKind.Medal, recipe.TargetRole);
			return Build(recipe, source, target, VisualEffectSourceKind.Medal, medal?.Id, medal?.Name, string.Empty, isPreview);
		}

		public static VisualEffectRequested ForEnemyAttack(
			EntityManager entityManager,
			Entity enemyEntity,
			EnemyAttackBase attack,
			VisualEffectRecipe recipe,
			bool isPreview = false)
		{
			if (entityManager == null || enemyEntity == null || attack == null) return null;
			var resolvedRecipe = recipe ?? attack.AttackEffectRecipe;
			if (resolvedRecipe == null) return null;
			var target = ResolveTarget(entityManager, VisualEffectSourceKind.EnemyAttack, resolvedRecipe.TargetRole);
			return Build(resolvedRecipe, enemyEntity, target, VisualEffectSourceKind.EnemyAttack, attack.Id.ToKey(), attack.Name, string.Empty, isPreview);
		}

		public static VisualEffectRequested ForDebugPreview(
			EntityManager entityManager,
			VisualEffectSourceKind sourceKind,
			string sourceId,
			string displayName,
			VisualEffectRecipe recipe)
		{
			if (entityManager == null || recipe == null) return null;
			var source = sourceKind == VisualEffectSourceKind.EnemyAttack
				? FindEnemy(entityManager)
				: FindPlayer(entityManager);
			var target = ResolveTarget(entityManager, sourceKind, recipe.TargetRole);
			return Build(recipe, source, target, sourceKind, sourceId, displayName, string.Empty, true);
		}

		private static VisualEffectRequested Build(
			VisualEffectRecipe recipe,
			Entity source,
			Entity target,
			VisualEffectSourceKind sourceKind,
			string sourceId,
			string displayName,
			string contextId,
			bool isPreview)
		{
			if (recipe == null || source == null || target == null) return null;
			return new VisualEffectRequested
			{
				RequestId = Guid.NewGuid(),
				Recipe = recipe.Clone(),
				Source = source,
				Target = target,
				SourceKind = sourceKind,
				SourceId = sourceId ?? string.Empty,
				ContextId = contextId ?? string.Empty,
				DisplayName = displayName ?? string.Empty,
				IsPreview = isPreview
			};
		}

		private static IReadOnlyList<VisualEffectRequested> BuildSequence(
			EntityManager entityManager,
			VisualEffectSequence sequence,
			Entity source,
			VisualEffectSourceKind sourceKind,
			string sourceId,
			string displayName,
			bool isPreview)
		{
			if (sequence?.Beats == null || sequence.Beats.Count == 0 || source == null)
			{
				return Array.Empty<VisualEffectRequested>();
			}

			var sequenceId = Guid.NewGuid();
			var requests = new List<VisualEffectRequested>();
			for (int index = 0; index < sequence.Beats.Count; index++)
			{
				var beat = sequence.Beats[index];
				if (beat == null) continue;
				var target = ResolveTarget(entityManager, sourceKind, beat.TargetRole);
				if (target == null) continue;
				requests.Add(new VisualEffectRequested
				{
					RequestId = Guid.NewGuid(),
					Recipe = beat.ToLegacyRecipe(),
					Source = source,
					Target = target,
					SourceKind = sourceKind,
					SourceId = sourceId ?? string.Empty,
					DisplayName = displayName ?? string.Empty,
					IsPreview = isPreview,
					DelaySeconds = Math.Max(0f, beat.DelaySeconds),
					TimingOverride = beat.ToTiming(),
					DrivesGameplayImpact = beat.DrivesGameplayImpact,
					SequenceId = sequenceId,
					BeatIndex = index
				});
			}
			return requests;
		}

		private static Entity ResolveTarget(EntityManager entityManager, VisualEffectSourceKind sourceKind, VisualEffectTargetRole role)
		{
			return role switch
			{
				VisualEffectTargetRole.Enemy => FindEnemy(entityManager),
				VisualEffectTargetRole.Player => FindPlayer(entityManager),
				VisualEffectTargetRole.Self => sourceKind == VisualEffectSourceKind.EnemyAttack ? FindEnemy(entityManager) : FindPlayer(entityManager),
				VisualEffectTargetRole.Opponent => sourceKind == VisualEffectSourceKind.EnemyAttack ? FindPlayer(entityManager) : FindEnemy(entityManager),
				_ => FindEnemy(entityManager)
			};
		}

		private static Entity FindPlayer(EntityManager entityManager)
		{
			return entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
		}

		private static Entity FindEnemy(EntityManager entityManager)
		{
			var named = entityManager.GetEntity("Enemy");
			if (named != null && named.GetComponent<Enemy>() != null) return named;
			return entityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
		}

	}
}
