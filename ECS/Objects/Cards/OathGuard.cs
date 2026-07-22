using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class OathGuard : CardBase
    {
        public override bool GrantsRandomDualColorOnUpgrade => true;

        public OathGuard()
        {
            CardId = CardIds.OathGuard.ToKey();
            Rarity = Rarity.Common;
            Name = "Oath Guard";
            Text = "This card cannot block if you don't have a pledged card.";
            Type = CardType.Block;
            Block = 6;
            VisualEffectRecipe = DefensiveGuardEffect();

            CanPlay = (entityManager, card) =>
                PledgeService.HasPledgedCardInHand(entityManager);

            OnCantPlay = (entityManager, card) =>
            {
                EventManager.Publish(new CantPlayCardMessage
                {
                    Message = "Requires a pledged card!"
                });
            };
        }
    }
}
