using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class WhetstoneGauntlets : EquipmentBase
  {
    private readonly int SharpenAmount = 2;

    public WhetstoneGauntlets()
    {
      Id = "whetstone_gauntlets";
      Name = "Whetstone Gauntlets";
      Slot = EquipmentSlot.Arms;
      Block = 0;
      Color = CardData.CardColor.Black;
      Text = $"Gain sharpen {SharpenAmount}.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ApplyPassiveEvent
        {
          Target = entityManager.GetEntity("Player"),
          Type = AppliedPassiveType.Sharpen,
          Delta = SharpenAmount
        });
      };
    }
  }
}
