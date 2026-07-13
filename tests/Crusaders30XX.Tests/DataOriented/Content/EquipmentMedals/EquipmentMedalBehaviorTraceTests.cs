#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Equipment;
using Crusaders30XX.ECS.DataOriented.Content.Medals;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.Tests.DataOriented.Content.EquipmentMedals;

public sealed class EquipmentMedalBehaviorTraceTests
{
    [Theory]
    [MemberData(nameof(EquipmentTraces))]
    public void Every_equipment_dispatches_its_exact_command_trace(EquipmentId id, string expectedTrace)
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer();
        var context = new EquipmentHandlerContext(
            world.AsReadOnly(), commands.Writer, new RuleInvocationId(1),
            new EntityId(10, 1), new EntityId(20, 1), id,
            RuleTriggerIds.EquipmentActivated,
            TargetHandle.ForEntity(new EntityId(30, 1)));

        bool dispatched = GeneratedEquipmentCatalog.Dispatch(id, ref context);

        Assert.Equal(expectedTrace.Length > 0, dispatched);
        Assert.Equal(expectedTrace, Trace(commands));
    }

    [Theory]
    [MemberData(nameof(MedalTraces))]
    public void Every_medal_dispatches_its_exact_primary_command_trace(MedalId id, string expectedTrace)
    {
        RuleCommandBuffer commands = RunMedal(id, acquire: id is MedalId.StNicholas or MedalId.StThomasAquinas,
            out bool dispatched);

        Assert.Equal(expectedTrace.Length > 0, dispatched);
        Assert.Equal(expectedTrace, Trace(commands));
    }

    [Fact]
    public void Augustine_acquisition_and_battle_start_are_distinct_staged_traces()
    {
        RuleCommandBuffer acquired = RunMedal(MedalId.StAugustine, acquire: true, out bool acquiredDispatch);
        RuleCommandBuffer battle = RunMedal(MedalId.StAugustine, acquire: false, out bool battleDispatch);

        Assert.True(acquiredDispatch);
        Assert.True(battleDispatch);
        Assert.Equal("ModifyMaxHealth", Trace(acquired));
        Assert.Equal("RandomCardZone", Trace(battle));
        Assert.Equal(RandomCardZoneOperation.Mill, battle[0].Payload.RandomCardZone.Operation);
    }

    [Fact]
    public void Equipment_exceptional_commands_preserve_card_deck_and_remove_all_semantics()
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer();
        var owner = new EntityId(20, 1);
        var pledged = new EntityId(30, 1);
        var oathbreaker = new EquipmentHandlerContext(
            world.AsReadOnly(), commands.Writer, new RuleInvocationId(1),
            new EntityId(10, 1), owner, EquipmentId.OathbreakerCoif,
            RuleTriggerIds.EquipmentActivated, TargetHandle.ForEntity(pledged));
        GeneratedEquipmentCatalog.Dispatch(EquipmentId.OathbreakerCoif, ref oathbreaker);

        Assert.Equal(pledged, commands[0].Payload.RemovePledge.Card);
        Assert.Equal(PledgeRemovalReason.Replaced, commands[0].Payload.RemovePledge.Reason);

        commands.Clear();
        var sunder = new EquipmentHandlerContext(
            world.AsReadOnly(), commands.Writer, new RuleInvocationId(2),
            new EntityId(11, 1), owner, EquipmentId.SunderstepTreads,
            RuleTriggerIds.EquipmentActivated, TargetHandle.PrimaryEnemy);
        GeneratedEquipmentCatalog.Dispatch(EquipmentId.SunderstepTreads, ref sunder);

        Assert.Equal(RuleEffectIds.Guard, commands[0].Payload.RemoveEffect.Effect);
        Assert.Equal(RemoveEffectRuleCommand.AllStacks, commands[0].Payload.RemoveEffect.StackCount);
    }

    [Fact]
    public void Nicholas_freezes_four_distinct_candidates_and_homobonus_scopes_all_resources()
    {
        RuleCommandBuffer nicholas = RunMedal(MedalId.StNicholas, acquire: true, out _);
        EntityId[] frozen = nicholas.AsReadOnlySpan()[1..]
            .ToArray()
            .Select(command => command.Payload.Effect.Target.Entity)
            .ToArray();

        Assert.Equal(4, frozen.Length);
        Assert.Equal(4, frozen.Distinct().Count());
        Assert.All(nicholas.AsReadOnlySpan()[1..].ToArray(), command =>
            Assert.Equal(RuleEffectIds.CardFrozen, command.Payload.Effect.Effect.Id));

        RuleCommandBuffer homobonus = RunMedal(MedalId.StHomobonus, acquire: false, out _);
        Assert.Equal(
            [RuleMetaResourceIds.RedClimbResource, RuleMetaResourceIds.WhiteClimbResource, RuleMetaResourceIds.BlackClimbResource],
            homobonus.AsReadOnlySpan().ToArray().Select(command => command.Payload.MetaResource.Resource));
        const MetaResourceScope expectedScope = MetaResourceScope.Runtime | MetaResourceScope.PendingReward |
            MetaResourceScope.EventPayload | MetaResourceScope.Persist;
        Assert.All(homobonus.AsReadOnlySpan().ToArray(), command =>
            Assert.Equal(expectedScope, command.Payload.MetaResource.Scope));
    }

    [Fact]
    public void Anthony_rescue_is_synchronous_filtered_shuffle_and_bartholomew_uses_preview_damage()
    {
        RuleCommandBuffer anthony = RunMedal(MedalId.StAnthonyOfPadua, acquire: false, out _);
        ref readonly MedalDefinition anthonyDefinition = ref GeneratedMedalCatalog.GetDefinition(MedalId.StAnthonyOfPadua);
        Assert.Equal(RuleActivationTiming.SynchronousBeforeOriginContinues, anthonyDefinition.Trigger.Activation.Timing);
        Assert.Equal(RandomCardZoneOperation.ShuffleInto, anthony[0].Payload.RandomCardZone.Operation);
        Assert.Equal(RuleCardFilter.ExcludeWeapon, anthony[0].Payload.RandomCardZone.Filter);
        Assert.Equal(4, anthony[0].Payload.RandomCardZone.Count);

        RuleCommandBuffer qualifying = RunMedal(MedalId.StBartholomew, acquire: false, out _);
        RuleCommandBuffer belowThreshold = RunMedal(
            MedalId.StBartholomew, acquire: false, out _, hpPreviewDelta: -7);
        Assert.Equal("ApplyEffect", Trace(qualifying));
        Assert.Equal(string.Empty, Trace(belowThreshold));
    }

    public static IEnumerable<object[]> EquipmentTraces()
    {
        foreach (EquipmentId id in Enum.GetValues<EquipmentId>())
            yield return [id, ExpectedEquipmentTrace(id)];
    }

    public static IEnumerable<object[]> MedalTraces()
    {
        foreach (MedalId id in Enum.GetValues<MedalId>())
            yield return [id, ExpectedMedalTrace(id)];
    }

    private static RuleCommandBuffer RunMedal(
        MedalId id,
        bool acquire,
        out bool dispatched,
        int hpPreviewDelta = -8)
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer(initialCapacity: 8);
        var owner = new EntityId(20, 1);
        var medal = new EntityId(10, 1);
        var enemy = new EntityId(30, 1);
        ref readonly MedalDefinition definition = ref GeneratedMedalCatalog.GetDefinition(id);
        RuleTriggerEnvelope trigger = acquire
            ? new RuleTriggerEnvelope(RuleTriggerKind.None, RuleTriggerIds.MedalAcquired, default)
            : BuildTrigger(definition, owner, enemy, hpPreviewDelta);
        MedalCounterSpec counter = definition.Trigger.Activation.Counter;
        var state = new MedalRuntimeState
        {
            BattleEpoch = 1,
            Count = counter.Progression switch
            {
                MedalCounterProgression.IncrementToThreshold => (ushort)Math.Max(0, counter.Threshold - 1),
                MedalCounterProgression.ConsumeCharge => 1,
                _ => 0,
            },
            Flags = MedalRuntimeFlags.Initialized,
        };
        var input = new MedalHandlerInput(
            new RuleInvocationId(1), medal, owner, id, trigger,
            acquire ? MedalHandlerFlags.Acquired : MedalHandlerFlags.None,
            RulePhase.Action,
            new CombatResourceSnapshot(5, 0, 0, 0, 0, 0, 0),
            new DeckStateSnapshot(new EntityId(40, 1), new EntityId(41, 1), 10, 5, 5, 0),
            state,
            TargetHandle.ForEntity(enemy));
        Span<RuleHandlerResult> results = stackalloc RuleHandlerResult[4];
        Span<EntityId> targets = stackalloc EntityId[5]
        {
            new(101, 1), new(102, 1), new(103, 1), new(104, 1), new(105, 1),
        };
        var resultState = default(RuleResultWriterState);
        RuleRandomState randomState = RuleRandomState.FromSeed(1234);
        var context = new MedalHandlerContext(
            world.AsReadOnly(), commands.Writer, in input,
            ReadOnlySpan<RuleFact>.Empty, targets, results,
            ref resultState, ref randomState);

        dispatched = GeneratedMedalCatalog.Dispatch(id, ref context);
        return commands;
    }

    private static RuleTriggerEnvelope BuildTrigger(
        MedalDefinition definition,
        EntityId owner,
        EntityId enemy,
        int hpPreviewDelta)
    {
        RuleTriggerPayload payload = default;
        switch (definition.Trigger.Event)
        {
            case MedalReactiveEvent.BattlePhaseChanged:
                RulePhase phase = definition.Trigger.Filter switch
                {
                    MedalTriggerFilter.StartBattle => RulePhase.StartBattle,
                    MedalTriggerFilter.PlayerStart => RulePhase.PlayerStart,
                    _ => RulePhase.Action,
                };
                var phasePayload = new PhaseChangedTriggerPayload(RulePhase.None, phase, 1);
                payload = RuleTriggerPayload.From(in phasePayload);
                return new RuleTriggerEnvelope(RuleTriggerKind.PhaseChanged, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.CardBlocked:
            case MedalReactiveEvent.PledgeAdded:
            case MedalReactiveEvent.CardPlayed:
                RuleCardTraits traits = definition.Trigger.Filter switch
                {
                    MedalTriggerFilter.ScorchedCard => RuleCardTraits.Scorched,
                    MedalTriggerFilter.ThornedCard => RuleCardTraits.Thorned,
                    MedalTriggerFilter.WeaponCard => RuleCardTraits.Weapon,
                    _ => RuleCardTraits.Block,
                };
                var cardPayload = new CardTriggerPayload(
                    new EntityId(50, 1), owner, CardId.Strike,
                    definition.Trigger.Event == MedalReactiveEvent.CardPlayed
                        ? RuleCardEventKind.Played
                        : definition.Trigger.Event == MedalReactiveEvent.PledgeAdded
                            ? RuleCardEventKind.Pledged
                            : RuleCardEventKind.Blocked,
                    RuleCardColor.Black, traits);
                payload = RuleTriggerPayload.From(in cardPayload);
                return new RuleTriggerEnvelope(RuleTriggerKind.Card, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.PassiveApplied:
                var passive = new PassiveTriggerPayload(owner, owner, RuleEffectIds.Aggression, 1);
                payload = RuleTriggerPayload.From(in passive);
                return new RuleTriggerEnvelope(RuleTriggerKind.Passive, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.EncounterReward:
                var reward = new EncounterRewardTriggerPayload(new EntityId(60, 1), 1);
                payload = RuleTriggerPayload.From(in reward);
                return new RuleTriggerEnvelope(RuleTriggerKind.EncounterReward, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.DrawPileEmpty:
                var draw = new DrawPileEmptyTriggerPayload(new EntityId(40, 1), 4);
                payload = RuleTriggerPayload.From(in draw);
                return new RuleTriggerEnvelope(RuleTriggerKind.DrawPileEmpty, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.HpRequested:
                var hp = new HpRequestedTriggerPayload(owner, enemy, new EntityId(50, 1), RuleDamageKind.Attack, -8, hpPreviewDelta);
                payload = RuleTriggerPayload.From(in hp);
                return new RuleTriggerEnvelope(RuleTriggerKind.HpRequested, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.Tracking:
                var tracking = new TrackingTriggerPayload(owner, ConditionId.Null, 1);
                payload = RuleTriggerPayload.From(in tracking);
                return new RuleTriggerEnvelope(RuleTriggerKind.Tracking, definition.Trigger.Activation.Trigger, payload);
            case MedalReactiveEvent.MilledCard:
                var mill = new MillTriggerPayload(new EntityId(40, 1), new EntityId(50, 1));
                payload = RuleTriggerPayload.From(in mill);
                return new RuleTriggerEnvelope(RuleTriggerKind.Mill, definition.Trigger.Activation.Trigger, payload);
            default:
                return new RuleTriggerEnvelope(RuleTriggerKind.None, definition.Trigger.Activation.Trigger, payload);
        }
    }

    private static string ExpectedEquipmentTrace(EquipmentId id) => id switch
    {
        EquipmentId.BulwarkPlate => "ApplyEffect",
        EquipmentId.FleetfootGreaves => "ModifyStat",
        EquipmentId.HeartforgeCuirass => "ApplyEffect",
        EquipmentId.HelmOfSeeing => "Present,RandomCardZone",
        EquipmentId.KunaiSheath => "SpawnCard",
        EquipmentId.OathbreakerCoif => "RemovePledge",
        EquipmentId.PiercedHeartPlate => "ModifyStat,ApplyEffect",
        EquipmentId.PurgingBracers => "ApplyEffect",
        EquipmentId.SanctifiedCirclet => "ModifyStat",
        EquipmentId.SunderstepTreads => "RemoveEffect",
        EquipmentId.WarbringerBracers => "ApplyEffect",
        EquipmentId.WhetstoneGauntlets => "ApplyEffect",
        _ => string.Empty,
    };

    private static string ExpectedMedalTrace(MedalId id) => id switch
    {
        MedalId.StLuke => "Present,ApplyEffect",
        MedalId.StMichael => "ModifyStat",
        MedalId.StMonica => "RandomCardZone",
        MedalId.StNicholas => "ModifyMaxHealth,ApplyEffect,ApplyEffect,ApplyEffect,ApplyEffect",
        MedalId.StPeter => "RandomCardZone",
        MedalId.StPaulMiki => "SpawnCard",
        MedalId.StLouieIX => "ApplyEffect",
        MedalId.StSebastian => "ModifyMaxHealth",
        MedalId.StFrancisDeSales => "ModifyStat",
        MedalId.StHomobonus => "ModifyMetaResource,ModifyMetaResource,ModifyMetaResource",
        MedalId.StIgnatius => "Present,ApplyEffect",
        MedalId.StClare => "Damage",
        MedalId.StElijah => "ApplyEffect",
        MedalId.StJoanOfArc => "ApplyEffect",
        MedalId.StJerome => "ModifyStat",
        MedalId.StLonginus => "SpawnCard",
        MedalId.StBenedict => "ApplyEffect",
        MedalId.StSimonOfCyrene => "ApplyEffect",
        MedalId.StThomasAquinas => "ModifyMaxHealth,ModifyMaxHandSize",
        MedalId.StAugustine => "RandomCardZone",
        MedalId.StAnthonyOfPadua => "RandomCardZone",
        MedalId.StBartholomew => "ApplyEffect",
        MedalId.StRita => "RandomCardZone",
        MedalId.StLazarus => "RandomCardZone",
        _ => string.Empty,
    };

    private static string Trace(RuleCommandBuffer commands) =>
        string.Join(',', commands.AsReadOnlySpan().ToArray().Select(command => command.Kind.ToString()));

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        return new World(registry);
    }
}
