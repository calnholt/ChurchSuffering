using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class HoldTheLine : CardBase
    {
        public int Courage = 1;
        public int BlockUpgrade = 1;
        public HoldTheLine()
        {
            CardId = CardIds.HoldTheLine.ToKey();
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