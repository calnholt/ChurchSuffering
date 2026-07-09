using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class CardApplicationManagementSystem : Core.System
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
			};

		public CardApplicationManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ApplyCardApplicationEvent>(OnApplyCardApplication);
			EventManager.Subscribe<RemoveCardApplication>(OnRemoveCardApplication);
			EventManager.Subscribe<RemoveCardApplications>(OnRemoveCardApplications);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnApplyCardApplication(ApplyCardApplicationEvent evt)
		{
			if (evt.Type == CardApplicationType.Cursed || evt.Amount <= 0) return;

			var definition = GetDefinition(evt.Type);
			var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, evt.Card, evt.Target)
				.Where(CardApplicationTargetingService.IsEligibleForApplication)
				.Where(card => !definition.IsApplied(card))
				.Distinct()
				.OrderBy(_ => Random.Shared.Next())
				.Take(evt.Amount)
				.ToList();

			if (cards.Count == 0)
			{
				LoggingService.Append(
					"CardApplicationManagementSystem.Apply",
					new JsonObject
					{
						["message"] = "no eligible cards",
						["applicationType"] = evt.Type.ToString(),
						["target"] = evt.Target.ToString(),
					});
				return;
			}

			foreach (var card in cards)
			{
				definition.Apply(EntityManager, card);
				RunScopedStateService.SyncCardRestrictionsFromComponents(card);
				LoggingService.Append(
					"CardApplicationManagementSystem.Apply.card",
					new JsonObject
					{
						["applicationType"] = evt.Type.ToString(),
						["target"] = evt.Target.ToString(),
						["cardId"] = card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
					});
			}
		}

		private void OnRemoveCardApplication(RemoveCardApplication evt)
		{
			if (evt?.Card == null || evt.Type == CardApplicationType.Cursed) return;
			RemoveApplication(evt.Card, evt.Type);
		}

		private void OnRemoveCardApplications(RemoveCardApplications evt)
		{
			if (evt == null || evt.Type == CardApplicationType.Cursed || evt.Amount <= 0) return;

			var definition = GetDefinition(evt.Type);
			var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, null, evt.Target)
				.Where(CardApplicationTargetingService.IsNonWeaponCard)
				.Where(definition.IsApplied)
				.Distinct()
				.OrderBy(_ => Random.Shared.Next())
				.Take(evt.Amount)
				.ToList();

			foreach (var card in cards)
			{
				RemoveApplication(card, evt.Type);
			}
		}

		private void RemoveApplication(Entity card, CardApplicationType type)
		{
			var definition = GetDefinition(type);
			if (!definition.IsApplied(card)) return;

			definition.Remove(EntityManager, card);
			RunScopedStateService.SyncCardRestrictionsFromComponents(card);
		}

		private ApplicationDefinition GetDefinition(CardApplicationType type)
		{
			if (ApplicationDefinitions.TryGetValue(type, out var definition)) return definition;
			throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported card application type.");
		}
	}
}
