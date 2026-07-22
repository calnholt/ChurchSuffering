using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Services
{
	public enum HandBlockEligibilityFailure
	{
		None,
		InvalidContext,
		NotInHand,
		MissingCardData,
		WeaponOrToken,
		Intimidated,
		Pledged,
		CannotBlockThisAttack,
		AlreadyAssigned,
		Transitioning,
		SelectedForPayment,
		FilteredFromHand,
		CardUnavailable,
		ShackledCardUnavailable,
		BlockingRestriction,
	}

	public readonly record struct HandBlockEligibilityResult(
		bool IsEligible,
		HandBlockEligibilityFailure Failure,
		string RejectionMessage);

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
			return EvaluateHandBlocker(entityManager, card, plannedAttack).IsEligible;
		}

		public static HandBlockEligibilityResult EvaluateHandBlocker(
			EntityManager entityManager,
			Entity card,
			PlannedAttack plannedAttack)
		{
			if (entityManager == null || card == null || plannedAttack?.AttackDefinition == null)
				return Ineligible(HandBlockEligibilityFailure.InvalidContext);

			var deck = GetDeck(entityManager);
			if (deck?.Hand == null || !deck.Hand.Contains(card))
				return Ineligible(HandBlockEligibilityFailure.NotInHand);

			var data = card.GetComponent<CardData>();
			if (data?.Card == null) return Ineligible(HandBlockEligibilityFailure.MissingCardData);
			if (data.Card.IsWeapon || data.Card.IsToken)
				return Ineligible(HandBlockEligibilityFailure.WeaponOrToken);
			if (card.GetComponent<Intimidated>() != null)
				return Ineligible(
					HandBlockEligibilityFailure.Intimidated,
					"Can't block with intimidated cards!");
			if (card.GetComponent<Pledge>() != null
				&& !HandBlockRestrictionOverrideService.IsOverridden(
					entityManager,
					card,
					plannedAttack,
					HandBlockRestriction.Pledged))
			{
				return Ineligible(
					HandBlockEligibilityFailure.Pledged,
					"Can't block with pledged card!");
			}
			if (card.GetComponent<CannotBlockThisAttack>() is CannotBlockThisAttack cannotBlock)
				return Ineligible(HandBlockEligibilityFailure.CannotBlockThisAttack, cannotBlock.Reason);
			if (card.GetComponent<AssignedBlockCard>() != null)
				return Ineligible(HandBlockEligibilityFailure.AlreadyAssigned);
			if (card.GetComponent<AnimatingHandToDiscard>() != null
				|| card.GetComponent<AnimatingHandToZone>() != null
				|| card.GetComponent<AnimatingHandToDrawPile>() != null)
			{
				return Ineligible(HandBlockEligibilityFailure.Transitioning);
			}
			if (card.GetComponent<SelectedForPayment>() != null)
				return Ineligible(HandBlockEligibilityFailure.SelectedForPayment);
			if (card.GetComponent<FilteredFromHand>() != null)
				return Ineligible(HandBlockEligibilityFailure.FilteredFromHand);

			if (data.Card.Type == CardType.Block
				&& data.Card.CanPlay != null
				&& !data.Card.CanPlay(entityManager, card))
			{
				return Ineligible(HandBlockEligibilityFailure.CardUnavailable);
			}

			if (card.GetComponent<Shackle>() != null && !AllShackledBlockCardsArePlayable(entityManager, deck.Hand))
			{
				return Ineligible(
					HandBlockEligibilityFailure.ShackledCardUnavailable,
					"All shackled cards must be playable!");
			}

			var restriction = plannedAttack.AttackDefinition.BlockingRestrictionType;
			if (!CardColorQualificationService.MeetsBlockingRestriction(card, restriction))
			{
				var message = EnemyAttackTextHelper.GetBlockingRestrictionText(restriction);
				if (message.EndsWith('.')) message = message[..^1] + "!";
				return Ineligible(HandBlockEligibilityFailure.BlockingRestriction, message);
			}

			return new HandBlockEligibilityResult(true, HandBlockEligibilityFailure.None, string.Empty);
		}

		public static bool IsEligibleEquipmentBlocker(EntityManager entityManager, Entity equipmentEntity, PlannedAttack plannedAttack)
		{
			if (entityManager == null || equipmentEntity == null || plannedAttack?.AttackDefinition == null) return false;

			var equipped = equipmentEntity.GetComponent<EquippedEquipment>();
			var equipment = equipped?.Equipment;
			if (equipment == null) return false;
			if (!equipment.IsAvailable) return false;
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

		private static HandBlockEligibilityResult Ineligible(
			HandBlockEligibilityFailure failure,
			string rejectionMessage = "")
		{
			return new HandBlockEligibilityResult(false, failure, rejectionMessage ?? string.Empty);
		}
	}
}
