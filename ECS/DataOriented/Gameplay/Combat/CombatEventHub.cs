#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Combat;

/// <summary>Initialization-owned streams for every ECS-042 event contract.</summary>
public sealed class CombatEventHub
{
    public EventStream<AmbushTimerExpired> AmbushTimerExpired { get; } = new();
    public EventStream<ChangeBattlePhaseEvent> ChangeBattlePhase { get; } = new();
    public EventStream<ProceedToNextPhase> ProceedToNextPhase { get; } = new();
    public EventStream<ShowConfirmButtonEvent> ShowConfirmButton { get; } = new();
    public EventStream<AssignedBlockReturnCompleted> AssignedBlockReturnCompleted { get; } = new();
    public EventStream<BlockAssignmentAdded> BlockAssignmentAdded { get; } = new();
    public EventStream<BlockAssignmentRemoved> BlockAssignmentRemoved { get; } = new();
    public EventStream<CardBlockedEvent> CardBlocked { get; } = new();
    public EventStream<MustBeBlockedEvent> MustBeBlocked { get; } = new();
    public EventStream<ApplyEffect> ApplyEffect { get; } = new();
    public EventStream<AttackResolved> AttackResolved { get; } = new();
    public EventStream<EnemyAbsorbComplete> EnemyAbsorbComplete { get; } = new();
    public EventStream<EnemyAttackImpactNow> EnemyAttackImpactNow { get; } = new();
    public EventStream<EnemyDamageAppliedEvent> EnemyDamageApplied { get; } = new();
    public EventStream<EnemyKilledEvent> EnemyKilled { get; } = new();
    public EventStream<EnemyPhaseLethalEvent> EnemyPhaseLethal { get; } = new();
    public EventStream<EnemyPhaseResetEvent> EnemyPhaseReset { get; } = new();
    public EventStream<IntentPlanned> IntentPlanned { get; } = new();
    public EventStream<OnEnemyAttackHitEvent> EnemyAttackHit { get; } = new();
    public EventStream<ResolveAttack> ResolveAttack { get; } = new();
    public EventStream<ResolvingEnemyDamageEvent> ResolvingEnemyDamage { get; } = new();
    public EventStream<ShowStunnedOverlay> ShowStunnedOverlay { get; } = new();
    public EventStream<TriggerEnemyAttackDisplayEvent> TriggerEnemyAttackDisplay { get; } = new();
    public EventStream<ApplyBattleMaxHpEvent> ApplyBattleMaxHp { get; } = new();
    public EventStream<FullyHealEvent> FullyHeal { get; } = new();
    public EventStream<HealEvent> Heal { get; } = new();
    public EventStream<IncreaseMaxHpEvent> IncreaseMaxHp { get; } = new();
    public EventStream<ModifyHpEvent> ModifyHp { get; } = new();
    public EventStream<PlayerDied> PlayerDied { get; } = new();
    public EventStream<SetHpEvent> SetHp { get; } = new();
    public EventStream<ModifyThreatEvent> ModifyThreat { get; } = new();
    public EventStream<SetThreatEvent> SetThreat { get; } = new();

    /// <summary>
    /// Returns the combat fragment for the coordinator-owned root endpoint. This method never
    /// creates or attaches an <see cref="EventRuntime"/>.
    /// </summary>
    public IEventRoute[] BuildRoutes(CombatRouteConsumers? consumers = null)
    {
        consumers ??= new CombatRouteConsumers();
        return
        [
            Route(42001, nameof(AmbushTimerExpired), AmbushTimerExpired, consumers),
            Route(42002, nameof(ChangeBattlePhaseEvent), ChangeBattlePhase, consumers),
            Route(42003, nameof(ProceedToNextPhase), ProceedToNextPhase, consumers),
            Route(42004, nameof(ShowConfirmButtonEvent), ShowConfirmButton, consumers),
            Route(42005, nameof(AssignedBlockReturnCompleted), AssignedBlockReturnCompleted, consumers),
            Route(42006, nameof(BlockAssignmentAdded), BlockAssignmentAdded, consumers),
            Route(42007, nameof(BlockAssignmentRemoved), BlockAssignmentRemoved, consumers),
            Route(42008, nameof(CardBlockedEvent), CardBlocked, consumers),
            Route(42009, nameof(MustBeBlockedEvent), MustBeBlocked, consumers),
            Route(42010, nameof(ApplyEffect), ApplyEffect, consumers),
            Route(42011, nameof(AttackResolved), AttackResolved, consumers),
            Route(42012, nameof(EnemyAbsorbComplete), EnemyAbsorbComplete, consumers),
            Route(42013, nameof(EnemyAttackImpactNow), EnemyAttackImpactNow, consumers),
            Route(42014, nameof(EnemyDamageAppliedEvent), EnemyDamageApplied, consumers),
            Route(42015, nameof(EnemyKilledEvent), EnemyKilled, consumers),
            Route(42016, nameof(EnemyPhaseLethalEvent), EnemyPhaseLethal, consumers),
            Route(42017, nameof(EnemyPhaseResetEvent), EnemyPhaseReset, consumers),
            Route(42018, nameof(IntentPlanned), IntentPlanned, consumers),
            Route(42019, nameof(OnEnemyAttackHitEvent), EnemyAttackHit, consumers),
            Route(42020, nameof(ResolveAttack), ResolveAttack, consumers),
            Route(42021, nameof(ResolvingEnemyDamageEvent), ResolvingEnemyDamage, consumers),
            Route(42022, nameof(ShowStunnedOverlay), ShowStunnedOverlay, consumers),
            Route(42023, nameof(TriggerEnemyAttackDisplayEvent), TriggerEnemyAttackDisplay, consumers),
            Route(42024, nameof(ApplyBattleMaxHpEvent), ApplyBattleMaxHp, consumers),
            Route(42025, nameof(FullyHealEvent), FullyHeal, consumers),
            Route(42026, nameof(HealEvent), Heal, consumers),
            Route(42027, nameof(IncreaseMaxHpEvent), IncreaseMaxHp, consumers),
            Route(42028, nameof(ModifyHpEvent), ModifyHp, consumers),
            Route(42029, nameof(PlayerDied), PlayerDied, consumers),
            Route(42030, nameof(SetHpEvent), SetHp, consumers),
            Route(42031, nameof(ModifyThreatEvent), ModifyThreat, consumers),
            Route(42032, nameof(SetThreatEvent), SetThreat, consumers),
        ];
    }

    private static EventRoute<T> Route<T>(int id, string name, EventStream<T> stream, CombatRouteConsumers consumers)
        where T : unmanaged => new(id, name, stream, consumers.Get<T>());
}

/// <summary>Initialization-only consumer registrations used by the root event composition.</summary>
public sealed class CombatRouteConsumers
{
    private readonly Dictionary<Type, object> registrations = new();

    public CombatRouteConsumers Add<T>(IEventConsumer<T> consumer, int priority = 0, string? name = null)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(consumer);
        Type type = typeof(T);
        if (!registrations.TryGetValue(type, out object? value))
        {
            value = new List<EventConsumerRegistration<T>>();
            registrations.Add(type, value);
        }

        ((List<EventConsumerRegistration<T>>)value).Add(new EventConsumerRegistration<T>(
            priority, name ?? consumer.GetType().Name, consumer));
        return this;
    }

    internal EventConsumerRegistration<T>[] Get<T>() where T : unmanaged
    {
        if (!registrations.TryGetValue(typeof(T), out object? value)) return [];
        return ((List<EventConsumerRegistration<T>>)value).ToArray();
    }
}

public static class CombatEventTypeIds
{
    public const int First = 42001;
    public const int Last = 42032;
    public const int Count = Last - First + 1;
}
