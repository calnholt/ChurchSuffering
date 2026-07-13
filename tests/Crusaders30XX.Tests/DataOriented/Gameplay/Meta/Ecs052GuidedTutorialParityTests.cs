#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Meta;

public sealed class Ecs052GuidedTutorialParityTests
{
    [Theory]
    [MemberData(nameof(StockHands))]
    public void All_nine_characterized_stock_hands_are_exact(
        int section,
        int turn,
        GuidedTutorialCardDefinition[] expected)
    {
        Assert.Equal(expected, GuidedTutorialCatalog.GetCards(section, turn).ToArray());
    }

    [Fact]
    public void All_section_stats_turns_attacks_and_pending_dialogues_are_exact()
    {
        GuidedTutorialSectionDefinition[] sections = GuidedTutorialCatalog.RegisteredSections.ToArray();

        Assert.Equal(8, sections.Length);
        Assert.Equal([3, 6, 8, 10, 5, 8, 10, 12], sections.Select(value => value.EnemyHp));
        Assert.Equal([1, 1, 1, 9, 1, 1, 1, 1], sections.Select(value => value.PlayerHp));
        Assert.Equal([1, 1, 1, 1, 1, 1, 1, 2], sections.Select(value => value.TurnCount));
        Assert.Equal(
            [
                EnemyAttackId.TutorialHordeStrike3,
                EnemyAttackId.TutorialHordeStrike3,
                EnemyAttackId.TutorialHordeStrike3,
                EnemyAttackId.TutorialHordeStrike8,
                EnemyAttackId.TutorialHordeStrike8,
                EnemyAttackId.TutorialHordeStrike6,
                EnemyAttackId.TutorialHordeStrike6,
                EnemyAttackId.TutorialHordeStrike8,
            ],
            sections.Select(value => value.EnemyAttack));
        Assert.Equal(EnemyAttackId.TutorialHordeStrike6, Assert.Single(GuidedTutorialCatalog.GetAttacks(8, 2).ToArray()));
        Assert.Equal(new[] { 0, 0, 73101, 73102, 0, 0, 0, 73103 },
            sections.Select(value => value.PendingDialogue.Value));
        Assert.True((sections[0].Flags & GuidedTutorialSectionFlags.TeachesRules) != 0);
        Assert.Equal(GuidedTutorialSectionFlags.None, sections[3].Flags);
        Assert.True((sections[7].Flags & GuidedTutorialSectionFlags.ShowsDrawPile) != 0);
    }

    [Fact]
    public void Fourteen_guided_messages_preserve_keys_targets_conditions_and_phase_schedule()
    {
        GuidedTutorialMessageDefinition[] messages = GuidedTutorialCatalog.RegisteredMessages.ToArray();

        Assert.Equal(14, messages.Length);
        Assert.Equal(14, messages.Select(value => value.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal("Enemy", Message(GuidedTutorialMessageId.TeachWin).TargetId);
        Assert.Equal("UI_PlayerHudHealth", Message(GuidedTutorialMessageId.TeachLoss).TargetId);
        Assert.Equal("UI_PlayerHudCourage", Message(GuidedTutorialMessageId.TeachCourageHud).TargetId);
        Assert.Equal("UI_PlayerHudTemperance", Message(GuidedTutorialMessageId.TeachTemperanceHud).TargetId);
        Assert.Equal("EnemyIntentPips", Message(GuidedTutorialMessageId.TeachIntentPips).TargetId);
        Assert.Equal("UI_PlayerHudPledge", Message(GuidedTutorialMessageId.TeachPledge).TargetId);
        Assert.Equal("has_litany_of_wrath_in_hand", Message(GuidedTutorialMessageId.TeachFreeActions).Condition);
        Assert.Equal("has_reckoning_in_hand", Message(GuidedTutorialMessageId.TeachReckoningDiscard).Condition);

        Assert.Equal(
            [GuidedTutorialMessageId.TeachWin, GuidedTutorialMessageId.TeachLoss, GuidedTutorialMessageId.TeachEnemyAttack],
            Scheduled(1, GuidedTutorialPhase.Block));
        Assert.Equal([GuidedTutorialMessageId.TeachActionPoints], Scheduled(1, GuidedTutorialPhase.Action));
        Assert.Empty(Scheduled(2, GuidedTutorialPhase.Block));
        Assert.Equal([GuidedTutorialMessageId.TeachFreeActions], Scheduled(2, GuidedTutorialPhase.Action));
        Assert.Equal([GuidedTutorialMessageId.TeachReckoningDiscard], Scheduled(3, GuidedTutorialPhase.Action));
        Assert.Equal([GuidedTutorialMessageId.TeachBlackBlock], Scheduled(5, GuidedTutorialPhase.Block));
        Assert.Equal([GuidedTutorialMessageId.TeachWeapon], Scheduled(5, GuidedTutorialPhase.Action));
        Assert.Equal(
            [GuidedTutorialMessageId.TeachRedCourage, GuidedTutorialMessageId.TeachCourageHud],
            Scheduled(6, GuidedTutorialPhase.Block));
        Assert.Equal(
            [GuidedTutorialMessageId.TeachWhiteTemperance, GuidedTutorialMessageId.TeachTemperanceHud],
            Scheduled(7, GuidedTutorialPhase.Block));
        Assert.Equal([GuidedTutorialMessageId.TeachIntentPips], Scheduled(8, GuidedTutorialPhase.Block));
        Assert.Equal([GuidedTutorialMessageId.TeachPledge], Scheduled(8, GuidedTutorialPhase.Action));
    }

    [Fact]
    public void Authored_section_progresses_by_exact_actions_and_preserves_completion_id()
    {
        World world = CreateWorld();
        EntityId tutorialEntity = GuidedTutorialCatalog.Materialize(world, 8);
        GuidedTutorial tutorial = world.Get<GuidedTutorial>(tutorialEntity);
        DynamicBuffer<TutorialStepEntry> steps = world.GetDynamicBuffer(tutorial.Steps);
        Assert.Equal(2, steps.Count);
        Assert.Equal((int)GuidedTutorialAction.InspectIntent, steps[0].RequiredAction);
        Assert.Equal((int)GuidedTutorialAction.PledgeCard, steps[1].RequiredAction);

        var events = new MetaGameEventHub();
        var completed = new Capture<TutorialCompletedEvent>();
        var allCompleted = new Capture<AllTutorialsCompletedEvent>();
        var rootConsumers = new MetaGameRouteConsumers()
            .Add<TutorialCompletedEvent>(completed, -10)
            .Add<AllTutorialsCompletedEvent>(allCompleted, -10);
        MetaGameComposition composition = MetaGameComposition.Create(world, events, rootConsumers);
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes())));

        events.TutorialStarted.Publish(new(tutorialEntity, 808));
        events.AdvanceTutorial.Publish(new(tutorialEntity, (int)GuidedTutorialAction.PlayWeapon));
        world.Events.DrainBarrier();

        tutorial = world.Get<GuidedTutorial>(tutorialEntity);
        Assert.Equal(TutorialState.Running, tutorial.State);
        Assert.Equal(808, tutorial.TutorialId);
        Assert.Equal(0, tutorial.CurrentStep);
        Assert.Equal((byte)0, steps[0].Completed);

        events.AdvanceTutorial.Publish(new(tutorialEntity, (int)GuidedTutorialAction.InspectIntent));
        events.AdvanceTutorial.Publish(new(tutorialEntity, (int)GuidedTutorialAction.PledgeCard));
        world.Events.DrainBarrier();

        tutorial = world.Get<GuidedTutorial>(tutorialEntity);
        Assert.Equal(TutorialState.Complete, tutorial.State);
        Assert.Equal(2, tutorial.CurrentStep);
        Assert.All(steps.AsSpan().ToArray(), step => Assert.Equal((byte)1, step.Completed));
        Assert.Equal(tutorialEntity, completed.Value.Tutorial);
        Assert.Equal(808, completed.Value.TutorialId);
        Assert.Equal(tutorialEntity, allCompleted.Value.Tutorial);
        Assert.Equal(1, completed.Count);
        Assert.Equal(1, allCompleted.Count);
    }

    [Fact]
    public void Restart_clears_every_step_and_skip_emits_terminal_notification_once()
    {
        World world = CreateWorld();
        EntityId tutorialEntity = GuidedTutorialCatalog.Materialize(world, 1);
        GuidedTutorial tutorial = world.Get<GuidedTutorial>(tutorialEntity);
        DynamicBuffer<TutorialStepEntry> steps = world.GetDynamicBuffer(tutorial.Steps);
        var events = new MetaGameEventHub();
        var allCompleted = new Capture<AllTutorialsCompletedEvent>();
        var rootConsumers = new MetaGameRouteConsumers().Add<AllTutorialsCompletedEvent>(allCompleted, -10);
        MetaGameComposition composition = MetaGameComposition.Create(world, events, rootConsumers);
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes())));

        events.TutorialStarted.Publish(new(tutorialEntity, 101));
        events.AdvanceTutorial.Publish(new(tutorialEntity, steps[0].RequiredAction));
        events.GuidedTutorialRestartRequested.Publish(new(tutorialEntity));
        world.Events.DrainBarrier();

        tutorial = world.Get<GuidedTutorial>(tutorialEntity);
        Assert.Equal(TutorialState.Running, tutorial.State);
        Assert.Equal(0, tutorial.CurrentStep);
        Assert.All(steps.AsSpan().ToArray(), step => Assert.Equal((byte)0, step.Completed));

        events.GuidedTutorialSkipRequested.Publish(new(tutorialEntity));
        events.GuidedTutorialSkipRequested.Publish(new(tutorialEntity));
        world.Events.DrainBarrier();

        Assert.Equal(TutorialState.Skipped, world.Get<GuidedTutorial>(tutorialEntity).State);
        Assert.Equal(tutorialEntity, allCompleted.Value.Tutorial);
        Assert.Equal(1, allCompleted.Count);
    }

    public static IEnumerable<object[]> StockHands()
    {
        yield return Hand(1, 1, C(CardId.Smite), C(CardId.Smite));
        yield return Hand(2, 1, C(CardId.Smite), C(CardId.LitanyOfWrath), C(CardId.Smite));
        yield return Hand(3, 1, C(CardId.Smite), C(CardId.LitanyOfWrath), C(CardId.Smite), C(CardId.Reckoning));
        yield return Hand(4, 1, C(CardId.Absolution), C(CardId.LitanyOfWrath), C(CardId.Smite), C(CardId.Reckoning));
        yield return Hand(5, 1, B(CardId.Smite), B(CardId.Smite), B(CardId.Smite), B(CardId.Smite));
        yield return Hand(6, 1, B(CardId.Stab), R(CardId.Smite), W(CardId.Smite), W(CardId.Smite));
        yield return Hand(7, 1, W(CardId.Smite), W(CardId.Smite), B(CardId.Smite), B(CardId.Smite));
        yield return Hand(8, 1, B(CardId.Courageous), B(CardId.Smite), B(CardId.Smite), B(CardId.Fervor));
        yield return Hand(8, 2, R(CardId.LitanyOfWrath), R(CardId.Absolution), B(CardId.Reckoning), R(CardId.Smite));
    }

    private static object[] Hand(int section, int turn, params GuidedTutorialCardDefinition[] cards) =>
        [section, turn, cards];

    private static GuidedTutorialCardDefinition C(CardId id) => new(id, RuleCardColor.Black, 1);
    private static GuidedTutorialCardDefinition B(CardId id) => new(id, RuleCardColor.Black, 0);
    private static GuidedTutorialCardDefinition R(CardId id) => new(id, RuleCardColor.Red, 0);
    private static GuidedTutorialCardDefinition W(CardId id) => new(id, RuleCardColor.White, 0);

    private static GuidedTutorialMessageDefinition Message(GuidedTutorialMessageId id) =>
        GuidedTutorialCatalog.GetMessage(id);

    private static GuidedTutorialMessageId[] Scheduled(int section, GuidedTutorialPhase phase)
    {
        Span<GuidedTutorialMessageId> messages = stackalloc GuidedTutorialMessageId[4];
        int count = GuidedTutorialCatalog.CopyMessages(section, 1, phase, messages);
        return messages[..count].ToArray();
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private sealed class Capture<T> : IEventConsumer<T> where T : unmanaged
    {
        public T Value { get; private set; }
        public int Count { get; private set; }

        public void Consume(in T value, ref EventDispatchContext context)
        {
            Value = value;
            Count++;
        }
    }
}
