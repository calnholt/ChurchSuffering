using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class BloodMartyr : EnemyBase
{
  public BloodMartyr()
  {
    Id = EnemyId.BloodMartyr;
    Name = "Blood Martyr";
    HP = 35;
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    var attacks = new List<EnemyAttackId> { EnemyAttackId.Flagellation, EnemyAttackId.BloodWard, EnemyAttackId.BloodTithe, EnemyAttackId.Masochism };
    return ArrayUtils.TakeRandomWithoutReplacement(attacks, 2);
  }

  public override void Dispose()
  {
  }
}

public class Flagellation : EnemyAttackBase
{
  private const int BurnAmount = 1;
  private const int WoundedAmount = 1;

  public Flagellation()
  {
    Id = EnemyAttackId.Flagellation;
    Name = "Flagellation";
    Damage = 7;
    ConditionType = ConditionType.OnHit;
    Text = $"On attack - this enemy gains {BurnAmount} burn and {WoundedAmount} wounded.";

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent
      {
        Target = entityManager.GetEntity("Enemy"),
        Type = AppliedPassiveType.Burn,
        Delta = BurnAmount
      });
      EventManager.Publish(new ApplyPassiveEvent
      {
        Target = entityManager.GetEntity("Enemy"),
        Type = AppliedPassiveType.Wounded,
        Delta = WoundedAmount
      });
    };
  }
}

public class BloodWard : EnemyAttackBase
{
  private const int ArmorAmount = 1;

  public BloodWard()
  {
    Id = EnemyAttackId.BloodWard;
    Name = "Blood Ward";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = $"On hit - this enemy gains {ArmorAmount} armor.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent
      {
        Target = entityManager.GetEntity("Enemy"),
        Type = AppliedPassiveType.Armor,
        Delta = ArmorAmount
      });
    };
  }
}

public class Masochism : EnemyAttackBase
{
  private const int SelfDamage = 1;
  public Masochism()
  {
    Id = EnemyAttackId.Masochism;
    Name = "Masochism";
    Damage = 4;
    ConditionType = ConditionType.OnBlockedByAtLeast1Card;
    Text = $"On not blocked by at least one card - The enemy loses {SelfDamage} HP.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ModifyHpRequestEvent {
        Source = EntityManager.GetEntity("Enemy"),
        Target = EntityManager.GetEntity("Enemy"),
        Delta = -SelfDamage,
        DamageType = ModifyTypeEnum.Effect
      });
    };
  }
}

public class BloodTithe : EnemyAttackBase
{
  private const int BaseDamage = 3;

  public BloodTithe()
  {
    Id = EnemyAttackId.BloodTithe;
    Name = "Blood Tithe";
    Damage = BaseDamage;
    Text = "On hit - remove all burn and wounded from Blood Martyr.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new RemovePassive
      {
        Owner = entityManager.GetEntity("Enemy"),
        Type = AppliedPassiveType.Wounded,
      });
      EventManager.Publish(new RemovePassive
      {
        Owner = entityManager.GetEntity("Enemy"),
        Type = AppliedPassiveType.Burn,
      });
    };
  }
}
