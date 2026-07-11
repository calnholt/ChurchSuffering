using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CarpeDiemTests : IDisposable
{
    public CarpeDiemTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Carpe_diem_gains_courage_and_applies_passive_on_play()
    {
        var entityManager = BuildWorld();
        PlayCarpeDiem(entityManager);

        var player = entityManager.GetEntity("Player");
        Assert.Equal(5, player.GetComponent<Courage>().Amount);
        Assert.True(player.GetComponent<AppliedPassives>().Passives.TryGetValue(AppliedPassiveType.CarpeDiem, out int stacks));
        Assert.Equal(1, stacks);
    }

    [Fact]
    public void Carpe_diem_drains_all_courage_and_removes_passive_at_player_end()
    {
        var entityManager = BuildWorld();
        PlayCarpeDiem(entityManager);

        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd, Previous = SubPhase.Action });

        var player = entityManager.GetEntity("Player");
        Assert.Equal(0, player.GetComponent<Courage>().Amount);
        Assert.False(player.GetComponent<AppliedPassives>().Passives.ContainsKey(AppliedPassiveType.CarpeDiem));
    }

    [Fact]
    public void Carpe_diem_drains_courage_gained_after_playing_the_card()
    {
        var entityManager = BuildWorld();
        PlayCarpeDiem(entityManager);

        EventManager.Publish(new ModifyCourageRequestEvent { Delta = 3, Type = ModifyCourageType.Gain });

        var player = entityManager.GetEntity("Player");
        Assert.Equal(8, player.GetComponent<Courage>().Amount);

        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd, Previous = SubPhase.Action });

        Assert.Equal(0, player.GetComponent<Courage>().Amount);
    }

    private static EntityManager BuildWorld()
    {
        var entityManager = new EntityManager();
        _ = new CourageManagerSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new Courage { Amount = 0 });
        entityManager.AddComponent(player, new AppliedPassives());

        return entityManager;
    }

    private static void PlayCarpeDiem(EntityManager entityManager)
    {
        var cardEntity = EntityFactory.CreateCardFromDefinition(entityManager, "carpe_diem", CardData.CardColor.White, false, 0);
        var card = cardEntity.GetComponent<CardData>().Card as CarpeDiem;
        Assert.NotNull(card);
        card.OnPlay?.Invoke(entityManager, cardEntity);
    }
}
