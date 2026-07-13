#nullable enable

using System;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Integration;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Integration;

public sealed class DataOrientedGameRuntimeTests
{
    [Fact]
    public void Root_composes_one_world_runtime_and_operational_scheduler()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();

        Assert.Same(runtime.Events, runtime.World.Events);
        Assert.Equal(250, runtime.Events.RouteCount);
        Assert.Equal(26, runtime.Scheduler.Count);
        Assert.Equal(SceneGroup.TitleMenu, runtime.Scheduler.ActiveScene);
        Assert.True(runtime.World.IsAlive(runtime.Globals.Scene));
        Assert.True(runtime.World.IsAlive(runtime.MetaEntities.Run));
        Assert.Empty(runtime.Effects.Systems.ToArray());

        runtime.Update(TimeSpan.Zero);
        Assert.True(runtime.Packets.Count > 0);
        Assert.True(runtime.TextPackets.Count > 0);
        Assert.NotNull(runtime.MetaSceneAuthoring.Current);
    }

    [Fact]
    public void Input_and_nested_scene_requests_flow_through_the_root_barrier()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        var snapshot = new HostInputSnapshot(
            new Vector2(960, 540), new Vector2(950, 530), Vector2.Zero, Vector2.Zero,
            0f, 0f, 0, 0, 1UL, 0UL,
            PlayerInputDevice.KeyboardMouse, true,
            new Rectangle(0, 0, 1920, 1080), 1920, 1080);

        runtime.SubmitInput(snapshot);
        PlayerInputState input = runtime.World.Get<PlayerInputState>(runtime.Globals.PlayerInput);
        Assert.Equal(new Vector2(960, 540), input.Frame.PointerPosition);
        Assert.True(input.Frame.WasPressed(PlayerInputButton.Primary));

        runtime.MetaEvents.SceneTransitionRequested.Publish(new SceneTransitionRequested(SceneGroup.Climb));
        runtime.Events.DrainBarrier();
        SceneTransitionState transition = runtime.World.Get<SceneTransitionState>(runtime.Globals.Scene);
        Assert.Equal(SceneGroup.Climb, transition.To);
        Assert.True(runtime.Events.LastBarrierWaveCount >= 2);

        runtime.Update(TimeSpan.FromMilliseconds(16));
        runtime.Update(TimeSpan.FromMilliseconds(16));
        Assert.Equal(SceneGroup.Climb, runtime.World.Get<SceneState>(runtime.Globals.Scene).Current);
        Assert.Equal(SceneGroup.Climb, runtime.MetaSceneAuthoring.Current?.Scene);
        Assert.True(runtime.Packets.Count > 0);
    }

    [Fact]
    public void Root_reuses_registered_combat_systems_across_successive_battles()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        CombatSession first = runtime.BeginCombat(EnemyId.TrainingDemon, seed: 11);
        EntityId firstPlayer = first.Player;
        Assert.NotNull(runtime.CombatPresentation);
        runtime.EndCombat();
        Assert.False(runtime.World.IsAlive(firstPlayer));
        Assert.Null(runtime.CombatPresentation);

        CombatSession second = runtime.BeginCombat(EnemyId.Skeleton, seed: 12);
        runtime.CombatEvents.SetThreat.Publish(new SetThreatEvent(second.Player, 7));
        runtime.Events.DrainBarrier();

        Assert.Same(second, runtime.CombatSessions.RequireActive());
        Assert.Equal(7, runtime.World.Get<Threat>(second.Player).Amount);
        runtime.EndCombat();
    }

    [Fact]
    public void Root_materializes_and_cleans_a_stable_id_test_fight()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create(SceneGroup.Battle);
        var fixture = new DataOrientedTestFightFixture(
            CardId.Hammer,
            EnemyId.Skeleton,
            ClimbDifficulty.Hard,
            Seed: 42);

        CombatSession session = runtime.BeginTestCombat(in fixture);
        EntityId player = session.Player;
        EntityId deck = runtime.CombatPresentation!.Deck;
        runtime.Update(TimeSpan.FromMilliseconds(16));

        Assert.True(runtime.World.IsAlive(deck));
        Assert.Equal(deck, session.Deck);
        Assert.True(runtime.Packets.Count > 0);
        Assert.Equal(20, runtime.CombatPresentation.Entities.ToArray()
            .Count(value => value.Kind == CombatPresentationEntityKind.Card));

        runtime.EndCombat();
        Assert.False(runtime.World.IsAlive(deck));
        Assert.False(runtime.World.IsAlive(player));
    }

    [Fact]
    public void Root_executes_typed_card_pledge_and_block_input_requests_in_the_owning_domains()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create(SceneGroup.Battle);
        var fixture = new DataOrientedTestFightFixture(
            CardId.Sword,
            EnemyId.TrainingDemon,
            ClimbDifficulty.Hard,
            Seed: 71);
        CombatSession session = runtime.BeginTestCombat(in fixture);
        EntityId deck = session.Deck;

        EntityId played = CardGameplayFactory.CreateCard(
            runtime.World,
            deck,
            CardId.Strike,
            CardZone.Hand,
            color: RuleCardColor.Red);
        ref CardData playedData = ref runtime.World.Get<CardData>(played);
        playedData.CostCount = 0;
        playedData.Type = RuleCardType.Attack;
        runtime.World.Get<ActionPoints>(session.Player).Current = 2;

        runtime.MetaEvents.PlayCardRequested.Publish(new(played, session.Player));
        runtime.Events.DrainBarrier();

        Assert.Equal(CardZone.DiscardPile, runtime.World.Get<CardZoneLocation>(played).Zone);
        Assert.Equal(1, runtime.World.Get<ActionPoints>(session.Player).Current);
        Assert.Equal(1, runtime.World.Get<Crusaders30XX.ECS.DataOriented.Gameplay.Cards.Player>(session.Player).ActionPoints);

        EntityId pledged = CardGameplayFactory.CreateCard(
            runtime.World,
            deck,
            CardId.Fervor,
            CardZone.Hand,
            color: RuleCardColor.Red);
        runtime.MetaEvents.PledgeCardRequested.Publish(new(pledged));
        runtime.Events.DrainBarrier();
        Assert.True(runtime.World.Has<Pledge>(pledged));

        EntityId blocker = CardGameplayFactory.CreateCard(
            runtime.World,
            deck,
            CardId.Mantlet,
            CardZone.Hand,
            color: RuleCardColor.White);
        ref CardData blockerData = ref runtime.World.Get<CardData>(blocker);
        blockerData.Block = Math.Max(1, blockerData.Block);
        ref BattleStateInfo battle = ref runtime.World.Get<BattleStateInfo>(session.Battle);
        battle.Flags |= CombatFlags.AwaitingBlockConfirmation;

        runtime.MetaEvents.AssignCardAsBlockRequested.Publish(new(blocker, session.Battle));
        runtime.Events.DrainBarrier();
        Assert.True(runtime.World.Has<Crusaders30XX.ECS.DataOriented.Gameplay.Combat.AssignedBlockCard>(blocker));

        runtime.MetaEvents.UnassignCardAsBlockRequested.Publish(new(blocker, session.Battle));
        runtime.Events.DrainBarrier();
        Assert.False(runtime.World.Has<Crusaders30XX.ECS.DataOriented.Gameplay.Combat.AssignedBlockCard>(blocker));
    }

    [Fact]
    public void Meta_start_battle_requests_are_explicit_host_outputs()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        runtime.MetaEvents.StartBattleRequested.Publish(new StartBattleRequested(runtime.MetaEntities.Run, new(77)));
        runtime.Events.DrainBarrier();

        Assert.True(runtime.BattleRequests.TryDequeue(out StartBattleRequested request));
        Assert.Equal(runtime.MetaEntities.Run, request.Run);
        Assert.Equal(77, request.Encounter.Value);
    }
}
