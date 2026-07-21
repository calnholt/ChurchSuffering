using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Telemetry;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public sealed class CardUsageTrackingSystem : Core.System
	{
		private readonly CardUsageTelemetryStore _store;

		public CardUsageTrackingSystem(
			EntityManager entityManager,
			CardUsageTelemetryStore store)
			: base(entityManager)
		{
			_store = store ?? throw new ArgumentNullException(nameof(store));
			EventManager.Subscribe<CardPlayedEvent>(evt => Record(evt?.Card, CardUsageKind.Played));
			EventManager.Subscribe<CardBlockedEvent>(evt => Record(evt?.Card, CardUsageKind.Blocked));
			EventManager.Subscribe<CardDiscardedForCostEvent>(
				evt => Record(evt?.Card, CardUsageKind.DiscardedForCost));
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		private void Record(Entity card, CardUsageKind kind)
		{
			if (GuidedTutorialService.IsActive(EntityManager)) return;
			var definition = card?.GetComponent<CardData>()?.Card;
			if (definition == null || string.IsNullOrWhiteSpace(definition.CardId)) return;

			_store.Record(
				definition.CardId,
				definition.Name,
				definition.Type.ToString(),
				kind);
		}
	}
}
