using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class WarbringerBracers : EquipmentBase
  {
    private readonly int MightAmount = 1;

    public WarbringerBracers()
    {
      Id = "warbringer_bracers";
      Name = "Warbringer Bracers";
      Slot = EquipmentSlot.Arms;
      Block = 0;
      Color = CardData.CardColor.Red;
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
