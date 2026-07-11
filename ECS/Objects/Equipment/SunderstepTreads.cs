using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Equipment
{
  public class SunderstepTreads : EquipmentBase
  {
    public SunderstepTreads()
    {
      Id = "sunderstep_treads";
      Name = "Sunderstep Treads";
      Slot = EquipmentSlot.Legs;
      Block = 0;
      Color = CardData.CardColor.Red;
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
