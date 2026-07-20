using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Enemies;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class HexBailiffTests
{
    [Fact]
    public void Hex_bailiff_has_one_ten_damage_hex_attack()
    {
        var enemy = Assert.IsType<HexBailiff>(EnemyFactory.Create(EnemyId.HexBailiff));
        var attack = EnemyAttackFactory.Create(EnemyAttackId.WritOfMalice);

        Assert.Equal(32, enemy.HP);
        Assert.Equal([EnemyAttackId.WritOfMalice], enemy.GetAttackIds(new EntityManager(), 1));
        Assert.Equal(10, attack.Damage);
        Assert.Equal("Writ of Malice", attack.Name);
        Assert.Equal("On reveal - A random card in your hand becomes Hex.", attack.Text);
    }

    [Fact]
    public void Writ_of_malice_requests_one_hand_hex_on_reveal()
    {
        EventManager.Clear();
        try
        {
            ApplyCardApplicationEvent applied = null;
            EventManager.Subscribe<ApplyCardApplicationEvent>(evt => applied = evt);

            EnemyAttackFactory.Create(EnemyAttackId.WritOfMalice).OnAttackReveal(new EntityManager());

            Assert.NotNull(applied);
            Assert.Equal(1, applied.Amount);
            Assert.Equal(CardApplicationType.Hex, applied.Type);
            Assert.Equal(CardApplicationTarget.Hand, applied.Target);
        }
        finally
        {
            EventManager.Clear();
        }
    }
}
