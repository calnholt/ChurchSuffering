#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

public readonly record struct EquipmentAbilityTriggered(EntityId Equipment, EntityId Owner, EquipmentId Definition);
public readonly record struct EquipmentActivated(EntityId Equipment, EntityId Owner, EquipmentId Definition, ushort Uses);
public readonly record struct EquipmentDestroyed(EntityId Equipment, EntityId Owner, EquipmentId Definition);
public readonly record struct EquipmentUseResolved(EntityId Equipment, EntityId Owner, EquipmentId Definition, byte Succeeded);
public readonly record struct MedalTriggered(EntityId Medal, EntityId Owner, MedalId Definition, ushort Count);
public readonly record struct EquipmentActivateEvent(
    EntityId Equipment,
    EntityId Owner,
    RulePhase Phase,
    byte AvailabilitySatisfied,
    int BattleEpoch,
    CombatResourceSnapshot OwnerResources,
    DeckStateSnapshot Deck,
    TargetHandle PrimaryTarget)
{
    public EquipmentActivateEvent(EntityId equipment, EntityId owner, RulePhase phase, byte availabilitySatisfied)
        : this(equipment, owner, phase, availabilitySatisfied, 0, default, default, default) { }
}
public readonly record struct MedalActivateEvent(
    EntityId Medal,
    RuleTriggerEnvelope Trigger,
    RulePhase Phase,
    int BattleEpoch,
    CombatResourceSnapshot OwnerResources,
    DeckStateSnapshot Deck,
    TargetHandle PrimaryTarget)
{
    public MedalActivateEvent(EntityId medal, RuleTriggerEnvelope trigger)
        : this(medal, trigger, RulePhase.None, 0, default, default, default) { }
}
public readonly record struct ApplyPassiveEvent(EntityId Source, EntityId Target, EffectId Effect, int Stacks, PassiveLifetime Lifetime);
public readonly record struct FrostbiteTriggered(EntityId Source, EntityId Target, EntityId PrimaryEnemy, byte PrimaryEnemyEligible);
public readonly record struct PassiveTriggered(EntityId Source, EntityId Target, EffectId Effect, int Stacks);
public readonly record struct RemoveAllPassives(EntityId Target, PassiveLifetime MaximumLifetime);
public readonly record struct RemovePassive(EntityId Target, EffectId Effect, int Stacks);
public readonly record struct TribulationTriggered(EntityId Source, EntityId Target, int Level);
public readonly record struct UpdatePassive(EntityId Target, EffectId Effect, int Stacks, PassiveLifetime Lifetime);
public readonly record struct PoisonDamageEvent(EntityId Source, EntityId Target, int Damage);
public readonly record struct ModifyTemperanceEvent(EntityId Owner, int Delta);
public readonly record struct SetTemperanceEvent(EntityId Owner, int Amount);
public readonly record struct TriggerTemperance(EntityId Owner, EntityId PrimaryEnemy, EntityId Deck);

/// <summary>Typed hand-off for generated equipment, medal, and temperance commands.</summary>
public readonly record struct EffectRuleCommandEvent(RuleCommand Command);

/// <summary>The fixed-size event form of a shared replacement plan.</summary>
public readonly record struct ReplacementEffectResolved(
    ReplacementPlan Plan,
    ReplacementAction Action0,
    ReplacementAction Action1,
    ReplacementAction Action2,
    ReplacementAction Action3)
{
    public ReplacementAction GetAction(int index) => index switch
    {
        0 when Plan.ActionCount > 0 => Action0,
        1 when Plan.ActionCount > 1 => Action1,
        2 when Plan.ActionCount > 2 => Action2,
        3 when Plan.ActionCount > 3 => Action3,
        _ => throw new System.ArgumentOutOfRangeException(nameof(index)),
    };
}

public readonly record struct TemperanceAbilityResolved(EntityId Owner, TemperanceAbilityId Ability, int Threshold);
public readonly record struct DrawCardsForTemperance(EntityId Owner, EntityId Deck, int Count);

public sealed class EffectGameplayEventHub
{
    public EventStream<EquipmentAbilityTriggered> EquipmentAbilityTriggered { get; } = new();
    public EventStream<EquipmentActivated> EquipmentActivated { get; } = new();
    public EventStream<EquipmentDestroyed> EquipmentDestroyed { get; } = new();
    public EventStream<EquipmentUseResolved> EquipmentUseResolved { get; } = new();
    public EventStream<MedalTriggered> MedalTriggered { get; } = new();
    public EventStream<EquipmentActivateEvent> EquipmentActivate { get; } = new();
    public EventStream<MedalActivateEvent> MedalActivate { get; } = new();
    public EventStream<ApplyPassiveEvent> ApplyPassive { get; } = new();
    public EventStream<FrostbiteTriggered> Frostbite { get; } = new();
    public EventStream<PassiveTriggered> PassiveTriggered { get; } = new();
    public EventStream<RemoveAllPassives> RemoveAllPassives { get; } = new();
    public EventStream<RemovePassive> RemovePassive { get; } = new();
    public EventStream<TribulationTriggered> Tribulation { get; } = new();
    public EventStream<UpdatePassive> UpdatePassive { get; } = new();
    public EventStream<PoisonDamageEvent> PoisonDamage { get; } = new();
    public EventStream<ModifyTemperanceEvent> ModifyTemperance { get; } = new();
    public EventStream<SetTemperanceEvent> SetTemperance { get; } = new();
    public EventStream<TriggerTemperance> TriggerTemperance { get; } = new();
    public EventStream<EffectRuleCommandEvent> Commands { get; } = new();
    public EventStream<ReplacementEffectResolved> Replacements { get; } = new();
    public EventStream<TemperanceAbilityResolved> TemperanceResolved { get; } = new();
    public EventStream<DrawCardsForTemperance> DrawCards { get; } = new();

    /// <summary>All ECS-043 routes for the root-owned, cross-domain event runtime.</summary>
    public IEventRoute[] BuildRoutes(EffectGameplayRouteConsumers? consumers = null)
    {
        consumers ??= new EffectGameplayRouteConsumers();
        IEventRoute[] ledger = BuildLedgerRoutes(consumers);
        var routes = new IEventRoute[ledger.Length + 2];
        ledger.CopyTo(routes, 0);
        routes[20] = Route(43021, nameof(TemperanceAbilityResolved), TemperanceResolved, consumers);
        routes[21] = Route(43022, nameof(DrawCardsForTemperance), DrawCards, consumers);
        return routes;
    }

    /// <summary>The twenty migration-ledger event routes owned by ECS-043.</summary>
    public IEventRoute[] BuildLedgerRoutes(EffectGameplayRouteConsumers? consumers = null)
    {
        consumers ??= new EffectGameplayRouteConsumers();
        return
    [
            Route(43001, nameof(EquipmentAbilityTriggered), EquipmentAbilityTriggered, consumers),
            Route(43002, nameof(EquipmentActivated), EquipmentActivated, consumers),
            Route(43003, nameof(EquipmentDestroyed), EquipmentDestroyed, consumers),
            Route(43004, nameof(EquipmentUseResolved), EquipmentUseResolved, consumers),
            Route(43005, nameof(MedalTriggered), MedalTriggered, consumers),
            Route(43006, nameof(EquipmentActivateEvent), EquipmentActivate, consumers),
            Route(43007, nameof(MedalActivateEvent), MedalActivate, consumers),
            Route(43008, nameof(ApplyPassiveEvent), ApplyPassive, consumers),
            Route(43009, nameof(FrostbiteTriggered), Frostbite, consumers),
            Route(43010, nameof(PassiveTriggered), PassiveTriggered, consumers),
            Route(43011, nameof(RemoveAllPassives), RemoveAllPassives, consumers),
            Route(43012, nameof(RemovePassive), RemovePassive, consumers),
            Route(43013, nameof(TribulationTriggered), Tribulation, consumers),
            Route(43014, nameof(UpdatePassive), UpdatePassive, consumers),
            Route(43015, nameof(PoisonDamageEvent), PoisonDamage, consumers),
            Route(43016, nameof(ModifyTemperanceEvent), ModifyTemperance, consumers),
            Route(43017, nameof(SetTemperanceEvent), SetTemperance, consumers),
            Route(43018, nameof(TriggerTemperance), TriggerTemperance, consumers),
            Route(43019, nameof(EffectRuleCommandEvent), Commands, consumers),
            Route(43020, nameof(ReplacementEffectResolved), Replacements, consumers),
    ];
    }

    private static EventRoute<T> Route<T>(int id, string name, EventStream<T> stream, EffectGameplayRouteConsumers consumers) where T : unmanaged =>
        new(id, name, stream, consumers.Get<T>());
}

public sealed class EffectGameplayRouteConsumers
{
    private readonly Dictionary<Type, object> registrations = new();

    public EffectGameplayRouteConsumers Add<T>(IEventConsumer<T> consumer, int priority = 0, string? name = null)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (!registrations.TryGetValue(typeof(T), out object? value))
        {
            value = new List<EventConsumerRegistration<T>>();
            registrations.Add(typeof(T), value);
        }
        ((List<EventConsumerRegistration<T>>)value).Add(new(priority, name ?? consumer.GetType().Name, consumer));
        return this;
    }

    internal EventConsumerRegistration<T>[] Get<T>() where T : unmanaged =>
        registrations.TryGetValue(typeof(T), out object? value)
            ? ((List<EventConsumerRegistration<T>>)value).ToArray()
            : [];
}

public static class EffectGameplayEventTypeIds
{
    public const int First = 43001;
    public const int Last = 43022;
}
