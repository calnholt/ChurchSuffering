using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class SanctifiedCirclet : EquipmentBase
  {
    private readonly int TemperanceAmount = 2;

    public SanctifiedCirclet()
    {
      Id = "sanctified_circlet";
      Name = "Sanctified Circlet";
      Slot = EquipmentSlot.Head;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = $"Gain {TemperanceAmount} temperance.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ModifyTemperanceEvent { Delta = TemperanceAmount });
      };
    }
  }
}
