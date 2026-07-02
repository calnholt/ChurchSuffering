using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class ZealousVow : CardBase
    {
        private int SharpenAmount = 2;
        private int AggressionAmount = 2;

        public ZealousVow()
        {
            CardId = "zealous_vow";
            Name = "Zealous Vow";
            Target = "Player";
            Text = $"Gain {AggressionAmount} aggression.\n\nWhen this is pledged, gain {SharpenAmount} sharpen. ";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                if (IsUpgraded)
                {
                    EventManager.Publish(new ModifyHpRequestEvent {
                        Source = player,
                        Target = enemy,
                        Delta = -1,
                    });
                }
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Aggression,
                    Delta = AggressionAmount
                });
            };

            OnPledged = (entityManager, card) =>
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
                Type = CardType.Attack;
                Damage = 1;
            };
        }
    }
}
