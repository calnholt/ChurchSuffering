using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class SeizeTests : IDisposable
{
    public SeizeTests()
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
    public void Seize_gains_bonus_damage_after_spending_courage_in_action_phase()
    {
        var (entityManager, seizeEntity) = BuildCombatWorld();

        EventManager.Publish(new ModifyCourageRequestEvent { Delta = 3, Type = ModifyCourageType.Gain });
        EventManager.Publish(new ModifyCourageRequestEvent { Delta = -1, Type = ModifyCourageType.Spent });

        var seize = seizeEntity.GetComponent<CardData>().Card as Seize;
        Assert.NotNull(seize);
        Assert.Equal(2, seize.GetConditionalDamage(entityManager, seizeEntity));
    }

    [Fact]
    public void Seize_has_no_bonus_without_courage_loss()
    {
        var (entityManager, seizeEntity) = BuildCombatWorld();

        EventManager.Publish(new ModifyCourageRequestEvent { Delta = 3, Type = ModifyCourageType.Gain });

        var seize = seizeEntity.GetComponent<CardData>().Card as Seize;
        Assert.NotNull(seize);
        Assert.Equal(0, seize.GetConditionalDamage(entityManager, seizeEntity));
    }

    [Fact]
    public void Seize_gains_bonus_after_set_courage_reduction_in_action_phase()
    {
        var (entityManager, seizeEntity) = BuildCombatWorld();
        var player = entityManager.GetEntity("Player");
        player.GetComponent<Courage>().Amount = 5;

        EventManager.Publish(new SetCourageEvent { Amount = 0 });

        var seize = seizeEntity.GetComponent<CardData>().Card as Seize;
        Assert.NotNull(seize);
        Assert.Equal(2, seize.GetConditionalDamage(entityManager, seizeEntity));
    }

    [Fact]
    public void Seize_bonus_resets_after_action_phase_ends()
    {
        var (entityManager, seizeEntity) = BuildCombatWorld();

        EventManager.Publish(new ModifyCourageRequestEvent { Delta = -1, Type = ModifyCourageType.Spent });

        var seize = seizeEntity.GetComponent<CardData>().Card as Seize;
        Assert.NotNull(seize);
        Assert.Equal(2, seize.GetConditionalDamage(entityManager, seizeEntity));

        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd });
        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });

        Assert.Equal(0, seize.GetConditionalDamage(entityManager, seizeEntity));
    }

    private static (EntityManager EntityManager, Entity SeizeEntity) BuildCombatWorld()
    {
        var entityManager = new EntityManager();
        _ = new CourageManagerSystem(entityManager);
        _ = new BattleStateInfoManagementSystem(entityManager);

        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState { Sub = SubPhase.Action, Main = MainPhase.PlayerTurn });

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new Courage { Amount = 0 });
        entityManager.AddComponent(player, new BattleStateInfo());

        var seizeEntity = EntityFactory.CreateCardFromDefinition(entityManager, "seize", CardData.CardColor.Red, false, 0);
        return (entityManager, seizeEntity);
    }
}
