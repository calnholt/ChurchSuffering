using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class ColorlessBlock : CardBase
    {
        public ColorlessBlock()
        {
            CardId = CardIds.Colorless3Block.ToKey();
            Rarity = Rarity.Common;
            Name = "Protect";
            Block = 3;
            Type = CardType.Block;
            CanAddToLoadout = false;
        }
    }
}
