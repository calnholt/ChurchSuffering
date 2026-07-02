using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class RallyTheFaithful : CardBase
    {
        private int MightAmount = 1;

        private int CourageAmountUpgrade = 1;
        public RallyTheFaithful()
        {
            CardId = "rally_the_faithful";
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
