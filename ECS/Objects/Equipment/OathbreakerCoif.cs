using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
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
