using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Equipment
{
  public class SunderstepTreads : EquipmentBase
  {
    public SunderstepTreads()
    {
      Id = "sunderstep_treads";
      Name = "Sunderstep Treads";
      Slot = EquipmentSlot.Legs;
      Block = 1;
      Color = CardData.CardColor.Black;
      Text = "Remove all guard from the enemy.";
      CanActivateDuringActionPhase = true;

      OnActivate = (entityManager, entity) =>
      {
        var enemy = entityManager.GetEntity("Enemy");
        EventManager.Publish(new RemovePassive { Owner = enemy, Type = AppliedPassiveType.Guard });
      };
    }
  }
}
