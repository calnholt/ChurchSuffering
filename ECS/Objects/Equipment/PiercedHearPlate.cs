using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class PiercedHeartPlate : EquipmentBase
  {
    private readonly int Courage = 2;
    private readonly int BleedAmount = 1;

    public PiercedHeartPlate()
    {
      Id = "pierced_heart_plate";
      Name = "Pierced Heart Plate";
      Slot = EquipmentSlot.Chest;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = $"Gain {Courage} courage. Gain {BleedAmount} bleed.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ModifyCourageRequestEvent { Delta = Courage, Type = ModifyCourageType.Gain });
        EventManager.Publish(new ApplyPassiveEvent
        {
          Target = entityManager.GetEntity("Player"),
          Type = AppliedPassiveType.Bleed,
          Delta = BleedAmount
        });
      };
    }
  }
}
