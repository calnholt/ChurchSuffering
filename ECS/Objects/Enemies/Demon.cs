using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Utils;
using static Crusaders30XX.ECS.Systems.MustBeBlockedSystem;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks;

public class Demon : EnemyBase
{
  public Demon(EnemyDifficulty difficulty = EnemyDifficulty.Easy) : base(difficulty)
  {
    Id = "demon";
    Name = "Demon";
    HP = 29;
  }

  public override IEnumerable<string> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    var random = Random.Shared.Next(0, 100);
    if (random >= 60)
    {
      return ["razor_maw"];
    }
    else if (random >= 20)
    {
      return ["scorching_claw"];
    }
    return ["infernal_execution"];
  }
}

public class RazorMaw : EnemyAttackBase
{
  private int Burn = 1;
  public RazorMaw()
  {
    Id = "razor_maw";
    Name = "Razor Maw";
    Damage = 9;
    AttackEffectRecipe = EnemyBiteEffect();
    BlockRequiredToPreventEffect = 7;
    Text = $"{EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, EnemyAttackTextHelper.GetText(EnemyAttackTextType.Burn, Burn, ConditionType))}";

    OnDamageThresholdMet = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Burn, Delta = Burn });
    };
  }
}

public class ScorchingClaw : EnemyAttackBase
{
  private int Burn = 1;
  public ScorchingClaw()
  {
    Id = "scorching_claw";
    Name = "Scorching Claw";
    Damage = 10;
    AttackEffectRecipe = EnemyClawSlashEffect();
    ConditionType = ConditionType.OnBlockedByAtLeast2Cards;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Burn, Burn, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Burn, Delta = Burn });
    };
  }
}

public class InfernalExecution : EnemyAttackBase
{
  private int Burn = 1;
  private int Threshold = 2;
  public InfernalExecution()
  {
    Id = "infernal_execution";
    Name = "Infernal Execution";
    Damage = 8;
    ConditionType = ConditionType.MustBeBlockedByAtLeast2Cards;

    OnChannelApplied = (entityManager) =>
    {
      Burn += Channel;
      Text = $"On attack - Gain {Burn}* burn.\n\n{EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, 2)}";
      if (Channel > 0)
      {
        Text += $"\n\n* Increased by channel.";
      }
      else{
        Text = Text.Replace("*", "");
      }
    };

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Burn, Delta = Burn });
      EventManager.Publish(new MustBeBlockedEvent { Threshold = Threshold, Type = MustBeBlockedByType.AtLeast });
    };
  }
}
