using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class ScarletVest : EquipmentBase
    {
        public ScarletVest()
        {
            Id = "scarlet_vest";
            Name = "Scarlet Vest";
            Slot = EquipmentSlot.Chest;
            Block = 1;
            Uses = 2;
            Color = CardData.CardColor.Red;
            FlavorText = "Cut close and dyed deep. Worn by crusaders who prefer speed to ceremony.";
            CanActivate = () => false;
        }
    }
}
