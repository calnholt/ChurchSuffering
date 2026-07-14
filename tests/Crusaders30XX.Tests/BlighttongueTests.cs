using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Enemies;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class BlighttongueTests
{
    [Fact]
    public void Factory_registers_blighttongue_and_its_two_attacks()
    {
        var enemy = Assert.IsType<Blighttongue>(EnemyFactory.Create(EnemyId.Blighttongue));
        Assert.Equal(28, enemy.HP);
        Assert.NotNull(EnemyAttackFactory.Create(EnemyAttackId.VenomLash));
        Assert.NotNull(EnemyAttackFactory.Create(EnemyAttackId.ToxicDeluge));
    }

    [Fact]
    public void Venom_lash_applies_three_poison_when_its_condition_fails()
    {
        EventManager.Clear();
        try
        {
            ApplyPassiveEvent applied = null;
            EventManager.Subscribe<ApplyPassiveEvent>(evt => applied = evt);
            EnemyAttackFactory.Create(EnemyAttackId.VenomLash).OnAttackHit(new EntityManager());
            Assert.Equal(AppliedPassiveType.Poison, applied.Type);
            Assert.Equal(3, applied.Delta);
        }
        finally { EventManager.Clear(); }
    }

    [Fact]
    public void Toxic_deluge_has_a_six_or_seven_block_threshold_and_applies_two_poison()
    {
        EventManager.Clear();
        try
        {
            var attack = EnemyAttackFactory.Create(EnemyAttackId.ToxicDeluge);
            Assert.Equal(10, attack.Damage);
            Assert.Contains(attack.BlockRequiredToPreventEffect!.Value, new[] { 6, 7 });
            ApplyPassiveEvent applied = null;
            EventManager.Subscribe<ApplyPassiveEvent>(evt => applied = evt);
            attack.OnDamageThresholdMet(new EntityManager());
            Assert.Equal(AppliedPassiveType.Poison, applied.Type);
            Assert.Equal(2, applied.Delta);
        }
        finally { EventManager.Clear(); }
    }
}
