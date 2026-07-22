using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class CarpeDiem : CardBase
    {
        private int CourageGain = 5;
        private int MightGainUpgrade = 1;

        public CarpeDiem()
        {
            CardId = CardIds.CarpeDiem.ToKey();
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
