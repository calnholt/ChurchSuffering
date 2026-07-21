using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Utils;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class Ninja : EnemyBase
{
  public Ninja()
  {
    Id = EnemyId.Ninja;
    Name = "Ninja";
    HP = 22;

    OnStartOfBattle = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Stealth, Delta = 1 });
    };
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    var hasSliceAndDice = false;
    var attacks = new List<EnemyAttackId> { EnemyAttackId.Slice };
    int random = Random.Shared.Next(0, 100);
    if (random >= 90 || turnNumber == 1)
    {
      return [EnemyAttackId.Slice, EnemyAttackId.Dice, EnemyAttackId.SharpenBlade, EnemyAttackId.NightveilGuillotine];
    }
    random = Random.Shared.Next(0, 100);
    if (random >= 50)
    {
      attacks.Add(EnemyAttackId.Dice);
      hasSliceAndDice = true;
    }
    // give shadow_step higher weight - maybe improve array util function?
    var linkers = new List<EnemyAttackId> { EnemyAttackId.DuskFlick, EnemyAttackId.CloakedReaver, EnemyAttackId.SilencingStab, EnemyAttackId.SharpenBlade, EnemyAttackId.ShadowStep, EnemyAttackId.ShadowStep, EnemyAttackId.ShadowStep };
    var count = (Random.Shared.Next(0, 100) >= 50 ? 1 : 0) + 2;
    attacks.AddRange(ArrayUtils.TakeRandomWithReplacement(linkers, count));
    var shuffledAttacks = ArrayUtils.Shuffled(attacks);
    random = Random.Shared.Next(0, 100);
    if (random >= 80 && hasSliceAndDice)
    {
      return shuffledAttacks.Append(EnemyAttackId.NightveilGuillotine);
    }
    else if (random >= 60)
    {
      return shuffledAttacks.Append(EnemyAttackId.HaveNoMercy);
    }
    else if (random >= 50)
    {
      shuffledAttacks.Append(ArrayUtils.TakeRandomWithReplacement(linkers, 1).FirstOrDefault());
    }
    return shuffledAttacks;
  }
}

public class Slice : EnemyAttackBase
{
  public Slice()
  {
    Id = EnemyAttackId.Slice;
    Name = "Slice";
    Damage = 1;
  }
}

public class Dice : EnemyAttackBase
{
  public Dice()
  {
    Id = EnemyAttackId.Dice;
    Name = "Dice";
    Damage = 1;
  }
}

public class DuskFlick : EnemyAttackBase
{
  private int Wounded = 1;
  public DuskFlick()
  {
    Id = EnemyAttackId.DuskFlick;
    Name = "Dusk Flick";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Wounded, 1, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Wounded, Delta = Wounded });
    };
  }
}

public class CloakedReaver : EnemyAttackBase
{
  public CloakedReaver()
  {
    Id = EnemyAttackId.CloakedReaver;
    Name = "Cloaked Reaver";
    Damage = 3;
  }
}

public class SilencingStab : EnemyAttackBase
{
  private int Frozen = 3;
  public SilencingStab()
  {
    Id = EnemyAttackId.SilencingStab;
    Name = "Silencing Stab";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Frozen, Frozen, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyCardApplicationEvent
      {
        Amount = Frozen,
        Type = CardApplicationType.Frozen,
        Target = CardApplicationTarget.HandAndDrawPile,
      });
    };
  }
}
public class SharpenBlade : EnemyAttackBase
{
  private int Aggression = 3;
  public SharpenBlade()
  {
    Id = EnemyAttackId.SharpenBlade;
    Name = "Sharpen Blade";
    Damage = 2;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Aggression, Aggression, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Aggression, Delta = Aggression });
    };
  }
}

public class ShadowStep : EnemyAttackBase
{
  private int Corrode = 2;
  public ShadowStep()
  {
    Id = EnemyAttackId.ShadowStep;
    Name = "Shadow Step";
    Damage = 3;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Corrode, Corrode);

    OnAttackReveal = (entityManager) =>
    {
      if (IsOneBattleOrLastBattle)
      {
        Text = string.Empty;
      }
    };

    OnBlockProcessed = (entityManager, card) =>
    {
      if (!IsOneBattleOrLastBattle)
      {
        // TODO: should send an event to the player to block the attack
        BlockValueService.ApplyDelta(card, -Corrode, "Corrode");
      }
    };
  }
}

public class NightveilGuillotine : EnemyAttackBase
{
  private int DamageIncrease = 4;
  public NightveilGuillotine()
  {
    Id = EnemyAttackId.NightveilGuillotine;
    Name = "Nightveil Guillotine";
    Damage = 4;
    Text = $"If both Slice and Dice hit this turn, this gains +{DamageIncrease} damage.";

    OnAttackReveal = (entityManager) =>
    {
      var battleStateInfo = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault().GetComponent<BattleStateInfo>();
      battleStateInfo.TurnTracking.TryGetValue(EnemyAttackId.Slice.ToKey(), out int sliceCount);
      battleStateInfo.TurnTracking.TryGetValue(EnemyAttackId.Dice.ToKey(), out int diceCount);
      Console.WriteLine($"[NightveilGuillotine]: slice: {sliceCount} // dice: {diceCount}");
      if (sliceCount > 0 && diceCount > 0)
      {
        Damage += DamageIncrease;
      }
    };
  }
}
