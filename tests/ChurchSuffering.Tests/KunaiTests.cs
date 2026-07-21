using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class KunaiTests : IDisposable
{
    public KunaiTests()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    [Fact]
    public void Kunai_applies_wounded_after_four_player_attacks_in_action_phase()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();

        for (int i = 0; i < 4; i++)
        {
            PublishAttackDamage(player, enemy, 1);
        }

        PlayKunai(entityManager, player, enemy);

        Assert.True(enemy.GetComponent<AppliedPassives>().Passives.TryGetValue(AppliedPassiveType.Wounded, out int wounded));
        Assert.Equal(1, wounded);
    }

    [Fact]
    public void Kunai_does_not_apply_wounded_with_fewer_than_four_hits()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();

        for (int i = 0; i < 3; i++)
        {
            PublishAttackDamage(player, enemy, 1);
        }

        PlayKunai(entityManager, player, enemy);

        Assert.False(enemy.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.Wounded));
    }

    [Fact]
    public void Kunai_counter_resets_on_new_action_phase()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();

        for (int i = 0; i < 3; i++)
        {
            PublishAttackDamage(player, enemy, 1);
        }

        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd, Previous = SubPhase.Action });
        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action, Previous = SubPhase.PlayerStart });

        PublishAttackDamage(player, enemy, 1);
        PlayKunai(entityManager, player, enemy);

        Assert.False(enemy.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.Wounded));
    }

    private static void PlayKunai(EntityManager entityManager, Entity player, Entity enemy)
    {
        var kunaiEntity = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White, false, 0);
        var kunai = kunaiEntity.GetComponent<CardData>().Card as Kunai;
        Assert.NotNull(kunai);
        kunai.OnPlay?.Invoke(entityManager, kunaiEntity);
    }

    private static (EntityManager EntityManager, Entity Player, Entity Enemy) BuildCombatWorld()
    {
        var entityManager = new EntityManager();
        _ = new HpManagementSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);
        _ = new BattleStateInfoManagementSystem(entityManager);

        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState { Sub = SubPhase.Action, Main = MainPhase.PlayerTurn });

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new HP { Max = 25, Current = 25 });
        entityManager.AddComponent(player, new AppliedPassives());
        entityManager.AddComponent(player, new BattleStateInfo());

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new HP { Max = 30, Current = 30 });
        entityManager.AddComponent(enemy, new AppliedPassives());

        return (entityManager, player, enemy);
    }

    private static void PublishAttackDamage(Entity source, Entity target, int damage)
    {
        EventManager.Publish(new ModifyHpRequestEvent
        {
            Source = source,
            Target = target,
            Delta = -damage,
            DamageType = ModifyTypeEnum.Attack
        });
    }
}
