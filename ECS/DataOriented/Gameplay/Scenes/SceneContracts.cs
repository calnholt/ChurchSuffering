#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Components;

public enum SceneTransitionPhase : byte
{
    Idle = 0,
    Requested = 1,
    Preparing = 2,
    Ready = 3,
    Activating = 4,
    Failed = 5,
}

public struct SceneTransitionState : IComponent
{
    public Guid PreparationId;
    public SceneGroup From;
    public SceneGroup To;
    public SceneTransitionPhase Phase;
    public bool IsReload;
}

public enum ScenePreparationStatus : byte
{
    Idle = 0,
    Preparing = 1,
    Ready = 2,
    Failed = 3,
}

/// <summary>Hot preparation progress. Asset/job names and errors stay in presentation-owned catalogs.</summary>
public struct ScenePreparationState : IComponent
{
    public Guid PreparationId;
    public SceneGroup TargetScene;
    public ScenePreparationStatus Status;
    public int CompletedJobs;
    public int TotalJobs;
    public float SlowestJobMilliseconds;
}

public struct SceneStateSingleton : ITag
{
}

public struct ScenePreparationSingleton : ITag
{
}

public struct GameOverOverlayState : IComponent
{
    public float Elapsed;
    public bool IsActive;
    public bool SceneSwitched;
}

public enum ModalAnimationPhase : byte
{
    Hidden = 0,
    Entering = 1,
    Visible = 2,
    Exiting = 3,
}

public struct ModalAnimation : IComponent
{
    public StringId InputContextId;
    public float ElapsedSeconds;
    public float EnterDurationSeconds;
    public float ExitDurationSeconds;
    public float ShellDurationSeconds;
    public float StartScale;
    public float StartBrightness;
    public float DimExitHoldPercent;
    public float VisibleShadowAlpha;
    public int ExitSequence;
    public int CompletedExitSequence;
    public ModalAnimationPhase Phase;
    public bool RequestedVisible;

    public readonly bool BlocksInput => Phase != ModalAnimationPhase.Hidden || RequestedVisible;

    public readonly bool IsTransitioning =>
        Phase is ModalAnimationPhase.Entering or ModalAnimationPhase.Exiting;
}

/// <summary>Tracks the modal-owned suppression contribution on one UI entity.</summary>
public struct ModalInputSuppression : IComponent
{
    public StringId ContextId;
    public bool IsApplied;
}

public enum QueuedEventType : byte
{
    Enemy = 0,
    Event = 1,
    Shop = 2,
    Church = 3,
}

public readonly record struct QueuedEventData(
    StringId EventId,
    StringId ModificationSetId,
    QueuedEventType Type);

/// <summary>The ordered payload resides in a world-owned dynamic buffer.</summary>
public struct QueuedEvents : IComponent
{
    public DynamicBufferHandle<QueuedEventData> Items;
    public StringId ClimbEncounterSlotId;
    public StringId LocationId;
    public StringId BattleLocationId;
    public int CurrentIndex;
    public int QuestIndex;
    public bool IsFirst;
    public bool IsClimbEncounter;
}

public struct EntityListOverlay : IComponent
{
    public float TextScale;
    public float ScrollOffset;
    public int PanelX;
    public int PanelY;
    public int PanelWidth;
    public int PanelHeight;
    public int RowHeight;
    public int Padding;
    public bool IsOpen;
}

public struct NarrativeEventOverlayState : IComponent
{
    public StringId RunMapEventId;
    public StringId EventTypeId;
    public StringId ResolutionContextId;
    public bool IsOpen;
}

public struct LocationCustomizeButton : ITag
{
}
