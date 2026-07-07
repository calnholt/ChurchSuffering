using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class RenounceAndHone : CardBase
    {
        private int VigorAmount = 2;
        private int CourageAmount = 2;
        private int CourageAmountUpgrade = 2;

        public RenounceAndHone()
        {
            CardId = "renounce_and_hone";
            Name = "Renounce and Hone";
            Target = "Player";
            Text = $"As an additional cost, discard your pledged card that was not pledged this turn. Gain {VigorAmount} vigor and {GetCourageAmount(IsUpgraded)} courage.";
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var pledgedCard = PledgeService.TryFindPriorTurnPledgedCardInHand(entityManager);
                if (pledgedCard == null) return;

                var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                EventManager.Publish(new RemovePledgeFromCardRequested { Card = pledgedCard });
                EventManager.Publish(new CardMoveRequested
                {
                    Card = pledgedCard,
                    Deck = deckEntity,
                    Destination = CardZoneType.DiscardPile,
                    Reason = "RenounceAndHone"
                });

                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Vigor,
                    Delta = VigorAmount
                });
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = GetCourageAmount(IsUpgraded), Type = ModifyCourageType.Gain });
            };

            CanPlay = (entityManager, card) =>
            {
                return PledgeService.TryFindPriorTurnPledgedCardInHand(entityManager) != null;
            };

            OnCantPlay = (entityManager, card) =>
            {
                if (PledgeService.TryFindPriorTurnPledgedCardInHand(entityManager) == null)
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = "Requires a pledged card from a prior turn!" });
                }
            };
            OnUpgrade = (entityManager, card) =>
            {
                Text = $"As an additional cost, discard your pledged card that was not pledged this turn. Gain {VigorAmount} vigor and {GetCourageAmount(IsUpgraded)} courage.";
            };
        }
        private int GetCourageAmount(bool isUpgraded)
        {
            return isUpgraded ? CourageAmount + CourageAmountUpgrade : CourageAmount;
        }
    }
}
