

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.RegularExpressions;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks
{
  public class EnemyAttackBase
  {
    public EnemyAttackId Id { get; set; }
    public string Name { get; set; }
    public int Damage { get; set; }
    public int AdditionalDamage { get; set; } = 0;
    public ConditionType ConditionType { get; set; } = ConditionType.None;
    public BlockingRestrictionType BlockingRestrictionType { get; set; } = BlockingRestrictionType.None;
    public string Text { get; set; } = "";
    public int AmbushPercentage { get; set; } = 0;
    public bool IsTextConditionFulfilled { get; set; } = true;
    public bool IsOneBattleOrLastBattle { get; set; } = false;
    public int Channel { get; set; } = 0;
    public bool IgnoresAegis { get; set; } = false;
    public int? BlockRequiredToPreventEffect { get; protected set; }
    public VisualEffectRecipe AttackEffectRecipe { get; protected set; }
    private VisualEffectSequence _attackEffectSequence;
    public VisualEffectSequence AttackEffectSequence
    {
      get => _attackEffectSequence ??= VisualEffectSequenceAuthoring.ForEnemyAttack(this);
      protected set => _attackEffectSequence = value;
    }

    // Probability (0.0–1.0) that guard conversion is attempted. Set to 0f to opt out.
    public float GuardConversionChance { get; protected set; } = 0.75f;

    // Min conversion as a ratio of damage (floor applied, clamped to minimum of 1)
    public float GuardConversionMinRatio { get; protected set; } = 0f;

    // Max conversion as a ratio of damage (exclusive upper bound, floor applied)
    public float GuardConversionMaxRatio { get; protected set; } = 0.5f;

    protected static VisualEffectRecipe EnemySlashEffect()
    {
      return VisualEffectPresets.EnemySlash();
    }

    protected static VisualEffectRecipe EnemyClawSlashEffect()
    {
      return VisualEffectPresets.EnemyClawSlash();
    }

    protected static VisualEffectRecipe EnemyBiteEffect()
    {
      return VisualEffectPresets.EnemyBite();
    }

    protected static VisualEffectRecipe EnemyRockBlastEffect()
    {
      return VisualEffectPresets.EnemyRockBlast();
    }

    public virtual int RollGuardConversion(int damage)
    {
      if (damage <= 1) return 0;
      if (Random.Shared.NextDouble() >= GuardConversionChance) return 0;
      int min = Math.Max(1, (int)Math.Floor(damage * GuardConversionMinRatio));
      int max = (int)Math.Floor(damage * GuardConversionMaxRatio);
      if (max <= min) return min;
      return Random.Shared.Next(min, max);
    }

    #nullable enable annotations
    public Action<EntityManager>? OnAttackReveal { get; protected set; }
    public Action<EntityManager>? OnAttackHit { get; protected set; }
    public Action<EntityManager>? OnDamageThresholdMet { get; protected set; }
    public Action<EntityManager>? OnBlocksConfirmed { get; protected set; }
    public Action<EntityManager, Entity>? OnBlockProcessed { get; protected set; }
    public Action<EntityManager>? OnBlockAssigned { get; protected set; }
    public Action<EntityManager>? OnChannelApplied { get; protected set; }
    public Func<EntityManager, bool>? ProgressOverride { get; protected set; }

    public void Initialize(EntityManager entityManager)    
    {
      IsOneBattleOrLastBattle = GetComponentHelper.IsLastBattleOfQuest(entityManager);
      GetComponentHelper.GetAppliedPassives(entityManager, "Enemy").Passives.TryGetValue(AppliedPassiveType.Channel, out int count);
      Channel = count;
      OnChannelApplied?.Invoke(entityManager);
    }
  }


  public enum ConditionType
  {
    OnHit,
    OnBlockedByAtLeast1Card,
    OnBlockedByAtLeast2Cards,
    OnBlockedByAtLeast2DifferentColors,
    MustBeBlockedByAtLeast1Card,
    MustBeBlockedByAtLeast2Cards,
    MustBeBlockedByExactly1Card,
    MustBeBlockedByExactly2Cards,
    None,
  }

  public enum BlockingRestrictionType
  {
    None,
    OnlyRed,
    OnlyBlack,
    OnlyWhite,
    NotRed,
    NotBlack,
    NotWhite,
  }
}
