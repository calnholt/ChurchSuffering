#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Content.Enemies;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using BrittleCard = Crusaders30XX.ECS.DataOriented.Gameplay.Cards.Brittle;
using CardDataComponent = Crusaders30XX.ECS.DataOriented.Gameplay.Cards.CardData;
using ColorlessCard = Crusaders30XX.ECS.DataOriented.Gameplay.Cards.Colorless;
using CardPlayer = Crusaders30XX.ECS.DataOriented.Gameplay.Cards.Player;
using PledgeAvailabilityState = Crusaders30XX.ECS.DataOriented.Gameplay.Cards.PledgeAvailabilityState;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Combat;

public sealed class CombatSession
{
    private readonly CommandBuffer commands = new(initialCommandCapacity: 32);
    private readonly EventRuntime events;

    private CombatSession(World world, EntityId battle, CombatRuleRouter router, EventRuntime events, CombatEventHub eventHub)
    {
        World = world;
        Battle = battle;
        Router = router;
        Rules = new QueuedRuleRuntime<CombatRuleState>(router, initialCapacityPerLane: 16);
        this.events = events;
        EventHub = eventHub;
    }

    public World World { get; }
    public EntityId Battle { get; }
    public CombatRuleRouter Router { get; }
    public QueuedRuleRuntime<CombatRuleState> Rules { get; }
    public CombatEventHub EventHub { get; }

    public EntityId Player => World.Get<BattleInfo>(Battle).Player;
    public EntityId Enemy => World.Get<BattleInfo>(Battle).Enemy;
    public EntityId Deck => World.Get<BattleInfo>(Battle).Deck;

    public static CombatSession Create(
        World world,
        CombatEventHub eventHub,
        EnemyId enemyId,
        int playerHealth = 30,
        ulong seed = 1,
        bool finalBattle = false)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(eventHub);
        if (!world.HasEventRuntime)
            throw new InvalidOperationException(
                "Attach the coordinator-owned root event runtime before creating a combat session.");
        ref readonly EnemyDefinitionData definition = ref GeneratedEnemyCatalog.GetDefinition(enemyId);

        var playerBundle = new SpawnBundle(7);
        playerBundle.Add(new HP { Max = playerHealth, Current = playerHealth, UnscarredMax = playerHealth });
        playerBundle.Add(new Courage());
        playerBundle.Add(new Temperance());
        playerBundle.Add(new ActionPoints());
        playerBundle.Add(new Threat());
        playerBundle.Add(new CardPlayer
        {
            Health = playerHealth,
            MaximumHealth = playerHealth,
        });
        playerBundle.Add(new PledgeAvailabilityState { Enabled = 1 });
        EntityId player = world.Create(in playerBundle);

        var enemyBundle = new SpawnBundle(2);
        enemyBundle.Add(new HP
        {
            Max = definition.BaseHealth,
            Current = definition.BaseHealth - definition.StartingHealthBelowMax,
            UnscarredMax = definition.BaseHealth,
        });
        enemyBundle.Add(new Enemy { Definition = enemyId, Phase = 1, PhaseTurn = 1 });
        EntityId enemy = world.Create(in enemyBundle);

        var empty = new SpawnBundle(0);
        EntityId battle = world.Create(in empty);
        DynamicBufferHandle<CombatPassive> playerPassives = world.CreateDynamicBuffer<CombatPassive>(battle, 16);
        DynamicBufferHandle<CombatPassive> enemyPassives = world.CreateDynamicBuffer<CombatPassive>(battle, 16);
        DynamicBufferHandle<CombatTraceEntry> trace = world.CreateDynamicBuffer<CombatTraceEntry>(battle, 64);
        DynamicBufferHandle<EnemyAttackId> arsenal = world.CreateDynamicBuffer<EnemyAttackId>(battle, 16);
        DynamicBufferHandle<EnemyAttackId> intent = world.CreateDynamicBuffer<EnemyAttackId>(battle, 16);
        DynamicBufferHandle<EnemyAttackId> nextIntent = world.CreateDynamicBuffer<EnemyAttackId>(battle, 16);
        DynamicBufferHandle<BlockAssignmentEntry> blocks = world.CreateDynamicBuffer<BlockAssignmentEntry>(battle, 8);

        world.GetDynamicBuffer(arsenal).AddRange(definition.Phases[0].Arsenal);
        world.Add(battle, new BattleInfo { Player = player, Enemy = enemy, Seed = seed });
        world.Add(battle, new Battlefield { Player = player, Enemy = enemy });
        world.Add(battle, new BattleStateInfo
        {
            Turn = 1,
            BattleEpoch = 1,
            Flags = finalBattle ? CombatFlags.FinalBattle : CombatFlags.None,
            PlayerPassives = playerPassives,
            EnemyPassives = enemyPassives,
            Trace = trace,
        });
        world.Add(battle, new PhaseState { Current = CombatPhase.None, Turn = 1 });
        world.Add(battle, new EnemyArsenal { Attacks = arsenal });
        world.Add(battle, new AttackIntent { Attacks = intent, CurrentIndex = -1 });
        world.Add(battle, new NextTurnAttackIntent { Attacks = nextIntent });
        world.Add(battle, new EnemyAttackProgress { Blocks = blocks });
        world.Add(battle, new AmbushState());

        EventRuntime eventRuntime = world.Events;
        var router = new CombatRuleRouter(eventHub);
        var session = new CombatSession(world, battle, router, eventRuntime, eventHub);
        session.EnqueueMandatory(CombatRuleKind.StartBattle);
        return session;
    }

    public void EnqueueMandatory(CombatRuleKind kind, int waitFrames = 0, int value0 = 0, int value1 = 0)
    {
        var state = new CombatRuleState
        {
            Kind = kind,
            Battle = Battle,
            WaitFrames = waitFrames,
            Value0 = value0,
            Value1 = value1,
        };
        Rules.EnqueueMandatory(new RuleTypeId((int)kind), in state);
    }

    public void EnqueueReactive(CombatRuleKind kind, int value0 = 0, int value1 = 0)
    {
        var state = new CombatRuleState { Kind = kind, Battle = Battle, Value0 = value0, Value1 = value1 };
        Rules.EnqueueReactiveTrigger(new RuleTypeId((int)kind), in state);
    }

    /// <summary>Root composition hook for the battle's card state.</summary>
    public void BindCardBoundary(EntityId deck, ICombatCardBoundary boundary)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        if (!World.IsAlive(deck)) throw new ArgumentException("The combat deck must be alive.", nameof(deck));
        World.Get<BattleInfo>(Battle).Deck = deck;
        Router.BindCardBoundary(deck, boundary);
    }

    public RuleProcessingResult Process() => Process(commands);

    public RuleProcessingResult Process(CommandBuffer commandBuffer)
    {
        RuleProcessingResult result = Rules.Process(World, commandBuffer, events);
        if (commandBuffer.Count > 0) commandBuffer.Playback(World);
        if (events.PendingEventCount > 0) events.DrainBarrier();
        return result;
    }

    public bool AssignBlock(EntityId card, int block, RuleCardColor color, bool equipment = false, bool frozen = false, bool sealedCard = false)
    {
        if (block < 0) throw new ArgumentOutOfRangeException(nameof(block));
        ref BattleStateInfo state = ref World.Get<BattleStateInfo>(Battle);
        if ((state.Flags & CombatFlags.AwaitingBlockConfirmation) == 0) return false;
        ref EnemyAttackProgress progress = ref World.Get<EnemyAttackProgress>(Battle);
        if ((sealedCard && progress.Attack == EnemyAttackId.BasiliskGlare) ||
            (World.IsAlive(card) && World.Has<CannotBlockThisAttack>(card))) return false;
        DynamicBuffer<BlockAssignmentEntry> blocks = World.GetDynamicBuffer(progress.Blocks);
        for (var index = 0; index < blocks.Count; index++)
            if (blocks[index].Card == card) return false;
        blocks.Add(new BlockAssignmentEntry(card, block, color, equipment ? (byte)1 : (byte)0, frozen ? (byte)1 : (byte)0, sealedCard ? (byte)1 : (byte)0));
        if (World.Has<AssignedBlockCard>(card))
            commands.Set(card, new AssignedBlockCard { AttackOwner = Battle, AttackIndex = blocks.Count - 1, Block = block, Color = color });
        else
            commands.Add(card, new AssignedBlockCard { AttackOwner = Battle, AttackIndex = blocks.Count - 1, Block = block, Color = color });
        EventHub.BlockAssignmentAdded.Publish(new BlockAssignmentAdded(Battle, card, block, color));
        RecalculateBlocks(World, Battle);
        Router.ProcessBlockAssignment(World, commands, Battle, card);
        if (commands.Count > 0) commands.Playback(World);
        return true;
    }

    public bool RemoveBlock(EntityId card)
    {
        ref EnemyAttackProgress progress = ref World.Get<EnemyAttackProgress>(Battle);
        DynamicBuffer<BlockAssignmentEntry> blocks = World.GetDynamicBuffer(progress.Blocks);
        for (var index = 0; index < blocks.Count; index++)
        {
            if (blocks[index].Card != card) continue;
            blocks.RemoveAt(index);
            if (World.Has<AssignedBlockCard>(card)) commands.Remove<AssignedBlockCard>(card);
            EventHub.BlockAssignmentRemoved.Publish(new BlockAssignmentRemoved(Battle, card));
            RecalculateBlocks(World, Battle);
            Router.ProcessProgressOverride(World, commands, Battle);
            if (commands.Count > 0) commands.Playback(World);
            return true;
        }
        return false;
    }

    public void ConfirmBlocks() => EnqueueMandatory(CombatRuleKind.ConfirmBlocks);

    public void EndActionPhase() => EnqueueMandatory(CombatRuleKind.EndActionPhase);

    public void TickAmbush(int elapsedMilliseconds)
    {
        if (elapsedMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));
        ref AmbushState ambush = ref World.Get<AmbushState>(Battle);
        if (ambush.Active == 0 || ambush.Expired != 0) return;
        ambush.RemainingMilliseconds = Math.Max(0, ambush.RemainingMilliseconds - elapsedMilliseconds);
        if (ambush.RemainingMilliseconds != 0) return;
        ambush.Expired = 1;
        EventHub.AmbushTimerExpired.Publish(new AmbushTimerExpired(Battle));
        events.DrainBarrier();
    }

    public int GetPassiveStacks(EntityId target, EffectId effect) =>
        Router.GetPassiveStacks(World, Battle, target, effect);

    public void GrantPassive(EntityId target, EffectId effect, int stacks)
    {
        if (stacks == 0) return;
        Router.GrantPassive(World, commands, Battle, target, effect, stacks);
        if (commands.Count > 0) commands.Playback(World);
    }

    public void DamageEnemy(int amount, CombatDamageKind kind = CombatDamageKind.Attack)
    {
        Router.ApplyDamage(World, commands, Battle, Player, Enemy, amount, kind, RuleValueFlags.None);
        CombatFlags flags = World.Get<BattleStateInfo>(Battle).Flags;
        if ((flags & CombatFlags.PhaseTransitionPending) != 0)
            EnqueueMandatory(CombatRuleKind.AdvanceEnemyPhase);
        else if ((flags & CombatFlags.EnemyDefeated) != 0)
            EnqueueMandatory(CombatRuleKind.CompleteVictory);
    }

    public ReadOnlySpan<CombatTraceEntry> Trace =>
        World.GetDynamicBuffer(World.Get<BattleStateInfo>(Battle).Trace).AsReadOnlySpan();

    internal static void RecalculateBlocks(World world, EntityId battle)
    {
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battle);
        ReadOnlySpan<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(progress.Blocks).AsReadOnlySpan();
        int total = 0;
        int colors = 0;
        for (var index = 0; index < blocks.Length; index++)
        {
            total += Math.Max(0, blocks[index].Block + (blocks[index].Color == RuleCardColor.Black ? 1 : 0));
            int bit = blocks[index].Color switch
            {
                RuleCardColor.Red => 1,
                RuleCardColor.White => 2,
                RuleCardColor.Black => 4,
                _ => 0,
            };
            colors |= bit;
        }
        progress.AssignedBlock = total;
        progress.AssignedBlockerCount = blocks.Length;
        progress.DistinctBlockerColors = ((colors & 1) != 0 ? 1 : 0) + ((colors & 2) != 0 ? 1 : 0) + ((colors & 4) != 0 ? 1 : 0);
        progress.AssignedColorMask = (byte)colors;
    }
}

public sealed class CombatRuleRouter : IRuleRoutingEndpoint<CombatRuleState>
{
    private readonly CombatEventHub? events;
    private readonly RuleCommandBuffer ruleCommands = new(initialCapacity: 32);
    private readonly RuleHandlerResult[] results = new RuleHandlerResult[32];
    private readonly EnemyAttackId[] plan = new EnemyAttackId[16];
    private readonly RuleFact[] facts = new RuleFact[16];
    private readonly EntityId[] targets = new EntityId[32];
    private RuleRandomState random;
    private EntityId cardDeck;
    private ICombatCardBoundary? cardBoundary;

    public CombatRuleRouter(CombatEventHub? events = null) => this.events = events;

    public void BindCardBoundary(EntityId deck, ICombatCardBoundary boundary)
    {
        cardDeck = deck;
        cardBoundary = boundary ?? throw new ArgumentNullException(nameof(boundary));
    }

    public RuleExecutionStatus Execute(
        RuleTypeId ruleType,
        ref CombatRuleState state,
        ref RuleExecutionContext<CombatRuleState> context)
    {
        if ((int)state.Kind != ruleType.Value)
            throw new InvalidOperationException("Combat rule type and state kind diverged.");
        if (state.WaitFrames > 0)
        {
            state.WaitFrames--;
            return RuleExecutionStatus.Pending;
        }

        switch (state.Kind)
        {
            case CombatRuleKind.StartBattle: StartBattle(ref state, ref context); break;
            case CombatRuleKind.PlanEnemyIntent: PlanEnemy(ref state, ref context); break;
            case CombatRuleKind.BeginEnemyAttack: BeginAttack(ref state, ref context); break;
            case CombatRuleKind.ConfirmBlocks: ConfirmBlocks(ref state, ref context); break;
            case CombatRuleKind.PresentEnemyAttack: PresentAttack(ref state, ref context); break;
            case CombatRuleKind.ResolveEnemyImpact: ResolveImpact(ref state, ref context); break;
            case CombatRuleKind.CompleteEnemyAttack: CompleteAttack(ref state, ref context); break;
            case CombatRuleKind.BeginActionPhase: BeginAction(ref state, ref context); break;
            case CombatRuleKind.EndActionPhase: EndAction(ref state, ref context); break;
            case CombatRuleKind.BeginEnemyTurn: BeginEnemyTurn(ref state, ref context); break;
            case CombatRuleKind.AdvanceEnemyPhase: AdvanceEnemyPhase(ref state, ref context); break;
            case CombatRuleKind.CompleteVictory:
                SetPhase(context.World, state.Battle, CombatPhase.Victory);
                Trace(context.World, state.Battle, CombatRuleKind.CompleteVictory);
                break;
            case CombatRuleKind.CompleteDefeat:
                SetPhase(context.World, state.Battle, CombatPhase.Defeat);
                Trace(context.World, state.Battle, CombatRuleKind.CompleteDefeat);
                break;
            default: throw new ArgumentOutOfRangeException(nameof(state.Kind));
        }
        return RuleExecutionStatus.Completed;
    }

    public int GetPassiveStacks(World world, EntityId battleEntity, EntityId target, EffectId effect)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        return GetPassive(world, target == info.Player ? battle.PlayerPassives : battle.EnemyPassives, effect);
    }

    public void GrantPassive(World world, CommandBuffer commands, EntityId battleEntity, EntityId target, EffectId effect, int stacks)
    {
        var spec = new EffectSpec(effect, stacks, 0, ConditionSpec.Always, RuleValueFlags.BattleOnly);
        ApplyPassive(world, commands, battleEntity, target, in spec);
    }

    private void StartBattle(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        ref BattleStateInfo battleState = ref context.World.Get<BattleStateInfo>(state.Battle);
        battleState.Flags |= CombatFlags.BattleStarted;
        InvokeEnemy(context.World, context.Commands, state.Battle, RuleTriggerIds.BattleStart);
        Trace(context.World, state.Battle, CombatRuleKind.StartBattle);
        Enqueue(context.Rules, CombatRuleKind.PlanEnemyIntent, state.Battle);
    }

    private void PlanEnemy(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref BattleInfo info = ref world.Get<BattleInfo>(state.Battle);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(state.Battle);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        ref AttackIntent intent = ref world.Get<AttackIntent>(state.Battle);
        DynamicBuffer<EnemyAttackId> destination = world.GetDynamicBuffer(intent.Attacks);
        destination.Clear();
        Array.Clear(plan);
        Array.Clear(results);
        ruleCommands.Clear();
        random = RuleRandomState.FromSeed(info.Seed + (ulong)(battle.Turn * 131 + enemy.Phase * 17));
        int factCount = BuildPlanningFacts(world, state.Battle);
        var input = new EnemyHandlerInput(
            NextInvocation(ref info), info.Enemy, enemy.Definition, default,
            Trigger(RuleTriggerIds.DefinitionLifecycle), EnemyHandlerFlags.Planning |
                (((battle.Flags & CombatFlags.FinalBattle) != 0) ? EnemyHandlerFlags.FinalBattle : EnemyHandlerFlags.None),
            RulePhase.EnemyStart, battle.Turn, battle.BattleEpoch, default, enemy.PlanningMemory, TargetHandle.Player);
        var resultState = new RuleResultWriterState();
        var planState = new EnemyPlanWriterState();
        EnemyPlanningMemory memory = enemy.PlanningMemory;
        var handler = new EnemyHandlerContext(
            world.AsReadOnly(), ruleCommands.Writer, in input, facts.AsSpan(0, factCount),
            ReadOnlySpan<EntityId>.Empty, results, plan, ref memory, ref resultState, ref planState, ref random);
        if (!GeneratedEnemyCatalog.Dispatch(enemy.Definition, ref handler))
            throw new InvalidOperationException($"Enemy {enemy.Definition} has no generated planner.");
        destination.AddRange(handler.Plan.WrittenSpan);
        events?.IntentPlanned.Publish(new IntentPlanned(state.Battle, destination.Count));
        enemy.PlanningMemory = memory;
        intent.CurrentIndex = -1;
        intent.Current = default;
        Trace(world, state.Battle, CombatRuleKind.PlanEnemyIntent, default, destination.Count);
        Enqueue(context.Rules, CombatRuleKind.BeginEnemyAttack, state.Battle);
    }

    private void BeginAttack(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref AttackIntent intent = ref world.Get<AttackIntent>(state.Battle);
        DynamicBuffer<EnemyAttackId> attacks = world.GetDynamicBuffer(intent.Attacks);
        int next = intent.CurrentIndex + 1;
        if (next >= attacks.Count)
        {
            Enqueue(context.Rules, CombatRuleKind.BeginActionPhase, state.Battle);
            return;
        }
        intent.CurrentIndex = next;
        intent.Current = attacks[next];
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(state.Battle);
        DynamicBufferHandle<BlockAssignmentEntry> blockHandle = progress.Blocks;
        DynamicBuffer<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(progress.Blocks);
        blocks.Clear();
        ref readonly EnemyAttackDefinitionData definition = ref GeneratedEnemyAttackCatalog.GetDefinition(intent.Current);
        progress = new EnemyAttackProgress
        {
            Attack = intent.Current,
            BaseDamage = definition.MinimumDamage,
            EffectiveDamage = definition.MinimumDamage,
            AdditionalDamage = definition.AdditionalDamage,
            RequiredAmount = definition.MinimumBlockThreshold,
            Blocks = blockHandle,
        };
        InvokeAttack(world, context.Commands, state.Battle, RuleTriggerIds.EnemyAttackChannelApplied);
        InvokeAttack(world, context.Commands, state.Battle, RuleTriggerIds.EnemyAttackReveal);
        NormalizeImpossibleRequirement(world, state.Battle);
        progress.EffectiveDamage += GetPassive(world, world.Get<BattleStateInfo>(state.Battle).EnemyPassives, RuleEffectIds.Power);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(state.Battle);
        battle.Flags |= CombatFlags.AwaitingBlockConfirmation;
        events?.TriggerEnemyAttackDisplay.Publish(new TriggerEnemyAttackDisplayEvent(state.Battle, intent.Current));
        events?.ShowConfirmButton.Publish(new ShowConfirmButtonEvent(state.Battle, 1));
        ref AmbushState ambush = ref world.Get<AmbushState>(state.Battle);
        bool isAmbush = definition.AmbushPercentage > 0 || GetPassive(world, battle.PlayerPassives, RuleEffectIds.Fear) > 0;
        ambush.Active = isAmbush ? (byte)1 : (byte)0;
        ambush.Expired = 0;
        ambush.BaseMilliseconds = 20_000;
        ambush.RemainingMilliseconds = isAmbush
            ? Math.Max(1_000, ambush.BaseMilliseconds - GetPassive(world, battle.PlayerPassives, RuleEffectIds.Slow) * 1_000)
            : 0;
        SetPhase(world, state.Battle, CombatPhase.Block);
        Trace(world, state.Battle, CombatRuleKind.BeginEnemyAttack, intent.Current, next);
    }

    private void ConfirmBlocks(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(state.Battle);
        if ((battle.Flags & CombatFlags.AwaitingBlockConfirmation) == 0) return;
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(state.Battle);
        CombatSession.RecalculateBlocks(world, state.Battle);
        ProcessProgressOverride(world, context.Commands, state.Battle);
        bool requirementMet = RequirementMet(in progress);
        if (state.Value0 == 0 && !requirementMet) return;
        if (progress.Requirement == RequirementKind.ExactBlockers && requirementMet)
            ApplyExactBlockerPrevention(world, context.Commands, ref progress);
        progress.Confirmed = 1;
        battle.Flags &= ~CombatFlags.AwaitingBlockConfirmation;
        events?.ShowConfirmButton.Publish(new ShowConfirmButtonEvent(state.Battle, 0));
        InvokeAttack(world, context.Commands, state.Battle, RuleTriggerIds.EnemyAttackBlocksConfirmed);
        SetPhase(world, state.Battle, CombatPhase.ResolvingEnemyAttacks);
        Trace(world, state.Battle, CombatRuleKind.ConfirmBlocks, progress.Attack, progress.AssignedBlock);
        Enqueue(context.Rules, CombatRuleKind.PresentEnemyAttack, state.Battle, waitFrames: 1);
    }

    private static void PresentAttack(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        Trace(context.World, state.Battle, CombatRuleKind.PresentEnemyAttack, context.World.Get<EnemyAttackProgress>(state.Battle).Attack);
        Enqueue(context.Rules, CombatRuleKind.ResolveEnemyImpact, state.Battle);
    }

    private void ResolveImpact(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref BattleInfo info = ref world.Get<BattleInfo>(state.Battle);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(state.Battle);
        ref readonly EnemyAttackDefinitionData definition = ref GeneratedEnemyAttackCatalog.GetDefinition(progress.Attack);
        ResolveBlockResourcesAndPassives(world, context.Commands, state.Battle);
        int raw = progress.FullyPreventedBySpecial != 0
            ? 0
            : Math.Max(0, progress.EffectiveDamage + progress.AdditionalDamage - progress.AssignedBlock);
        events?.ResolvingEnemyDamage.Publish(new ResolvingEnemyDamageEvent(state.Battle, progress.Attack, raw));
        events?.EnemyAttackImpactNow.Publish(new EnemyAttackImpactNow(state.Battle, progress.Attack));
        bool effectHit = ShouldTriggerHit(definition.Condition, progress.AssignedBlockerCount, progress.DistinctBlockerColors, raw);
        int dealt = ApplyDamage(world, context.Commands, state.Battle, info.Enemy, info.Player, raw, CombatDamageKind.Attack,
            (definition.Flags & EnemyAttackFlags.IgnoresAegis) != 0 ? RuleValueFlags.IgnoreAegis : RuleValueFlags.None);
        progress.DamageDealt = dealt;
        events?.EnemyAttackHit.Publish(new OnEnemyAttackHitEvent(state.Battle, progress.Attack, dealt));
        if (effectHit) InvokeAttack(world, context.Commands, state.Battle, RuleTriggerIds.EnemyAttackHit, attackBlocked: false);
        if (definition.Condition == EnemyAttackCondition.DamageThreshold &&
            progress.AssignedBlock < progress.RequiredAmount && dealt > 0)
            InvokeAttack(world, context.Commands, state.Battle, RuleTriggerIds.EnemyAttackDamageThresholdMet);
        ResolveMarkedDiscard(context.Commands, state.Battle, in definition, in progress, dealt);
        Trace(world, state.Battle, CombatRuleKind.ResolveEnemyImpact, progress.Attack, dealt, progress.AssignedBlock);
        Enqueue(context.Rules, CombatRuleKind.CompleteEnemyAttack, state.Battle);
    }

    private void CompleteAttack(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(state.Battle);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(state.Battle);
        battle.AttacksResolved++;
        events?.AttackResolved.Publish(new AttackResolved(state.Battle, progress.Attack, progress.DamageDealt));
        Trace(world, state.Battle, CombatRuleKind.CompleteEnemyAttack, progress.Attack, progress.DamageDealt);
        if ((battle.Flags & CombatFlags.PlayerDefeated) != 0)
            Enqueue(context.Rules, CombatRuleKind.CompleteDefeat, state.Battle);
        else
            Enqueue(context.Rules, CombatRuleKind.BeginEnemyAttack, state.Battle);
    }

    private static void BeginAction(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        SetPhase(context.World, state.Battle, CombatPhase.Action);
        ref BattleInfo info = ref context.World.Get<BattleInfo>(state.Battle);
        context.World.Get<ActionPoints>(info.Player).Current = 1;
        if (context.World.TryGet(info.Player, out CardPlayer cardPlayer))
        {
            cardPlayer.ActionPoints = 1;
            context.World.Set(info.Player, in cardPlayer);
        }
        if (context.World.TryGet(info.Player, out PledgeAvailabilityState pledge))
        {
            pledge.Enabled = 1;
            pledge.PledgedThisActionPhase = 0;
            context.World.Set(info.Player, in pledge);
        }
        Trace(context.World, state.Battle, CombatRuleKind.BeginActionPhase);
    }

    private void EndAction(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        InvokeEnemy(context.World, context.Commands, state.Battle, RuleTriggerIds.ActionPhaseEnd);
        SetPhase(context.World, state.Battle, CombatPhase.EnemyTurn);
        Trace(context.World, state.Battle, CombatRuleKind.EndActionPhase);
        Enqueue(context.Rules, CombatRuleKind.BeginEnemyTurn, state.Battle);
    }

    private void BeginEnemyTurn(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(state.Battle);
        ref BattleInfo info = ref world.Get<BattleInfo>(state.Battle);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        battle.Turn++;
        battle.DamageTakenThisTurn = 0;
        enemy.PhaseTurn++;
        InvokeEnemy(world, context.Commands, state.Battle, RuleTriggerIds.EnemyTurnStart);
        Trace(world, state.Battle, CombatRuleKind.BeginEnemyTurn, default, battle.Turn);
        Enqueue(context.Rules, CombatRuleKind.PlanEnemyIntent, state.Battle);
    }

    private void AdvanceEnemyPhase(ref CombatRuleState state, ref RuleExecutionContext<CombatRuleState> context)
    {
        World world = context.World;
        ref BattleInfo info = ref world.Get<BattleInfo>(state.Battle);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(state.Battle);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        ref readonly EnemyDefinitionData definition = ref GeneratedEnemyCatalog.GetDefinition(enemy.Definition);
        if (enemy.Phase >= definition.Phases.Length)
        {
            battle.Flags |= CombatFlags.EnemyDefeated;
            Enqueue(context.Rules, CombatRuleKind.CompleteVictory, state.Battle);
            return;
        }
        enemy.Phase++;
        enemy.PhaseTurn = 1;
        enemy.PlanningMemory.Phase = enemy.Phase;
        enemy.PlanningMemory.PhaseTurn = 1;
        ref HP hp = ref world.Get<HP>(info.Enemy);
        hp.Current = hp.Max;
        DynamicBuffer<EnemyAttackId> arsenal = world.GetDynamicBuffer(world.Get<EnemyArsenal>(state.Battle).Attacks);
        arsenal.Clear();
        arsenal.AddRange(definition.Phases[enemy.Phase - 1].Arsenal);
        world.GetDynamicBuffer(battle.EnemyPassives).Clear();
        battle.Flags &= ~CombatFlags.PhaseTransitionPending;
        events?.EnemyPhaseReset.Publish(new EnemyPhaseResetEvent(state.Battle, enemy.Phase));
        SetPhase(world, state.Battle, CombatPhase.PhaseTransition);
        Trace(world, state.Battle, CombatRuleKind.AdvanceEnemyPhase, default, enemy.Phase);
        Enqueue(context.Rules, CombatRuleKind.PlanEnemyIntent, state.Battle, waitFrames: 1);
    }

    public int ApplyDamage(
        World world,
        CommandBuffer commands,
        EntityId battleEntity,
        EntityId source,
        EntityId target,
        int amount,
        CombatDamageKind kind,
        RuleValueFlags flags)
    {
        if (amount <= 0 || !world.TryGet(target, out HP hp)) return 0;
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        bool playerTarget = target == info.Player;
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        DynamicBufferHandle<CombatPassive> handle = playerTarget ? battle.PlayerPassives : battle.EnemyPassives;
        int adjusted = amount;
        if (kind == CombatDamageKind.Attack)
        {
            adjusted += GetPassive(world, handle, RuleEffectIds.Wounded);
            adjusted = Math.Max(0, adjusted - GetPassive(world, handle, RuleEffectIds.Armor));
            int guard = GetPassive(world, handle, RuleEffectIds.Guard);
            adjusted = Math.Max(0, adjusted - guard);
            if (guard > 0) SetPassive(world, handle, RuleEffectIds.Guard, 0);
        }
        if ((flags & (RuleValueFlags.Unpreventable | RuleValueFlags.IgnoreAegis)) == 0)
        {
            int aegis = GetPassive(world, handle, RuleEffectIds.Aegis);
            int prevented = Math.Min(adjusted, aegis);
            adjusted -= prevented;
            if (prevented > 0) SetPassive(world, handle, RuleEffectIds.Aegis, aegis - prevented);
        }
        adjusted = Math.Min(adjusted, hp.Current);
        hp.Current -= adjusted;
        world.Set(target, in hp);
        if (playerTarget) battle.DamageTakenThisTurn += adjusted;
        events?.ModifyHp.Publish(new ModifyHpEvent(source, target, -adjusted, kind));
        if (!playerTarget) events?.EnemyDamageApplied.Publish(new EnemyDamageAppliedEvent(battleEntity, adjusted));
        if (hp.Current == 0)
        {
            if (playerTarget)
            {
                battle.Flags |= CombatFlags.PlayerDefeated;
                events?.PlayerDied.Publish(new PlayerDied(battleEntity, target));
            }
            else
            {
                ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
                ref readonly EnemyDefinitionData definition = ref GeneratedEnemyCatalog.GetDefinition(enemy.Definition);
                if (enemy.Phase < definition.Phases.Length)
                {
                    battle.Flags |= CombatFlags.PhaseTransitionPending;
                    events?.EnemyPhaseLethal.Publish(new EnemyPhaseLethalEvent(battleEntity, enemy.Phase));
                    EnqueuePendingPhase(battleEntity);
                }
                else
                {
                    battle.Flags |= CombatFlags.EnemyDefeated;
                    events?.EnemyKilled.Publish(new EnemyKilledEvent(battleEntity, enemy.Definition));
                }
            }
        }
        return adjusted;

        void EnqueuePendingPhase(EntityId battleId)
        {
            // The owning session observes this flag and schedules the phase rule; command recording
            // remains reserved for structural defeat/presentation tags at scheduler playback.
            Trace(world, battleId, CombatRuleKind.AdvanceEnemyPhase);
        }
    }

    private void InvokeEnemy(World world, CommandBuffer commands, EntityId battleEntity, TriggerId stage)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        ruleCommands.Clear();
        Array.Clear(results);
        random = RuleRandomState.FromSeed(info.Seed + (ulong)(battle.Turn * 211 + stage.Value));
        int factCount = BuildRuntimeFacts(world, battleEntity);
        var input = new EnemyHandlerInput(
            NextInvocation(ref info), info.Enemy, enemy.Definition, default, Trigger(stage),
            ((battle.Flags & CombatFlags.FinalBattle) != 0) ? EnemyHandlerFlags.FinalBattle : EnemyHandlerFlags.None,
            ToRulePhase(world.Get<PhaseState>(battleEntity).Current), battle.Turn, battle.BattleEpoch,
            default, enemy.PlanningMemory, TargetHandle.Player);
        var resultState = new RuleResultWriterState();
        var planState = new EnemyPlanWriterState();
        EnemyPlanningMemory memory = enemy.PlanningMemory;
        var handler = new EnemyHandlerContext(
            world.AsReadOnly(), ruleCommands.Writer, in input, facts.AsSpan(0, factCount),
            ReadOnlySpan<EntityId>.Empty, results, Span<EnemyAttackId>.Empty,
            ref memory, ref resultState, ref planState, ref random);
        GeneratedEnemyCatalog.Dispatch(enemy.Definition, ref handler);
        ExecuteCommands(world, commands, battleEntity, ruleCommands.AsReadOnlySpan());
        enemy.PlanningMemory = memory;
    }

    private void InvokeAttack(
        World world,
        CommandBuffer commands,
        EntityId battleEntity,
        TriggerId stage,
        bool attackBlocked = false,
        EntityId primaryTarget = default)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
        ruleCommands.Clear();
        Array.Clear(results);
        random = RuleRandomState.FromSeed(info.Seed + (ulong)(battle.Turn * 307 + progress.Attack.GetHashCode() * 13 + stage.Value));
        int factCount = BuildRuntimeFacts(world, battleEntity);
        int targetCount = CopyTargets(world, progress.Blocks, progress.Attack, stage);
        EnemyHandlerFlags handlerFlags = attackBlocked ? EnemyHandlerFlags.AttackBlocked : EnemyHandlerFlags.None;
        if ((battle.Flags & CombatFlags.FinalBattle) != 0) handlerFlags |= EnemyHandlerFlags.FinalBattle;
        var input = new EnemyHandlerInput(
            NextInvocation(ref info), info.Enemy, enemy.Definition, progress.Attack, Trigger(stage), handlerFlags,
            ToRulePhase(world.Get<PhaseState>(battleEntity).Current), battle.Turn, battle.BattleEpoch,
            new EnemyCombatSnapshot(progress.BaseDamage, progress.EffectiveDamage, progress.DamageDealt,
                progress.RequiredAmount, progress.AssignedBlockerCount, progress.DistinctBlockerColors,
                progress.AssignedBlock, GetPassive(world, battle.EnemyPassives, RuleEffectIds.Channel), 0),
            enemy.PlanningMemory, !primaryTarget.IsNull ? TargetHandle.ForEntity(primaryTarget) : targetCount > 0 ? TargetHandle.ForEntity(targets[0]) : TargetHandle.Player);
        var resultState = new RuleResultWriterState();
        var planState = new EnemyPlanWriterState();
        EnemyPlanningMemory memory = enemy.PlanningMemory;
        var handler = new EnemyHandlerContext(
            world.AsReadOnly(), ruleCommands.Writer, in input, facts.AsSpan(0, factCount), targets.AsSpan(0, targetCount),
            results, Span<EnemyAttackId>.Empty, ref memory, ref resultState, ref planState, ref random);
        GeneratedEnemyAttackCatalog.Dispatch(progress.Attack, ref handler);
        ApplyResults(ref progress, handler.Results.WrittenSpan);
        ExecuteCommands(world, commands, battleEntity, ruleCommands.AsReadOnlySpan());
    }

    public void ProcessBlockAssignment(World world, CommandBuffer commands, EntityId battleEntity, EntityId card)
    {
        InvokeAttack(world, commands, battleEntity, RuleTriggerIds.EnemyAttackBlockAssigned, primaryTarget: card);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
        bool cardOnlyRestriction = progress.Attack is EnemyAttackId.FallenShepherdPhase1 or
            EnemyAttackId.FallenShepherdBreakFaith;
        if (!cardOnlyRestriction || !IsEquipmentBlocker(world, in progress, card))
            InvokeAttack(world, commands, battleEntity, RuleTriggerIds.EnemyAttackBlockProcessed, primaryTarget: card);
        ProcessProgressOverride(world, commands, battleEntity);
    }

    public void ProcessProgressOverride(World world, CommandBuffer commands, EntityId battleEntity)
    {
        EnemyAttackId attack = world.Get<EnemyAttackProgress>(battleEntity).Attack;
        ref readonly EnemyAttackDefinitionData definition = ref GeneratedEnemyAttackCatalog.GetDefinition(attack);
        if ((definition.Flags & EnemyAttackFlags.ProgressHook) != 0)
            InvokeAttack(world, commands, battleEntity, RuleTriggerIds.EnemyAttackProgressOverride);
    }

    private void ExecuteCommands(World world, CommandBuffer commands, EntityId battleEntity, ReadOnlySpan<RuleCommand> values)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        for (var index = 0; index < values.Length; index++)
        {
            ref readonly RuleCommand command = ref values[index];
            RuleCommandPayload payload = command.Payload;
            switch (command.Kind)
            {
                case RuleCommandKind.Damage:
                {
                    EntityId target = ResolveTarget(payload.ResourceDelta.Target, info);
                    EntityId source = ResolveTarget(payload.ResourceDelta.Source, info);
                    ApplyDamage(world, commands, battleEntity, source, target, payload.ResourceDelta.Amount,
                        CombatDamageKind.Effect, payload.ResourceDelta.Flags);
                    break;
                }
                case RuleCommandKind.Heal:
                {
                    EntityId target = ResolveTarget(payload.ResourceDelta.Target, info);
                    if (world.TryGet(target, out HP hp))
                    {
                        hp.Current = Math.Min(hp.Max, hp.Current + Math.Max(0, payload.ResourceDelta.Amount));
                        world.Set(target, in hp);
                    }
                    break;
                }
                case RuleCommandKind.ModifyStat:
                    ModifyStat(world, battleEntity, payload.ResourceDelta);
                    break;
                case RuleCommandKind.ApplyEffect:
                {
                    EntityId target = ResolveTarget(payload.Effect.Target, info);
                    EffectSpec effect = payload.Effect.Effect;
                    if (!TryExecuteCardEffect(world, commands, battleEntity, target, in effect, remove: false))
                        ApplyPassive(world, commands, battleEntity, target, in effect);
                    break;
                }
                case RuleCommandKind.RemoveEffect:
                {
                    EntityId target = ResolveTarget(payload.RemoveEffect.Target, info);
                    var effect = new EffectSpec(payload.RemoveEffect.Effect, payload.RemoveEffect.StackCount, 0, ConditionSpec.Always, RuleValueFlags.None);
                    if (!TryExecuteCardEffect(world, commands, battleEntity, target, in effect, remove: true))
                        RemovePassive(world, commands, battleEntity, target, payload.RemoveEffect.Effect, payload.RemoveEffect.StackCount);
                    break;
                }
                case RuleCommandKind.SetRequirement:
                {
                    ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
                    if (command.Payload.Requirement.Kind is RequirementKind.OnlyCardColor or RequirementKind.ExcludeCardColor)
                    {
                        progress.ColorRequirement = command.Payload.Requirement.Kind;
                        progress.RequiredColor = command.Payload.Requirement.Color;
                    }
                    else
                    {
                        progress.Requirement = command.Payload.Requirement.Kind;
                        progress.RequiredAmount = command.Payload.Requirement.Amount;
                    }
                    break;
                }
                case RuleCommandKind.SetResolvedValue:
                {
                    ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
                    if (command.Payload.ResolvedValue.Kind == ResolvedValueKind.Damage)
                        progress.EffectiveDamage = command.Payload.ResolvedValue.Value;
                    else if (command.Payload.ResolvedValue.Kind == ResolvedValueKind.AdditionalDamage)
                        progress.AdditionalDamage = command.Payload.ResolvedValue.Value;
                    else if (command.Payload.ResolvedValue.Kind == ResolvedValueKind.EffectiveBlock)
                        progress.AssignedBlock = command.Payload.ResolvedValue.Value;
                    break;
                }
                case RuleCommandKind.MutateCard:
                    if (world.IsAlive(command.Payload.CardMutation.Card) && world.Has<ModifiedBlock>(command.Payload.CardMutation.Card))
                    {
                        ref ModifiedBlock block = ref world.Get<ModifiedBlock>(command.Payload.CardMutation.Card);
                        block.QuestDelta += command.Payload.CardMutation.Amount;
                    }
                    break;
                case RuleCommandKind.RandomCardZone:
                    ExecuteCardZoneCommand(commands, battleEntity, in payload.RandomCardZone);
                    break;
                case RuleCommandKind.Present:
                case RuleCommandKind.MoveCard:
                case RuleCommandKind.SpawnCard:
                case RuleCommandKind.RemovePledge:
                case RuleCommandKind.Schedule:
                    break;
                case RuleCommandKind.Custom:
                    throw new InvalidOperationException("Converted enemy content cannot emit custom combat commands.");
            }
        }
    }

    private static void ApplyResults(ref EnemyAttackProgress progress, ReadOnlySpan<RuleHandlerResult> values)
    {
        for (var index = 0; index < values.Length; index++)
        {
            ref readonly RuleHandlerResult result = ref values[index];
            if (result.Kind == RuleHandlerResultKind.StatModifier && result.Stat == RuleStatIds.AttackAdditionalDamage)
                progress.AdditionalDamage += result.Value;
            else if (result.Kind == RuleHandlerResultKind.Value && result.Result == RuleFactIds.AssignedBlockTotal)
                progress.RequiredAmount = result.Value;
        }
    }

    private bool TryExecuteCardEffect(
        World world,
        CommandBuffer commands,
        EntityId battleEntity,
        EntityId target,
        in EffectSpec effect,
        bool remove)
    {
        if (cardBoundary is null || cardDeck.IsNull || !world.IsAlive(target) ||
            !world.Has<CardDataComponent>(target)) return false;
        var command = new CombatCardCommand(
            remove ? CombatCardCommandKind.RemoveEffect : CombatCardCommandKind.ApplyEffect,
            battleEntity,
            cardDeck,
            target,
            effect.Id,
            effect.Magnitude);
        cardBoundary.Execute(in command, commands);
        return true;
    }

    private void ExecuteCardZoneCommand(
        CommandBuffer commands,
        EntityId battleEntity,
        in RandomCardZoneRuleCommand value)
    {
        if (cardBoundary is null || cardDeck.IsNull) return;
        CombatCardCommandKind kind = value.Operation switch
        {
            RandomCardZoneOperation.MoveTop when value.SourceZone == CardZone.DrawPile &&
                value.DestinationZone == CardZone.Hand => CombatCardCommandKind.Draw,
            RandomCardZoneOperation.Mill => CombatCardCommandKind.Mill,
            _ => CombatCardCommandKind.None,
        };
        if (kind == CombatCardCommandKind.None) return;
        var command = new CombatCardCommand(kind, battleEntity, cardDeck, EntityId.Null, EffectId.Null, value.Count);
        cardBoundary.Execute(in command, commands);
    }

    private void NormalizeImpossibleRequirement(World world, EntityId battleEntity)
    {
        if (cardBoundary is null || cardDeck.IsNull) return;
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
        if (progress.Requirement is not (RequirementKind.MinimumBlockers or RequirementKind.ExactBlockers)) return;
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        CombatCardFacts cardFacts = cardBoundary.ReadFacts(cardDeck, info.Player);
        int eligible = cardFacts.CountEligible(progress.ColorRequirement, progress.RequiredColor);
        if (eligible >= progress.RequiredAmount) return;
        progress.Requirement = RequirementKind.None;
        progress.RequiredAmount = 0;
    }

    private void ResolveMarkedDiscard(
        CommandBuffer commands,
        EntityId battleEntity,
        in EnemyAttackDefinitionData definition,
        in EnemyAttackProgress progress,
        int damageDealt)
    {
        if (cardBoundary is null || cardDeck.IsNull ||
            progress.Attack is not (EnemyAttackId.HaveNoMercy or EnemyAttackId.FallenShepherdPhase3)) return;
        bool discard = progress.Attack == EnemyAttackId.HaveNoMercy
            ? progress.AssignedBlockerCount < 1
            : definition.Condition == EnemyAttackCondition.DamageThreshold &&
              progress.AssignedBlock < progress.RequiredAmount && damageDealt > 0;
        var command = new CombatCardCommand(
            CombatCardCommandKind.ResolveMarkedDiscard,
            battleEntity,
            cardDeck,
            EntityId.Null,
            RuleEffectIds.MarkedForSpecificDiscard,
            1,
            discard ? (byte)1 : (byte)0);
        cardBoundary.Execute(in command, commands);
    }

    private static bool RequirementMet(in EnemyAttackProgress progress)
    {
        bool blockers = progress.Requirement switch
        {
            RequirementKind.None => true,
            RequirementKind.MinimumBlockers => progress.AssignedBlockerCount >= progress.RequiredAmount,
            RequirementKind.ExactBlockers => progress.AssignedBlockerCount == progress.RequiredAmount,
            _ => true,
        };
        int colorBit = progress.RequiredColor switch
        {
            RuleCardColor.Red => 1,
            RuleCardColor.White => 2,
            RuleCardColor.Black => 4,
            _ => 0,
        };
        bool colors = progress.ColorRequirement switch
        {
            RequirementKind.OnlyCardColor => (progress.AssignedColorMask & ~colorBit) == 0,
            RequirementKind.ExcludeCardColor => (progress.AssignedColorMask & colorBit) == 0,
            _ => true,
        };
        return blockers && colors;
    }

    private static bool ShouldTriggerHit(EnemyAttackCondition condition, int blockers, int colors, int damage) => condition switch
    {
        EnemyAttackCondition.OnHit => damage > 0,
        EnemyAttackCondition.IfNotBlockedByAtLeastOneCard => blockers < 1,
        EnemyAttackCondition.IfNotBlockedByAtLeastTwoCards => blockers < 2,
        EnemyAttackCondition.IfNotBlockedByAtLeastTwoColors => colors < 2,
        _ => damage > 0,
    };

    private static int GetPassive(World world, DynamicBufferHandle<CombatPassive> handle, EffectId effect)
    {
        ReadOnlySpan<CombatPassive> values = world.GetDynamicBuffer(handle).AsReadOnlySpan();
        for (var index = 0; index < values.Length; index++) if (values[index].Effect == effect) return values[index].Stacks;
        return 0;
    }

    private static void SetPassive(World world, DynamicBufferHandle<CombatPassive> handle, EffectId effect, int stacks)
    {
        DynamicBuffer<CombatPassive> values = world.GetDynamicBuffer(handle);
        for (var index = 0; index < values.Count; index++)
        {
            if (values[index].Effect != effect) continue;
            if (stacks <= 0) values.RemoveAt(index);
            else values[index] = values[index] with { Stacks = stacks };
            return;
        }
        if (stacks > 0) values.Add(new CombatPassive(effect, stacks, 0, RuleValueFlags.None));
    }

    private static void ApplyPassive(World world, CommandBuffer commands, EntityId battleEntity, EntityId target, in EffectSpec effect)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        if (target != info.Player && target != info.Enemy)
        {
            if (!world.IsAlive(target) || effect.Magnitude <= 0) return;
            if (effect.Id == RuleEffectIds.CannotBlockCurrentAttack && !world.Has<CannotBlockThisAttack>(target))
                commands.Add(target, new CannotBlockThisAttack { Attack = world.Get<EnemyAttackProgress>(battleEntity).Attack });
            else if (effect.Id == RuleEffectIds.Colorless && !world.Has<ColorlessCard>(target))
                commands.AddTag<ColorlessCard>(target);
            else if (effect.Id == RuleEffectIds.Brittle && !world.Has<BrittleCard>(target))
                commands.AddTag<BrittleCard>(target);
            return;
        }
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        DynamicBufferHandle<CombatPassive> handle = target == info.Player ? battle.PlayerPassives : battle.EnemyPassives;
        SetPassive(world, handle, effect.Id, GetPassive(world, handle, effect.Id) + effect.Magnitude);
        if (effect.Id == RuleEffectIds.Scar && target == info.Player)
        {
            ref HP hp = ref world.Get<HP>(target);
            hp.Max = Math.Max(1, hp.Max - Math.Max(0, effect.Magnitude));
            hp.Current = Math.Min(hp.Current, hp.Max);
        }
    }

    private static bool IsEquipmentBlocker(World world, in EnemyAttackProgress progress, EntityId card)
    {
        ReadOnlySpan<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(progress.Blocks).AsReadOnlySpan();
        for (var index = 0; index < blocks.Length; index++)
            if (blocks[index].Card == card) return blocks[index].IsEquipment != 0;
        return false;
    }

    private static void ApplyExactBlockerPrevention(
        World world,
        CommandBuffer commands,
        ref EnemyAttackProgress progress)
    {
        progress.FullyPreventedBySpecial = 1;
        ReadOnlySpan<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(progress.Blocks).AsReadOnlySpan();
        for (var index = 0; index < blocks.Length; index++)
        {
            ref readonly BlockAssignmentEntry blocker = ref blocks[index];
            if (blocker.IsEquipment != 0 || !world.IsAlive(blocker.Card) || world.Has<ExhaustOnBlock>(blocker.Card))
                continue;
            commands.AddTag<ExhaustOnBlock>(blocker.Card);
        }
    }

    private static void RemovePassive(World world, CommandBuffer commands, EntityId battleEntity, EntityId target, EffectId effect, int count)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        if (target != info.Player && target != info.Enemy)
        {
            if (effect == RuleEffectIds.CannotBlockCurrentAttack && world.IsAlive(target) && world.Has<CannotBlockThisAttack>(target))
                commands.Remove<CannotBlockThisAttack>(target);
            return;
        }
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        DynamicBufferHandle<CombatPassive> handle = target == info.Player ? battle.PlayerPassives : battle.EnemyPassives;
        int current = GetPassive(world, handle, effect);
        SetPassive(world, handle, effect, count < 0 ? 0 : Math.Max(0, current - count));
    }

    private static void ModifyStat(World world, EntityId battleEntity, ResourceDeltaRuleCommand command)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        EntityId target = ResolveTarget(command.Target, info);
        if (command.Stat == RuleStatIds.Courage && world.Has<Courage>(target)) world.Get<Courage>(target).Amount += command.Amount;
        else if (command.Stat == RuleStatIds.Temperance && world.Has<Temperance>(target)) world.Get<Temperance>(target).Amount += command.Amount;
        else if (command.Stat == RuleStatIds.ActionPoints && world.Has<ActionPoints>(target)) world.Get<ActionPoints>(target).Current += command.Amount;
        else if (command.Stat == RuleStatIds.Threat && world.Has<Threat>(target)) world.Get<Threat>(target).Amount += command.Amount;
    }

    private int BuildPlanningFacts(World world, EntityId battleEntity)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        facts[0] = new RuleFact(RuleFactIds.Phase, enemy.Phase);
        facts[1] = new RuleFact(RuleFactIds.Turn, battle.Turn);
        facts[2] = new RuleFact(RuleFactIds.FrozenInHand,
            cardBoundary?.ReadFacts(cardDeck, info.Player).FrozenInHand ?? 0);
        facts[3] = new RuleFact(RuleFactIds.CourageLostThisBattle, battle.CourageLost);
        return 4;
    }

    private int BuildRuntimeFacts(World world, EntityId battleEntity)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        ref Enemy enemy = ref world.Get<Enemy>(info.Enemy);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
        facts[0] = new RuleFact(RuleFactIds.Phase, enemy.Phase);
        facts[1] = new RuleFact(RuleFactIds.Turn, battle.Turn);
        facts[2] = new RuleFact(RuleFactIds.AssignedBlockTotal, progress.AssignedBlock);
        facts[3] = new RuleFact(RuleFactIds.AssignedBlockerCount, progress.AssignedBlockerCount);
        facts[4] = new RuleFact(RuleFactIds.AssignedBlockColorCount, progress.DistinctBlockerColors);
        facts[5] = new RuleFact(RuleFactIds.IsFinalBattle, (battle.Flags & CombatFlags.FinalBattle) != 0 ? 1 : 0);
        facts[6] = new RuleFact(RuleFactIds.CourageLostThisBattle, battle.CourageLost);
        facts[7] = new RuleFact(RuleFactIds.DamageTakenThisTurn, battle.DamageTakenThisTurn);
        facts[8] = new RuleFact(RuleFactIds.ResultValue, world.Get<Courage>(info.Player).Amount);
        facts[9] = new RuleFact(RuleFactIds.PassiveStacks, GetPassive(world, battle.PlayerPassives, RuleEffectIds.Enflamed));
        facts[10] = new RuleFact(RuleFactIds.CandidateCount, CountProgressCandidates(world, progress));
        return 11;
    }

    private int CopyTargets(
        World world,
        DynamicBufferHandle<BlockAssignmentEntry> handle,
        EnemyAttackId attack,
        TriggerId stage)
    {
        if (cardBoundary is not null && !cardDeck.IsNull)
        {
            if (attack == EnemyAttackId.StrangeForce &&
                (stage == RuleTriggerIds.EnemyAttackReveal || stage == RuleTriggerIds.EnemyAttackHit))
            {
                targets[0] = cardDeck;
                return 1;
            }
            if (stage == RuleTriggerIds.EnemyAttackDamageThresholdMet &&
                attack is EnemyAttackId.Entomb or EnemyAttackId.FrozenClaw)
                return cardBoundary.CopyCandidates(cardDeck, CombatCardCandidateKind.TopOfDrawPile, targets);
            if (stage == RuleTriggerIds.EnemyAttackReveal &&
                attack is EnemyAttackId.HaveNoMercy or EnemyAttackId.FallenShepherdPhase3)
                return cardBoundary.CopyCandidates(cardDeck, CombatCardCandidateKind.Hand, targets);
        }
        ReadOnlySpan<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(handle).AsReadOnlySpan();
        int count = Math.Min(blocks.Length, targets.Length);
        for (var index = 0; index < count; index++) targets[index] = blocks[index].Card;
        return count;
    }

    private static int CountProgressCandidates(World world, in EnemyAttackProgress progress)
    {
        ReadOnlySpan<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(progress.Blocks).AsReadOnlySpan();
        int count = 0;
        for (var index = 0; index < blocks.Length; index++)
        {
            if ((progress.Attack == EnemyAttackId.FrostEater && blocks[index].IsFrozen != 0) ||
                (progress.Attack == EnemyAttackId.StoneSkin && blocks[index].IsSealed != 0)) count++;
        }
        return count;
    }

    private static void ResolveBlockResourcesAndPassives(World world, CommandBuffer commands, EntityId battleEntity)
    {
        ref BattleInfo info = ref world.Get<BattleInfo>(battleEntity);
        ref BattleStateInfo battle = ref world.Get<BattleStateInfo>(battleEntity);
        ref EnemyAttackProgress progress = ref world.Get<EnemyAttackProgress>(battleEntity);
        ReadOnlySpan<BlockAssignmentEntry> blocks = world.GetDynamicBuffer(progress.Blocks).AsReadOnlySpan();
        int red = 0;
        int white = 0;
        int black = 0;
        for (var index = 0; index < blocks.Length; index++)
        {
            switch (blocks[index].Color)
            {
                case RuleCardColor.Red: red++; break;
                case RuleCardColor.White: white++; break;
                case RuleCardColor.Black: black++; break;
            }
            if (blocks[index].IsFrozen != 0 && GetPassive(world, battle.PlayerPassives, RuleEffectIds.Windchill) > 0)
            {
                var scar = new EffectSpec(RuleEffectIds.Scar, 1, 0, ConditionSpec.Always, RuleValueFlags.BattleOnly);
                ApplyPassive(world, commands, battleEntity, info.Player, in scar);
            }
        }
        world.Get<Courage>(info.Player).Amount += red;
        world.Get<Temperance>(info.Player).Amount += white;
        int bleed = GetPassive(world, battle.PlayerPassives, RuleEffectIds.Bleed);
        int bleedTriggers = (red >= 2 ? 1 : 0) + (white >= 2 ? 1 : 0) + (black >= 2 ? 1 : 0);
        if (bleed > 0 && bleedTriggers > 0)
        {
            int triggers = Math.Min(bleed, bleedTriggers);
            SetPassive(world, battle.PlayerPassives, RuleEffectIds.Bleed, bleed - triggers);
            ref HP hp = ref world.Get<HP>(info.Player);
            hp.Current = Math.Max(0, hp.Current - triggers);
            if (hp.Current == 0) battle.Flags |= CombatFlags.PlayerDefeated;
        }
    }

    private static EntityId ResolveTarget(TargetHandle target, BattleInfo info) => target.Kind switch
    {
        TargetKind.Entity => target.Entity,
        TargetKind.Player => info.Player,
        TargetKind.PrimaryEnemy => info.Enemy,
        TargetKind.Source => info.Enemy,
        _ => EntityId.Null,
    };

    private static RuleInvocationId NextInvocation(ref BattleInfo info) => new(++info.InvocationSequence);
    private static RuleTriggerEnvelope Trigger(TriggerId id) => new(RuleTriggerKind.Passive, id, default);
    private static RulePhase ToRulePhase(CombatPhase phase) => phase switch
    {
        CombatPhase.BattleStart => RulePhase.StartBattle,
        CombatPhase.Block => RulePhase.PlayerStart,
        CombatPhase.Action => RulePhase.Action,
        CombatPhase.EnemyTurn => RulePhase.EnemyStart,
        CombatPhase.Victory or CombatPhase.Defeat => RulePhase.EndBattle,
        _ => RulePhase.None,
    };

    private static void SetPhase(World world, EntityId battleEntity, CombatPhase phase)
    {
        ref PhaseState state = ref world.Get<PhaseState>(battleEntity);
        if (state.Current == phase) return;
        state.Previous = state.Current;
        state.Current = phase;
        state.Sequence++;
    }

    private static void Trace(World world, EntityId battle, CombatRuleKind rule, EnemyAttackId attack = default, int value0 = 0, int value1 = 0)
    {
        ref BattleStateInfo state = ref world.Get<BattleStateInfo>(battle);
        DynamicBuffer<CombatTraceEntry> trace = world.GetDynamicBuffer(state.Trace);
        trace.Add(new CombatTraceEntry(trace.Count, world.Get<PhaseState>(battle).Current, rule, attack, value0, value1));
    }

    private static void Enqueue(QueuedRuleRuntime<CombatRuleState> rules, CombatRuleKind kind, EntityId battle, int waitFrames = 0)
    {
        var state = new CombatRuleState { Kind = kind, Battle = battle, WaitFrames = waitFrames };
        rules.EnqueueMandatory(new RuleTypeId((int)kind), in state);
    }
}
