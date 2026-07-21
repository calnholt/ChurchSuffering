using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class PurgingBracers : EquipmentBase
  {
    private readonly int Aggression = 2;
    public PurgingBracers()
    {
      Id = "purging_bracers";
      Name = "Purging Bracers";
      Slot = EquipmentSlot.Arms;
      Block = 1;

      Color = CardData.CardColor.Black;
      Text = $"Gain {Aggression} aggression.";
      CanActivateDuringActionPhase = true;
      
      OnActivate = (entityManager, entity) =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Aggression, Delta = Aggression });
      };
    }
  }
}
