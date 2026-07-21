using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Objects.Equipment
{
    public class ScarletVest : EquipmentBase
    {
        public ScarletVest()
        {
            Id = "scarlet_vest";
            Name = "Scarlet Vest";
            Slot = EquipmentSlot.Chest;
            Block = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Cut close and dyed deep. Worn by crusaders who prefer speed to ceremony.";
            CanActivate = () => false;
        }
    }
}
