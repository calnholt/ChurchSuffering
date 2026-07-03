using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class PiercedHeartPlate : EquipmentBase
  {
    private readonly int Cost = 2;
    private readonly int Courage = 2;
    public PiercedHeartPlate()
    {
      Id = "pierced_heart_plate";
      Name = "Pierced Heart Plate";
      Slot = EquipmentSlot.Chest;
      Block = 1;
      Uses = 2;
      Color = CardData.CardColor.Black;
      Text = $"Gain {Courage} courage. Costs {Cost} uses.";
      CanActivateDuringActionPhase = true;
      CanActivate = () => RemainingUses == Uses;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ModifyCourageRequestEvent { Delta = Courage, Type = ModifyCourageType.Gain});
        for (int i = 0; i < Cost; i++)
        {
          RemainingUses--;
        }
      };
    }
  }
}
