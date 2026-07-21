using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Objects.Equipment
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
            FlavorText = "Standard issue of the order. Proofed for the grip that does not slip.";
            CanActivate = () => false;
        }
    }
}