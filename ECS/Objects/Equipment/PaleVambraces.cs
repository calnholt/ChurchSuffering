using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class PaleVambraces : EquipmentBase
    {
        public PaleVambraces()
        {
            Id = "pale_vambraces";
            Name = "Pale Vambraces";
            Slot = EquipmentSlot.Arms;
            Block = 2;
            Uses = 1;
            Color = CardData.CardColor.White;
            FlavorText = "Cold iron over the forearm. Heavy, honest, and difficult to ignore.";
            CanActivate = () => false;
        }
    }
}
