using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class StrangeForceConditionTests
{
    [Fact]
    public void Condition_requires_two_distinct_blocking_colors()
    {
        var entityManager = new EntityManager();
        var attack = new StrangeForce();
        var progress = new EnemyAttackProgress { PlayedRed = 1, PlayedWhite = 0 };

        Assert.False(ConditionService.Evaluate(attack.ConditionType, entityManager, progress));

        progress.PlayedWhite = 1;

        Assert.True(ConditionService.Evaluate(attack.ConditionType, entityManager, progress));

        progress.PlayedRed = 2;
        progress.PlayedWhite = 0;

        Assert.False(ConditionService.Evaluate(attack.ConditionType, entityManager, progress));
    }

    [Fact]
    public void Mill_effect_skipped_when_two_colors_block_even_if_damage_hits()
    {
        var entityManager = new EntityManager();
        var attack = new StrangeForce();
        var progress = new EnemyAttackProgress { PlayedRed = 1, PlayedWhite = 1 };

        Assert.False(ConditionService.ShouldTriggerNotBlockedEffect(
            attack.ConditionType,
            entityManager,
            progress,
            blockedAtResolution: false,
            wasHit: true,
            damage: attack.Damage));
    }

    [Fact]
    public void Mill_effect_runs_when_color_requirement_not_met_and_damage_hits()
    {
        var entityManager = new EntityManager();
        var attack = new StrangeForce();
        var progress = new EnemyAttackProgress { PlayedRed = 2 };

        Assert.True(ConditionService.ShouldTriggerNotBlockedEffect(
            attack.ConditionType,
            entityManager,
            progress,
            blockedAtResolution: false,
            wasHit: true,
            damage: attack.Damage));
    }
}
