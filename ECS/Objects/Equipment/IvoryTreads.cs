using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Objects.Equipment
{
    public class IvoryTreads : EquipmentBase
    {
        public IvoryTreads()
        {
            Id = "ivory_treads";
            Name = "Ivory Treads";
            Slot = EquipmentSlot.Legs;
            Block = 1;
            Color = CardData.CardColor.White;
            FlavorText = "Soft leather over hard miles. The faithful learn to keep walking.";
            CanActivate = () => false;
        }
    }
}
