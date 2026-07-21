using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class EnemyDamageThresholdTests : IDisposable
{
    public EnemyDamageThresholdTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Theory]
    [InlineData(5, 0, 0, false, 1)]
    [InlineData(5, 1, 0, false, 0)]
    [InlineData(5, 0, 1, false, 1)]
    [InlineData(5, 0, 1, true, 1)]
    public void Threshold_effect_uses_block_required_and_final_damage_after_prevention(
        int damage,
        int assignedBlock,
        int aegis,
        bool ignoresAegis,
        int expectedTriggers)
    {
        var attack = new ThresholdAttack(damage, blockRequired: 1, ignoresAegis);
        var entityManager = CreateCombat(attack, assignedBlock, aegis);

        ResolveAttack(entityManager);

        Assert.Equal(expectedTriggers, attack.ThresholdTriggerCount);
    }

    [Fact]
    public void Threshold_effect_does_not_run_when_special_effect_fully_prevents_damage()
    {
        var attack = new ThresholdAttack(damage: 5, blockRequired: 1, ignoresAegis: false);
        var entityManager = CreateCombat(attack, assignedBlock: 0, aegis: 0, fullyPreventedBySpecial: true);

        ResolveAttack(entityManager);

        Assert.Equal(0, attack.ThresholdTriggerCount);
    }

    [Fact]
    public void Threshold_and_on_hit_effects_can_both_run_once()
    {
        var attack = new ThresholdAttack(damage: 5, blockRequired: 1, ignoresAegis: false, useOnHit: true);
        var entityManager = CreateCombat(attack, assignedBlock: 0, aegis: 0);

        ResolveAttack(entityManager);

        Assert.Equal(1, attack.ThresholdTriggerCount);
        Assert.Equal(1, attack.OnHitTriggerCount);
    }

    [Theory]
    [InlineData(10, 5, 0, 1)]
    [InlineData(10, 6, 0, 0)]
    [InlineData(10, 5, 5, 0)]
    [InlineData(10, 5, 2, 1)]
    public void Threshold_effect_matches_block_required_cases(
        int damage,
        int assignedBlock,
        int aegis,
        int expectedTriggers)
    {
        var attack = new ThresholdAttack(damage, blockRequired: 6, ignoresAegis: false);
        var entityManager = CreateCombat(attack, assignedBlock, aegis);

        ResolveAttack(entityManager);

        Assert.Equal(expectedTriggers, attack.ThresholdTriggerCount);
    }

    [Fact]
    public void Block_threshold_text_describes_required_block()
    {
        Assert.Equal(
            "If this attack deals 7 or more damage - Freeze the top card of your draw pile.",
            EnemyAttackTextHelper.GetBlockThresholdText(6, "Freeze the top card of your draw pile."));
    }

    [Fact]
    public void Damage_threshold_text_describes_minimum_damage()
    {
        Assert.Equal(
            "If this attack deals 5 or more damage - Gain 2 burn.",
            EnemyAttackTextHelper.GetDamageThresholdText(5, "Gain 2 burn."));
    }

    [Theory]
    [InlineData(10, 0, 0, 6, false)]
    [InlineData(10, 6, 0, 6, true)]
    [InlineData(10, 5, 5, 6, true)]
    [InlineData(10, 3, 0, 6, false)]
    public void Block_threshold_display_condition_reflects_assigned_block_and_final_damage(
        int damage,
        int assignedBlock,
        int aegis,
        int blockRequired,
        bool expectedMet)
    {
        var progress = new EnemyAttackProgress
        {
            AssignedBlockTotal = assignedBlock,
            AegisTotal = aegis,
        };
        int predictedFinalDamage = Math.Max(damage - assignedBlock - aegis, 0);

        Assert.Equal(
            expectedMet,
            ConditionService.EvaluateBlockRequiredToPreventEffect(blockRequired, progress, predictedFinalDamage));
    }

    [Fact]
    public void Block_threshold_display_condition_met_when_fully_prevented_by_special()
    {
        var progress = new EnemyAttackProgress
        {
            AssignedBlockTotal = 0,
            FullyPreventedBySpecial = true,
        };

        Assert.True(ConditionService.EvaluateBlockRequiredToPreventEffect(6, progress, predictedFinalDamage: 10));
    }

    private static EntityManager CreateCombat(
        ThresholdAttack attack,
        int assignedBlock,
        int aegis,
        bool fullyPreventedBySpecial = false)
    {
        var entityManager = new EntityManager();
        var enemy = entityManager.CreateEntity("Enemy");
        var player = entityManager.CreateEntity("Player");
        var progressEntity = entityManager.CreateEntity("EnemyAttackProgress[test-context]");

        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(enemy, new AttackIntent
        {
            ActiveAttackSequence = 1,
            Planned =
            [
                new PlannedAttack
                {
                    AttackId = attack.Id,
                    AttackDefinition = attack
                }
            ]
        });
        entityManager.AddComponent(progressEntity, new EnemyAttackProgress
        {
            Enemy = enemy,
            AttackId = attack.Id,
            AttackSequence = 1,
            AssignedBlockTotal = assignedBlock,
            AegisTotal = aegis,
            BaseDamage = attack.Damage,
            FullyPreventedBySpecial = fullyPreventedBySpecial,
            IgnoresAegis = attack.IgnoresAegis
        });

        _ = new AttackResolutionSystem(entityManager);
        _ = new EnemyDamageManagerSystem(entityManager);

        return entityManager;
    }

    private static void ResolveAttack(EntityManager entityManager)
    {
        EventManager.Publish(new ResolveAttack());
        EventManager.Publish(new EnemyAttackImpactNow());
    }

    private sealed class ThresholdAttack : EnemyAttackBase
    {
        public int ThresholdTriggerCount { get; private set; }
        public int OnHitTriggerCount { get; private set; }

        public ThresholdAttack(int damage, int blockRequired, bool ignoresAegis, bool useOnHit = false)
        {
            Id = EnemyAttackId.Cinderbolt;
            Name = "Threshold Test";
            Damage = damage;
            BlockRequiredToPreventEffect = blockRequired;
            IgnoresAegis = ignoresAegis;
            ConditionType = useOnHit ? ConditionType.OnHit : ConditionType.None;
            OnDamageThresholdMet = _ => ThresholdTriggerCount++;
            OnAttackHit = _ => OnHitTriggerCount++;
        }
    }
}
