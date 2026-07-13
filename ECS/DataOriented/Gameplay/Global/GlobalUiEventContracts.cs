#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Events;

/// <summary>
/// ECS-040 reserves one contiguous range so generated/integration routing can preserve
/// descriptor IDs without discovering event types at runtime.
/// </summary>
public static class GlobalUiEventTypeIds
{
    public const int PlayerInput = 4001;
    public const int PlayerCommand = 4002;
    public const int SetPlayerInputEnabled = 4003;
    public const int CursorInput = 4004;
    public const int LoadScene = 4010;
    public const int PrepareScene = 4011;
    public const int ScenePreparationReady = 4012;
    public const int SceneDeactivating = 4013;
    public const int SceneActivating = 4014;
    public const int SceneActivated = 4015;
    public const int DeleteCaches = 4016;
    public const int PrepareMusicTrack = 4017;
    public const int ShowNarrativeEventOverlay = 4020;
    public const int NarrativeEventOverlayClosed = 4021;
    public const int OpenWayStationSaintsMedalsModal = 4022;
    public const int TreasureChestOpened = 4023;
    public const int UiHoverChanged = 4030;
    public const int UiClick = 4031;
    public const int UiAction = 4032;
    public const int TimerElapsed = 4040;
}

public enum PlayerCommand : byte
{
    QuitApplication = 0,
    ToggleFullScreen = 1,
    ToggleDebugMenu = 2,
    ToggleEntityList = 3,
    DealDebugDamage = 4,
    ToggleProfiler = 5,
    Cancel = 6,
    ShowHint = 7,
}

public readonly record struct PlayerInputEvent(PlayerInputFrame Frame);

public readonly record struct PlayerCommandEvent(PlayerCommand Command, PlayerInputDevice Source);

public readonly record struct SetPlayerInputEnabledEvent(bool Enabled);

/// <summary>
/// Host-captured cursor data that can be forwarded to card and presentation routes without
/// allowing a gameplay system to poll MonoGame hardware.
/// </summary>
public readonly record struct CursorInputEvent(
    Vector2 Position,
    Vector2 Delta,
    float ScrollDelta,
    float ScrollStickY,
    bool PrimaryDown,
    bool PrimaryPressed,
    bool SecondaryDown,
    bool SecondaryPressed,
    PlayerInputDevice Source);

public readonly record struct LoadSceneEvent(
    Guid PreparationId,
    SceneGroup Scene,
    SceneGroup PreviousScene,
    bool Reload = false);

public readonly record struct PrepareSceneEvent(Guid PreparationId, SceneGroup Scene);

public readonly record struct ScenePreparationReady(Guid PreparationId, SceneGroup Scene);

public readonly record struct SceneDeactivating(SceneGroup From, SceneGroup To);

public readonly record struct SceneActivating(Guid PreparationId, SceneGroup From, SceneGroup To);

public readonly record struct SceneActivated(Guid PreparationId, SceneGroup Scene);

public readonly record struct DeleteCachesEvent(SceneGroup Scene);

public readonly record struct PrepareMusicTrackEvent(SoundId Track);

public readonly record struct OpenWayStationSaintsMedalsModalEvent;

public readonly record struct ShowNarrativeEventOverlay(
    StringId RunMapEventId,
    StringId EventTypeId,
    StringId ResolutionContextId,
    StringId ContentDefinitionId);

public readonly record struct NarrativeEventOverlayClosedEvent(
    StringId RunMapEventId,
    StringId EventTypeId,
    int OptionIndex);

public readonly record struct TreasureChestOpened(
    int RewardGold,
    StringId RewardMedalId,
    StringId RewardEquipmentId);

public readonly record struct UIHoverChangedEvent(
    EntityId Previous,
    EntityId Current,
    PlayerInputDevice Source);

public readonly record struct UIClickEvent(
    EntityId Entity,
    bool Secondary,
    PlayerInputDevice Source);

public readonly record struct UIActionEvent(
    EntityId Entity,
    UIElementEventType Action,
    PlayerInputDevice Source);

public readonly record struct TimerElapsedEvent(EntityId Entity, int Sequence);
