using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class HeartforgeCuirass : EquipmentBase
  {
    private readonly int VigorAmount = 1;

    public HeartforgeCuirass()
    {
      Id = "heartforge_cuirass";
      Name = "Heartforge Cuirass";
      Slot = EquipmentSlot.Chest;
      Block = 1;
      Color = CardData.CardColor.Black;
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
