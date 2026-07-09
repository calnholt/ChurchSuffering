using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Services
{
	public static class CardApplicationTargetingService
	{
		public static IEnumerable<Entity> ResolveCandidates(
			EntityManager entityManager,
			Entity exactCard,
			CardApplicationTarget target)
		{
			if (exactCard != null)
			{
				return new[] { exactCard };
			}

			var deck = entityManager.GetEntitiesWithComponent<Deck>()
				.FirstOrDefault()
				?.GetComponent<Deck>();
			if (deck == null) return Enumerable.Empty<Entity>();

			return target switch
			{
				CardApplicationTarget.HandAndDrawPile => GetHandCards(entityManager)
					.Concat(GetNonWeaponCards(deck.DrawPile)),
				CardApplicationTarget.TopXCards => GetNonWeaponCards(deck.DrawPile),
				CardApplicationTarget.DrawPile => GetNonWeaponCards(deck.DrawPile),
				CardApplicationTarget.DrawPileAndDiscard => GetNonWeaponCards(deck.DrawPile)
					.Concat(GetNonWeaponCards(deck.DiscardPile)),
				CardApplicationTarget.Hand => GetHandCards(entityManager),
				CardApplicationTarget.Deck => GetNonWeaponCards(deck.Cards),
				_ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported card application target."),
			};
		}

		public static bool IsEligibleForApplication(Entity card)
		{
			return IsNonWeaponCard(card) && !card.HasComponent<Pledge>();
		}

		public static bool IsNonWeaponCard(Entity card)
		{
			return card != null
				&& card.GetComponent<CardData>() != null
				&& (card.GetComponent<CardData>()?.Card?.IsWeapon ?? false) == false;
		}

		private static IEnumerable<Entity> GetHandCards(EntityManager entityManager)
		{
			return GetComponentHelper.GetHandOfCards(entityManager)
				?? Enumerable.Empty<Entity>();
		}

		private static IEnumerable<Entity> GetNonWeaponCards(IEnumerable<Entity> cards)
		{
			return cards?.Where(IsNonWeaponCard)
				?? Enumerable.Empty<Entity>();
		}
	}
}
