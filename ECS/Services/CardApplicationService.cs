using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Services
{
	public static class CardApplicationService
	{
		private sealed record ApplicationDefinition(
			Func<Entity, bool> IsApplied,
			Action<EntityManager, Entity> Apply,
			Action<EntityManager, Entity> Remove);

		private static readonly IReadOnlyDictionary<CardApplicationType, ApplicationDefinition> ApplicationDefinitions =
			new Dictionary<CardApplicationType, ApplicationDefinition>
			{
				[CardApplicationType.Frozen] = new(
					card => card.HasComponent<Frozen>(),
					(entityManager, card) => entityManager.AddComponent(card, new Frozen { Owner = card }),
					(entityManager, card) => entityManager.RemoveComponent<Frozen>(card)),
				[CardApplicationType.Brittle] = new(
					card => card.HasComponent<Brittle>(),
					(entityManager, card) => entityManager.AddComponent(card, new Brittle { Owner = card }),
					(entityManager, card) => entityManager.RemoveComponent<Brittle>(card)),
				[CardApplicationType.Scorched] = new(
					card => card.HasComponent<Scorched>(),
					(entityManager, card) => entityManager.AddComponent(card, new Scorched { Owner = card }),
					(entityManager, card) => entityManager.RemoveComponent<Scorched>(card)),
				[CardApplicationType.Thorned] = new(
					card => card.HasComponent<Thorned>(),
					(entityManager, card) => entityManager.AddComponent(card, new Thorned { Owner = card }),
					(entityManager, card) => entityManager.RemoveComponent<Thorned>(card)),
				[CardApplicationType.Colorless] = new(
					card => card.HasComponent<Colorless>(),
					(entityManager, card) => entityManager.AddComponent(card, new Colorless { Owner = card }),
					(entityManager, card) => entityManager.RemoveComponent<Colorless>(card)),
				[CardApplicationType.Cursed] = new(
					card => card.HasComponent<Cursed>(),
					(entityManager, card) => CursedManagementSystem.ApplyCursedRuntime(entityManager, card),
					(entityManager, card) => CursedManagementSystem.RemoveCursedRuntime(entityManager, card)),
			};

		public static bool IsApplied(Entity card, CardApplicationType type)
		{
			if (card == null) return false;
			return GetDefinition(type).IsApplied(card);
		}

		public static void ApplyRestriction(EntityManager entityManager, Entity card, CardApplicationType type)
		{
			if (entityManager == null || card == null) return;
			var definition = GetDefinition(type);
			if (definition.IsApplied(card)) return;

			definition.Apply(entityManager, card);
			RunScopedStateService.SyncCardRestrictionsFromComponents(card);
		}

		public static void RemoveRestriction(EntityManager entityManager, Entity card, CardApplicationType type)
		{
			if (entityManager == null || card == null) return;
			var definition = GetDefinition(type);
			if (!definition.IsApplied(card)) return;

			definition.Remove(entityManager, card);
			RunScopedStateService.SyncCardRestrictionsFromComponents(card);
		}

		private static ApplicationDefinition GetDefinition(CardApplicationType type)
		{
			if (ApplicationDefinitions.TryGetValue(type, out var definition)) return definition;
			throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported card application type.");
		}
	}
}
