using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Objects.Equipment
{
    public class IvoryCoif : EquipmentBase
    {
        public IvoryCoif()
        {
            Id = "ivory_coif";
            Name = "Ivory Coif";
            Slot = EquipmentSlot.Head;
            Block = 1;
            Uses = 2;
            Color = CardData.CardColor.White;
            FlavorText = "Woven for the long vigil. Keeps the sun from your eyes and doubt from your thoughts.";
            CanActivate = () => false;
        }
    }
}
