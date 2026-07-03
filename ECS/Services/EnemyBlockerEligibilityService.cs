using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Services
{
	public static class EnemyBlockerEligibilityService
	{
		public static int CountEligibleBlockers(EntityManager entityManager, PlannedAttack plannedAttack)
		{
			if (entityManager == null || plannedAttack?.AttackDefinition == null) return 0;

			return CountEligibleHandBlockers(entityManager, plannedAttack)
				+ CountEligibleEquipmentBlockers(entityManager, plannedAttack);
		}

		public static bool IsEligibleHandBlocker(EntityManager entityManager, Entity card, PlannedAttack plannedAttack)
		{
			if (entityManager == null || card == null || plannedAttack?.AttackDefinition == null) return false;

			var deck = GetDeck(entityManager);
			if (deck?.Hand == null || !deck.Hand.Contains(card)) return false;

			var data = card.GetComponent<CardData>();
			if (data?.Card == null) return false;
			if (data.Card.IsWeapon || data.Card.IsToken) return false;
			if (card.GetComponent<Intimidated>() != null) return false;
			if (card.GetComponent<Pledge>() != null) return false;
			if (card.GetComponent<CannotBlockThisAttack>() != null) return false;
			if (card.GetComponent<AssignedBlockCard>() != null) return false;
			if (card.GetComponent<AnimatingHandToDiscard>() != null) return false;
			if (card.GetComponent<AnimatingHandToZone>() != null) return false;
			if (card.GetComponent<AnimatingHandToDrawPile>() != null) return false;
			if (card.GetComponent<SelectedForPayment>() != null) return false;
			if (card.GetComponent<FilteredFromHand>() != null) return false;

			if (data.Card.Type == CardType.Block
				&& data.Card.CanPlay != null
				&& !data.Card.CanPlay(entityManager, card))
			{
				return false;
			}

			if (card.GetComponent<Shackle>() != null && !AllShackledBlockCardsArePlayable(entityManager, deck.Hand))
			{
				return false;
			}

			return CardColorQualificationService.MeetsBlockingRestriction(
				card,
				plannedAttack.AttackDefinition.BlockingRestrictionType);
		}

		public static bool IsEligibleEquipmentBlocker(EntityManager entityManager, Entity equipmentEntity, PlannedAttack plannedAttack)
		{
			if (entityManager == null || equipmentEntity == null || plannedAttack?.AttackDefinition == null) return false;

			var equipped = equipmentEntity.GetComponent<EquippedEquipment>();
			var equipment = equipped?.Equipment;
			if (equipment == null) return false;
			if (!equipment.HasUses) return false;
			if (equipment.Block <= 0) return false;

			var zone = equipmentEntity.GetComponent<EquipmentZone>();
			if (zone != null && zone.Zone != EquipmentZoneType.Default) return false;

			return EquipmentMeetsBlockingRestriction(
				equipment.Color,
				plannedAttack.AttackDefinition.BlockingRestrictionType);
		}

		private static int CountEligibleHandBlockers(EntityManager entityManager, PlannedAttack plannedAttack)
		{
			var deck = GetDeck(entityManager);
			if (deck?.Hand == null) return 0;

			return deck.Hand.Count(card => IsEligibleHandBlocker(entityManager, card, plannedAttack));
		}

		private static int CountEligibleEquipmentBlockers(EntityManager entityManager, PlannedAttack plannedAttack)
		{
			return entityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Count(equipment => IsEligibleEquipmentBlocker(entityManager, equipment, plannedAttack));
		}

		private static Deck GetDeck(EntityManager entityManager)
		{
			return entityManager.GetEntitiesWithComponent<Deck>()
				.FirstOrDefault()
				?.GetComponent<Deck>();
		}

		private static bool AllShackledBlockCardsArePlayable(EntityManager entityManager, IEnumerable<Entity> hand)
		{
			var shackledCards = hand.Where(card => card.GetComponent<Shackle>() != null);
			foreach (var shackledCard in shackledCards)
			{
				var data = shackledCard.GetComponent<CardData>();
				if (data?.Card == null || data.Card.Type != CardType.Block) continue;
				if (data.Card.CanPlay != null && !data.Card.CanPlay(entityManager, shackledCard))
				{
					return false;
				}
			}

			return true;
		}

		private static bool EquipmentMeetsBlockingRestriction(
			CardData.CardColor color,
			BlockingRestrictionType restriction)
		{
			return restriction switch
			{
				BlockingRestrictionType.OnlyRed => color == CardData.CardColor.Red,
				BlockingRestrictionType.OnlyWhite => color == CardData.CardColor.White,
				BlockingRestrictionType.OnlyBlack => color == CardData.CardColor.Black,
				BlockingRestrictionType.NotRed => color != CardData.CardColor.Red,
				BlockingRestrictionType.NotWhite => color != CardData.CardColor.White,
				BlockingRestrictionType.NotBlack => color != CardData.CardColor.Black,
				_ => true,
			};
		}
	}
}
