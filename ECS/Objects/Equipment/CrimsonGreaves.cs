using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class CrimsonGreaves : EquipmentBase
    {
        public CrimsonGreaves()
        {
            Id = "crimson_greaves";
            Name = "Crimson Greaves";
            Slot = EquipmentSlot.Legs;
            Block = 2;
            Uses = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Weighted steel for feet that refuse to give ground.";
            CanActivate = () => false;
        }
    }
}
