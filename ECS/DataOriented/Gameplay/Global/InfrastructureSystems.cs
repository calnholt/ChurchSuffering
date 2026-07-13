#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

/// <summary>Drives the shared deterministic two-lane rule runtime from the Rules phase.</summary>
public sealed class EventQueueSystem<TState> : IGameSystem
    where TState : unmanaged
{
    private readonly QueuedRuleRuntime<TState> rules;

    public EventQueueSystem(QueuedRuleRuntime<TState> rules, SystemId[]? runsAfter = null)
    {
        this.rules = rules ?? throw new ArgumentNullException(nameof(rules));
        Descriptor = new SystemDescriptor(
            GlobalUiSystemIds.EventQueue,
            "EventQueueSystem",
            SystemPhase.Rules,
            SceneGroup.Global,
            runsAfter: runsAfter,
            recordsStructuralCommands: true,
            eventBarrier: EventBarrier.AfterSystem,
            requiresExclusiveWorldAccess: true);
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context) =>
        _ = rules.Process(context.World, context.Commands, context.Events);
}

public sealed class TimerSchedulerSystem : IGameSystem
{
    private readonly Query<ScheduledTimer> timers;
    private readonly EventStream<TimerElapsedEvent> elapsedEvents;

    public TimerSchedulerSystem(World world, EventStream<TimerElapsedEvent> elapsedEvents)
    {
        ArgumentNullException.ThrowIfNull(world);
        timers = world.Query<ScheduledTimer>(new QueryFilter(DebugName: "ECS040.ScheduledTimers"));
        this.elapsedEvents = elapsedEvents ?? throw new ArgumentNullException(nameof(elapsedEvents));
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<ScheduledTimer>.Id);
        Descriptor = new SystemDescriptor(
            GlobalUiSystemIds.TimerScheduler,
            nameof(TimerSchedulerSystem),
            SystemPhase.Gameplay,
            SceneGroup.Global,
            writeComponents: writes,
            emittedEventTypeIds: [GlobalUiEventTypeIds.TimerElapsed],
            eventBarrier: EventBarrier.AfterPhase);
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        float elapsed = (float)context.Elapsed.TotalSeconds;
        foreach (QueryChunk<ScheduledTimer> chunk in timers)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<ScheduledTimer> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref ScheduledTimer timer = ref values[row];
                if ((timer.Flags & ScheduledTimerFlags.Running) == 0)
                {
                    continue;
                }

                timer.RemainingSeconds -= elapsed;
                if (timer.RemainingSeconds > 0f)
                {
                    continue;
                }

                timer.Sequence++;
                elapsedEvents.Publish(new TimerElapsedEvent(entities[row], timer.Sequence));
                if ((timer.Flags & ScheduledTimerFlags.Repeating) != 0)
                {
                    float interval = Math.Max(0.001f, timer.IntervalSeconds);
                    do
                    {
                        timer.RemainingSeconds += interval;
                    }
                    while (timer.RemainingSeconds <= 0f);
                }
                else
                {
                    timer.RemainingSeconds = 0f;
                    timer.Flags &= ~ScheduledTimerFlags.Running;
                }
            }
        }
    }
}

/// <summary>Owns validation/default invariants for the unique highlight tuning component.</summary>
public sealed class HighlightSettingsSystem : IGameSystem
{
    public HighlightSettingsSystem()
    {
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<EquipmentHighlightSettings>.Id);
        Descriptor = new SystemDescriptor(
            GlobalUiSystemIds.HighlightSettings,
            nameof(HighlightSettingsSystem),
            SystemPhase.Presentation,
            SceneGroup.Global,
            writeComponents: writes);
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        EntityId entity = context.World.GetUnique<HighlightSettingsSingleton>();
        ref EquipmentHighlightSettings settings = ref context.World.Get<EquipmentHighlightSettings>(entity);
        settings.GlowLayers = Math.Max(1, settings.GlowLayers);
        settings.GlowSpread = Math.Max(0f, settings.GlowSpread);
        settings.GlowSpreadSpeed = Math.Max(0f, settings.GlowSpreadSpeed);
        settings.GlowSpreadAmplitude = Math.Max(0f, settings.GlowSpreadAmplitude);
        settings.MaxAlpha = Math.Clamp(settings.MaxAlpha, 0f, 1f);
        settings.GlowPulseSpeed = Math.Max(0.1f, settings.GlowPulseSpeed);
        settings.GlowEasingPower = Math.Max(0.2f, settings.GlowEasingPower);
        settings.GlowMinIntensity = Math.Clamp(settings.GlowMinIntensity, 0f, 1f);
        settings.GlowMaxIntensity = Math.Clamp(settings.GlowMaxIntensity, 0f, 1f);
        settings.CornerRadius = Math.Max(0, settings.CornerRadius);
        settings.HighlightBorderThickness = Math.Max(0, settings.HighlightBorderThickness);
    }
}
