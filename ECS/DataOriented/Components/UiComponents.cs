#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Components;

[Flags]
public enum InputContextFlags : byte
{
    None = 0,
    Active = 1 << 0,
    AcceptsCursor = 1 << 1,
    AcceptsCommands = 1 << 2,
    Diagnostic = 1 << 3,
}

public struct InputContext : IComponent
{
    public StringId Id;
    public int Priority;
    public InputContextFlags Flags;

    public readonly bool IsActive => (Flags & InputContextFlags.Active) != 0;

    public readonly bool AcceptsCursor => (Flags & InputContextFlags.AcceptsCursor) != 0;

    public readonly bool AcceptsCommands => (Flags & InputContextFlags.AcceptsCommands) != 0;

    public readonly bool IsDiagnostic => (Flags & InputContextFlags.Diagnostic) != 0;
}

public enum UIElementEventType : byte
{
    None = 0,
    UnassignCardAsBlock,
    AssignCardAsBlock,
    UnassignEquipmentAsBlock,
    AssignEquipmentAsBlock,
    CardListModalClose,
    ActivateEquipment,
    EndTurn,
    ConfirmBlocks,
    PlayCardRequested,
    SelectedCardForCost,
    LocationSelect,
    QuestSelect,
    NextQuest,
    PreviousQuest,
    ChangeEquipment,
    ViewDiscard,
    ViewDeck,
    AbandonQuest,
    SkipTutorial,
    PayCostCancel,
    PledgeCard,
    OpenLoadout,
    CardClicked,
    ClimbShopSlotSelect,
    ClimbEncounterSlotSelect,
    ClimbEventSlotSelect,
    WayStationDialoguePoiSelect,
    SkipDialog,
    BoosterPackOpeningClose,
    ToggleRumble,
}

public enum UILayerType : byte
{
    Default = 0,
    Overlay = 1,
}

[Flags]
public enum UIInteractionFlags : ushort
{
    None = 0,
    BaseInteractable = 1 << 0,
    Hovered = 1 << 1,
    Clicked = 1 << 2,
    PreventDefaultClick = 1 << 3,
    Hidden = 1 << 4,
    ShowHoverHighlight = 1 << 5,
}

/// <summary>Hot bounds, action, and pointer state shared by interactive UI entities.</summary>
public struct UIElement : IComponent
{
    public Rectangle Bounds;
    public int SuppressCount;
    public UIInteractionFlags Flags;
    public UIElementEventType EventType;
    public UIElementEventType SecondaryEventType;
    public UILayerType LayerType;

    public readonly bool IsInteractable =>
        (Flags & UIInteractionFlags.BaseInteractable) != 0 && SuppressCount == 0;
}

public enum TooltipType : byte
{
    None = 0,
    Quests,
    Card,
    Equipment,
    Text,
}

public enum TooltipPosition : byte
{
    Above = 0,
    Below,
    Right,
    Left,
}

/// <summary>
/// Cold tooltip metadata split from <see cref="UIElement"/> so pointer queries do not
/// stride over strings or presentation-only state.
/// </summary>
public struct TooltipMetadata : IComponent
{
    public StringId Text;
    public StringId KeywordSource;
    public int OffsetPixels;
    public TooltipType Type;
    public TooltipPosition Position;
}
