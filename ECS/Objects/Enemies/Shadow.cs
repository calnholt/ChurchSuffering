using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Utils;

namespace ChurchSuffering.ECS.Objects.Enemies
{
  public class Shadow : EnemyBase
  {
    private int StartAnathema = 4;
    public Shadow()
    {
      Id = EnemyId.Shadow;
      Name = "Shadow";
      HP = 42;
      ClimbPool = ClimbEncounterPool.Late;

      OnStartOfBattle = (entityManager) =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Anathema, Delta = StartAnathema });
      };
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
      if (turnNumber % 2 == 0)
      {
        return ArrayUtils.TakeRandomWithoutReplacement(new List<EnemyAttackId> { EnemyAttackId.SnuffOutTheLight, EnemyAttackId.NightFall, EnemyAttackId.FromTheShadows, EnemyAttackId.UmbraSlice }, 3);
      }
      return ArrayUtils.TakeRandomWithoutReplacement(new List<EnemyAttackId> { EnemyAttackId.ShadowStrike, EnemyAttackId.DissipatingDarkness }, 1);
    }
  }
}

public class ShadowStrike : EnemyAttackBase
{
  private int AnathemaLoss = 1;
  public ShadowStrike()
  {
    Id = EnemyAttackId.ShadowStrike;
    Name = "Shadow Strike";
    Damage = 10;
    BlockRequiredToPreventEffect = 7;
    Text = $"{EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, $"The enemy loses {AnathemaLoss} anathema.")}";

    OnDamageThresholdMet = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Anathema, Delta = -AnathemaLoss });
    };
  }
}

public class EncroachingDarkness : EnemyAttackBase
{
  private int AnathemaGain = 1;
  public EncroachingDarkness()
  {
    Id = EnemyAttackId.DissipatingDarkness;
    Name = "Encroaching Darkness";
    Damage = 10;
    ConditionType = ConditionType.OnBlockedByAtLeast2Cards;
    Text = $"{EnemyAttackTextHelper.GetConditionText(ConditionType)} the enemy gains {AnathemaGain} anathema.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Anathema, Delta = AnathemaGain });
    };
  }
}

public class SnuffOutTheLight : EnemyAttackBase
{
  private int SilencedGain = 1;
  public SnuffOutTheLight()
  {
    Id = EnemyAttackId.SnuffOutTheLight;
    Name = "Snuff Out the Light";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = $"On hit - Gain {SilencedGain} silenced.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Silenced, Delta = SilencedGain });
    };
  }
}

public class FromTheShadows : EnemyAttackBase
{
  public FromTheShadows()
  {
    Id = EnemyAttackId.FromTheShadows;
    Name = "From the Shadows";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Intimidate, 1);

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new IntimidateEvent { Amount = 1 });
    };

  }
}

public class NightFall : EnemyAttackBase
{
  private int AnathemaLoss = 1;
  public NightFall()
  {
    Id = EnemyAttackId.NightFall;
    Name = "Night Fall";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = $"On hit - the enemy loses {AnathemaLoss} anathema.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Anathema, Delta = -AnathemaLoss });
    };
  }
}

public class UmbraSlice : EnemyAttackBase
{
  private int Scar = 1;
  public UmbraSlice()
  {
    Id = EnemyAttackId.UmbraSlice;
    Name = "Umbra Slice";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = $"On hit - gain {Scar} scar{(Scar > 1 ? "s" : "")}.";

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = Scar });
    };
  }
}
