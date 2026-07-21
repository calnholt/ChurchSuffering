using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class WhetstoneGauntlets : EquipmentBase
  {
    private readonly int SharpenAmount = 2;

    public WhetstoneGauntlets()
    {
      Id = "whetstone_gauntlets";
      Name = "Whetstone Gauntlets";
      Slot = EquipmentSlot.Arms;
      Block = 1;
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
