using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class InfernalExecutionTests
{
    [Fact]
    public void Condition_requires_at_least_two_blocking_cards()
    {
        var entityManager = new EntityManager();
        var attack = new InfernalExecution();
        var progress = new EnemyAttackProgress { PlayedCards = 1 };

        Assert.False(ConditionService.Evaluate(attack.ConditionType, entityManager, progress));

        progress.PlayedCards = 2;

        Assert.True(ConditionService.Evaluate(attack.ConditionType, entityManager, progress));
    }
}
