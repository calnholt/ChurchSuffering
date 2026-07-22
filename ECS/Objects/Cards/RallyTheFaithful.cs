using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class RallyTheFaithful : CardBase
    {
        private int MightAmount = 1;

        private int CourageAmountUpgrade = 1;
        public RallyTheFaithful()
        {
            CardId = CardIds.RallyTheFaithful.ToKey();
            Rarity = Rarity.Common;
            Name = "Rally the Faithful";
            Target = "Player";
            Text = $"Gain {MightAmount} might.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Might,
                    Delta = MightAmount
                });
                if (IsUpgraded)
                {
                    EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageAmountUpgrade, Type = ModifyCourageType.Gain });
                }
            };
            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Gain {MightAmount} might and {CourageAmountUpgrade} courage.";
            };
        }
    }
}
