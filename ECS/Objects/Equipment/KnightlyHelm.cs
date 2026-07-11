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
            CanActivate = () => false;
        }
    }
}