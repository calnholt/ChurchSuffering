using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class CursedManagementSystem : Core.System
	{
		public CursedManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ApplyCardApplicationEvent>(OnApplyCardApplication);
			EventManager.Subscribe<RemoveCardApplication>(OnRemoveCardApplication);
			EventManager.Subscribe<RemoveCardApplications>(OnRemoveCardApplications);
			EventManager.Subscribe<StartBattleRequested>(_ => RefreshAllCursedCardPresentations(), priority: -1);
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene, priority: -1);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt.Scene != SceneId.Battle) return;
			RefreshAllCursedCardPresentations();
		}

		private void RefreshAllCursedCardPresentations()
		{
			foreach (var card in EntityManager.GetEntitiesWithComponent<Cursed>())
			{
				RefreshCursedCardPresentation(EntityManager, card);
			}
		}

		private void OnApplyCardApplication(ApplyCardApplicationEvent evt)
		{
			if (evt.Type != CardApplicationType.Cursed || evt.Amount <= 0) return;

			var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, evt.Card, evt.Target)
				.Where(CardApplicationTargetingService.IsEligibleForApplication)
				.Where(card => !card.HasComponent<Cursed>())
				.Distinct()
				.OrderBy(_ => Random.Shared.Next())
				.Take(evt.Amount)
				.ToList();

			if (cards.Count == 0)
			{
				LoggingService.Append(
					"CursedManagementSystem.Apply",
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
					Type = CardApplicationType.Cursed,
				});
				LoggingService.Append(
					"CursedManagementSystem.Apply.card",
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
			if (evt?.Card == null || evt.Type != CardApplicationType.Cursed) return;
			RemoveApplication(evt.Card);
		}

		private void OnRemoveCardApplications(RemoveCardApplications evt)
		{
			if (evt == null || evt.Type != CardApplicationType.Cursed || evt.Amount <= 0) return;

			var cards = CardApplicationTargetingService.ResolveCandidates(EntityManager, null, evt.Target)
				.Where(CardApplicationTargetingService.IsNonWeaponCard)
				.Where(card => card.HasComponent<Cursed>())
				.Distinct()
				.OrderBy(_ => Random.Shared.Next())
				.Take(evt.Amount)
				.ToList();

			foreach (var card in cards)
			{
				RemoveApplication(card);
			}
		}

		private void RemoveApplication(Entity card)
		{
			if (!card.HasComponent<Cursed>()) return;

			RemoveCursedRuntime(EntityManager, card);
			RunScopedStateService.SyncCardRestrictionsFromComponents(card);

			EventManager.Publish(new TrackingEvent
			{
				Type = TrackingTypeEnum.CursesRemoved.ToString(),
				Delta = 1
			});
		}

		public static void ApplyCursedRuntime(EntityManager entityManager, Entity card)
		{
			if (entityManager == null || card == null) return;
			var cardData = card.GetComponent<CardData>();
			var currentCard = cardData?.Card;
			if (cardData == null || currentCard == null) return;

			var original = card.GetComponent<CursedOriginalCard>();
			if (original == null && !string.Equals(currentCard.CardId, Curse.CardIdValue, StringComparison.OrdinalIgnoreCase))
			{
				original = new CursedOriginalCard
				{
					CardId = currentCard.CardId ?? string.Empty,
					Color = cardData.Color,
					IsUpgraded = currentCard.IsUpgraded,
					IsStarter = currentCard.IsStarter,
				};
				entityManager.AddComponent(card, original);
			}

			if (!card.HasComponent<Cursed>())
			{
				entityManager.AddComponent(card, new Cursed { Owner = card });
			}

			if (!string.Equals(cardData.Card?.CardId, Curse.CardIdValue, StringComparison.OrdinalIgnoreCase))
			{
				var replacedCard = cardData.Card;
				var curse = CardFactory.Create(Curse.CardIdValue);
				if (curse == null) return;
				cardData.Card = curse;
				curse.Initialize(entityManager, card);
				DisposeReplacedCard(replacedCard, curse);
			}

			RefreshCursedCardPresentation(entityManager, card);
		}

		public static void RemoveCursedRuntime(EntityManager entityManager, Entity card)
		{
			if (entityManager == null || card == null) return;
			if (card.HasComponent<Cursed>())
			{
				entityManager.RemoveComponent<Cursed>(card);
			}

			var original = card.GetComponent<CursedOriginalCard>();
			var cardData = card.GetComponent<CardData>();
			if (original != null && cardData != null && !string.IsNullOrWhiteSpace(original.CardId))
			{
				var restored = CardFactory.Create(original.CardId);
				if (restored != null)
				{
					var replacedCard = cardData.Card;
					restored.IsUpgraded = original.IsUpgraded;
					restored.IsStarter = original.IsStarter;
					cardData.Card = restored;
					cardData.Color = original.Color;
					restored.Initialize(entityManager, card);
					DisposeReplacedCard(replacedCard, restored);
				}
				entityManager.RemoveComponent<CursedOriginalCard>(card);
			}

			RefreshNormalCardPresentation(entityManager, card);
		}

		private static void DisposeReplacedCard(CardBase replacedCard, CardBase replacementCard)
		{
			if (replacedCard == null || ReferenceEquals(replacedCard, replacementCard)) return;
			replacedCard.Dispose();
		}

		public static void RefreshCardTooltipPresentation(
			EntityManager entityManager,
			Entity card,
			TooltipPosition position = TooltipPosition.Above,
			int offsetPx = 30)
		{
			if (entityManager == null || card == null) return;
			if (card.HasComponent<Cursed>())
			{
				RefreshCursedCardPresentation(entityManager, card);
				var ui = card.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.TooltipPosition = position;
					ui.TooltipOffsetPx = offsetPx;
				}
				return;
			}

			RefreshNormalCardPresentation(entityManager, card, position, offsetPx);
		}

		public static void RefreshCursedCardPresentation(EntityManager entityManager, Entity card)
		{
			if (entityManager == null || card == null || !card.HasComponent<Cursed>()) return;
			var original = card.GetComponent<CursedOriginalCard>();
			if (original == null || string.IsNullOrWhiteSpace(original.CardId)) return;

			var ui = card.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.Tooltip = string.Empty;
				ui.TooltipType = TooltipType.Card;
				ui.TooltipPosition = TooltipPosition.Above;
				ui.TooltipOffsetPx = 30;
			}

			var tooltip = card.GetComponent<CardTooltip>();
			if (tooltip == null)
			{
				tooltip = new CardTooltip();
				entityManager.AddComponent(card, tooltip);
			}

			tooltip.Owner = card;
			tooltip.CardId = original.CardId;
			tooltip.CardColor = original.Color;
			tooltip.IsUpgraded = original.IsUpgraded;
			tooltip.TooltipScale = 0.6f;
			tooltip.CrossfadeUpgradePreview = false;
			tooltip.PreviewRestrictionNames = BuildActivePreviewRestrictions(card);

			var hint = card.GetComponent<Hint>();
			var cardData = card.GetComponent<CardData>();
			if (hint != null && cardData?.Card != null)
			{
				hint.Text = cardData.Card.GetCardHint(cardData.Color);
			}
		}

		private static void RefreshNormalCardPresentation(
			EntityManager entityManager,
			Entity card,
			TooltipPosition position = TooltipPosition.Above,
			int offsetPx = 30)
		{
			var cardData = card?.GetComponent<CardData>();
			var definition = cardData?.Card;
			if (card == null || definition == null) return;

			var hint = card.GetComponent<Hint>();
			if (hint != null)
			{
				hint.Text = definition.GetCardHint(cardData.Color);
			}

			var ui = card.GetComponent<UIElement>();
			if (ui == null) return;

			string displayText = definition.GetDisplayText();
			ui.Tooltip = string.Empty;
			ui.TooltipKeywordSource = displayText ?? string.Empty;
			ui.TooltipType = TooltipType.Text;
			ui.TooltipPosition = position;
			ui.TooltipOffsetPx = offsetPx;

			var existingTooltip = card.GetComponent<CardTooltip>();
			if (existingTooltip != null && string.IsNullOrWhiteSpace(definition.CardTooltip))
			{
				entityManager.RemoveComponent<CardTooltip>(card);
			}
			else if (!string.IsNullOrWhiteSpace(definition.CardTooltip))
			{
				if (existingTooltip == null)
				{
					entityManager.AddComponent(card, new CardTooltip
					{
						CardId = definition.CardTooltip,
						CardColor = cardData.Color,
					});
				}
				else
				{
					existingTooltip.CardId = definition.CardTooltip;
					existingTooltip.CardColor = cardData.Color;
					existingTooltip.IsUpgraded = false;
					existingTooltip.CrossfadeUpgradePreview = false;
					existingTooltip.PreviewRestrictionNames = new List<string>();
				}
				ui.TooltipType = TooltipType.Card;
			}
		}

		private static List<string> BuildActivePreviewRestrictions(Entity card)
		{
			var restrictions = new List<string>();
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionFrozen, card.HasComponent<Frozen>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionSealed, card.HasComponent<Sealed>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionBrittle, card.HasComponent<Brittle>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionScorched, card.HasComponent<Scorched>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionThorned, card.HasComponent<Thorned>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionColorless, card.HasComponent<Colorless>());
			return restrictions;
		}

		private static void AddRestrictionIfPresent(Entity card, List<string> restrictions, string restrictionName, bool isPresent)
		{
			if (card == null || restrictions == null || !isPresent || string.IsNullOrWhiteSpace(restrictionName)) return;
			if (!restrictions.Contains(restrictionName, StringComparer.OrdinalIgnoreCase))
			{
				restrictions.Add(restrictionName);
			}
		}
	}
}
