#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Content.Cards;

public sealed class CardBehaviorTraceTests
{
    private static readonly (CardHookStages Hook, TriggerId Trigger)[] Stages =
    [
        (CardHookStages.Validate, RuleTriggerIds.CardValidate),
        (CardHookStages.ResolvePlay, RuleTriggerIds.CardResolvePlay),
        (CardHookStages.ResolveBlock, RuleTriggerIds.CardResolveBlock),
        (CardHookStages.DiscardedForCost, RuleTriggerIds.CardDiscardedForCost),
        (CardHookStages.Pledged, RuleTriggerIds.CardPledged),
        (CardHookStages.ConditionalDamage, RuleTriggerIds.CardConditionalDamage),
        (CardHookStages.Reactive, RuleTriggerIds.CardReactive),
        (CardHookStages.Lifecycle, RuleTriggerIds.CardLifecycle),
    ];

    [Fact]
    public void Every_declared_stage_dispatches_for_base_and_upgraded_variants_without_opaque_commands()
    {
        foreach (CardId id in Enum.GetValues<CardId>())
        {
            CardDefinitionData definition = GeneratedCardCatalog.GetDefinition(id);
            foreach ((CardHookStages hook, TriggerId trigger) in Stages)
            {
                if ((definition.Hooks & hook) == 0) continue;

                foreach (bool upgraded in new[] { false, true })
                {
                    Trace trace = Invoke(id, trigger, upgraded, richState: true);
                    Assert.True(trace.Dispatched, $"{id}/{hook}/{upgraded}");
                    Assert.DoesNotContain(trace.Commands, command => command.Kind == RuleCommandKind.Custom);

                    if (hook == CardHookStages.Validate)
                        Assert.Equal(RuleValidationDecision.Allowed, trace.Validation);
                    if (hook == CardHookStages.ConditionalDamage)
                        Assert.Single(trace.Results, result => result.Kind == RuleHandlerResultKind.StatModifier);
                    if (hook is CardHookStages.ResolvePlay or CardHookStages.ResolveBlock or CardHookStages.DiscardedForCost)
                        Assert.NotEmpty(trace.Commands);
                }
            }
        }
    }

    [Fact]
    public void Exceptional_card_command_and_result_traces_preserve_legacy_order_and_values()
    {
        Trace courageous = Invoke(CardId.Courageous, RuleTriggerIds.CardResolvePlay, upgraded: true);
        AssertKinds(courageous, RuleCommandKind.ModifyStat, RuleCommandKind.Schedule);
        Assert.Equal(4, courageous.Commands[0].Payload.ResourceDelta.Amount);
        Assert.Equal(100, courageous.Commands[1].Payload.Scheduled.DelayMilliseconds);
        Assert.Equal(RuleHandlerIds.ResolveEndTurnRequest, courageous.Commands[1].Payload.Scheduled.Handler);

        Trace relentless = Invoke(CardId.RelentlessStrike, RuleTriggerIds.CardResolvePlay, upgraded: true);
        AssertKinds(relentless, RuleCommandKind.Damage, RuleCommandKind.MoveCard, RuleCommandKind.MutateCard);
        Assert.Equal(4, relentless.Commands[2].Payload.CardMutation.Amount);
        Assert.Equal(RuleValueFlags.BattleOnly, relentless.Commands[2].Payload.CardMutation.Flags);

        Trace swordIntoShield = Invoke(CardId.SwordIntoShield, RuleTriggerIds.CardResolvePlay, upgraded: false);
        AssertKinds(swordIntoShield, RuleCommandKind.ApplyEffect, RuleCommandKind.MutateCard, RuleCommandKind.MutateCard);
        Assert.Equal(CardMutationKind.SetType, swordIntoShield.Commands[1].Payload.CardMutation.Kind);
        Assert.Equal(RuleCardType.Block, swordIntoShield.Commands[1].Payload.CardMutation.CardType);
        Assert.Equal(CardMutationKind.ClearDisplayText, swordIntoShield.Commands[2].Payload.CardMutation.Kind);

        Trace purge = Invoke(CardId.Purge, RuleTriggerIds.CardReactive, upgraded: true);
        Assert.Equal(6, purge.Commands.Length);
        Assert.Equal(5, purge.Commands.Count(command => command.Kind == RuleCommandKind.RemoveEffect));
        Assert.Equal(RuleEffectIds.Might, purge.Commands[^1].Payload.Effect.Effect.Id);
        Assert.Equal(5, purge.Commands[^1].Payload.Effect.Effect.Magnitude);

        Trace exaltation = Invoke(CardId.Exaltation, RuleTriggerIds.CardResolvePlay, upgraded: true);
        AssertKinds(exaltation, RuleCommandKind.ModifyStat, RuleCommandKind.Damage);
        Assert.Equal(-3, exaltation.Commands[0].Payload.ResourceDelta.Amount);

        Trace shield = Invoke(CardId.ShieldOfFaith, RuleTriggerIds.CardResolvePlay, upgraded: true);
        Assert.Equal(12, shield.Commands.Single().Payload.Effect.Effect.Magnitude);

        Trace pouch = Invoke(CardId.PouchOfKunai, RuleTriggerIds.CardResolvePlay, upgraded: true);
        AssertKinds(pouch, RuleCommandKind.SpawnCard);
        Assert.InRange(pouch.Commands[0].Payload.SpawnCard.Count, 3, 4);
        Assert.False(pouch.Commands[0].Payload.SpawnCard.IsUpgraded);

        Trace vindicate = Invoke(CardId.Vindicate, RuleTriggerIds.CardConditionalDamage, upgraded: false);
        RuleHandlerResult conditional = Assert.Single(vindicate.Results);
        Assert.Equal(RuleStatIds.AttackAdditionalDamage, conditional.Stat);
        Assert.Equal(7, conditional.Value);
    }

    [Fact]
    public void Validation_rejections_and_non_play_hooks_emit_typed_traces()
    {
        Trace deusVult = Invoke(CardId.DeusVult, RuleTriggerIds.CardValidate, upgraded: false, richState: false);
        Assert.Equal(RuleValidationDecision.Rejected, deusVult.Validation);
        AssertKinds(deusVult, RuleCommandKind.Reject);
        Assert.Equal(RuleRejectionReason.MissingWeaponAttack, deusVult.Commands[0].Payload.Rejection.Reason);

        Trace hiddenKunai = Invoke(CardId.HiddenKunai, RuleTriggerIds.CardResolveBlock, upgraded: true);
        AssertKinds(hiddenKunai, RuleCommandKind.SpawnCard);
        Assert.True(hiddenKunai.Commands[0].Payload.SpawnCard.IsUpgraded);

        Trace ark = Invoke(CardId.ArkOfTheCovenant, RuleTriggerIds.CardDiscardedForCost, upgraded: true);
        AssertKinds(ark, RuleCommandKind.Heal);
        Assert.Equal(3, ark.Commands[0].Payload.ResourceDelta.Amount);

        Trace absolution = Invoke(CardId.Absolution, RuleTriggerIds.CardPledged, upgraded: true);
        AssertKinds(absolution, RuleCommandKind.ModifyStat);
        Assert.Equal(2, absolution.Commands[0].Payload.ResourceDelta.Amount);

        Trace graveward = Invoke(CardId.Graveward, RuleTriggerIds.CardReactive, upgraded: true);
        AssertKinds(graveward, RuleCommandKind.ApplyEffect);
        Assert.Equal(4, graveward.Commands[0].Payload.Effect.Effect.Magnitude);
    }

    private static Trace Invoke(
        CardId id,
        TriggerId stage,
        bool upgraded,
        bool richState = true)
    {
        World world = CreateWorld();
        var commandBuffer = new RuleCommandBuffer(initialCapacity: 32);
        var resultStorage = new RuleHandlerResult[16];
        var resultState = new RuleResultWriterState();
        RuleRandomState randomState = RuleRandomState.FromSeed(0xC30UL + (ushort)id);
        EntityId card = new(10, 1);
        EntityId player = new(11, 1);
        RuleTriggerEnvelope trigger = TriggerFor(id, stage, player);
        CardHandlerFlags flags = upgraded ? CardHandlerFlags.Upgraded : CardHandlerFlags.None;
        if (richState)
        {
            flags |= CardHandlerFlags.Pledged | CardHandlerFlags.Scorched |
                CardHandlerFlags.FirstPlayThisBattle | CardHandlerFlags.WeaponUsedThisAction;
        }

        CardPaymentSnapshot payment = id == CardId.Reap
            ? new CardPaymentSnapshot(2, 2, 0, 0, 0, 0)
            : id == CardId.EmberHarvest
                ? new CardPaymentSnapshot(1, 0, 0, 1, 0, 1)
                : new CardPaymentSnapshot(0, 0, 0, 0, 0, 0);
        RulePhase phase = id == CardId.Stalwart ? RulePhase.EnemyAction : RulePhase.Action;
        var input = new CardHandlerInput(
            new RuleInvocationId(1),
            card,
            player,
            id,
            trigger,
            flags,
            new CardPhaseSnapshot(phase, Turn: 3, BattleEpoch: 4, ActionSequence: 2),
            payment,
            richState
                ? new CombatResourceSnapshot(6, 4, 1, 2, 2, 2, 3)
                : new CombatResourceSnapshot(0, 0, 0, 0, 0, 0, 0),
            richState
                ? new CardBattleSnapshot(3, 5, 2, 2, 1, 1, 1)
                : new CardBattleSnapshot(0, 0, 0, 0, 0, 0, 0),
            new DeckStateSnapshot(new EntityId(12, 1), richState ? new EntityId(13, 1) : EntityId.Null, 10, 4, 5, 1),
            DerivedDamage: GeneratedCardCatalog.GetDefinition(id).Damage,
            ResolvedDamage: 7,
            TargetHandle.PrimaryEnemy);
        EntityId[] candidates = [new(20, 1), new(21, 1), new(22, 1)];
        var context = new CardHandlerContext(
            world.AsReadOnly(),
            commandBuffer.Writer,
            in input,
            ReadOnlySpan<RuleFact>.Empty,
            ReadOnlySpan<EntityId>.Empty,
            candidates,
            resultStorage,
            ref resultState,
            ref randomState);

        bool dispatched = GeneratedCardCatalog.Dispatch(id, ref context);
        return new Trace(
            dispatched,
            commandBuffer.AsReadOnlySpan().ToArray(),
            context.Results.WrittenSpan.ToArray(),
            context.Results.Validation);
    }

    private static RuleTriggerEnvelope TriggerFor(CardId id, TriggerId stage, EntityId owner)
    {
        if (stage == RuleTriggerIds.CardReactive && id == CardId.Graveward)
        {
            var payload = new MillTriggerPayload(new EntityId(12, 1), new EntityId(20, 1));
            return new RuleTriggerEnvelope(RuleTriggerKind.Mill, stage, RuleTriggerPayload.From(in payload));
        }

        RuleCardTraits traits = RuleCardTraits.Frozen | RuleCardTraits.Brittle |
            RuleCardTraits.Scorched | RuleCardTraits.Thorned | RuleCardTraits.Curse;
        var cardPayload = new CardTriggerPayload(
            new EntityId(20, 1), owner, id, RuleCardEventKind.Pledged, RuleCardColor.White, traits);
        return new RuleTriggerEnvelope(RuleTriggerKind.Card, stage, RuleTriggerPayload.From(in cardPayload));
    }

    private static void AssertKinds(Trace trace, params RuleCommandKind[] kinds) =>
        Assert.Equal(kinds, trace.Commands.Select(command => command.Kind));

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        return new World(registry);
    }

    private sealed record Trace(
        bool Dispatched,
        RuleCommand[] Commands,
        RuleHandlerResult[] Results,
        RuleValidationDecision Validation);
}
