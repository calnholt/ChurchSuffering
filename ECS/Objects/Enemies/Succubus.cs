using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;
using static Crusaders30XX.ECS.Systems.MustBeBlockedSystem;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks;

public class Succubus : EnemyBase
{
  public static readonly int SiphonMultiplier = 1;
  public Succubus(EnemyDifficulty difficulty = EnemyDifficulty.Easy) : base(difficulty)
  {
    Id = "succubus";
    Name = "Succubus";
    HP = 26;
    StartingHealthBelowMax = 3;

    OnStartOfBattle = (entityManager) =>
    {
      EventManager.Subscribe<ModifyCourageEvent>(OnModifyCourageEvent);
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Siphon, Delta = 1 });
    };
  }

  private void OnModifyCourageEvent(ModifyCourageEvent evt)
  {
    if (evt.Reason == "Succubus" && EntityManager != null)
    {
      var enemy = EntityManager.GetEntity("Enemy");
      var passive = GetComponentHelper.GetAppliedPassives(EntityManager, "Enemy");
      passive.Passives.TryGetValue(AppliedPassiveType.Siphon, out int count);
      EventManager.Publish(new HealEvent { Target = enemy, Delta = -(evt.Delta * SiphonMultiplier) });
      EventManager.Publish(new PassiveTriggered { Owner = enemy, Type = AppliedPassiveType.Siphon });
    }
  }

  public override IEnumerable<string> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    var linkers = new List<string> { "enthralling_gaze", "teasing_nip", "crushing_adoration" };
    var battleStateInfo = GetComponentHelper.GetBattleStateInfo(entityManager);
    battleStateInfo.BattleTracking.TryGetValue("courage_lost", out int count);
    var enders = new List<string> { "soul_siphon" };
    if (count > 0)
    {
      enders.Add("velvet_fangs");
    }

    // Select two random from linkers (with replacement)
    var pickedLinkers = ArrayUtils.TakeRandomWithReplacement(linkers, 2).ToList();
    // Select one random from enders
    var pickedEnder = ArrayUtils.TakeRandomWithReplacement(enders, 1);

    // Ensure "crushing_adoration" is not the last key
    // Prepare final list: pickedLinkers + pickedEnder
    var attacks = pickedLinkers.Concat(pickedEnder).ToList();

    // Shuffle, but ensure "crushing_adoration" is never last
    var shuffled = ArrayUtils.Shuffled(attacks).ToList();
    if (shuffled.Count > 1 && shuffled[^1] == "crushing_adoration")
    {
      // Try to swap with a previous entry
      int swapIndex = shuffled.FindIndex(x => x != "crushing_adoration");
      if (swapIndex != -1)
      {
        // Swap the last and swapIndex
        var temp = shuffled[swapIndex];
        shuffled[swapIndex] = shuffled[^1];
        shuffled[^1] = temp;
      }
    }
    return shuffled;
  }

  public static void HandleLoseCourage(EntityManager entityManager, int amount)
  {
    var courage = entityManager.GetEntitiesWithComponent<Courage>().FirstOrDefault();
    if (courage == null) return;
    var courageAmount = courage.GetComponent<Courage>().Amount;
    int courageLost = System.Math.Min(System.Math.Abs(amount), courageAmount);
    EventManager.Publish(new ModifyCourageRequestEvent { Delta = -amount, Reason = "Succubus", Type = ModifyCourageType.Lost });
    EventManager.Publish(new TrackingEvent { Type = "courage_lost", Delta = courageLost });
  }

    public override void Dispose()
    {
        Console.WriteLine($"[Succubus] Unsubscribed from ModifyCourageEvent");
        EventManager.Unsubscribe<ModifyCourageEvent>(OnModifyCourageEvent);
    }
}

public class SoulSiphon : EnemyAttackBase
{
  private int CourageLoss = 1;
  public SoulSiphon()
  {
    Id = "soul_siphon";
    Name = "Soul Siphon";
    Damage = 3;
    ConditionType = ConditionType.OnHit;
    BlockingRestrictionType = BlockingRestrictionType.NotRed;
    Text = $"{EnemyAttackTextHelper.GetBlockingRestrictionText(BlockingRestrictionType)}\n\n{EnemyAttackTextHelper.GetText(EnemyAttackTextType.Custom, 0, ConditionType, 100, $"Lose {CourageLoss} courage.")}";

    OnAttackHit = (entityManager) =>
    {
      Succubus.HandleLoseCourage(entityManager, CourageLoss);
    };
  }
}

public class EnthrallingGaze : EnemyAttackBase
{
  public EnthrallingGaze()
  {
    Id = "enthralling_gaze";
    Name = "Enthralling Gaze";
    Damage = 3;
    // ConditionType = ConditionType.MustBeBlockedByAtLeast1Card;
    // Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, 1);

    // OnAttackReveal = (entityManager) =>
    // {
    //   EventManager.Publish(new MustBeBlockedEvent { Threshold = ValuesParse[0], Type = MustBeBlockedByType.AtLeast });
    // };
  }
}

public class TeasingNip : EnemyAttackBase
{
  private int CourageLoss = 1;
  public TeasingNip()
  {
    Id = "teasing_nip";
    Name = "Teasing Nip";
    Damage = 2;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Custom, 0, ConditionType, 50, $"Lose {CourageLoss} courage.");

    OnAttackHit = (entityManager) =>
    {
      if (Random.Shared.Next(0, 100) >= 50) return;
      Succubus.HandleLoseCourage(entityManager, CourageLoss);
    };
  }
}

public class CrushingAdoration : EnemyAttackBase
{
  private int Aggression = 2;
  public CrushingAdoration()
  {
    Id = "crushing_adoration";
    Name = "Crushing Adoration";
    Damage = 2;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Aggression, Aggression, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Aggression, Delta = Aggression });
    };
  }
}

public class VelvetFangs : EnemyAttackBase
{
  public int Multiplier { get; set; } = 1;
  public VelvetFangs()
  {
    Id = "velvet_fangs";
    Name = "Velvet Fangs";
    Damage = 6;
    AttackEffectRecipe = EnemyBiteEffect();
    ConditionType = ConditionType.OnHit;

    OnAttackReveal = (entityManager) =>
    {
      var battleStateInfo = GetComponentHelper.GetBattleStateInfo(entityManager);
      battleStateInfo.BattleTracking.TryGetValue("courage_lost", out int count);
      Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Custom, 0, ConditionType, 100, $"This monster heals {Multiplier} HP per courage lost this battle ({count * Multiplier} HP).");

    };
    OnAttackHit = (entityManager) =>
    {
      var battleStateInfo = GetComponentHelper.GetBattleStateInfo(entityManager);
      battleStateInfo.BattleTracking.TryGetValue("courage_lost", out int count);
      EventManager.Publish(new HealEvent { Target = entityManager.GetEntity("Enemy"), Delta = +(count * Multiplier) });
    };
  }
}
