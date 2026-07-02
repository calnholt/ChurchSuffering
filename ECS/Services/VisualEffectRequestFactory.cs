using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Services
{
	public static class VisualEffectRequestFactory
	{
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
			string contextId,
			bool isPreview = false)
		{
			if (entityManager == null || enemyEntity == null || attack == null) return null;
			var resolvedRecipe = recipe ?? attack.AttackEffectRecipe ?? VisualEffectPresets.EnemyAttackLunge();
			var target = ResolveTarget(entityManager, VisualEffectSourceKind.EnemyAttack, resolvedRecipe.TargetRole);
			return Build(resolvedRecipe, enemyEntity, target, VisualEffectSourceKind.EnemyAttack, attack.Id, attack.Name, contextId, isPreview);
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
