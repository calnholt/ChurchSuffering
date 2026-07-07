using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Services
{
	public static class BattleTransientStateCleanupService
	{
		public static void ClearInteractionState(EntityManager entityManager)
		{
			if (entityManager == null) return;

			foreach (var assigned in entityManager.GetEntitiesWithComponent<AssignedBlockCard>().ToList())
			{
				CardTransientStateService.ClearAssignedBlockHotKey(entityManager, assigned);
				entityManager.RemoveComponent<AssignedBlockCard>(assigned);
				var equipmentZone = assigned.GetComponent<EquipmentZone>();
				if (equipmentZone != null) equipmentZone.Zone = EquipmentZoneType.Default;
			}

			foreach (var card in entityManager.GetEntitiesWithComponent<CardData>().ToList())
			{
				CardTransientStateService.ClearAssignedBlockHotKey(entityManager, card);
				RemoveIfPresent<SelectedForPayment>(entityManager, card);
				RemoveIfPresent<MarkedForSpecificDiscard>(entityManager, card);
				RemoveIfPresent<MarkedForReturnToDeck>(entityManager, card);
				RemoveIfPresent<MarkedForBottomOfDrawPile>(entityManager, card);
				RemoveIfPresent<MarkedForExhaust>(entityManager, card);
				RemoveIfPresent<MarkedForEndOfTurnDiscard>(entityManager, card);
				RemoveIfPresent<AnimatingHandToDiscard>(entityManager, card);
				RemoveIfPresent<AnimatingHandToZone>(entityManager, card);
				RemoveIfPresent<AnimatingHandToDrawPile>(entityManager, card);
				RemoveIfPresent<CardToDiscardFlight>(entityManager, card);
				RemoveIfPresent<FilteredFromHand>(entityManager, card);
				RemoveIfPresent<CannotBlockThisAttack>(entityManager, card);
			}

			var payCostState = entityManager.GetEntitiesWithComponent<PayCostOverlayState>()
				.FirstOrDefault()?.GetComponent<PayCostOverlayState>();
			if (payCostState != null)
			{
				payCostState.IsOpen = false;
				payCostState.CardToPlay = null;
				payCostState.SelectedCards.Clear();
				payCostState.ConsumedCostByCardId.Clear();
			}

			var ambushState = entityManager.GetEntitiesWithComponent<AmbushState>()
				.FirstOrDefault()?.GetComponent<AmbushState>();
			if (ambushState != null)
			{
				ambushState.IsActive = false;
				ambushState.IntroActive = false;
				ambushState.TimerRemainingSeconds = 0f;
				ambushState.FiredAutoConfirm = false;
				ambushState.ActiveAttackSequence = 0;
			}

			var paymentCache = entityManager.GetEntitiesWithComponent<LastPaymentCache>()
				.FirstOrDefault()?.GetComponent<LastPaymentCache>();
			if (paymentCache != null)
			{
				paymentCache.CardPlayed = null;
				paymentCache.PaymentCards.Clear();
				paymentCache.HasData = false;
			}
		}

		private static void RemoveIfPresent<T>(EntityManager entityManager, Entity entity)
			where T : class, IComponent
		{
			if (entity.HasComponent<T>()) entityManager.RemoveComponent<T>(entity);
		}
	}
}
