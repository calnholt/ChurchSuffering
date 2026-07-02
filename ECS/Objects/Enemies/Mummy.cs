using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks;

public class Mummy : EnemyBase
{
  public Mummy()
  {
    Id = EnemyId.Mummy;
    Name = "Mummy";
    HP = 26;
  }
  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    if (turnNumber == 5)
    {
      return [EnemyAttackId.Leprosy];
    }
    int random = Random.Shared.Next(0, 100);
    if (random <= 70)
    {
      return [EnemyAttackId.Entomb];
    }
    return [EnemyAttackId.Mummify];
  }
}

public class Entomb : EnemyAttackBase
{
  public Entomb()
  {
    Id = EnemyAttackId.Entomb;
    Name = "Entomb";
    Damage = 10;
    BlockRequiredToPreventEffect = Random.Shared.Next(0, 100) <= 50 ? 6 : 7;

    OnAttackReveal = (entityManager) => 
    {
      Text = EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, "Apply brittle to the top card of your draw pile.");
    };

    OnDamageThresholdMet = (entityManager) =>
    {
      EventManager.Publish(new ApplyCardApplicationEvent
      {
        Amount = 1,
        Type = CardApplicationType.Brittle,
        Target = CardApplicationTarget.TopXCards,
      });
    };
  }
}

public class Mummify : EnemyAttackBase
{
  private int Scar = 2;
  public Mummify()
  {
    Id = EnemyAttackId.Mummify;
    Name = "Mummify";
    Damage = 10;
    BlockRequiredToPreventEffect = Random.Shared.Next(0, 100) <= 50 ? 6 : 7;

    OnAttackReveal = (entityManager) => 
    {
      Text = EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, $"Gain {Scar} scars.");
    };

    OnDamageThresholdMet = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = Scar });
    };
  }
}

public class Leprosy : EnemyAttackBase
{
  private int Brittle = 2;
  public Leprosy()
  {
    Id = EnemyAttackId.Leprosy;
    Name = "Leprosy";
    Damage = 9;
    Text = $"On attack - {Brittle} random cards in your hand become brittle.";

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new ApplyCardApplicationEvent
      {
        Amount = Brittle,
        Type = CardApplicationType.Brittle,
        Target = CardApplicationTarget.Hand,
      });
    };
    
  }
}
