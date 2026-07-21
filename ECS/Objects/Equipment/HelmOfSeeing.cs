using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
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
