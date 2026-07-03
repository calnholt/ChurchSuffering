using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Courageous : CardBase
    {
        private int CourageBonus = 3;
        private int CourageBonusUpgrade = 1;
        public Courageous()
        {
            CardId = "courageous";
            Rarity = Rarity.Starter;
            Name = "Courageous";
            Target = "Player";
            Text = $"Gain {GetCourageBonus(false)} courage. End your turn.";
            IsFreeAction = true;
            Type = CardType.Prayer;
            Block = 2;
            VisualEffectRecipe = PlayerBuffEffect();

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = GetCourageBonus(IsUpgraded), Type = ModifyCourageType.Gain });
                TimerScheduler.Schedule(0.1f, () => {
                    EventManager.Publish(new EndTurnRequested());
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Gain {GetCourageBonus(true)} courage. End your turn.";
            };
        }

        public int GetCourageBonus(bool isUpgraded)
        {
            return isUpgraded ? CourageBonus + CourageBonusUpgrade : CourageBonus;
        }
    }
}