using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class KnightlyHelm : EquipmentBase
    {
        public KnightlyHelm()
        {
            Id = "knightly_helm";
            Name = "Knightly Helm";
            Slot = EquipmentSlot.Head;
            Block = 2;
            Color = CardData.CardColor.Black;
            FlavorText = "Standard issue of the order. Keeps rank heads down when arrows find the sky.";
            CanActivate = () => false;
        }
    }
}