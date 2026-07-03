using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class AppliedPassivesServiceGalvanizeTests : IDisposable
{
    public AppliedPassivesServiceGalvanizeTests()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(3, 2)]
    [InlineData(7, 4)]
    [InlineData(0, 0)]
    public void GetGalvanizeBonus_rounds_up_fifty_percent(int preGalvanizeDamage, int expectedBonus)
    {
        Assert.Equal(expectedBonus, AppliedPassivesService.GetGalvanizeBonus(preGalvanizeDamage));
    }

    [Fact]
    public void Preview_non_weapon_attack_applies_galvanize_bonus()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGalvanize(player);

        int preview = PreviewAttackDamage(entityManager, player, enemy, 5, isWeapon: false);

        Assert.Equal(8, preview);
    }

    [Fact]
    public void Preview_weapon_attack_ignores_galvanize()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGalvanize(player);

        int preview = PreviewAttackDamage(entityManager, player, enemy, 5, isWeapon: true);

        Assert.Equal(5, preview);
        Assert.Equal(1, GetPassive(player, AppliedPassiveType.Galvanize));
    }

    [Fact]
    public void Preview_includes_might_in_galvanize_base()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGalvanize(player);
        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Might, Delta = 2 });

        int preview = PreviewAttackDamage(entityManager, player, enemy, 5, isWeapon: false);

        Assert.Equal(11, preview);
    }

    [Fact]
    public void Preview_rounds_galvanize_bonus_up()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGalvanize(player);

        int preview = PreviewAttackDamage(entityManager, player, enemy, 3, isWeapon: false);

        Assert.Equal(5, preview);
    }

    [Fact]
    public void Runtime_matches_preview_and_consumes_galvanize()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGalvanize(player);

        int preview = PreviewAttackDamage(entityManager, player, enemy, 5, isWeapon: false);
        int hpBefore = enemy.GetComponent<HP>().Current;
        PublishAttackDamage(entityManager, player, enemy, 5, isWeapon: false);
        int hpAfter = enemy.GetComponent<HP>().Current;

        Assert.Equal(hpBefore - hpAfter, preview);
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Galvanize));
    }

    [Fact]
    public void Weapon_attack_does_not_consume_galvanize()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGalvanize(player);

        PublishAttackDamage(entityManager, player, enemy, 5, isWeapon: true);

        Assert.Equal(1, GetPassive(player, AppliedPassiveType.Galvanize));
    }

    private static int PreviewAttackDamage(EntityManager entityManager, Entity player, Entity enemy, int rawDamage, bool isWeapon)
    {
        var attackCard = CreateAttackCard(entityManager, isWeapon);
        var preview = new ModifyHpRequestEvent
        {
            Source = player,
            Target = enemy,
            AttackCard = attackCard,
            DamageType = ModifyTypeEnum.Attack
        };
        return AppliedPassivesService.GetPreviewAttackDamage(preview, rawDamage, ReadOnly: true);
    }

    private static void PublishAttackDamage(EntityManager entityManager, Entity source, Entity target, int damage, bool isWeapon)
    {
        var attackCard = CreateAttackCard(entityManager, isWeapon);
        EventManager.Publish(new ModifyHpRequestEvent
        {
            Source = source,
            Target = target,
            AttackCard = attackCard,
            Delta = -damage,
            DamageType = ModifyTypeEnum.Attack
        });
    }

    private static Entity CreateAttackCard(EntityManager entityManager, bool isWeapon)
    {
        var card = entityManager.CreateEntity(isWeapon ? "WeaponCard" : "AttackCard");
        entityManager.AddComponent(card, new CardData
        {
            Card = new CardBase
            {
                CardId = isWeapon ? "hammer" : "strike",
                Name = isWeapon ? "Hammer" : "Strike",
                IsWeapon = isWeapon,
            },
        });
        return card;
    }

    private static (EntityManager EntityManager, Entity Player, Entity Enemy) BuildCombatWorld()
    {
        var entityManager = new EntityManager();
        _ = new HpManagementSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new HP { Max = 25, Current = 25 });
        entityManager.AddComponent(player, new AppliedPassives());

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new HP { Max = 30, Current = 30 });
        entityManager.AddComponent(enemy, new AppliedPassives());

        return (entityManager, player, enemy);
    }

    private static void ApplyGalvanize(Entity player)
    {
        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Galvanize,
            Delta = 1
        });
    }

    private static int GetPassive(Entity owner, AppliedPassiveType type)
    {
        return owner.GetComponent<AppliedPassives>().Passives.TryGetValue(type, out int stacks) ? stacks : 0;
    }
}
