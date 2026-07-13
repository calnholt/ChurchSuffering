#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MasterDeckCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DrawPileCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct HandCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DiscardPileCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ExhaustPileCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct AssignedBlockCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PaymentCard(EntityId Card);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardApplicationEntry(EffectId Effect, int Amount, int Duration);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardStatModifierEntry(CardStatKind Kind, int Delta, EntityId Source);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ModalCardEntry(EntityId Card, int Context);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TooltipLineEntry(int TextId, int Style);
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DropdownEntry(int TextId, int Value);

[Flags]
public enum CardRuntimeFlags : ushort
{
    None = 0,
    Upgraded = 1 << 0,
    FreeAction = 1 << 1,
    ExhaustsOnEndTurn = 1 << 2,
    Weapon = 1 << 3,
    Token = 1 << 4,
    Starter = 1 << 5,
    CanAddToLoadout = 1 << 6,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CardData : IComponent
{
    public CardId Definition;
    public CardId PrintedDefinition;
    public EntityId Deck;
    public RuleCardColor PrintedColor;
    public RuleCardColor RuntimeColor;
    public RuleCardType Type;
    public CardRuntimeFlags Flags;
    public int Damage;
    public int Block;
    public byte CostCount;
    public CardCostColor Cost0;
    public CardCostColor Cost1;
    public CardCostColor Cost2;
    public CardCostColor Cost3;

    public readonly bool IsUpgraded => (Flags & CardRuntimeFlags.Upgraded) != 0;
    public readonly bool IsWeapon => (Flags & CardRuntimeFlags.Weapon) != 0;
    public readonly bool IsToken => (Flags & CardRuntimeFlags.Token) != 0;
    public readonly bool IsFreeAction => (Flags & CardRuntimeFlags.FreeAction) != 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CardZoneLocation : IComponent
{
    public EntityId Deck;
    public CardZone Zone;
    public int Index;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Deck : IComponent
{
    public EntityId Owner;
    public DynamicBufferHandle<MasterDeckCard> Cards;
    public DynamicBufferHandle<DrawPileCard> DrawPile;
    public DynamicBufferHandle<HandCard> Hand;
    public DynamicBufferHandle<DiscardPileCard> DiscardPile;
    public DynamicBufferHandle<ExhaustPileCard> ExhaustPile;
    public DynamicBufferHandle<AssignedBlockCard> AssignedBlocks;
    public RuleRandomState Random;
    public int MaximumHandSize;
    public int CardsMilled;
    public int ActionAttackHits;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Player : IComponent
{
    public int ActionPoints;
    public int Courage;
    public int Temperance;
    public int Health;
    public int MaximumHealth;
    public int Frostbite;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CardPlayStatContext : IComponent
{
    public DynamicBufferHandle<CardStatModifierEntry> Modifiers;
    public int DerivedDamage;
    public int ResolvedDamage;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct LastPaymentCache : IComponent
{
    public DynamicBufferHandle<PaymentCard> Cards;
    public int RedCount;
    public int WhiteCount;
    public int BlackCount;
    public int AnyCount;
    public int ScorchedCount;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PayCostOverlayState : IComponent
{
    public EntityId CardToPlay;
    public DynamicBufferHandle<PaymentCard> Candidates;
    public DynamicBufferHandle<PaymentCard> Selected;
    public byte RequiredCount;
    public byte IsOpen;
    public byte IsReturning;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CardListModal : IComponent
{
    public DynamicBufferHandle<ModalCardEntry> Cards;
    public EntityId Deck;
    public CardZone SourceZone;
    public int TitleId;
    public int Context;
    public byte IsOpen;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CardTooltip : IComponent
{
    public DynamicBufferHandle<TooltipLineEntry> Lines;
    public CardId Definition;
    public RuleCardColor Color;
    public byte IsUpgraded;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ModifiedDamage : IComponent
{
    public DynamicBufferHandle<CardStatModifierEntry> Modifiers;
    public int BaseDamage;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct UIDropdown : IComponent
{
    public DynamicBufferHandle<DropdownEntry> Entries;
    public int SelectedIndex;
    public byte IsOpen;
}

public struct AnimatingHandToZone : IComponent { public CardZone Destination; public float Progress; }
public struct CanPlayHighlightSettings : IComponent { public float Alpha; public float PulseSpeed; }
public struct CardGeometrySettings : IComponent { public float Width; public float Height; public float HandSpacing; }
public struct CardListModalSelectionMetadata : IComponent { public int Context; public int Minimum; public int Maximum; }
public struct CardSheen : IComponent { public float Progress; public float Intensity; }
public struct CardToDiscardFlight : IComponent { public float Progress; public CardZone Destination; }
public struct CursedOriginalCard : IComponent { public CardId Definition; public RuleCardColor Color; public CardRuntimeFlags Flags; }
public struct DebugMenu : IComponent { public int ActiveTab; public byte IsOpen; }
public struct EquippedWeapon : IComponent { public EntityId Card; public int UsesThisAction; }
public struct HPBarAnchor : IComponent { public float X; public float Y; }
public struct HPBarOverride : IComponent { public int Current; public int Maximum; }
public struct Hint : IComponent { public int TextId; public float RemainingSeconds; }
public enum HotKeyHintPosition : byte
{
    Above,
    Below,
    Left,
    Right,
    Center,
}

[Flags]
public enum HotKeyFlags : byte
{
    None = 0,
    RequiresHold = 1 << 0,
    Active = 1 << 1,
    AllowWhenNonInteractable = 1 << 2,
    Pressed = 1 << 3,
    Holding = 1 << 4,
}

public struct HotKey : IComponent
{
    public EntityId Parent;
    public int KeyboardBinding;
    public byte GamepadBinding;
    public float HoldDurationSeconds;
    public float HoldProgressSeconds;
    public HotKeyHintPosition HintPosition;
    public HotKeyFlags Flags;
}
public struct Pledge : IComponent { public byte CanPlay; }
public struct PledgeAvailabilityState : IComponent { public byte Enabled; public byte PledgedThisActionPhase; }
public struct PlunderRescueFlight : IComponent { public float Progress; }
public struct PlunderSnatchFlight : IComponent { public float Progress; }
public struct Plundered : IComponent { public int DamageThreshold; public int DamageDealt; }
public struct PortraitInfo : IComponent { public int TextureId; public int Frame; }
public struct ProfilerOverlay : IComponent { public int SelectedSystem; public byte IsOpen; }
public struct Recoil : IComponent { public int Stacks; }
public struct Sealed : IComponent { public int Seals; }
public struct SelectedForPayment : IComponent { public int SelectionIndex; }
public struct Shackle : IComponent { public int Group; }
public struct TooltipOverrideBackup : IComponent { public int TextId; public int TooltipType; }

public readonly struct AnimatingHandToDiscard : ITag { }
public readonly struct AnimatingHandToDrawPile : ITag { }
public readonly struct Brittle : ITag { }
public readonly struct CardListModalClose : ITag { }
public readonly struct Colorless : ITag { }
public readonly struct Cursed : ITag { }
public readonly struct FilteredFromHand : ITag { }
public readonly struct Frozen : ITag { }
public readonly struct Intimidated : ITag { }
public readonly struct MarkedForBottomOfDrawPile : ITag { }
public readonly struct MarkedForEndOfTurnDiscard : ITag { }
public readonly struct MarkedForExhaust : ITag { }
public readonly struct MarkedForReturnToDeck : ITag { }
public readonly struct MarkedForSpecificDiscard : ITag { }
public readonly struct PayCostCancelButton : ITag { }
public readonly struct PledgePreview : ITag { }
public readonly struct Scorched : ITag { }
public readonly struct SuppressCardVisualEffects : ITag { }
public readonly struct SuppressCardZoneRender : ITag { }
public readonly struct SuppressPortraitRender : ITag { }
public readonly struct SuppressStatDeltaDisplay : ITag { }
public readonly struct Thorned : ITag { }

public enum CardPaymentFailure : byte
{
    None,
    CardNotInHand,
    CardCannotBePlayed,
    PledgeLocked,
    Sealed,
    InsufficientActionPoints,
    InsufficientPaymentCards,
    DefinitionRejected,
}

public enum PledgeFailure : byte
{
    None,
    Disabled,
    NotInHand,
    AlreadyPledgedThisAction,
    ExistingPledge,
    AlreadyPledged,
    Sealed,
    Weapon,
    Block,
    Relic,
    Token,
}
