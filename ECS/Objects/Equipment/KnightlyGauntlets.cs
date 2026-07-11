using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class KnightlyGauntlets : EquipmentBase
    {
        public KnightlyGauntlets()
        {
            Id = "knightly_gauntlets";
            Name = "Knightly Gauntlets";
            Slot = EquipmentSlot.Arms;
            Block = 2;
            Color = CardData.CardColor.Black;
            CanActivate = () => false;
        }
    }
}