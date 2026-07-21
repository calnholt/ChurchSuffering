using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class TemperTheBlade : CardBase
    {
        private int SharpenAmount = 4;

        private int BlockUpgrade = 1;

        public TemperTheBlade()
        {
            CardId = "temper_the_blade";
            Name = "Temper the Blade";
            Target = "Player";
            Text = $"Gain sharpen {SharpenAmount}.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Sharpen,
                    Delta = SharpenAmount
                });
            };
            OnUpgrade = (entityManager, card) =>
            {
                Block += BlockUpgrade;
            };
        }
    }
}
