using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Services
{
	public static class PileViewService
	{
		public const string DrawPileTitle = "Draw Pile";
		public const string DiscardPileTitle = "Discard Pile";

		public static void OpenDrawPile(EntityManager entityManager)
		{
			var deck = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
			if (deck == null) return;

			EventManager.Publish(new OpenCardListModalEvent
			{
				Title = DrawPileTitle,
				Cards = deck.DrawPile.ToList(),
				Mode = CardListModalMode.CardList,
			});
		}

		public static void OpenDiscardPile(EntityManager entityManager)
		{
			var deck = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
			if (deck == null) return;

			EventManager.Publish(new OpenCardListModalEvent
			{
				Title = DiscardPileTitle,
				Cards = deck.DiscardPile.ToList(),
				Mode = CardListModalMode.CardList,
			});
		}

		public static void ClosePileView(EntityManager entityManager)
		{
			EventManager.Publish(new CloseCardListModalEvent());
		}

		public static bool TryGetOpenPileView(EntityManager entityManager, out bool isDrawPile)
		{
			isDrawPile = false;
			var modal = entityManager.GetEntitiesWithComponent<CardListModal>().FirstOrDefault()?.GetComponent<CardListModal>();
			if (modal == null || !modal.IsOpen) return false;

			if (modal.Title == DrawPileTitle)
			{
				isDrawPile = true;
				return true;
			}

			if (modal.Title == DiscardPileTitle)
			{
				isDrawPile = false;
				return true;
			}

			return false;
		}

		public static bool IsUnrelatedModalOpen(EntityManager entityManager)
		{
			var modal = entityManager.GetEntitiesWithComponent<CardListModal>().FirstOrDefault()?.GetComponent<CardListModal>();
			return modal != null && modal.IsOpen && !TryGetOpenPileView(entityManager, out _);
		}
	}
}
