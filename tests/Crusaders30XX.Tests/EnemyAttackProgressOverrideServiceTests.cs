using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyAttackProgressOverrideServiceTests
{
    [Fact]
    public void Exact_block_count_fully_prevents_damage_and_marks_exhaust()
    {
        var entityManager = new EntityManager();
        var progressEntity = entityManager.CreateEntity("EnemyAttackProgress");
        var progress = new EnemyAttackProgress
        {
            DamageBeforePrevention = 10,
            AssignedBlockTotal = 5,
        };
        entityManager.AddComponent(progressEntity, progress);

        var blocker1 = CreateBlocker(entityManager, "Blocker1");
        var blocker2 = CreateBlocker(entityManager, "Blocker2");

        bool applied = EnemyAttackProgressOverrideService.TryApplyExactBlockCountPrevention(
            entityManager,
            requiredBlockCount: 2,
            attackDamage: 10);

        Assert.True(applied);
        Assert.True(progress.FullyPreventedBySpecial);
        Assert.Equal(0, progress.ActualDamage);
        Assert.True(progress.IsConditionMet);
        Assert.Equal(10, progress.BaseDamage);
        Assert.Equal(5, progress.PreventedDamageFromBlockCondition);
        Assert.Equal(10, progress.TotalPreventedDamage);
        Assert.NotNull(blocker1.GetComponent<ExhaustOnBlock>());
        Assert.NotNull(blocker2.GetComponent<ExhaustOnBlock>());
    }

    [Fact]
    public void Wrong_block_count_does_not_apply_override()
    {
        var entityManager = new EntityManager();
        var progressEntity = entityManager.CreateEntity("EnemyAttackProgress");
        var progress = new EnemyAttackProgress
        {
            DamageBeforePrevention = 10,
            AssignedBlockTotal = 3,
        };
        entityManager.AddComponent(progressEntity, progress);

        CreateBlocker(entityManager, "Blocker1");

        bool applied = EnemyAttackProgressOverrideService.TryApplyExactBlockCountPrevention(
            entityManager,
            requiredBlockCount: 2,
            attackDamage: 10);

        Assert.False(applied);
        Assert.False(progress.IsConditionMet);
        Assert.False(progress.FullyPreventedBySpecial);
    }

    private static Entity CreateBlocker(EntityManager entityManager, string name)
    {
        var card = entityManager.CreateEntity(name);
        entityManager.AddComponent(card, new CardData());
        entityManager.AddComponent(card, new AssignedBlockCard
        {
            IsEquipment = false,
        });
        return card;
    }
}
