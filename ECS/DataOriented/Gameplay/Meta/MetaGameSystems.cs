#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

public static class MetaGameSystemIds
{
    // Exact unscheduled compatibility ledger IDs.
    public static readonly SystemId AchievementExplosion = new(4401);
    public static readonly SystemId AchievementScene = new(4402);
    public static readonly SystemId GuidedTutorialDirector = new(4403);
    public static readonly SystemId TutorialManager = new(4404);
    public static readonly SystemId ClimbScene = new(4405);
    public static readonly SystemId WayStationClimbSettingsModal = new(4406);
    public static readonly SystemId WayStationDialogue = new(4407);
    public static readonly SystemId WayStationSaintsMedalsModal = new(4408);
    public static readonly SystemId ClimbEncounter = new(4409);
    public static readonly SystemId ClimbEvent = new(4410);
    public static readonly SystemId CollectionProgression = new(4411);
    public static readonly SystemId RunDeckLifecycle = new(4412);

    // Root scheduler allowlist. These consolidated systems own live behavior.
    public static readonly SystemId ClimbRuntime = new(4491);
    public static readonly SystemId WayStationRuntime = new(4492);
    public static readonly SystemId RewardRuntime = new(4493);
    public static readonly SystemId AchievementRuntime = new(4494);
    public static readonly SystemId TutorialRuntime = new(4495);
    public static readonly SystemId DialogueRuntime = new(4496);
    public static readonly SystemId RunLifecycleRuntime = new(4497);
}

public abstract class MetaGameSystem : IGameSystem
{
    protected MetaGameSystem(SystemDescriptor descriptor) => Descriptor = descriptor;
    public SystemDescriptor Descriptor { get; }
    public abstract void Update(ref SystemContext context);

    protected static ComponentSignature Signature<T>() where T : unmanaged, IComponent =>
        ComponentSignature.Empty.With(ComponentType<T>.Id);
}

public sealed class ClimbRuntimeSystem : MetaGameSystem,
    IEventConsumer<ClimbEncounterSlotSelectedEvent>, IEventConsumer<ClimbEventSlotSelectedEvent>,
    IEventConsumer<ClimbShopSlotSelectedEvent>, IEventConsumer<ClimbPreviewStartedEvent>,
    IEventConsumer<ClimbPreviewClearedEvent>, IEventConsumer<DialogueSequenceCompleted>
{
    private readonly World world;
    private readonly Query<ClimbColumnTransitionState> columns;

    public ClimbRuntimeSystem(World world) : base(new SystemDescriptor(
        MetaGameSystemIds.ClimbRuntime, nameof(ClimbRuntimeSystem), SystemPhase.Gameplay, SceneGroup.Climb,
        readComponents: Signature<ClimbShopSlotAction>().With(ComponentType<RunDeckCard>.Id),
        writeComponents: Signature<ClimbColumnTransitionState>()
            .With(ComponentType<ClimbShopSlotAction>.Id)
            .With(ComponentType<RunDeckCard>.Id),
        readDynamicBufferTypes: [typeof(ClimbSlotEntry), typeof(ShownShopItemEntry)],
        writeDynamicBufferTypes: [typeof(ClimbPreviewEntry), typeof(ShownShopItemEntry)],
        consumedEventTypeIds: [44033, 44035, 44037, 44038, 44041],
        emittedEventTypeIds: [44039, 44040, 44066, 44069],
        runsAfter: [MetaGameSystemIds.RunLifecycleRuntime], recordsStructuralCommands: true,
        eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        columns = world.Query<ClimbColumnTransitionState>();
    }

    public override void Update(ref SystemContext context)
    {
        float delta = (float)context.Elapsed.TotalSeconds * 3f;
        foreach (QueryChunk<ClimbColumnTransitionState> chunk in columns)
        foreach (int row in chunk.Rows)
        {
            ref ClimbColumnTransitionState state = ref chunk.Component1[row];
            if (state.SelectedSlot >= 0 && state.Progress < 1f) state.Progress = Math.Min(1f, state.Progress + delta);
        }
    }

    public void Consume(in ClimbEncounterSlotSelectedEvent value, ref EventDispatchContext context) => Select(value.Root, value.SlotIndex);
    public void Consume(in ClimbEventSlotSelectedEvent value, ref EventDispatchContext context) => Select(value.Root, value.SlotIndex);

    public void Consume(in ClimbShopSlotSelectedEvent value, ref EventDispatchContext context)
        => ClimbShopRuntime.TryPurchase(world, value.Slot, value.Buyer);

    public void Consume(in ClimbPreviewStartedEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Root, out ClimbPreviewState preview)) return;
        preview.Active = 1;
        preview.HoveredSlot = value.SlotIndex;
        world.Set(value.Root, in preview);
    }

    public void Consume(in ClimbPreviewClearedEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Root, out ClimbPreviewState preview)) return;
        preview.Active = 0;
        preview.HoveredSlot = -1;
        world.Set(value.Root, in preview);
    }

    public void Consume(in DialogueSequenceCompleted value, ref EventDispatchContext context)
    {
        foreach (QueryChunk<ClimbColumnTransitionState> chunk in columns)
        foreach (int row in chunk.Rows)
            chunk.Component1[row].CurrentColumn++;
    }

    private void Select(EntityId root, int slotIndex)
    {
        if (!world.TryGet(root, out ClimbColumnTransitionState state)) return;
        DynamicBuffer<ClimbSlotEntry> slots = world.GetDynamicBuffer(state.Slots);
        if ((uint)slotIndex >= (uint)slots.Count) return;
        state.SelectedSlot = slotIndex;
        state.CurrentColumn = slots[slotIndex].Column;
        state.Progress = 0f;
        world.Set(root, in state);
    }
}

public sealed class WayStationRuntimeSystem : MetaGameSystem,
    IEventConsumer<OpenWayStationClimbSettingsModalEvent>,
    IEventConsumer<Crusaders30XX.ECS.DataOriented.Events.OpenWayStationSaintsMedalsModalEvent>
{
    private readonly World world;
    private readonly Query<WayStationArrivalContextState> wayStations;

    public WayStationRuntimeSystem(World world) : base(new SystemDescriptor(
        MetaGameSystemIds.WayStationRuntime, nameof(WayStationRuntimeSystem), SystemPhase.Interaction, SceneGroup.WayStation,
        writeComponents: Signature<WayStationArrivalContextState>(),
        consumedEventTypeIds: [4022, 44064], recordsStructuralCommands: true, eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        wayStations = world.Query<WayStationArrivalContextState>();
    }

    public override void Update(ref SystemContext context)
    {
        foreach (QueryChunk<WayStationArrivalContextState> chunk in wayStations)
        foreach (int row in chunk.Rows)
        {
            ref WayStationArrivalContextState state = ref chunk.Component1[row];
            if (state.ModalDepth < 0) state.ModalDepth = 0;
        }
    }

    public void Consume(in OpenWayStationClimbSettingsModalEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.WayStation, out WayStationArrivalContextState arrival)) return;
        arrival.ModalDepth++;
        world.Set(value.WayStation, in arrival);

        var bundle = new SpawnBundle(2);
        bundle.AddTag<WayStationClimbModalRoot>();
        var choice = new WayStationClimbModalDifficultyChoice { Difficulty = ClimbDifficulty.Normal, Selected = 1 };
        bundle.Add(in choice);
        var modal = new ModalAnimation
        {
            RequestedVisible = true,
            Phase = ModalAnimationPhase.Entering,
            EnterDurationSeconds = 0.2f,
            ExitDurationSeconds = 0.15f,
            StartScale = 0.8f,
        };
        bundle.Add(in modal);
        world.Create(in bundle);

        Query<DialogOverlayState> dialogs = world.Query<DialogOverlayState>();
        foreach (QueryChunk<DialogOverlayState> chunk in dialogs)
        foreach (int row in chunk.Rows)
        {
            ref DialogOverlayState dialogue = ref chunk.Component1[row];
            if (dialogue.State == DialogueState.Playing)
            {
                dialogue.State = DialogueState.Interrupted;
                dialogue.InterruptedBy = MetaModalKind.ClimbSettings;
            }
        }
    }

    public void Consume(in Crusaders30XX.ECS.DataOriented.Events.OpenWayStationSaintsMedalsModalEvent value, ref EventDispatchContext context)
    {
        var bundle = new SpawnBundle(2);
        var root = new WayStationSaintsMedalsModalRoot
        {
            SelectedIndex = -1,
            Modal = MetaModalKind.SaintsMedals,
            Open = 1,
        };
        bundle.Add(in root);
        bundle.AddTag<WayStationSaintsMedalsModalPanel>();
        world.Create(in bundle);
    }
}

public sealed class RewardRuntimeSystem : MetaGameSystem,
    IEventConsumer<ShowBoosterPackOpeningOverlayEvent>, IEventConsumer<BoosterPackOpeningDismissedEvent>,
    IEventConsumer<CloseBoosterPackOpeningOverlayEvent>, IEventConsumer<ShowQuestRewardOverlay>
{
    private readonly World world;
    private readonly Query<BoosterPackOpeningOverlayState> boosters;

    public RewardRuntimeSystem(World world) : base(new SystemDescriptor(
        MetaGameSystemIds.RewardRuntime, nameof(RewardRuntimeSystem), SystemPhase.Gameplay, SceneGroup.Global,
        writeComponents: Signature<BoosterPackOpeningOverlayState>(),
        writeDynamicBufferTypes: [typeof(BoosterRewardEntry), typeof(QuestRewardEntry)],
        consumedEventTypeIds: [44059, 44063, 44067, 44068],
        emittedEventTypeIds: [44079, 44080], recordsStructuralCommands: true, eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        boosters = world.Query<BoosterPackOpeningOverlayState>();
    }

    public override void Update(ref SystemContext context)
    {
        foreach (QueryChunk<BoosterPackOpeningOverlayState> chunk in boosters)
        foreach (int row in chunk.Rows)
        {
            ref BoosterPackOpeningOverlayState state = ref chunk.Component1[row];
            if (state.Open == 0) state.RevealedCount = 0;
        }
    }

    public void Consume(in ShowBoosterPackOpeningOverlayEvent value, ref EventDispatchContext context)
    {
        if (!world.IsAlive(value.Overlay)) return;
        BoosterPackOpeningOverlayState state;
        if (!world.TryGet(value.Overlay, out state))
        {
            state.Rewards = world.CreateDynamicBuffer<BoosterRewardEntry>(value.Overlay, Math.Max(1, value.RewardCount));
            world.Add(value.Overlay, in state);
        }
        state.Open = 1;
        state.Modal = MetaModalKind.BoosterPack;
        state.RevealedCount = 0;
        world.Set(value.Overlay, in state);
    }

    public void Consume(in BoosterPackOpeningDismissedEvent value, ref EventDispatchContext context) => Close(value.Overlay);
    public void Consume(in CloseBoosterPackOpeningOverlayEvent value, ref EventDispatchContext context) => Close(value.Overlay);

    public void Consume(in ShowQuestRewardOverlay value, ref EventDispatchContext context)
    {
        if (!world.IsAlive(value.Overlay)) return;
        QuestRewardOverlayState state;
        if (!world.TryGet(value.Overlay, out state))
        {
            state.Rewards = world.CreateDynamicBuffer<QuestRewardEntry>(value.Overlay, 3);
            world.Add(value.Overlay, in state);
        }
        state.Open = 1;
        state.Modal = MetaModalKind.QuestReward;
        state.Selected = -1;
        world.Set(value.Overlay, in state);
    }

    private void Close(EntityId overlay)
    {
        if (!world.TryGet(overlay, out BoosterPackOpeningOverlayState state)) return;
        state.Open = 0;
        state.Modal = MetaModalKind.None;
        world.Set(overlay, in state);
    }
}

public sealed class AchievementRuntimeSystem : MetaGameSystem,
    IEventConsumer<AchievementProgressUpdatedEvent>, IEventConsumer<AchievementSeenEvent>,
    IEventConsumer<AchievementRevealClickedEvent>
{
    private readonly World world;
    private readonly MetaGameEventHub events;
    private readonly Query<AchievementGridItem> achievements;

    public AchievementRuntimeSystem(World world, MetaGameEventHub events) : base(new SystemDescriptor(
        MetaGameSystemIds.AchievementRuntime, nameof(AchievementRuntimeSystem), SystemPhase.Gameplay, SceneGroup.Global,
        writeComponents: Signature<AchievementGridItem>(),
        consumedEventTypeIds: [44002, 44003, 44007], emittedEventTypeIds: [44001, 44004],
        eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        this.events = events;
        achievements = world.Query<AchievementGridItem>();
    }

    public override void Update(ref SystemContext context)
    {
        foreach (QueryChunk<AchievementGridItem> chunk in achievements)
        foreach (int row in chunk.Rows)
        {
            ref AchievementGridItem item = ref chunk.Component1[row];
            if (item.Progress < 0) item.Progress = 0;
            if (item.Progress > item.Target) item.Progress = item.Target;
        }
    }

    public void Consume(in AchievementProgressUpdatedEvent value, ref EventDispatchContext context)
    {
        foreach (QueryChunk<AchievementGridItem> chunk in achievements)
        foreach (int row in chunk.Rows)
        {
            ref AchievementGridItem item = ref chunk.Component1[row];
            if (item.Achievement != value.Achievement) continue;
            item.Progress = Math.Clamp(value.SetAbsolute != 0 ? value.Absolute : item.Progress + value.Delta, 0, item.Target);
            if (item.Completed == 0 && item.Progress >= item.Target)
            {
                item.Completed = 1;
                events.AchievementCompleted.Publish(new(value.Achievement));
            }
            return;
        }
    }

    public void Consume(in AchievementSeenEvent value, ref EventDispatchContext context) => MarkSeen(value.Achievement);
    public void Consume(in AchievementRevealClickedEvent value, ref EventDispatchContext context)
    {
        MarkSeen(value.Achievement);
        events.AchievementAnimationComplete.Publish(new(value.Achievement));
    }

    private void MarkSeen(AchievementId id)
    {
        foreach (QueryChunk<AchievementGridItem> chunk in achievements)
        foreach (int row in chunk.Rows)
        {
            ref AchievementGridItem item = ref chunk.Component1[row];
            if (item.Achievement != id) continue;
            item.Seen = 1;
            return;
        }
    }
}

public sealed class TutorialRuntimeSystem : MetaGameSystem,
    IEventConsumer<TutorialStartedEvent>, IEventConsumer<AdvanceTutorialEvent>,
    IEventConsumer<GuidedTutorialRestartRequested>, IEventConsumer<GuidedTutorialSkipRequested>
{
    private readonly World world;
    private readonly MetaGameEventHub events;
    private readonly Query<GuidedTutorial> tutorials;

    public TutorialRuntimeSystem(World world, MetaGameEventHub events) : base(new SystemDescriptor(
        MetaGameSystemIds.TutorialRuntime, nameof(TutorialRuntimeSystem), SystemPhase.Gameplay, SceneGroup.Battle,
        writeComponents: Signature<GuidedTutorial>(), readDynamicBufferTypes: [typeof(TutorialStepEntry)],
        writeDynamicBufferTypes: [typeof(TutorialStepEntry)],
        consumedEventTypeIds: [44073, 44075, 44076, 44078], emittedEventTypeIds: [44074, 44077],
        eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        this.events = events;
        tutorials = world.Query<GuidedTutorial>();
    }

    public override void Update(ref SystemContext context)
    {
        foreach (QueryChunk<GuidedTutorial> chunk in tutorials)
        foreach (int row in chunk.Rows)
        {
            ref GuidedTutorial tutorial = ref chunk.Component1[row];
            if (tutorial.State != TutorialState.Running) continue;
            DynamicBuffer<TutorialStepEntry> steps = world.GetDynamicBuffer(tutorial.Steps);
            if (tutorial.CurrentStep >= steps.Count)
            {
                tutorial.State = TutorialState.Complete;
                events.TutorialCompleted.Publish(new(chunk.Entities[row], tutorial.TutorialId));
                events.AllTutorialsCompleted.Publish(new(chunk.Entities[row]));
            }
        }
    }

    public void Consume(in TutorialStartedEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Tutorial, out GuidedTutorial tutorial)) return;
        ClearSteps(in tutorial);
        tutorial.TutorialId = value.TutorialId;
        tutorial.CurrentStep = 0;
        tutorial.State = TutorialState.Running;
        world.Set(value.Tutorial, in tutorial);
    }

    public void Consume(in AdvanceTutorialEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Tutorial, out GuidedTutorial tutorial) || tutorial.State != TutorialState.Running) return;
        DynamicBuffer<TutorialStepEntry> steps = world.GetDynamicBuffer(tutorial.Steps);
        if ((uint)tutorial.CurrentStep >= (uint)steps.Count) return;
        TutorialStepEntry current = steps[tutorial.CurrentStep];
        if (current.RequiredAction != value.Action) return;
        steps[tutorial.CurrentStep] = current with { Completed = 1 };
        tutorial.CurrentStep++;
        if (tutorial.CurrentStep >= steps.Count)
        {
            tutorial.State = TutorialState.Complete;
            events.TutorialCompleted.Publish(new(value.Tutorial, tutorial.TutorialId));
            events.AllTutorialsCompleted.Publish(new(value.Tutorial));
        }
        world.Set(value.Tutorial, in tutorial);
    }

    public void Consume(in GuidedTutorialRestartRequested value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Tutorial, out GuidedTutorial tutorial)) return;
        ClearSteps(in tutorial);
        tutorial.CurrentStep = 0;
        tutorial.State = TutorialState.Running;
        world.Set(value.Tutorial, in tutorial);
    }

    public void Consume(in GuidedTutorialSkipRequested value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Tutorial, out GuidedTutorial tutorial)) return;
        if (tutorial.State is TutorialState.Skipped or TutorialState.Complete) return;
        tutorial.State = TutorialState.Skipped;
        world.Set(value.Tutorial, in tutorial);
        events.AllTutorialsCompleted.Publish(new(value.Tutorial));
    }

    private void ClearSteps(in GuidedTutorial tutorial)
    {
        DynamicBuffer<TutorialStepEntry> steps = world.GetDynamicBuffer(tutorial.Steps);
        for (var index = 0; index < steps.Count; index++) steps[index] = steps[index] with { Completed = 0 };
    }
}

public sealed class DialogueRuntimeSystem : MetaGameSystem,
    IEventConsumer<DialogueSequenceRequested>, IEventConsumer<WayStationDialoguePoiSelectedEvent>,
    IEventConsumer<DialogSkipRequested>, IEventConsumer<DialogEnded>, IEventConsumer<NarrativeModalChoiceRequested>
{
    private readonly World world;
    private readonly MetaGameEventHub events;
    private readonly Query<DialogOverlayState> dialogues;

    public DialogueRuntimeSystem(World world, MetaGameEventHub events) : base(new SystemDescriptor(
        MetaGameSystemIds.DialogueRuntime, nameof(DialogueRuntimeSystem), SystemPhase.Gameplay, SceneGroup.Global,
        writeComponents: Signature<DialogOverlayState>(), writeDynamicBufferTypes: [typeof(DialogueLineEntry)],
        consumedEventTypeIds: [44044, 44046, 44047, 44043, 44070], emittedEventTypeIds: [44042],
        recordsStructuralCommands: true, eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        this.events = events;
        dialogues = world.Query<DialogOverlayState>();
    }

    public override void Update(ref SystemContext context)
    {
        foreach (QueryChunk<DialogOverlayState> chunk in dialogues)
        foreach (int row in chunk.Rows)
        {
            ref DialogOverlayState state = ref chunk.Component1[row];
            if (state.State == DialogueState.Interrupted && state.InterruptedBy == MetaModalKind.None)
                state.State = DialogueState.Playing;
        }
    }

    public void Consume(in DialogueSequenceRequested value, ref EventDispatchContext context) => Start(value.Overlay, value.Sequence);
    public void Consume(in WayStationDialoguePoiSelectedEvent value, ref EventDispatchContext context) => Start(value.WayStation, value.Sequence);

    public void Consume(in DialogSkipRequested value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Overlay, out DialogOverlayState state)) return;
        DynamicBuffer<DialogueLineEntry> lines = world.GetDynamicBuffer(state.Lines);
        state.CurrentLine++;
        if (state.CurrentLine >= lines.Count)
        {
            state.State = DialogueState.Complete;
            events.DialogueSequenceCompleted.Publish(new(lines.Count > 0 ? lines[0].Speaker : default));
        }
        world.Set(value.Overlay, in state);
    }

    public void Consume(in DialogEnded value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Overlay, out DialogOverlayState state)) return;
        state.State = DialogueState.Hidden;
        state.InterruptedBy = MetaModalKind.None;
        world.Set(value.Overlay, in state);
    }

    public void Consume(in NarrativeModalChoiceRequested value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Root, out DialogOverlayState state)) return;
        state.State = DialogueState.Playing;
        state.InterruptedBy = MetaModalKind.None;
        world.Set(value.Root, in state);
    }

    private void Start(EntityId overlay, Crusaders30XX.ECS.DataOriented.Resources.StringId sequence)
    {
        if (!world.IsAlive(overlay)) return;
        DialogOverlayState state;
        if (!world.TryGet(overlay, out state))
        {
            state.Lines = world.CreateDynamicBuffer<DialogueLineEntry>(overlay, 2);
            world.Add(overlay, in state);
        }
        DynamicBuffer<DialogueLineEntry> lines = world.GetDynamicBuffer(state.Lines);
        lines.Clear();
        lines.Add(new(sequence, new(sequence.Value + 1), 1, 0));
        lines.Add(new(sequence, new(sequence.Value + 2), -1, 0));
        state.CurrentLine = 0;
        state.State = DialogueState.Playing;
        state.InterruptedBy = MetaModalKind.None;
        world.Set(overlay, in state);
    }
}

public sealed class RunLifecycleRuntimeSystem : MetaGameSystem,
    IEventConsumer<LoadoutCardAdded>, IEventConsumer<LoadoutCardRemoved>, IEventConsumer<QuestSelected>
{
    private readonly World world;
    private readonly Query<RunDeckCard> cards;

    public RunLifecycleRuntimeSystem(World world) : base(new SystemDescriptor(
        MetaGameSystemIds.RunLifecycleRuntime, nameof(RunLifecycleRuntimeSystem), SystemPhase.Gameplay, SceneGroup.Global,
        writeComponents: Signature<RunDeckCard>(), consumedEventTypeIds: [44052, 44053, 44065],
        recordsStructuralCommands: true, eventBarrier: EventBarrier.AfterSystem))
    {
        this.world = world;
        cards = world.Query<RunDeckCard>();
    }

    public override void Update(ref SystemContext context)
    {
        var order = 0;
        foreach (QueryChunk<RunDeckCard> chunk in cards)
        foreach (int row in chunk.Rows)
            chunk.Component1[row].Order = order++;
    }

    public void Consume(in LoadoutCardAdded value, ref EventDispatchContext context)
    {
        var bundle = new SpawnBundle(1);
        var card = new RunDeckCard { Definition = value.Card, Upgraded = value.Upgraded, Order = CountCards() };
        bundle.Add(in card);
        world.Create(in bundle);
    }

    public void Consume(in LoadoutCardRemoved value, ref EventDispatchContext context)
    {
        EntityId remove = EntityId.Null;
        foreach (QueryChunk<RunDeckCard> chunk in cards)
        foreach (int row in chunk.Rows)
        {
            RunDeckCard card = chunk.Component1[row];
            if (card.Definition == value.Card && card.Upgraded == value.Upgraded) { remove = chunk.Entities[row]; break; }
        }
        if (!remove.IsNull) world.Destroy(remove);
    }

    public void Consume(in QuestSelected value, ref EventDispatchContext context)
    {
        // Quest-scoped cleanup is made explicit at the data boundary; cards remain materialized.
        foreach (QueryChunk<RunDeckCard> chunk in cards)
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Order < 0) chunk.Component1[row].Order = 0;
    }

    private int CountCards()
    {
        var count = 0;
        foreach (QueryChunk<RunDeckCard> chunk in cards) count += chunk.Count;
        return count;
    }
}

/// <summary>Unscheduled name-only compatibility descriptors for exact twelve-row ledger reconciliation.</summary>
public sealed class MetaCompatibilitySystem : IGameSystem
{
    public MetaCompatibilitySystem(SystemId id, string name, SceneGroup scene) =>
        Descriptor = new SystemDescriptor(id, name, SystemPhase.Gameplay, scene);
    public SystemDescriptor Descriptor { get; }
    public void Update(ref SystemContext context) { }
}
