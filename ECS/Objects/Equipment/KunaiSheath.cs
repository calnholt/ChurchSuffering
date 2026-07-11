using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class KunaiSheath : EquipmentBase
  {
    private readonly int KunaiAmount = 1;

    public KunaiSheath()
    {
      Id = "kunai_sheath";
      Name = "Kunai Sheath";
      Slot = EquipmentSlot.Arms;
      Block = 0;
      Color = CardData.CardColor.Black;
      Text = $"Add {KunaiAmount} Kunai to your hand.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
        var kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White, false, 1);
        EventManager.Publish(new CardMoveRequested
        {
          Card = kunai,
          Deck = deckEntity,
          Destination = CardZoneType.Hand,
          Reason = "KunaiSheath"
        });
      };
    }
  }
}
