using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class HelmOfSeeing : EquipmentBase
  {
    private readonly int Cost = 1;
    private readonly int Cards = 1;
    private readonly int Courage = 4;
    public HelmOfSeeing()
    {
      Id = "helm_of_seeing";
      Name = "Helm of Seeing";
      Slot = EquipmentSlot.Head;
      Block = 2;
      Uses = 1;
      Color = CardData.CardColor.Red;
      Text = $"Draw {Cards} card. Lose {Cost} use and {Courage} courage.";
      ActivationEffectRecipe = DefensiveGuardEffect();
      CanActivateDuringActionPhase = true;
      CanActivate = () => {
        return RemainingUses == Uses && EntityManager.GetEntitiesWithComponent<Courage>().FirstOrDefault()?.GetComponent<Courage>().Amount >= Courage;
      };
      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new RequestDrawCardsEvent { Count = Cards });
        EventManager.Publish(new ModifyCourageRequestEvent { Delta = -Courage, Type = ModifyCourageType.Spent });
        for (int i = 0; i < Cost; i++)
        {
          RemainingUses--;
        }
      };
    }

    public override void CantActivateMessage()
    {
      if (RemainingUses != Uses)
      {
        EventManager.Publish(new CantPlayCardMessage { Message = "Not enough uses!" });
        return;
      }
      var courage = EntityManager.GetEntitiesWithComponent<Courage>().FirstOrDefault()?.GetComponent<Courage>().Amount;
      if (courage < Courage)
      {
        EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {Courage} courage!" });
        return;
      }
    }
  }
}
