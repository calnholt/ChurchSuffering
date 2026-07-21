using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Objects.Equipment
{
    public class ScarletCoif : EquipmentBase
    {
        public ScarletCoif()
        {
            Id = "scarlet_coif";
            Name = "Scarlet Coif";
            Slot = EquipmentSlot.Head;
            Block = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Dyed for the field. A lighter hood for those who mean to press the attack.";
            CanActivate = () => false;
        }
    }
}
