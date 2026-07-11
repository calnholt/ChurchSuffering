using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class HelmOfSeeing : EquipmentBase
  {
    private readonly int ResurrectAmount = 1;
    public HelmOfSeeing()
    {
      Id = "helm_of_seeing";
      Name = "Helm of Seeing";
      Slot = EquipmentSlot.Head;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = $"Resurrect {ResurrectAmount}.";
      ActivationEffectRecipe = DefensiveGuardEffect();
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
      };
    }
  }
}
