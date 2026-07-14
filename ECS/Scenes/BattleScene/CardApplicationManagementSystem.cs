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
			if (evt.Type == CardApplicationType.Cursed || evt.Type == CardApplicationType.Hex || evt.Amount <= 0) return;

			var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, evt.Card, evt.Target)
				.Where(CardApplicationTargetingService.IsEligibleForApplication)
				.Where(card => evt.Type == CardApplicationType.Sealed || !CardApplicationService.IsApplied(card, evt.Type))
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
				EventManager.Publish(new CardRestrictionMutationAnimationRequested
				{
					TargetCard = card,
					StacksPerCard = Math.Max(1, evt.StacksPerCard),
					Type = evt.Type,
				});
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
			if (evt?.Card == null || evt.Type == CardApplicationType.Cursed || evt.Type == CardApplicationType.Hex) return;
			CardApplicationService.RemoveRestriction(EntityManager, evt.Card, evt.Type);
		}

		private void OnRemoveCardApplications(RemoveCardApplications evt)
		{
			if (evt == null || evt.Type == CardApplicationType.Cursed || evt.Type == CardApplicationType.Hex || evt.Amount <= 0) return;

			var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, null, evt.Target)
				.Where(CardApplicationTargetingService.IsNonWeaponCard)
				.Where(card => CardApplicationService.IsApplied(card, evt.Type))
				.Distinct()
				.OrderBy(_ => Random.Shared.Next())
				.Take(evt.Amount)
				.ToList();

			foreach (var card in cards)
			{
				CardApplicationService.RemoveRestriction(EntityManager, card, evt.Type);
			}
		}
	}
}
