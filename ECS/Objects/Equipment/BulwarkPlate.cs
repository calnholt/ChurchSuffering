using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class BulwarkPlate : EquipmentBase
  {
    private readonly int AegisAmount = 2;

    public BulwarkPlate()
    {
      Id = "bulwark_plate";
      Name = "Bulwark Plate";
      Slot = EquipmentSlot.Chest;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = $"Gain {AegisAmount} aegis.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ApplyPassiveEvent
        {
          Target = entityManager.GetEntity("Player"),
          Type = AppliedPassiveType.Aegis,
          Delta = AegisAmount
        });
      };
    }
  }
}
