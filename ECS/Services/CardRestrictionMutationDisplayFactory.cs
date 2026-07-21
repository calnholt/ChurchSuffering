using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Objects.Cards;

namespace ChurchSuffering.ECS.Services
{
	public static class CardRestrictionMutationDisplayFactory
	{
		public static (Entity BaseCard, Entity FinalCard) CreateDisplayPairFromBattleCard(
			EntityManager entityManager,
			Entity targetCard,
			CardApplicationType applicationType,
			int stacksPerCard = 1)
		{
			if (entityManager == null || targetCard == null) return (null, null);

			string cardKey = ResolveCardKey(targetCard);
			if (string.IsNullOrWhiteSpace(cardKey)) return (null, null);

			var currentRestrictions = CollectRestrictionNames(targetCard);
			var finalRestrictions = new List<string>(currentRestrictions);
			AddRestriction(finalRestrictions, ToRestrictionName(applicationType));

			var secondaryColor = targetCard.GetComponent<DualColor>()?.SecondaryColor;
			var baseCard = CreateDisplayCard(entityManager, cardKey, currentRestrictions, secondaryColor);
			var finalCard = CreateDisplayCard(entityManager, cardKey, finalRestrictions, secondaryColor);
			if (applicationType == CardApplicationType.Sealed)
			{
				var currentSeals = targetCard.GetComponent<Sealed>()?.Seals ?? 0;
				var baseSealed = baseCard?.GetComponent<Sealed>();
				var finalSealed = finalCard?.GetComponent<Sealed>();
				if (baseSealed != null) baseSealed.Seals = currentSeals;
				if (finalSealed != null) finalSealed.Seals = currentSeals + Math.Max(1, stacksPerCard);
			}
			return (baseCard, finalCard);
		}

		public static (Entity BaseCard, Entity FinalCard) CreateDisplayPairFromKeys(
			EntityManager entityManager,
			string cardKey,
			IReadOnlyList<string> currentRestrictionNames,
			string newRestrictionName,
			CardData.CardColor? secondaryColor = null)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(cardKey)) return (null, null);

			var finalRestrictions = new List<string>(currentRestrictionNames ?? new List<string>());
			AddRestriction(finalRestrictions, newRestrictionName);
			var baseCard = CreateDisplayCard(entityManager, cardKey, currentRestrictionNames, secondaryColor);
			var finalCard = CreateDisplayCard(entityManager, cardKey, finalRestrictions, secondaryColor);
			return (baseCard, finalCard);
		}

		public static Entity CreateDisplayCard(
			EntityManager entityManager,
			string cardKey,
			IReadOnlyList<string> restrictionNames = null,
			CardData.CardColor? secondaryColor = null,
			IReadOnlyList<CardBoonSave> boons = null)
		{
			if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out var color, out var isUpgraded)) return null;
			var entity = EntityFactory.CreateCardFromDefinition(
				entityManager,
				cardId,
				color,
				allowWeapons: false,
				index: 0,
				isUpgraded: isUpgraded);
			if (entity == null) return null;
			if (secondaryColor.HasValue)
			{
				entityManager.AddComponent(entity, new DualColor
				{
					Owner = entity,
					SecondaryColor = secondaryColor.Value,
				});
			}

			var ui = entity.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.IsInteractable = false;
				ui.TooltipType = TooltipType.None;
			}

			ApplyRestrictions(entityManager, entity, restrictionNames);
			CardBoonApplicator.Synchronize(entityManager, entity, boons);
			return entity;
		}

		public static void DestroyDisplayCard(EntityManager entityManager, Entity card)
		{
			if (entityManager == null || card == null) return;
			entityManager.DestroyEntity(card.Id);
		}

		public static List<string> CollectRestrictionNames(Entity card)
		{
			var restrictions = new List<string>();
			if (card == null) return restrictions;
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionFrozen, card.HasComponent<Frozen>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionBrittle, card.HasComponent<Brittle>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionScorched, card.HasComponent<Scorched>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionThorned, card.HasComponent<Thorned>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionColorless, card.HasComponent<Colorless>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionSealed, card.HasComponent<Sealed>());
			AddRestrictionIfPresent(card, restrictions, RunScopedStateService.RestrictionCursed, card.HasComponent<Cursed>());
			AddRestrictionIfPresent(card, restrictions, HexManagementSystem.RestrictionName, card.HasComponent<Hexed>());
			return restrictions;
		}

		public static string ToRestrictionName(CardApplicationType type)
		{
			return type switch
			{
				CardApplicationType.Frozen => RunScopedStateService.RestrictionFrozen,
				CardApplicationType.Brittle => RunScopedStateService.RestrictionBrittle,
				CardApplicationType.Scorched => RunScopedStateService.RestrictionScorched,
				CardApplicationType.Thorned => RunScopedStateService.RestrictionThorned,
				CardApplicationType.Colorless => RunScopedStateService.RestrictionColorless,
				CardApplicationType.Sealed => RunScopedStateService.RestrictionSealed,
				CardApplicationType.Cursed => RunScopedStateService.RestrictionCursed,
				CardApplicationType.Hex => HexManagementSystem.RestrictionName,
				_ => string.Empty,
			};
		}

		public static SfxTrack ToModificationSfx(string restrictionName)
		{
			return restrictionName switch
			{
				RunScopedStateService.RestrictionFrozen => SfxTrack.ApplyFrozen,
				RunScopedStateService.RestrictionBrittle => SfxTrack.ApplyBrittle,
				RunScopedStateService.RestrictionScorched => SfxTrack.ApplyScorched,
				RunScopedStateService.RestrictionThorned => SfxTrack.ApplyThorns,
				RunScopedStateService.RestrictionCursed => SfxTrack.ApplyCurse,
				HexManagementSystem.RestrictionName => SfxTrack.ApplyCurse,
				_ => SfxTrack.None,
			};
		}

		private static string ResolveCardKey(Entity card)
		{
			var runDeckCard = card.GetComponent<RunDeckCard>();
			if (!string.IsNullOrWhiteSpace(runDeckCard?.CardKey)) return runDeckCard.CardKey;

			var cardData = card.GetComponent<CardData>();
			if (cardData?.Card == null) return string.Empty;
			return RunDeckService.BuildCardKey(
				cardData.Card.CardId,
				cardData.Color,
				cardData.Card.IsUpgraded);
		}

		private static void ApplyRestrictions(
			EntityManager entityManager,
			Entity card,
			IReadOnlyList<string> restrictionNames)
		{
			foreach (var restriction in restrictionNames ?? new List<string>())
			{
				switch (restriction)
				{
					case RunScopedStateService.RestrictionFrozen:
						if (!card.HasComponent<Frozen>()) entityManager.AddComponent(card, new Frozen { Owner = card });
						break;
					case RunScopedStateService.RestrictionBrittle:
						if (!card.HasComponent<Brittle>()) entityManager.AddComponent(card, new Brittle { Owner = card });
						break;
					case RunScopedStateService.RestrictionScorched:
						if (!card.HasComponent<Scorched>()) entityManager.AddComponent(card, new Scorched { Owner = card });
						break;
					case RunScopedStateService.RestrictionThorned:
						if (!card.HasComponent<Thorned>()) entityManager.AddComponent(card, new Thorned { Owner = card });
						break;
					case RunScopedStateService.RestrictionColorless:
						if (!card.HasComponent<Colorless>()) entityManager.AddComponent(card, new Colorless { Owner = card });
						break;
					case RunScopedStateService.RestrictionSealed:
						if (!card.HasComponent<Sealed>()) entityManager.AddComponent(card, new Sealed { Owner = card, Seals = 2 });
						break;
					case RunScopedStateService.RestrictionCursed:
						CursedManagementSystem.ApplyCursedRuntime(entityManager, card);
						break;
					case HexManagementSystem.RestrictionName:
						HexManagementSystem.ApplyHexRuntime(entityManager, card);
						break;
				}
			}
		}

		private static void AddRestriction(List<string> restrictions, string restriction)
		{
			if (restrictions == null || string.IsNullOrWhiteSpace(restriction)) return;
			if (!restrictions.Contains(restriction, StringComparer.OrdinalIgnoreCase))
			{
				restrictions.Add(restriction);
			}
		}

		private static void AddRestrictionIfPresent(
			Entity card,
			List<string> restrictions,
			string restrictionName,
			bool isPresent)
		{
			if (card == null || restrictions == null || !isPresent || string.IsNullOrWhiteSpace(restrictionName)) return;
			if (!restrictions.Contains(restrictionName, StringComparer.OrdinalIgnoreCase))
			{
				restrictions.Add(restrictionName);
			}
		}
	}
}
