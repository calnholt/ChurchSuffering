using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class IvoryWraps : EquipmentBase
    {
        public IvoryWraps()
        {
            Id = "ivory_wraps";
            Name = "Ivory Wraps";
            Slot = EquipmentSlot.Arms;
            Block = 1;
            Color = CardData.CardColor.White;
            FlavorText = "Cloth bindings worn thin on the road. They still hold when the strike comes.";
            CanActivate = () => false;
        }
    }
}
