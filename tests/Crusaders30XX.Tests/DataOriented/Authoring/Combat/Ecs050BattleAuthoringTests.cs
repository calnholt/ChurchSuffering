#nullable enable

using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Integration;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Authoring.Combat;

public sealed class Ecs050BattleAuthoringTests
{
    [Fact]
    public void Battle_materialization_extracts_packets_and_disposal_cleans_owned_entities()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        CombatSession session = runtime.BeginCombat(EnemyId.TrainingDemon, seed: 11);
        EntityId player = session.Player;
        CombatPresentationAuthoringHandle authored = runtime.CombatPresentation!;

        runtime.RenderExtraction.Extract();

        Assert.True(runtime.Packets.Count >= 11);
        Assert.Contains(AllPackets(runtime), packet =>
            packet.Entity == player && packet.Kind == RenderPacketKind.Player);
        Assert.Contains(AllPackets(runtime), packet =>
            packet.Entity == session.Enemy && packet.Kind == RenderPacketKind.Enemy);
        Assert.All(authored.Entities.ToArray(), value =>
        {
            Assert.True(runtime.World.IsAlive(value.Entity));
            Assert.Equal(SceneGroup.Battle, runtime.World.Get<OwnedByScene>(value.Entity).Scene);
        });

        EntityId[] owned = authored.Entities.ToArray().Select(value => value.Entity).ToArray();
        runtime.EndCombat();
        Assert.All(owned, entity => Assert.False(runtime.World.IsAlive(entity)));
        Assert.False(runtime.World.IsAlive(player));
    }

    [Fact]
    public void Test_fight_materializes_a_deterministic_catalog_deck_and_visible_hand()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        var fixture = new DataOrientedTestFightFixture(
            CardId.Hammer, EnemyId.Skeleton, ClimbDifficulty.Hard, Seed: 37);
        CombatSession session = runtime.BeginTestCombat(fixture);
        CombatPresentationAuthoringHandle authored = runtime.CombatPresentation!;

        Assert.Equal(20, CardZoneOperations.Count(runtime.World, authored.Deck, CardZone.MasterDeck));
        Assert.Equal(4, CardZoneOperations.Count(runtime.World, authored.Deck, CardZone.Hand));
        Assert.Equal(16, CardZoneOperations.Count(runtime.World, authored.Deck, CardZone.DrawPile));
        EntityId weapon = runtime.World.Get<EquippedWeapon>(session.Player).Card;
        Assert.Equal(CardId.Hammer, runtime.World.Get<CardData>(weapon).Definition);

        runtime.RenderExtraction.Extract();
        Assert.Equal(4, AllPackets(runtime).Count(packet => packet.Kind == RenderPacketKind.HandCard));

        runtime.EndCombat();
    }

    [Fact]
    public void Successive_battles_leave_no_stale_presentation_entities()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        int baselineEntities = runtime.World.EntityCount;

        for (var battleIndex = 0; battleIndex < 2; battleIndex++)
        {
            CombatSession session = runtime.BeginCombat(EnemyId.TrainingDemon, seed: (ulong)(90 + battleIndex));
            CombatPresentationAuthoringHandle authored = runtime.CombatPresentation!;
            EntityId[] owned = authored.Entities.ToArray().Select(value => value.Entity).ToArray();
            runtime.RenderExtraction.Extract();
            Assert.NotEqual(0, runtime.Packets.Count);

            runtime.EndCombat();
            Assert.All(owned, entity => Assert.False(runtime.World.IsAlive(entity)));
            Assert.Equal(baselineEntities, runtime.World.EntityCount);
        }
    }

    [Fact]
    public void Stable_test_fight_fixture_creates_difficulty_scaled_combat_session()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var hub = new CombatEventHub();
        var consumers = new CombatOwnedEventConsumers(world);
        var events = new Crusaders30XX.ECS.DataOriented.Events.EventRuntime(
            new Crusaders30XX.ECS.DataOriented.Events.EventRoutingEndpoint(hub.BuildRoutes(consumers.RegisterRoutes())));
        world.AttachEventRuntime(events);
        var fixture = new DataOrientedTestFightFixture(
            CardId.Sword, EnemyId.TrainingDemon, ClimbDifficulty.Easy, Seed: 7);

        CombatSession session = fixture.CreateSession(world, hub);

        Assert.Equal(25, world.Get<HP>(session.Player).Max);
        Assert.Equal(21, world.Get<HP>(session.Enemy).Max);
        Assert.Equal(EnemyId.TrainingDemon, world.Get<Enemy>(session.Enemy).Definition);
    }

    private static RenderPacket[] AllPackets(DataOrientedGameRuntime runtime) =>
        Enumerable.Range((int)RenderLayer.Background, 8)
            .SelectMany(layer => runtime.Packets.GetLayer((RenderLayer)layer).ToArray())
            .ToArray();
}
