using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class HeartforgeCuirass : EquipmentBase
  {
    private readonly int VigorAmount = 1;

    public HeartforgeCuirass()
    {
      Id = "heartforge_cuirass";
      Name = "Heartforge Cuirass";
      Slot = EquipmentSlot.Chest;
      Block = 0;
      Color = CardData.CardColor.White;
      Text = $"Gain {VigorAmount} vigor.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ApplyPassiveEvent
        {
          Target = entityManager.GetEntity("Player"),
          Type = AppliedPassiveType.Vigor,
          Delta = VigorAmount
        });
      };
    }
  }
}
