using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Sacrifice : CardBase
    {
        private int ScarAmount = 1;
        private int TemperanceAmount = 1;
        private int ResurrectAmount = 2;

        public Sacrifice()
        {
            CardId = "sacrifice";
            Name = "Sacrifice";
            Target = "Player";
            Text = $"Gain {ScarAmount} scar, {TemperanceAmount} temperance, and resurrect {ResurrectAmount}.";
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
                    Type = AppliedPassiveType.Scar,
                    Delta = ScarAmount
                });
                EventManager.Publish(new ModifyTemperanceEvent { Delta = TemperanceAmount });
                EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
            };
        }
    }
}
