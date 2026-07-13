#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Combat;

/// <summary>
/// Combat-owned command consumers. It is created before the root endpoint and bound to the
/// session after the root runtime has been attached to the world.
/// </summary>
public sealed class CombatOwnedEventConsumers :
    IEventConsumer<AmbushTimerExpired>,
    IEventConsumer<ChangeBattlePhaseEvent>,
    IEventConsumer<ProceedToNextPhase>,
    IEventConsumer<MustBeBlockedEvent>,
    IEventConsumer<ApplyEffect>,
    IEventConsumer<ResolveAttack>,
    IEventConsumer<ApplyBattleMaxHpEvent>,
    IEventConsumer<FullyHealEvent>,
    IEventConsumer<HealEvent>,
    IEventConsumer<IncreaseMaxHpEvent>,
    IEventConsumer<SetHpEvent>,
    IEventConsumer<ModifyThreatEvent>,
    IEventConsumer<SetThreatEvent>
{
    public const int Priority = 100;

    private readonly World world;
    private readonly CombatSessionSlot sessions;

    public CombatOwnedEventConsumers(World world) : this(new CombatSessionSlot(world)) { }

    public CombatOwnedEventConsumers(CombatSessionSlot sessions)
    {
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        world = sessions.World;
    }

    public CombatSessionSlot Sessions => sessions;

    public void Bind(CombatSession value)
    {
        ArgumentNullException.ThrowIfNull(value);
        sessions.Bind(value);
    }

    public void Unbind(CombatSession value) => sessions.Unbind(value);

    public CombatRouteConsumers RegisterRoutes(CombatRouteConsumers? routes = null)
    {
        routes ??= new CombatRouteConsumers();
        return routes
            .Add<AmbushTimerExpired>(this, Priority)
            .Add<ChangeBattlePhaseEvent>(this, Priority)
            .Add<ProceedToNextPhase>(this, Priority)
            .Add<MustBeBlockedEvent>(this, Priority)
            .Add<ApplyEffect>(this, Priority)
            .Add<ResolveAttack>(this, Priority)
            .Add<ApplyBattleMaxHpEvent>(this, Priority)
            .Add<FullyHealEvent>(this, Priority)
            .Add<HealEvent>(this, Priority)
            .Add<IncreaseMaxHpEvent>(this, Priority)
            .Add<SetHpEvent>(this, Priority)
            .Add<ModifyThreatEvent>(this, Priority)
            .Add<SetThreatEvent>(this, Priority);
    }

    public void Consume(in AmbushTimerExpired value, ref EventDispatchContext context)
    {
        CombatSession owner = RequireSession(value.Battle);
        owner.EnqueueMandatory(CombatRuleKind.ConfirmBlocks, value0: 1);
    }

    public void Consume(in ChangeBattlePhaseEvent value, ref EventDispatchContext context)
    {
        RequireSession(value.Battle);
        ref PhaseState phase = ref world.Get<PhaseState>(value.Battle);
        phase.Previous = value.Previous;
        phase.Current = value.Current;
        phase.Sequence = Math.Max(phase.Sequence, value.Sequence);
    }

    public void Consume(in ProceedToNextPhase value, ref EventDispatchContext context)
    {
        CombatSession owner = RequireSession(value.Battle);
        CombatPhase phase = world.Get<PhaseState>(value.Battle).Current;
        if (phase == CombatPhase.Block) owner.ConfirmBlocks();
        else if (phase == CombatPhase.Action) owner.EndActionPhase();
    }

    public void Consume(in MustBeBlockedEvent value, ref EventDispatchContext context)
    {
        RequireSession(value.Battle);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(value.Battle);
        progress.Requirement = value.Kind;
        progress.RequiredAmount = Math.Max(0, value.Amount);
    }

    public void Consume(in ApplyEffect value, ref EventDispatchContext context)
    {
        CombatSession owner = RequireSessionForEntity(value.Target);
        owner.GrantPassive(value.Target, value.Effect.Id, value.Effect.Magnitude);
    }

    public void Consume(in ResolveAttack value, ref EventDispatchContext context)
    {
        CombatSession owner = RequireSession(value.Battle);
        if (world.Get<EnemyAttackProgress>(value.Battle).Attack == value.Attack)
            owner.EnqueueMandatory(CombatRuleKind.ConfirmBlocks, value0: 1);
    }

    public void Consume(in ApplyBattleMaxHpEvent value, ref EventDispatchContext context)
    {
        ref HP hp = ref RequireHp(value.Target);
        hp.Max = Math.Max(1, value.Maximum);
        hp.Current = Math.Min(hp.Current, hp.Max);
    }

    public void Consume(in FullyHealEvent value, ref EventDispatchContext context)
    {
        ref HP hp = ref RequireHp(value.Target);
        hp.Current = hp.Max;
    }

    public void Consume(in HealEvent value, ref EventDispatchContext context)
    {
        ref HP hp = ref RequireHp(value.Target);
        hp.Current = Math.Min(hp.Max, hp.Current + Math.Max(0, value.Amount));
    }

    public void Consume(in IncreaseMaxHpEvent value, ref EventDispatchContext context)
    {
        ref HP hp = ref RequireHp(value.Target);
        int increase = Math.Max(0, value.Amount);
        hp.Max += increase;
        hp.UnscarredMax += increase;
        hp.Current += increase;
    }

    public void Consume(in SetHpEvent value, ref EventDispatchContext context)
    {
        ref HP hp = ref RequireHp(value.Target);
        hp.Current = Math.Clamp(value.Current, 0, hp.Max);
    }

    public void Consume(in ModifyThreatEvent value, ref EventDispatchContext context)
    {
        RequireActivePlayer(value.Player);
        ref Threat threat = ref world.Get<Threat>(value.Player);
        threat.Amount = Math.Max(0, threat.Amount + value.Delta);
    }

    public void Consume(in SetThreatEvent value, ref EventDispatchContext context)
    {
        RequireActivePlayer(value.Player);
        world.Get<Threat>(value.Player).Amount = Math.Max(0, value.Amount);
    }

    private CombatSession RequireSession(EntityId battle)
    {
        CombatSession value = sessions.Current ?? throw new InvalidOperationException(
            "Combat consumers must be bound after the root event runtime is attached.");
        if (value.Battle != battle) throw new InvalidOperationException("Combat event targets another battle session.");
        return value;
    }

    private CombatSession RequireSessionForEntity(EntityId target)
    {
        CombatSession value = sessions.Current ?? throw new InvalidOperationException(
            "Combat consumers must be bound after the root event runtime is attached.");
        if (target != value.Player && target != value.Enemy)
            throw new InvalidOperationException("Combat effect targets an entity outside the active battle.");
        return value;
    }

    private ref HP RequireHp(EntityId target)
    {
        RequireSessionForEntity(target);
        if (!world.Has<HP>(target)) throw new InvalidOperationException("Combat HP event target has no HP component.");
        return ref world.Get<HP>(target);
    }

    private void RequireActivePlayer(EntityId player)
    {
        CombatSession value = sessions.RequireActive();
        if (value.Player != player)
            throw new InvalidOperationException("Combat resource event targets a player outside the active battle.");
    }
}

/// <summary>
/// Root-owned active combat slot. Scheduler systems and event consumers keep this stable object
/// while successive battle sessions are explicitly bound and unbound.
/// </summary>
public sealed class CombatSessionSlot
{
    public CombatSessionSlot(World world) => World = world ?? throw new ArgumentNullException(nameof(world));

    public World World { get; }
    public CombatSession? Current { get; private set; }

    public void Bind(CombatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(session.World, World))
            throw new ArgumentException("The combat session must belong to the slot's world.", nameof(session));
        if (Current is not null && !ReferenceEquals(Current, session))
            throw new InvalidOperationException("Unbind the active combat session before binding another battle.");
        Current = session;
    }

    public void Unbind(CombatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(Current, session))
            throw new InvalidOperationException("Only the active combat session can be unbound.");
        if (World.HasEventRuntime && World.Events.PendingEventCount != 0)
            throw new InvalidOperationException("Drain the root event runtime before unbinding a combat session.");
        Current = null;
    }

    public CombatSession RequireActive() => Current ?? throw new InvalidOperationException(
        "A combat session must be bound before the Battle scheduler group runs.");
}
