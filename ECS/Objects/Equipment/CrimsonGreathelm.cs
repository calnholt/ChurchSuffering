using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class CrimsonGreathelm : EquipmentBase
    {
        public CrimsonGreathelm()
        {
            Id = "crimson_greathelm";
            Name = "Crimson Greathelm";
            Slot = EquipmentSlot.Head;
            Block = 2;
            Uses = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Forged for the front rank. Meant to be the last thing your enemy sees.";
            CanActivate = () => false;
        }
    }
}
