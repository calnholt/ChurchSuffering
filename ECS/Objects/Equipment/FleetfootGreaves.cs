using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class FleetfootGreaves : EquipmentBase
  {
    private readonly int ActionPointAmount = 1;

    public FleetfootGreaves()
    {
      Id = "fleetfoot_greaves";
      Name = "Fleetfoot Greaves";
      Slot = EquipmentSlot.Legs;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = $"Gain {ActionPointAmount} action point.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ModifyActionPointsEvent { Delta = ActionPointAmount });
      };
    }
  }
}
