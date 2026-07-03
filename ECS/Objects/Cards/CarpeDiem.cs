using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class CarpeDiem : CardBase
    {
        private int CourageGain = 5;
        private int MightGainUpgrade = 1;

        public CarpeDiem()
        {
            CardId = "carpe_diem";
            Name = "Carpe Diem";
            Target = "Player";
            Text = $"Gain {CourageGain} courage. At the end of the turn, lose all courage.";
            IsFreeAction = true;
            Type = CardType.Prayer;
            Block = 2;
            VisualEffectRecipe = PlayerBuffEffect();

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageGain, Type = ModifyCourageType.Gain });
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.CarpeDiem,
                    Delta = 1
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Gain {CourageGain} courage and {MightGainUpgrade} might. At the end of the turn, lose all courage.";
            };
        }
    }
}
