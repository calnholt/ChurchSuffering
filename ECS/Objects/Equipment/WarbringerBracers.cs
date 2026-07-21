using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class WarbringerBracers : EquipmentBase
  {
    private readonly int MightAmount = 1;

    public WarbringerBracers()
    {
      Id = "warbringer_bracers";
      Name = "Warbringer Bracers";
      Slot = EquipmentSlot.Arms;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = $"Gain {MightAmount} might.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ApplyPassiveEvent
        {
          Target = entityManager.GetEntity("Player"),
          Type = AppliedPassiveType.Might,
          Delta = MightAmount
        });
      };
    }
  }
}
