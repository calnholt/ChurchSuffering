using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class PaleGreathelm : EquipmentBase
    {
        public PaleGreathelm()
        {
            Id = "pale_greathelm";
            Name = "Pale Greathelm";
            Slot = EquipmentSlot.Head;
            Block = 2;
            Uses = 1;
            Color = CardData.CardColor.White;
            FlavorText = "Hammered white steel, bleached by salt wind. The faithful advance with their faces guarded.";
            CanActivate = () => false;
        }
    }
}
