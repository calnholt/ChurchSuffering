using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class CrimsonVambraces : EquipmentBase
    {
        public CrimsonVambraces()
        {
            Id = "crimson_vambraces";
            Name = "Crimson Vambraces";
            Slot = EquipmentSlot.Arms;
            Block = 2;
            Uses = 1;
            Color = CardData.CardColor.Red;
            FlavorText = "Iron for the forearms that do not lower their guard.";
            CanActivate = () => false;
        }
    }
}
