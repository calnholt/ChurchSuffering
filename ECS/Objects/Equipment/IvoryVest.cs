using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class IvoryVest : EquipmentBase
    {
        public IvoryVest()
        {
            Id = "ivory_vest";
            Name = "Ivory Vest";
            Slot = EquipmentSlot.Chest;
            Block = 1;
            Color = CardData.CardColor.White;
            FlavorText = "Blessed linen, light enough to march in and strong enough to turn a glancing blow.";
            CanActivate = () => false;
        }
    }
}
