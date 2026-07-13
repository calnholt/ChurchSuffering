#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Content.Equipment;
using Crusaders30XX.ECS.DataOriented.Content.Medals;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

public sealed class EffectGameplayComposition
{
    private readonly IGameSystem[] systems;
    private readonly IEventRoute[] routes;

    private EffectGameplayComposition(IGameSystem[] systems, IEventRoute[] routes)
    {
        this.systems = systems;
        this.routes = routes;
    }

    public ReadOnlySpan<IGameSystem> Systems => systems;
    public ReadOnlySpan<IEventRoute> Routes => routes;

    public static EffectGameplayComposition Create(
        World world,
        DynamicBufferStore buffers,
        EffectGameplayEventHub events,
        EffectGameplayRouteConsumers? rootConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(buffers);
        ArgumentNullException.ThrowIfNull(events);

        var passives = new PassiveEffectRuntimeSystem(world, buffers, events);
        var equipment = new EquipmentEffectRuntimeSystem(world, events);
        var medals = new MedalEffectRuntimeSystem(world, events);
        var temperance = new TemperanceEffectRuntimeSystem(world, events);
        var consumers = rootConsumers ?? new EffectGameplayRouteConsumers();
        consumers
            .Add<ApplyPassiveEvent>(passives, priority: 100)
            .Add<UpdatePassive>(passives, priority: 100)
            .Add<RemovePassive>(passives, priority: 100)
            .Add<RemoveAllPassives>(passives, priority: 100)
            .Add<EquipmentActivateEvent>(equipment, priority: 100)
            .Add<MedalActivateEvent>(medals, priority: 100)
            .Add<FrostbiteTriggered>(medals, priority: 100)
            .Add<ModifyTemperanceEvent>(temperance, priority: 100)
            .Add<SetTemperanceEvent>(temperance, priority: 100)
            .Add<TriggerTemperance>(temperance, priority: 100);
        return new EffectGameplayComposition(
            Array.Empty<IGameSystem>(),
            events.BuildRoutes(consumers));
    }
}

public sealed class PassiveEffectRuntimeSystem :
    IEventConsumer<ApplyPassiveEvent>, IEventConsumer<UpdatePassive>,
    IEventConsumer<RemovePassive>, IEventConsumer<RemoveAllPassives>
{
    private readonly World world;
    private readonly DynamicBufferStore buffers;
    private readonly EffectGameplayEventHub events;

    public PassiveEffectRuntimeSystem(World world, DynamicBufferStore buffers, EffectGameplayEventHub events)
    {
        this.world = world;
        this.buffers = buffers;
        this.events = events;
    }

    public void Consume(in ApplyPassiveEvent value, ref EventDispatchContext context)
    {
        if (!world.IsAlive(value.Target)) return;
        DynamicBuffer<PassiveEntry> passives = GetOrCreate(value.Target);
        int stacks = PassiveRuntime.Apply(passives, value.Source, value.Effect, value.Stacks, value.Lifetime, 0, 0);
        events.PassiveTriggered.Publish(new PassiveTriggered(value.Source, value.Target, value.Effect, stacks));
    }

    public void Consume(in UpdatePassive value, ref EventDispatchContext context)
    {
        if (!world.IsAlive(value.Target)) return;
        DynamicBuffer<PassiveEntry> passives = GetOrCreate(value.Target);
        PassiveRuntime.Set(passives, EntityId.Null, value.Effect, value.Stacks, value.Lifetime, 0, 0);
    }

    public void Consume(in RemovePassive value, ref EventDispatchContext context)
    {
        if (world.TryGet(value.Target, out AppliedPassives component) && buffers.TryGet(component.Entries, out DynamicBuffer<PassiveEntry> passives))
            PassiveRuntime.Remove(passives, value.Effect, value.Stacks);
    }

    public void Consume(in RemoveAllPassives value, ref EventDispatchContext context)
    {
        if (world.TryGet(value.Target, out AppliedPassives component) && buffers.TryGet(component.Entries, out DynamicBuffer<PassiveEntry> passives))
            PassiveRuntime.RemoveThroughLifetime(passives, value.MaximumLifetime);
    }

    private DynamicBuffer<PassiveEntry> GetOrCreate(EntityId target)
    {
        if (world.TryGet(target, out AppliedPassives component)) return buffers.Get(component.Entries);
        var created = new AppliedPassives { Entries = buffers.Create<PassiveEntry>(target, 8) };
        world.Add(target, in created);
        return buffers.Get(created.Entries);
    }
}

public sealed class EquipmentEffectRuntimeSystem : IEventConsumer<EquipmentActivateEvent>
{
    private readonly World world;
    private readonly EffectGameplayEventHub events;
    private readonly RuleCommandBuffer commands = new(8);

    public EquipmentEffectRuntimeSystem(World world, EffectGameplayEventHub events)
    {
        this.world = world;
        this.events = events;
    }

    public void Consume(in EquipmentActivateEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Equipment, out EquippedEquipment equipment) || equipment.Owner != value.Owner) return;
        commands.Clear();
        var trigger = new RuleTriggerEnvelope(RuleTriggerKind.None, RuleTriggerIds.EquipmentActivated, default);
        var input = new EquipmentHandlerInput(
            new RuleInvocationId(context.Wave), value.Equipment, value.Owner, equipment.Definition, trigger,
            EquipmentHandlerFlags.Equipped | EquipmentHandlerFlags.Activated, value.Phase,
            value.OwnerResources, value.Deck, equipment.Usage, value.PrimaryTarget);
        Span<RuleHandlerResult> results = stackalloc RuleHandlerResult[4];
        var resultState = default(RuleResultWriterState);
        bool used = EquipmentActivationRuntime.TryActivate(
            world.AsReadOnly(), value.Equipment, ref equipment, in input,
            value.AvailabilitySatisfied != 0, value.BattleEpoch, commands,
            ReadOnlySpan<RuleFact>.Empty, ReadOnlySpan<EntityId>.Empty, results, ref resultState);
        world.Set(value.Equipment, in equipment);
        if (used)
        {
            events.EquipmentAbilityTriggered.Publish(new(value.Equipment, value.Owner, equipment.Definition));
            events.EquipmentActivated.Publish(new(value.Equipment, value.Owner, equipment.Definition, equipment.Usage.Uses));
            PublishCommands();
        }
        events.EquipmentUseResolved.Publish(new(value.Equipment, value.Owner, equipment.Definition, used ? (byte)1 : (byte)0));
    }

    private void PublishCommands()
    {
        ReadOnlySpan<RuleCommand> values = commands.AsReadOnlySpan();
        for (var index = 0; index < values.Length; index++) events.Commands.Publish(new(values[index]));
    }
}

public sealed class MedalEffectRuntimeSystem : IEventConsumer<MedalActivateEvent>, IEventConsumer<FrostbiteTriggered>
{
    private readonly World world;
    private readonly EffectGameplayEventHub events;
    private readonly Query<EquippedMedal> medals;
    private readonly RuleCommandBuffer commands = new(12);

    public MedalEffectRuntimeSystem(World world, EffectGameplayEventHub events)
    {
        this.world = world;
        this.events = events;
        medals = world.Query<EquippedMedal>(new QueryFilter(DebugName: "ECS043.EquippedMedals"));
    }

    public void Consume(in MedalActivateEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Medal, out EquippedMedal medal)) return;
        var input = new MedalHandlerInput(
            new RuleInvocationId(context.Wave), value.Medal, medal.Owner, medal.Definition, value.Trigger,
            MedalHandlerFlags.None, value.Phase, value.OwnerResources, value.Deck, medal.State, value.PrimaryTarget);
        Span<RuleHandlerResult> results = stackalloc RuleHandlerResult[4];
        var resultState = default(RuleResultWriterState);
        commands.Clear();
        bool activated = MedalActivationRuntime.TryObserve(
            world.AsReadOnly(), value.Medal, ref medal, in input, value.BattleEpoch, commands,
            ReadOnlySpan<RuleFact>.Empty, ReadOnlySpan<EntityId>.Empty, results, ref resultState);
        world.Set(value.Medal, in medal);
        if (!activated) return;
        events.MedalTriggered.Publish(new(value.Medal, medal.Owner, medal.Definition, medal.State.Count));
        ReadOnlySpan<RuleCommand> generated = commands.AsReadOnlySpan();
        for (var index = 0; index < generated.Length; index++) events.Commands.Publish(new(generated[index]));
    }

    public void Consume(in FrostbiteTriggered value, ref EventDispatchContext context)
    {
        ProviderSource selected = default;
        bool found = false;
        foreach (QueryChunk<EquippedMedal> chunk in medals)
        foreach (int row in chunk.Rows)
        {
            EquippedMedal medal = chunk.Component1[row];
            if (medal.Active == 0 || medal.Owner != value.Target || medal.Definition != Crusaders30XX.ECS.Data.Ids.MedalId.StOlaf) continue;
            EntityId entity = chunk.Entities[row];
            if (!found || entity.Index < selected.Entity.Index || entity.Index == selected.Entity.Index && entity.Generation < selected.Entity.Generation)
            {
                selected = new ProviderSource(entity, medal.Owner, medal.Definition, ProviderLifetime.WhileEquipped, 1);
                found = true;
            }
        }
        if (!found) return;
        var query = new ReplacementQuery(
            RuleReplacementKind.EffectThresholdDamage, value.Source, value.Target, value.PrimaryEnemy,
            RuleEffectIds.Frostbite, RuleDamageKind.Effect, -3, value.PrimaryEnemyEligible);
        Span<ReplacementAction> actions = stackalloc ReplacementAction[4];
        var writer = new ReplacementPlanWriter(actions);
        MedalProviderRules.TryBuildReplacement(selected, in query, ref writer);
        ReplacementPlan plan = writer.BuildPlan();
        events.Replacements.Publish(new(
            plan,
            plan.ActionCount > 0 ? actions[0] : default,
            plan.ActionCount > 1 ? actions[1] : default,
            plan.ActionCount > 2 ? actions[2] : default,
            plan.ActionCount > 3 ? actions[3] : default));
    }
}

public sealed class TemperanceEffectRuntimeSystem :
    IEventConsumer<ModifyTemperanceEvent>, IEventConsumer<SetTemperanceEvent>, IEventConsumer<TriggerTemperance>
{
    private readonly World world;
    private readonly EffectGameplayEventHub events;
    private readonly RuleCommandBuffer commands = new(3);

    public TemperanceEffectRuntimeSystem(World world, EffectGameplayEventHub events)
    {
        this.world = world;
        this.events = events;
    }
    public void Consume(in ModifyTemperanceEvent value, ref EventDispatchContext context)
    {
        if (world.TryGet(value.Owner, out Temperance state)) { state.Amount = TemperanceRuntime.Modify(state.Amount, value.Delta); world.Set(value.Owner, in state); }
    }
    public void Consume(in SetTemperanceEvent value, ref EventDispatchContext context)
    {
        if (world.TryGet(value.Owner, out Temperance state)) { state.Amount = TemperanceRuntime.Set(value.Amount); world.Set(value.Owner, in state); }
    }
    public void Consume(in TriggerTemperance value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Owner, out Temperance state) || !world.TryGet(value.Owner, out EquippedTemperanceAbility equipped)) return;
        commands.Clear();
        if (!TemperanceRuntime.TryResolve(ref state.Amount, equipped.Definition, value.Owner, value.PrimaryEnemy, value.Deck, commands.Writer, out int draws)) return;
        world.Set(value.Owner, in state);
        ref readonly TemperanceAbilityDefinition definition = ref TemperanceAbilityCatalog.GetDefinition(equipped.Definition);
        events.TemperanceResolved.Publish(new(value.Owner, equipped.Definition, definition.Threshold));
        ReadOnlySpan<RuleCommand> generated = commands.AsReadOnlySpan();
        for (var index = 0; index < generated.Length; index++) events.Commands.Publish(new(generated[index]));
        if (draws > 0) events.DrawCards.Publish(new(value.Owner, value.Deck, draws));
    }
}
