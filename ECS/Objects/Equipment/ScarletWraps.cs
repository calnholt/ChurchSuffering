using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class ScarletWraps : EquipmentBase
    {
        public ScarletWraps()
        {
            Id = "scarlet_wraps";
            Name = "Scarlet Wraps";
            Slot = EquipmentSlot.Arms;
            Block = 1;
            Uses = 2;
            Color = CardData.CardColor.Red;
            FlavorText = "Red wrappings that mark the hand before it marks the enemy.";
            CanActivate = () => false;
        }
    }
}
