using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class OathbreakerCoif : EquipmentBase
  {
    public OathbreakerCoif()
    {
      Id = "oathbreaker_coif";
      Name = "Oathbreaker Coif";
      Slot = EquipmentSlot.Head;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = "Unpledge your pledged card.";
      CanActivateDuringActionPhase = true;

      CanActivate = () => PledgeService.TryFindPriorTurnPledgedCardInHand(EntityManager) != null;

      OnActivate = (entityManager, entity) =>
      {
        var pledgedCard = PledgeService.TryFindPriorTurnPledgedCardInHand(entityManager);
        if (pledgedCard == null) return;

        EventManager.Publish(new RemovePledgeFromCardRequested { Card = pledgedCard });
      };
    }

    public override void CantActivateMessage()
    {
      if (!IsAvailable)
      {
        base.CantActivateMessage();
        return;
      }

      EventManager.Publish(new CantPlayCardMessage { Message = "Requires a pledged card from a prior turn!" });
    }
  }
}
