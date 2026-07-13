#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Combat;

public static class CombatSystemIds
{
    // These IDs remain stable compatibility keys. Only AttackResolution and EnemyAttackProgress
    // are operational scheduler registrations; the mapping document assigns every other name to
    // one of those consolidated owners or to ECS-045 presentation extraction.
    public static readonly SystemId Anathema = new(4201);
    public static readonly SystemId AssignedBlockLifecycle = new(4202);
    public static readonly SystemId AttackResolution = new(4203);
    public static readonly SystemId BattleBackground = new(4204);
    public static readonly SystemId BattlePileGamepadInput = new(4205);
    public static readonly SystemId BattleScene = new(4206);
    public static readonly SystemId BattleStateInfo = new(4207);
    public static readonly SystemId CanPlayHighlight = new(4208);
    public static readonly SystemId CathedralLighting = new(4209);
    public static readonly SystemId Courage = new(4210);
    public static readonly SystemId DesertBackground = new(4211);
    public static readonly SystemId EnemyAttackProgress = new(4212);
    public static readonly SystemId EnemyDamage = new(4213);
    public static readonly SystemId EnemyDefeat = new(4214);
    public static readonly SystemId EnemyIntentPips = new(4215);
    public static readonly SystemId EnemyIntentPlanning = new(4216);
    public static readonly SystemId EnemyPhaseFlow = new(4217);
    public static readonly SystemId Hp = new(4218);
    public static readonly SystemId MarkedEndTurn = new(4219);
    public static readonly SystemId ActorPresentation = new(4220);
    public static readonly SystemId EffectCoordinator = new(4221);
    public static readonly SystemId MustBeBlocked = new(4222);
    public static readonly SystemId PhaseChangeEvents = new(4223);
    public static readonly SystemId PhaseCoordinator = new(4224);
    public static readonly SystemId HudFeedback = new(4225);
    public static readonly SystemId WispParticles = new(4226);
    public static readonly SystemId TestFightFlow = new(4227);
    public static readonly SystemId Thorned = new(4228);
    public static readonly SystemId Tribulation = new(4229);
    public static readonly SystemId Weapon = new(4230);
}

/// <summary>Common base only for operational combat systems.</summary>
public abstract class CombatSystemBase : IGameSystem
{
    protected CombatSystemBase(SystemDescriptor descriptor) => Descriptor = descriptor;

    public SystemDescriptor Descriptor { get; }
    public abstract void Update(ref SystemContext context);
}

/// <summary>Consolidated owner of the combat mandatory/reactive rule queue.</summary>
public sealed class AttackResolutionSystem : CombatSystemBase
{
    private static readonly Type[] WrittenBuffers =
    [
        typeof(CombatPassive),
        typeof(CombatTraceEntry),
        typeof(EnemyAttackId),
        typeof(BlockAssignmentEntry),
    ];

    private static readonly int[] EmittedEvents =
    [
        42004, 42011, 42013, 42014, 42015, 42016, 42017, 42018, 42019, 42021, 42023, 42028, 42029,
    ];

    private readonly CombatSessionSlot sessions;

    public AttackResolutionSystem(CombatSessionSlot sessions)
        : base(CreateDescriptor()) => this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

    public override void Update(ref SystemContext context) => sessions.RequireActive().Process(context.Commands);

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<Battlefield>.Id)
            .With(ComponentType<ModifiedBlock>.Id)
            .With(ComponentType<CannotBlockThisAttack>.Id);

        ComponentSignature writes = default;
        writes = writes.With(ComponentType<BattleInfo>.Id)
            .With(ComponentType<BattleStateInfo>.Id)
            .With(ComponentType<PhaseState>.Id)
            .With(ComponentType<Enemy>.Id)
            .With(ComponentType<EnemyArsenal>.Id)
            .With(ComponentType<AttackIntent>.Id)
            .With(ComponentType<NextTurnAttackIntent>.Id)
            .With(ComponentType<EnemyAttackProgress>.Id)
            .With(ComponentType<AmbushState>.Id)
            .With(ComponentType<HP>.Id)
            .With(ComponentType<Courage>.Id)
            .With(ComponentType<Temperance>.Id)
            .With(ComponentType<ActionPoints>.Id)
            .With(ComponentType<Threat>.Id);

        return new SystemDescriptor(
            CombatSystemIds.AttackResolution,
            nameof(AttackResolutionSystem),
            SystemPhase.Rules,
            SceneGroup.Battle,
            reads,
            writes,
            readDynamicBufferTypes: WrittenBuffers,
            writeDynamicBufferTypes: WrittenBuffers,
            emittedEventTypeIds: EmittedEvents,
            recordsStructuralCommands: true,
            eventBarrier: EventBarrier.AfterSystem);
    }
}

/// <summary>Consolidated owner of assigned-block aggregate reconciliation.</summary>
public sealed class EnemyAttackProgressManagementSystem : CombatSystemBase
{
    private readonly CombatSessionSlot sessions;

    public EnemyAttackProgressManagementSystem(CombatSessionSlot sessions)
        : base(CreateDescriptor()) => this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));

    public override void Update(ref SystemContext context) =>
        CombatSession.RecalculateBlocks(context.World, sessions.RequireActive().Battle);

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<EnemyAttackProgress>.Id);
        return new SystemDescriptor(
            CombatSystemIds.EnemyAttackProgress,
            nameof(EnemyAttackProgressManagementSystem),
            SystemPhase.Gameplay,
            SceneGroup.Battle,
            writeComponents: writes,
            readDynamicBufferTypes: [typeof(BlockAssignmentEntry)]);
    }
}

/// <summary>The explicit ECS-042 scheduler allowlist; compatibility ledger names are excluded.</summary>
public sealed class CombatGameplayComposition
{
    private readonly IGameSystem[] systems;

    private CombatGameplayComposition(IGameSystem[] systems) => this.systems = systems;

    public ReadOnlySpan<IGameSystem> Systems => systems;

    public static CombatGameplayComposition Create(CombatSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var sessions = new CombatSessionSlot(session.World);
        sessions.Bind(session);
        return Create(sessions);
    }

    public static CombatGameplayComposition Create(CombatSessionSlot sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        return new CombatGameplayComposition(
        [
            new AttackResolutionSystem(sessions),
            new EnemyAttackProgressManagementSystem(sessions),
        ]);
    }
}
