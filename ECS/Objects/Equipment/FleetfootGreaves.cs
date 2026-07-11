using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class FleetfootGreaves : EquipmentBase
  {
    private readonly int ActionPointAmount = 1;

    public FleetfootGreaves()
    {
      Id = "fleetfoot_greaves";
      Name = "Fleetfoot Greaves";
      Slot = EquipmentSlot.Legs;
      Block = 0;
      Color = CardData.CardColor.Red;
      Text = $"Gain {ActionPointAmount} action point.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ModifyActionPointsEvent { Delta = ActionPointAmount });
      };
    }
  }
}
