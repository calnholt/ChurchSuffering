using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class PurgingBracers : EquipmentBase
  {
    private readonly int Cost = 1;
    private readonly int Aggression = 2;
    public PurgingBracers()
    {
      Id = "purging_bracers";
      Name = "Purging Bracers";
      Slot = EquipmentSlot.Arms;
      Block = 2;
      Uses = 1;

      Color = CardData.CardColor.White;
      Text = $"Gain {Aggression} aggression. Lose {Cost} use.";
      CanActivateDuringActionPhase = true;
      CanActivate = () => RemainingUses >= Cost;
      
      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Aggression, Delta = Aggression });
        for (int i = 0; i < Cost; i++)
        {
          RemainingUses--;
        }
      };
    }
  }
}
