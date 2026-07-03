using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class CrimsonCuirass : EquipmentBase
    {
        public CrimsonCuirass()
        {
            Id = "crimson_cuirass";
            Name = "Crimson Cuirass";
            Slot = EquipmentSlot.Chest;
            Block = 2;
            Uses = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Plate tempered in campfires and conviction. The blow lands; you do not.";
            CanActivate = () => false;
        }
    }
}
