using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class KnightlyChest : EquipmentBase
    {
        public KnightlyChest()
        {
            Id = "knightly_chest";
            Name = "Knightly Chest";
            Slot = EquipmentSlot.Chest;
            Block = 2;
            Color = CardData.CardColor.Black;
            FlavorText = "Standard issue of the order. Meant to turn the blow that would stop the charge.";
            CanActivate = () => false;
        }
    }
}