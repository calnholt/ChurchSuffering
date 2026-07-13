#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Events;

public sealed class QueuedRuleRuntimeTests
{
    [Fact]
    public void Mandatory_fifo_drains_before_reactive_trigger_fifo()
    {
        var endpoint = new RecordingRuleEndpoint();
        var rules = new QueuedRuleRuntime<RuleState>(endpoint);
        rules.EnqueueReactiveTrigger(new RuleTypeId(2), new RuleState { Value = 20 });
        rules.EnqueueReactiveTrigger(new RuleTypeId(2), new RuleState { Value = 21 });
        rules.EnqueueMandatory(new RuleTypeId(1), new RuleState { Value = 10 });
        rules.EnqueueMandatory(new RuleTypeId(1), new RuleState { Value = 11 });

        RuleProcessingResult result = rules.Process(CreateWorld(), new CommandBuffer(), EmptyEvents());

        Assert.Equal(["1:10", "1:11", "2:20", "2:21"], endpoint.Trace);
        Assert.Equal(4, result.CompletedCount);
        Assert.False(result.IsWaiting);
        Assert.Equal(0, rules.Count);
    }

    [Fact]
    public void Pending_rule_persists_typed_state_and_resumes_on_later_updates()
    {
        var endpoint = new PendingRuleEndpoint();
        var rules = new QueuedRuleRuntime<RuleState>(endpoint);
        rules.EnqueueMandatory(new RuleTypeId(3), new RuleState { Value = 99 });
        World world = CreateWorld();
        var commands = new CommandBuffer();
        EventRuntime events = EmptyEvents();

        RuleProcessingResult first = rules.Process(world, commands, events);
        RuleProcessingResult second = rules.Process(world, commands, events);
        RuleProcessingResult third = rules.Process(world, commands, events);

        Assert.True(first.IsWaiting);
        Assert.True(second.IsWaiting);
        Assert.False(third.IsWaiting);
        Assert.Equal(RuleLane.Mandatory, first.WaitingLane);
        Assert.Equal([1, 2, 3], endpoint.ObservedSteps);
        Assert.Equal(0, rules.Count);
    }

    [Fact]
    public void Mandatory_rule_enqueued_by_reactive_handler_preempts_remaining_reactive_rules()
    {
        var endpoint = new EnqueueMandatoryEndpoint();
        var rules = new QueuedRuleRuntime<RuleState>(endpoint);
        rules.EnqueueReactiveTrigger(new RuleTypeId(5), new RuleState { Value = 1 });
        rules.EnqueueReactiveTrigger(new RuleTypeId(6), new RuleState { Value = 2 });

        RuleProcessingResult result = rules.Process(CreateWorld(), new CommandBuffer(), EmptyEvents());

        Assert.Equal([5, 1, 6], endpoint.Trace);
        Assert.Equal(3, result.CompletedCount);
    }

    [Fact]
    public void Warmed_established_capacity_rule_processing_allocates_zero_bytes()
    {
        var endpoint = new CountRuleEndpoint();
        var rules = new QueuedRuleRuntime<RuleState>(endpoint, initialCapacityPerLane: 4);
        World world = CreateWorld();
        var commands = new CommandBuffer();
        EventRuntime events = EmptyEvents();
        rules.EnqueueMandatory(new RuleTypeId(1), new RuleState());
        rules.Process(world, commands, events);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            rules.EnqueueMandatory(new RuleTypeId(1), new RuleState { Value = index });
            rules.Process(world, commands, events);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(101, endpoint.Count);
        Assert.Equal(0, allocated);
    }

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        return new World(registry);
    }

    private static EventRuntime EmptyEvents() => new(new EventRoutingEndpoint());

    private struct RuleState
    {
        public int Step;
        public int Value;
    }

    private sealed class RecordingRuleEndpoint : IRuleRoutingEndpoint<RuleState>
    {
        public List<string> Trace { get; } = [];

        public RuleExecutionStatus Execute(
            RuleTypeId ruleType,
            ref RuleState state,
            ref RuleExecutionContext<RuleState> context)
        {
            Trace.Add($"{ruleType.Value}:{state.Value}");
            return RuleExecutionStatus.Completed;
        }
    }

    private sealed class PendingRuleEndpoint : IRuleRoutingEndpoint<RuleState>
    {
        public List<int> ObservedSteps { get; } = [];

        public RuleExecutionStatus Execute(
            RuleTypeId ruleType,
            ref RuleState state,
            ref RuleExecutionContext<RuleState> context)
        {
            state.Step++;
            ObservedSteps.Add(state.Step);
            return state.Step < 3 ? RuleExecutionStatus.Pending : RuleExecutionStatus.Completed;
        }
    }

    private sealed class EnqueueMandatoryEndpoint : IRuleRoutingEndpoint<RuleState>
    {
        public List<int> Trace { get; } = [];

        public RuleExecutionStatus Execute(
            RuleTypeId ruleType,
            ref RuleState state,
            ref RuleExecutionContext<RuleState> context)
        {
            Trace.Add(ruleType.Value);
            if (ruleType.Value == 5)
            {
                context.Rules.EnqueueMandatory(new RuleTypeId(1), new RuleState { Value = 10 });
            }
            return RuleExecutionStatus.Completed;
        }
    }

    private sealed class CountRuleEndpoint : IRuleRoutingEndpoint<RuleState>
    {
        public int Count { get; private set; }

        public RuleExecutionStatus Execute(
            RuleTypeId ruleType,
            ref RuleState state,
            ref RuleExecutionContext<RuleState> context)
        {
            Count++;
            return RuleExecutionStatus.Completed;
        }
    }
}
