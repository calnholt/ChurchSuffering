using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Objects.Equipment
{
    public class ScarletTreads : EquipmentBase
    {
        public ScarletTreads()
        {
            Id = "scarlet_treads";
            Name = "Scarlet Treads";
            Slot = EquipmentSlot.Legs;
            Block = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Red leather scuffed at the toe. Built for closing distance.";
            CanActivate = () => false;
        }
    }
}
