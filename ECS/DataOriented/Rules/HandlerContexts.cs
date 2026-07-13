#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Crusaders30XX.ECS.DataOriented.Rules;

public readonly ref struct CardPlayContext
{
    public CardPlayContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        RuleInvocationId invocation,
        EntityId card,
        EntityId player,
        CardId definition,
        bool isUpgraded,
        TargetHandle primaryTarget = default,
        TargetCollectionHandle candidateTargets = default)
    {
        World = world;
        Commands = commands;
        Invocation = invocation;
        Card = card;
        Player = player;
        Definition = definition;
        IsUpgraded = isUpgraded;
        PrimaryTarget = primaryTarget;
        CandidateTargets = candidateTargets;
    }

    public ReadOnlyWorld World { get; }
    public RuleCommandWriter Commands { get; }
    public RuleInvocationId Invocation { get; }
    public EntityId Card { get; }
    public EntityId Player { get; }
    public CardId Definition { get; }
    public bool IsUpgraded { get; }
    public TargetHandle PrimaryTarget { get; }
    public TargetCollectionHandle CandidateTargets { get; }

    public RuleCommandIndex Append(in RuleCommand command) => Commands.Append(in command);
}

/// <summary>
/// Unified card-definition handler surface. Scalar snapshots are unmanaged; facts,
/// payment cards, targets, results, and random state remain caller-owned.
/// </summary>
public ref struct CardHandlerContext
{
    private RuleResultWriter results;
    private Span<RuleRandomState> randomState;

    public CardHandlerContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        in CardHandlerInput input,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> paymentCards,
        ReadOnlySpan<EntityId> candidateTargets,
        Span<RuleHandlerResult> resultStorage,
        ref RuleResultWriterState resultState,
        ref RuleRandomState randomState)
    {
        World = world;
        Commands = commands;
        Input = input;
        Facts = new RuleFactReader(facts);
        PaymentCards = paymentCards;
        CandidateTargets = candidateTargets;
        results = new RuleResultWriter(resultStorage, ref resultState);
        this.randomState = MemoryMarshal.CreateSpan(ref randomState, 1);
    }

    public ReadOnlyWorld World { get; }
    public RuleCommandWriter Commands { get; }
    public CardHandlerInput Input { get; }
    public RuleFactReader Facts { get; }
    public ReadOnlySpan<EntityId> PaymentCards { get; }
    public ReadOnlySpan<EntityId> CandidateTargets { get; }
    public RuleInvocationId Invocation => Input.Invocation;
    public EntityId Card => Input.Card;
    public EntityId Player => Input.Player;
    public CardId Definition => Input.Definition;
    public TriggerId Stage => Input.Trigger.SemanticTrigger;
    public RuleTriggerEnvelope Trigger => Input.Trigger;
    public bool IsUpgraded => Input.IsUpgraded;
    public TargetHandle PrimaryTarget => Input.PrimaryTarget;
    public RuleResultWriter Results => results;
    public DeterministicRuleRandom Random => new(ref randomState[0]);

    public RuleCommandIndex Append(in RuleCommand command) => Commands.Append(in command);
}

public readonly ref struct EnemyAttackContext
{
    public EnemyAttackContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        RuleInvocationId invocation,
        EntityId enemy,
        EnemyId enemyDefinition,
        EnemyAttackId attackDefinition,
        int phase,
        TargetHandle target)
    {
        World = world;
        Commands = commands;
        Invocation = invocation;
        Enemy = enemy;
        EnemyDefinition = enemyDefinition;
        AttackDefinition = attackDefinition;
        Phase = phase;
        Target = target;
    }

    public ReadOnlyWorld World { get; }
    public RuleCommandWriter Commands { get; }
    public RuleInvocationId Invocation { get; }
    public EntityId Enemy { get; }
    public EnemyId EnemyDefinition { get; }
    public EnemyAttackId AttackDefinition { get; }
    public int Phase { get; }
    public TargetHandle Target { get; }

    public RuleCommandIndex Append(in RuleCommand command) => Commands.Append(in command);
}

/// <summary>
/// Unified enemy planning, lifecycle, and attack handler surface. Attack stages may
/// receive empty plan storage; planning stages append bounded IDs and persist memory.
/// </summary>
public ref struct EnemyHandlerContext
{
    private RuleResultWriter results;
    private EnemyPlanWriter plan;
    private Span<RuleRandomState> randomState;

    public EnemyHandlerContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        in EnemyHandlerInput input,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> targets,
        Span<RuleHandlerResult> resultStorage,
        Span<EnemyAttackId> planStorage,
        ref EnemyPlanningMemory persistedMemory,
        ref RuleResultWriterState resultState,
        ref EnemyPlanWriterState planState,
        ref RuleRandomState randomState)
    {
        World = world;
        Commands = commands;
        Input = input;
        Facts = new RuleFactReader(facts);
        Targets = targets;
        results = new RuleResultWriter(resultStorage, ref resultState);
        persistedMemory = input.PlanningMemory;
        plan = new EnemyPlanWriter(planStorage, ref persistedMemory, ref planState);
        this.randomState = MemoryMarshal.CreateSpan(ref randomState, 1);
    }

    public ReadOnlyWorld World { get; }
    public RuleCommandWriter Commands { get; }
    public EnemyHandlerInput Input { get; }
    public RuleFactReader Facts { get; }
    public ReadOnlySpan<EntityId> Targets { get; }
    public RuleInvocationId Invocation => Input.Invocation;
    public EntityId Enemy => Input.Enemy;
    public EnemyId Definition => Input.Definition;
    public EnemyAttackId Attack => Input.Attack;
    public TriggerId Stage => Input.Trigger.SemanticTrigger;
    public RuleTriggerEnvelope Trigger => Input.Trigger;
    public TargetHandle PrimaryTarget => Input.PrimaryTarget;
    public RuleResultWriter Results => results;
    public EnemyPlanWriter Plan => plan;
    public DeterministicRuleRandom Random => new(ref randomState[0]);

    public RuleCommandIndex Append(in RuleCommand command) => Commands.Append(in command);
}

public ref struct EquipmentHandlerContext
{
    private readonly EntityId equipment;
    private readonly EntityId owner;
    private readonly EquipmentId definition;
    private readonly TriggerId trigger;
    private readonly TargetHandle target;
    private RuleResultWriter results;
    private Span<RuleRandomState> randomState;

    public EquipmentHandlerContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        RuleInvocationId invocation,
        EntityId equipment,
        EntityId owner,
        EquipmentId definition,
        TriggerId trigger,
        TargetHandle target = default)
    {
        World = world;
        Commands = commands;
        Invocation = invocation;
        this.equipment = equipment;
        this.owner = owner;
        this.definition = definition;
        this.trigger = trigger;
        this.target = target;
        Input = default;
        Facts = new RuleFactReader(default);
        Targets = default;
        results = default;
        randomState = default;
        HasUnifiedInput = false;
    }

    public EquipmentHandlerContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        in EquipmentHandlerInput input,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> targets,
        Span<RuleHandlerResult> resultStorage,
        ref RuleResultWriterState resultState,
        ref RuleRandomState randomState)
    {
        World = world;
        Commands = commands;
        Invocation = input.Invocation;
        equipment = input.Equipment;
        owner = input.Owner;
        definition = input.Definition;
        trigger = input.Trigger.SemanticTrigger;
        target = input.PrimaryTarget;
        Input = input;
        Facts = new RuleFactReader(facts);
        Targets = targets;
        results = new RuleResultWriter(resultStorage, ref resultState);
        this.randomState = MemoryMarshal.CreateSpan(ref randomState, 1);
        HasUnifiedInput = true;
    }

    public ReadOnlyWorld World { get; }
    public RuleCommandWriter Commands { get; }
    public RuleInvocationId Invocation { get; }
    public EntityId Equipment => equipment;
    public EntityId Owner => owner;
    public EquipmentId Definition => definition;
    public TriggerId Trigger => trigger;
    public TargetHandle Target => target;
    public EquipmentHandlerInput Input { get; }
    public RuleFactReader Facts { get; }
    public ReadOnlySpan<EntityId> Targets { get; }
    public bool HasUnifiedInput { get; }
    public RuleTriggerEnvelope TriggerEnvelope => Input.Trigger;
    public RuleResultWriter Results => results;
    public DeterministicRuleRandom Random => HasUnifiedInput
        ? new DeterministicRuleRandom(ref randomState[0])
        : throw new InvalidOperationException("The compatibility context has no random state.");

    public RuleCommandIndex Append(in RuleCommand command) => Commands.Append(in command);
}

public ref struct MedalHandlerContext
{
    private readonly EntityId medal;
    private readonly EntityId owner;
    private readonly MedalId definition;
    private readonly TriggerId trigger;
    private readonly TargetHandle target;
    private RuleResultWriter results;
    private Span<RuleRandomState> randomState;

    public MedalHandlerContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        RuleInvocationId invocation,
        EntityId medal,
        EntityId owner,
        MedalId definition,
        TriggerId trigger,
        TargetHandle target = default)
    {
        World = world;
        Commands = commands;
        Invocation = invocation;
        this.medal = medal;
        this.owner = owner;
        this.definition = definition;
        this.trigger = trigger;
        this.target = target;
        Input = default;
        Facts = new RuleFactReader(default);
        Targets = default;
        results = default;
        randomState = default;
        HasUnifiedInput = false;
    }

    public MedalHandlerContext(
        ReadOnlyWorld world,
        RuleCommandWriter commands,
        in MedalHandlerInput input,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> targets,
        Span<RuleHandlerResult> resultStorage,
        ref RuleResultWriterState resultState,
        ref RuleRandomState randomState)
    {
        World = world;
        Commands = commands;
        Invocation = input.Invocation;
        medal = input.Medal;
        owner = input.Owner;
        definition = input.Definition;
        trigger = input.Trigger.SemanticTrigger;
        target = input.PrimaryTarget;
        Input = input;
        Facts = new RuleFactReader(facts);
        Targets = targets;
        results = new RuleResultWriter(resultStorage, ref resultState);
        this.randomState = MemoryMarshal.CreateSpan(ref randomState, 1);
        HasUnifiedInput = true;
    }

    public ReadOnlyWorld World { get; }
    public RuleCommandWriter Commands { get; }
    public RuleInvocationId Invocation { get; }
    public EntityId Medal => medal;
    public EntityId Owner => owner;
    public MedalId Definition => definition;
    public TriggerId Trigger => trigger;
    public TargetHandle Target => target;
    public MedalHandlerInput Input { get; }
    public RuleFactReader Facts { get; }
    public ReadOnlySpan<EntityId> Targets { get; }
    public bool HasUnifiedInput { get; }
    public RuleTriggerEnvelope TriggerEnvelope => Input.Trigger;
    public RuleResultWriter Results => results;
    public DeterministicRuleRandom Random => HasUnifiedInput
        ? new DeterministicRuleRandom(ref randomState[0])
        : throw new InvalidOperationException("The compatibility context has no random state.");

    public RuleCommandIndex Append(in RuleCommand command) => Commands.Append(in command);
}
