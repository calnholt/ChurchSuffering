using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class SanctifiedCirclet : EquipmentBase
  {
    private readonly int TemperanceAmount = 2;

    public SanctifiedCirclet()
    {
      Id = "sanctified_circlet";
      Name = "Sanctified Circlet";
      Slot = EquipmentSlot.Head;
      Block = 0;
      Color = CardData.CardColor.White;
      Text = $"Gain {TemperanceAmount} temperance.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ModifyTemperanceEvent { Delta = TemperanceAmount });
      };
    }
  }
}
