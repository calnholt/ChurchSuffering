using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Services
{
    public enum PledgeAvailabilityFailure
    {
        None,
        Disabled,
        NotActionPhase,
        AlreadyPledgedThisActionPhase,
        CardAlreadyPledged,
        MissingHand,
        NoEligibleCard,
    }

    public enum PledgeCardEligibilityFailure
    {
        None,
        MissingCard,
        MissingCardData,
        AlreadyPledged,
        Sealed,
        Weapon,
        Block,
        Relic,
        Token,
    }

    public readonly record struct PledgeAvailabilityResult(
        bool IsAvailable,
        PledgeAvailabilityFailure Failure);

    public readonly record struct PledgeCardEligibilityResult(
        bool IsEligible,
        PledgeCardEligibilityFailure Failure,
        string RejectionMessage);

    public static class PledgeAvailabilityService
    {
        public static PledgeAvailabilityResult Evaluate(EntityManager entityManager)
        {
            if (!StateSingleton.IsPledgeEnabled)
                return Unavailable(PledgeAvailabilityFailure.Disabled);

            var phaseEntity = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
            var phase = phaseEntity?.GetComponent<PhaseState>();
            if (phase?.Sub != SubPhase.Action)
                return Unavailable(PledgeAvailabilityFailure.NotActionPhase);

            if (phaseEntity.GetComponent<PledgeAvailabilityState>()?.PledgedThisActionPhase == true)
                return Unavailable(PledgeAvailabilityFailure.AlreadyPledgedThisActionPhase);

            if (entityManager.GetEntitiesWithComponent<Pledge>().Any())
                return Unavailable(PledgeAvailabilityFailure.CardAlreadyPledged);

            var deck = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null)
                return Unavailable(PledgeAvailabilityFailure.MissingHand);

            if (!deck.Hand.Any(card => EvaluateCard(entityManager, card).IsEligible))
                return Unavailable(PledgeAvailabilityFailure.NoEligibleCard);

            return new PledgeAvailabilityResult(true, PledgeAvailabilityFailure.None);
        }

        public static bool IsAvailable(EntityManager entityManager)
        {
            return Evaluate(entityManager).IsAvailable;
        }

        public static PledgeCardEligibilityResult EvaluateCard(
            EntityManager entityManager,
            Entity card)
        {
            if (card == null)
                return Ineligible(PledgeCardEligibilityFailure.MissingCard);

            var cardData = card.GetComponent<CardData>();
            if (cardData?.Card == null)
                return Ineligible(PledgeCardEligibilityFailure.MissingCardData);

            if (card.GetComponent<Pledge>() != null)
                return Ineligible(PledgeCardEligibilityFailure.AlreadyPledged);

            if (card.GetComponent<Sealed>() != null)
                return Ineligible(PledgeCardEligibilityFailure.Sealed, "Sealed cards cannot be pledged!");

            if (cardData.Card.IsWeapon)
                return Ineligible(PledgeCardEligibilityFailure.Weapon, "Can't pledge weapons!");

            if (cardData.Card.Type == CardType.Block
                && !PledgeCardRestrictionOverrideService.IsOverridden(
                    entityManager,
                    card,
                    PledgeCardRestriction.Block))
            {
                return Ineligible(PledgeCardEligibilityFailure.Block, "Can't pledge block cards!");
            }

            if (cardData.Card.Type == CardType.Relic)
                return Ineligible(PledgeCardEligibilityFailure.Relic, "Can't pledge relics!");

            if (cardData.Card.IsToken)
                return Ineligible(PledgeCardEligibilityFailure.Token, "Can't pledge token cards!");

            return new PledgeCardEligibilityResult(true, PledgeCardEligibilityFailure.None, string.Empty);
        }

        public static bool IsCardEligible(EntityManager entityManager, Entity card)
        {
            return EvaluateCard(entityManager, card).IsEligible;
        }

        private static PledgeAvailabilityResult Unavailable(PledgeAvailabilityFailure failure)
        {
            return new PledgeAvailabilityResult(false, failure);
        }

        private static PledgeCardEligibilityResult Ineligible(
            PledgeCardEligibilityFailure failure,
            string rejectionMessage = "")
        {
            return new PledgeCardEligibilityResult(false, failure, rejectionMessage);
        }
    }
}
