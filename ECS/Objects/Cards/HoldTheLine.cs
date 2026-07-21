using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class HoldTheLine : CardBase
    {
        public int Courage = 1;
        public int BlockUpgrade = 1;
        public HoldTheLine()
        {
            CardId = "hold_the_line";
            Rarity = Rarity.Common;
            Name = "Hold the Line";
            Text = $"Gain {Courage} courage.";
            Block = 3;
            Type = CardType.Block;

            OnBlock = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = +Courage });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Block += BlockUpgrade;
            };
        }
    }
}