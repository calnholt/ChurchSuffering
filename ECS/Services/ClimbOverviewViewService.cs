using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Services
{
	public static class ClimbOverviewViewService
	{
		public const string OverviewTitle = "Climb Overview";

		public static void Open(EntityManager entityManager)
		{
			var deckEntity = RunDeckService.EnsureRunDeck(entityManager);
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck?.Cards == null) return;

			EventManager.Publish(new OpenCardListModalEvent
			{
				Title = OverviewTitle,
				Cards = deck.Cards.ToList(),
				Mode = CardListModalMode.Inventory,
			});
		}

		public static void Close(EntityManager entityManager)
		{
			EventManager.Publish(new CloseCardListModalEvent());
		}

		public static bool IsOverviewOpen(EntityManager entityManager)
		{
			var modal = entityManager.GetEntitiesWithComponent<CardListModal>().FirstOrDefault()?.GetComponent<CardListModal>();
			if (modal == null || !modal.IsOpen) return false;

			return string.Equals(modal.Title, OverviewTitle, StringComparison.Ordinal);
		}

		public static bool IsUnrelatedModalOpen(EntityManager entityManager)
		{
			var modal = entityManager.GetEntitiesWithComponent<CardListModal>().FirstOrDefault()?.GetComponent<CardListModal>();
			return modal != null && modal.IsOpen && !IsOverviewOpen(entityManager);
		}
	}
}
