using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Locations;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using System;
using Crusaders30XX.ECS.Objects.Medals;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.Components
{
    /// <summary>
    /// Component that holds the basic data for a card
    /// </summary>
    /// 
    /// 
    public class CardData : IComponent, IDisposable
    {
        public Entity Owner { get; set; }
        
        public CardBase Card { get; set; }

        public void Dispose()
        {
            Card?.Dispose();
        }
        
        // Instance-specific properties for the card entity
        public CardColor Color { get; set; } = CardColor.White;
        

        
        public enum CardRarity
        {
            Common,
            Uncommon,
            Rare,
            Legendary
        }
        
        public enum CardColor
        {
            White,
            Red,
            Black,
            Yellow
        }
        
        public enum CostType
        {
            NoCost,
            Red,
            White,
            Black,
            Any
        }
    }

    /// <summary>
    /// Component for entities that represent a deck
    /// </summary>
    public class Deck : IComponent
    {
        public Entity Owner { get; set; }
        
        public List<Entity> Cards { get; set; } = new();
        public List<Entity> DrawPile { get; set; } = new();
        public List<Entity> DiscardPile { get; set; } = new();
        public List<Entity> ExhaustPile { get; set; } = new();
        public List<Entity> Hand { get; set; } = new();
    }
    
    
    /// <summary>
    /// Component for the player entity
    /// </summary>
    public class Player : IComponent
    {
        public Entity Owner { get; set; }
        
        public Entity DeckEntity { get; set; }
        public Entity HandEntity { get; set; }
    }
    
    /// <summary>
    /// Component for enemies
    /// </summary>
    public class Enemy : IComponent, IDisposable
    {
        public void Dispose()
        {
            EnemyBase?.Dispose();
        }

        public Entity Owner { get; set; }
        
        public EnemyId Id { get; set; } = EnemyId.Demon;

        public string Name { get; set; } = "";
        public int MaxHealth { get; set; } = 40;
        public int CurrentHealth { get; set; } = 40;
        public int Block { get; set; } = 0;
        public EnemyBase EnemyBase { get; set; }
    }

    /// <summary>
    /// Optional resource component representing Courage for an entity (e.g., the player)
    /// </summary>
    public class Courage : IComponent
    {
        public Entity Owner { get; set; }
        
        public int Amount { get; set; } = 0;
    }

    /// <summary>
    /// Per-turn resource representing the player's available action points during the Action phase.
    /// </summary>
    public class ActionPoints : IComponent
    {
        public Entity Owner { get; set; }

        public int Current { get; set; } = 0;
    }
    
    /// <summary>
    /// Optional resource component representing Temperance for an entity (e.g., the player)
    /// </summary>
    public class Temperance : IComponent
    {
        public Entity Owner { get; set; }
        
        public int Amount { get; set; } = 0;
    }

    /// <summary>
    /// Optional resource component representing Threat for an entity (e.g., enemies)
    /// </summary>
    public class Threat : IComponent
    {
        public Entity Owner { get; set; }
        
        public int Amount { get; set; } = 0;
    }

    /// <summary>
    /// Player's currently equipped Temperance ability (by id). At most one should be equipped.
    /// </summary>
    public class EquippedTemperanceAbility : IComponent
    {
        public Entity Owner { get; set; }
        public string AbilityId { get; set; } = "";
    }

    /// <summary>
    /// Player's currently equipped weapon by id (e.g., "hammer"). Weapon is not in the deck.
    /// </summary>
    public class EquippedWeapon : IComponent
    {
        public Entity Owner { get; set; }
        public string WeaponId { get; set; } = "";
        public Entity SpawnedEntity { get; set; }
    }

    /// <summary>
    /// Player stat for how many cards to draw at start of Block phase.
    /// </summary>
    public class Intellect : IComponent
    {
        public Entity Owner { get; set; }
        public int Value { get; set; } = 0;
    }

    /// <summary>
    /// Player stat for maximum cards allowed in hand.
    /// A separate component allows UI and effects to modify it independently
    /// from the Deck's internal limit; a sync system keeps Deck.MaxHandSize aligned.
    /// </summary>
    public class MaxHandSize : IComponent
    {
        public Entity Owner { get; set; }
        public int Value { get; set; } = MAX_HAND_SIZE;
        public static readonly int MAX_HAND_SIZE = 4;
    }


	/// <summary>
	/// A single equipped item. Multiple instances may exist, each referencing the owning player.
	/// </summary>
	public class EquippedEquipment : IComponent, IDisposable
	{
		public void Dispose()
		{
			Equipment?.Dispose();
		}
		public Entity Owner { get; set; }
		public Entity EquippedOwner { get; set; } // player entity that owns this equipment
		public EquipmentBase Equipment { get; set; }
	}

    public enum EquipmentSlot
    {
        Head,
        Chest,
        Arms,
        Legs
    }

	/// <summary>
	/// Per-battle state: counters and once-per-battle flags for triggers.
	/// </summary>
	public class BattleStateInfo : IComponent
	{
		public Entity Owner { get; set; }
		public HashSet<string> EquipmentTriggeredThisBattle { get; set; } = new();
        public Dictionary<string, int> RunTracking { get; set; } = new();
        public Dictionary<string, int> BattleTracking { get; set; } = new();
        public Dictionary<string, int> TurnTracking { get; set; } = new();
        public Dictionary<string, int> PhaseTracking { get; set; } = new();
        /// <summary>Player attack hits that dealt damage to the enemy during the current action phase.</summary>
        public int PlayerActionPhaseAttackHits { get; set; }

	}

    public enum TrackingTypeEnum
    {
        CourageGained,
        CourageLost,
        NumberOfAttacksHitPlayer,
        CardsMilled,
        CursesRemoved,
    }
    
    /// <summary>
    /// Component for positioning and rendering
    /// </summary>
    public class Transform : IComponent
    {
        public Entity Owner { get; set; }

        public Vector2 Position { get; set; } = Vector2.Zero;
        public float Rotation { get; set; } = 0f;
        public Vector2 Scale { get; set; } = Vector2.One;
        public int ZOrder { get; set; } = 0;
    }

    /// <summary>
    /// Makes an entity's transform local to another entity's transform.
    /// Parent rotation and scale are intentionally not propagated.
    /// </summary>
    public class ParentTransform : IComponent
    {
        public Entity Owner { get; set; }
        public Entity Parent { get; set; }
    }
    
    /// <summary>
    /// Parallax configuration for UI/scene entities that should subtly move opposite the cursor.
    /// Attach this component and the ParallaxLayerSystem handles everything automatically.
    /// </summary>
    public class ParallaxLayer : IComponent
    {
        public Entity Owner { get; set; }

        public float MultiplierX { get; set; } = 0.03f;
        public float MultiplierY { get; set; } = 0.03f;
        public float MaxOffset { get; set; } = 48f;
        public float SmoothTime { get; set; } = 0.08f;

        public static ParallaxLayer GetUIParallaxLayer()
        {
            return new ParallaxLayer
            {
                MultiplierX = 0.025f,
                MultiplierY = 0.025f,
                MaxOffset = 48f,
                SmoothTime = 0.08f
            };
        }

        public static ParallaxLayer GetLocationParallaxLayer()
        {
            return new ParallaxLayer
            {
                MultiplierX = 0.01f,
                MultiplierY = 0.01f,
                MaxOffset = 12f,
                SmoothTime = 0.01f
            };
        }

        public static ParallaxLayer GetCharacterParallaxLayer()
        {
            return new ParallaxLayer
            {
                MultiplierX = 0.01f,
                MultiplierY = 0.01f,
                MaxOffset = 48f,
                SmoothTime = 0.08f
            };
        }
    }
    
    
    /// <summary>
    /// Component for rendering sprites
    /// </summary>
    public class Sprite : IComponent
    {
        public Entity Owner { get; set; }
        
        public string TexturePath { get; set; } = "";
        public Rectangle? SourceRectangle { get; set; } = null;
        public Color Tint { get; set; } = Color.White;
        public bool IsVisible { get; set; } = true;
    }
    
    /// <summary>
    /// Component for UI elements
    /// </summary>
    public class UIElement : IComponent
    {
        public Entity Owner { get; set; }

        public Rectangle Bounds { get; set; }
        public bool BaseInteractable { get; set; } = false;
        public int SuppressCount { get; set; } = 0;
        public bool IsInteractable
        {
            get => BaseInteractable && SuppressCount == 0;
            set => BaseInteractable = value;
        }
        public bool IsHovered { get; set; } = false;
        public bool IsClicked { get; set; } = false;
        public string Tooltip { get; set; } = "";
        public TooltipType TooltipType { get; set; } = TooltipType.Text;
        public TooltipPosition TooltipPosition { get; set; } = TooltipPosition.Above;
        public int TooltipOffsetPx { get; set; } = 6; // gap from element to tooltip
        public UIElementEventType EventType { get; set; } = UIElementEventType.None;
        public UIElementEventType SecondaryEventType { get; set; } = UIElementEventType.None;
        public UILayerType LayerType { get; set; } = UILayerType.Default;
        public bool IsPreventDefaultClick { get; set; } = false;
        public bool IsHidden { get; set; } = false;
        public bool ShowHoverHighlight { get; set; } = true;

        public void Suppress() => SuppressCount++;
        public void Restore() => SuppressCount = Math.Max(0, SuppressCount - 1);
    }

    /// <summary>
    /// Optional hint text for a hovered UI entity. Displayed via HintTooltipDisplaySystem
    /// when the player requests help (e.g., Left Stick click on gamepad).
    /// </summary>
    public class Hint : IComponent
    {
        public Entity Owner { get; set; }
        public string Text { get; set; } = "";
    }

    public enum UILayerType
    {
        Default,
        Overlay,
    }

    public enum TooltipType
    {
        None,
        Quests,
        Card,
        Equipment,
        Text,
    }

    public enum UIElementEventType
    {
        None,
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
        PayCostCancel,
        PledgeCard,
        LeaveShop,
        OpenLoadout,
        CardClicked,
        ClimbShopSlotSelect,
        ClimbEncounterSlotSelect,
        ClimbEventSlotSelect,
    }

    /// <summary>
    /// Anchor published by health display systems describing an entity's HP track rectangle.
    /// Used by other UI systems to align elements relative to the HP bar.
    /// </summary>
    public class HPBarAnchor : IComponent
    {
        public Entity Owner { get; set; }
        public Microsoft.Xna.Framework.Rectangle Rect { get; set; }
    }

    /// <summary>
    /// Per-entity override for the legacy plunder HP gauge positioning and size.
    /// When present, HPDisplaySystem uses these values instead of its global defaults.
    /// </summary>
    public class HPBarOverride : IComponent
    {
        public Entity Owner { get; set; }
        public int OffsetX { get; set; }
        public int OffsetY { get; set; }
        public int BarWidth { get; set; }
        public int BarHeight { get; set; }
    }

    public enum TooltipPosition
    {
        Above,
        Below,
        Right,
        Left
    }

    /// <summary>
    /// When attached to a hovered UI element, renders the specified card as a tooltip.
    /// </summary>
    public class CardTooltip : IComponent
    {
        public Entity Owner { get; set; }
        public string CardId { get; set; } = "";
        public float TooltipScale { get; set; } = 0.6f;
        public CardData.CardColor? CardColor { get; set; }
        public bool IsUpgraded { get; set; }
        public bool CrossfadeUpgradePreview { get; set; }
        public List<string> PreviewRestrictionNames { get; set; } = new();
    }

    /// <summary>
    /// Backups original tooltip configuration so temporary overrides can be restored later.
    /// </summary>
    public class TooltipOverrideBackup : IComponent
    {
        public Entity Owner { get; set; }
        public TooltipType OriginalType { get; set; } = TooltipType.Text;
        public TooltipPosition OriginalPosition { get; set; } = TooltipPosition.Above;
        public int OriginalOffsetPx { get; set; } = 30;
        public bool HadCardTooltip { get; set; } = false;
        public string OriginalCardTooltipId { get; set; } = "";
    }

    /// <summary>
    /// Marks a card in hand that has been preselected to be discarded by an enemy effect
    /// for a specific attack context.
    /// </summary>
    public class MarkedForSpecificDiscard : IComponent
    {
        public Entity Owner { get; set; }
    }
    
    /// <summary>
    /// Marks a card as intimidated. Intimidated cards cannot be used to block during the block phase.
    /// </summary>
    public class Intimidated : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card as frozen. Frozen cards lose frozen when used to block. Frozen cards cannot be played during action phase
    /// </summary>
    public class Frozen : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card as brittle. If used as the sole blocker for an attack, mills 1.
    /// Persists for the entire run. Rendered with the brittle crumble shader.
    /// </summary>
    public class Brittle : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card as scorched. Rendered with the scorched fire shader.
    /// </summary>
    public class Scorched : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card as thorned. Rendered with the thorny vines shader.
    /// </summary>
    public class Thorned : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card as cursed. Rendered with the cursed cracks shader.
    /// </summary>
    public class Cursed : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Stores the original runtime definition for a card that is currently shown
    /// and played as Curse.
    /// </summary>
    public class CursedOriginalCard : IComponent
    {
        public Entity Owner { get; set; }
        public string CardId { get; set; } = "";
        public CardData.CardColor Color { get; set; } = CardData.CardColor.White;
        public bool IsUpgraded { get; set; }
        public bool IsStarter { get; set; }
    }

    /// <summary>
    /// Marks a card as colorless. Its printed color remains unchanged, but it does not
    /// qualify as red, white, or black during combat.
    /// </summary>
    public class Colorless : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card with recoil stacks. If the card is not used to block the current attack,
    /// the player takes damage equal to Stacks. Removed each attack resolve.
    /// </summary>
    public class Recoil : IComponent
    {
        public Entity Owner { get; set; }
        public int Stacks { get; set; } = 1;
    }

    /// <summary>
    /// Marks a card as sealed (petrified). Sealed cards cannot be played or pledged, but can block.
    /// Seals count down: -1 per block, -1 per card played. At 0 seals, card is freed.
    /// </summary>
    public class Sealed : IComponent
    {
        public Entity Owner { get; set; }
        public int Seals { get; set; } = 3;
    }

    /// <summary>
    /// Marks a card as having its block value modified.
    /// </summary>
    public class ModifiedBlock : IComponent
    {
        public Entity Owner { get; set; }
        public List<Modification> Modifications { get; set; } = new List<Modification>();

    }

    /// <summary>
    /// Marks a card as having its damage value modified with reasons for each modification.
    /// </summary>
    public class ModifiedDamage : IComponent
    {
        public Entity Owner { get; set; }
        public List<Modification> Modifications { get; set; } = new List<Modification>();
        
    }

    /// <summary>
    /// Marks a card as shackled. Shackled cards are assigned and unassigned as block together.
    /// </summary>
    public class Shackle : IComponent
    {
        public Entity Owner { get; set; }
    }

    public struct Modification
    {
        public int Delta { get; set; }
        public string Reason { get; set; }
    }
    
    /// <summary>
    /// Marker for the courage hover area used to show a tooltip.
    /// </summary>
    public class CourageTooltipAnchor : IComponent
    {
        public Entity Owner { get; set; }
    }
    
    /// <summary>
    /// Marker for the temperance hover area used to show a tooltip.
    /// </summary>
    public class TemperanceTooltipAnchor : IComponent
    {
        public Entity Owner { get; set; }
    }
    
    /// <summary>
    /// Marker for the threat hover area used to show a tooltip.
    /// </summary>
    public class ThreatTooltipAnchor : IComponent
    {
        public Entity Owner { get; set; }
    }
    
    /// <summary>
    /// Component for quest tooltip data and fade state.
    /// </summary>
    public class QuestTooltip : IComponent
    {
        public Entity Owner { get; set; }
        
        public string LocationId { get; set; }
        public string Title { get; set; }
	public List<LocationEventDefinition> Events { get; set; }
	public List<TribulationDefinition> Tribulations { get; set; }
		public int RewardGold { get; set; } = 0;
		public bool IsCompleted { get; set; } = false;
	public string PoiType { get; set; } = "Quest";
	public float Alpha01 { get; set; } = 0f;
	public bool TargetVisible { get; set; } = false;
    }
    
    /// <summary>
    /// Component for animation
    /// </summary>
    public class Animation : IComponent
    {
        public Entity Owner { get; set; }
        
        public float Duration { get; set; } = 1.0f;
        public float CurrentTime { get; set; } = 0.0f;
        public bool IsPlaying { get; set; } = false;
        public bool IsLooping { get; set; } = false;
        public AnimationType Type { get; set; } = AnimationType.Fade;
        
        public enum AnimationType
        {
            Fade,
            Scale,
            Move,
            Rotate
        }
    }


    /// <summary>
    /// Shared portrait metadata for any entity drawn as a character/portrait.
    /// Systems set TextureWidth/Height once the texture is known and keep CurrentScale updated.
    /// </summary>
    public class PortraitInfo : IComponent
    {
        public Entity Owner { get; set; }
        public int TextureWidth { get; set; }
        public int TextureHeight { get; set; }
        public float CurrentScale { get; set; } = 1f; // actual draw scale this frame (may breathe)
        public float BaseScale { get; set; } = 1f;    // stable baseline scale for layout (no breathing)
        /// <summary>World center from the last portrait Draw (parallax + attack offset included).</summary>
        public Vector2 LastDrawCenter;
        /// <summary>Top-left of the last portrait Draw in world space (matches SpriteBatch placement).</summary>
        public Vector2 LastDrawTopLeft;
        /// <summary>Per-axis scale from the last portrait Draw (pulse + animation multiplier included).</summary>
        public Vector2 LastDrawScale;
    }

    /// <summary>
    /// Hides portrait draw for an entity (e.g. during defeat presentation pixel burst).
    /// </summary>
    public class SuppressPortraitRender : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Hides non-intrinsic DAMAGE/BLOCK deltas in preview contexts.
    /// Block still includes the black-card +1 bonus in value and delta slab.
    /// </summary>
    public class SuppressStatDeltaDisplay : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Temporarily hides readability-blocking card overlays while inspecting a hovered card.
    /// </summary>
    public class SuppressCardVisualEffects : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// State for the on-screen profiler overlay
    /// </summary>
    public class ProfilerOverlay : IComponent
    {
        public Entity Owner { get; set; }
        public bool IsOpen { get; set; } = false;
    }


    /// <summary>
    /// Component representing a generic card list modal
    /// </summary>
    public enum CardListModalMode
    {
        Auto,
        CardList,
        Inventory,
    }

    public class CardListModal : IComponent
    {
        public Entity Owner { get; set; }
        public bool IsOpen { get; set; } = false;
        public string Title { get; set; } = "";
        public List<Entity> Cards { get; set; } = new();
        public int ScrollOffset { get; set; } = 0;
        public int BuildScrollOffset { get; set; } = 0;
        public bool IsSelectable { get; set; } = false;
        public string SelectionContext { get; set; } = string.Empty;
        public int SelectedCardIndex { get; set; } = -1;
        public CardListModalMode Mode { get; set; } = CardListModalMode.Auto;
    }

    public class CardListModalSelectionMetadata : IComponent
    {
        public Entity Owner { get; set; }
        public string SelectionContext { get; set; } = string.Empty;
        public string EntryId { get; set; } = string.Empty;
        public string CardKey { get; set; } = string.Empty;
        public int SourceIndex { get; set; } = -1;
    }

    /// <summary>
    /// Marker for the modal close button (X) of the card list modal
    /// </summary>
    public class CardListModalClose : IComponent
    {
        public Entity Owner { get; set; }
    }

    public class ClimbShopSlotAction : IComponent
    {
        public Entity Owner { get; set; }
        public int SlotIndex { get; set; } = -1;
    }

    /// <summary>
    /// Component for a simple in-game debug menu state
    /// </summary>
    public class DebugMenu : IComponent
    {
        public Entity Owner { get; set; }
        public bool IsOpen { get; set; } = false;
        public int ActiveTabIndex { get; set; } = 0;
        // Persisted panel position and init flag for draggable debug menu
        public int PanelX { get; set; } = 0;
        public int PanelY { get; set; } = 0;
        public bool IsPositionSet { get; set; } = false;
    }


    public enum FaceButton
    {
        B,
        X,
        Y,
        View,
        Start,
        LB,
        RB
    }

    public enum HotKeyPosition
    {
        Top,
        Right,
        Left,
        Below
    }

    /// <summary>
    /// Declares a controller hotkey binding for a UI element. The system will draw
    /// a face-button hint next to the element and trigger its action when pressed.
    /// </summary>
    public class HotKey : IComponent
    {
        public Entity Owner { get; set; }
        public FaceButton Button { get; set; } = FaceButton.Y;
        public bool RequiresHold { get; set; } = false;
        public float HoldDurationSeconds { get; set; } = 0.75f;
        public Entity ParentEntity { get; set; }
        public HotKeyPosition Position { get; set; } = HotKeyPosition.Below;
        public bool IsActive { get; set; } = true;
        public bool AllowWhenNonInteractable { get; set; } = false;
    }

    public class TutorialInteractionPermitted : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// State for a simple "How To Play" overlay.
    /// </summary>
    public class HowToPlayOverlay : IComponent
    {
        public Entity Owner { get; set; }
        public bool IsOpen { get; set; } = false;
        public float ScrollOffset { get; set; }
        public long OpenedInputSequence { get; set; }
        public string Text { get; set; } = "Each battle starts with the block phase. During this phase you can prevent damage and effects by assigning cards from you hand or equipment from your hero to the attack. When you block with white cards, you gain Temperance. When you block with red cards, you gain Courage. When you block with black cards, you gain no resource (higher block value instead). Above the enemy are little circles which represent the number of attacks they will make, and the ones below are for the enemy's next turn.\n\nAfter the enemy turn is your action phase. You have one Action Point (AP) per action phase that can be spent to play cards. Some cards require no AP. Some cards have a cost, denoted by the circles underneath their name. You must discard that many cards of the matching color from you hand to play these cards.\n\nYou have an equipped Sword, which you always have access to on your turn.\n\nAt the end of your turn, you will draw 4 cards, up to your maximum hand size of 5.";
    }
    
    /// <summary>
    /// Component for a generic dropdown UI control used by the debug menu
    /// </summary>
    public class UIDropdown : IComponent
    {
        public Entity Owner { get; set; }
        public List<string> Items { get; set; } = new();
        public int SelectedIndex { get; set; } = 0;
        public bool IsOpen { get; set; } = false;
        public int RowHeight { get; set; } = 28;
        public float TextScale { get; set; } = 0.55f;
    }
    
    /// <summary>
    /// State for the pay-cost overlay flow.
    /// </summary>
    public class PayCostOverlayState : IComponent
    {
        public Entity Owner { get; set; }
        public bool IsOpen { get; set; } = false;
        public Entity CardToPlay { get; set; }
        public List<string> RequiredCosts { get; set; } = new(); // "Red","White","Black","Any"
        public List<Entity> SelectedCards { get; set; } = new();
        public System.Collections.Generic.Dictionary<int, string> ConsumedCostByCardId { get; set; } = new();
        public float OpenElapsedSeconds { get; set; } = 0f;
        public int OriginalHandIndex { get; set; } = -1;
        // Tween state for staged card movement
        public Microsoft.Xna.Framework.Vector2 StagedStartPos { get; set; } = Vector2.Zero;
        public float StagedMoveElapsedSeconds { get; set; } = 0f;
        public bool IsReturning { get; set; } = false;
        public float ReturnElapsedSeconds { get; set; } = 0f;
        public float StagedStartRotation { get; set; } = 0f;
        public PayCostOverlayType Type { get; set; } = PayCostOverlayType.ColorDiscard;
    }

    /// <summary>
    /// Marker for the cancel button inside the pay-cost overlay.
    /// </summary>
    public class PayCostCancelButton : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Singleton cache holding the most recent cost payment data.
    /// Other systems can query this to see which cards were used to pay for the last played card.
    /// </summary>
    public class LastPaymentCache : IComponent
    {
        public Entity Owner { get; set; }
        public Entity CardPlayed { get; set; }
        public List<Entity> PaymentCards { get; set; } = new();
        public bool HasData { get; set; } = false;
    }

    /// <summary>
    /// Exact, short-lived play context attached while a card's OnPlay delegate resolves.
    /// </summary>
    public class CardPlayStatContext : IComponent
    {
        public Entity Owner { get; set; }
        public List<Entity> PaymentCards { get; set; } = new();
    }

    /// <summary>
    /// Singleton-like world component describing the current battlefield location.
    /// Other systems read this instead of subscribing to an event.
    /// </summary>
    public class Battlefield : IComponent
    {
        public Entity Owner { get; set; }
        public BattleLocation Location { get; set; } = BattleLocation.Desert;
    }


    // New phase model with main/sub-phases
    public enum MainPhase
    {
        StartBattle,
        EnemyTurn,
        PlayerTurn
    }

    public enum SubPhase
    {
        StartBattle,
        // Enemy sub-phases
        EnemyStart,
        PreBlock,
        Block,
        EnemyAttack,
        EnemyEnd,
        // Player sub-phases
        PlayerStart,
        Action,
        PlayerEnd,
        Pledge
    }

    public class PhaseState : IComponent
    {
        public Entity Owner { get; set; }
        public MainPhase Main { get; set; } = MainPhase.StartBattle;
        public SubPhase Sub { get; set; } = SubPhase.StartBattle;
        public int TurnNumber { get; set; } = 1; // enemy turn counter
        public bool DefeatPresentationActive { get; set; }
        public bool PendingBlockConfirm { get; set; }
    }

    /// <summary>
    /// Tracks battle metadata such as the current enemy turn number.
    /// TurnNumber starts at 1 and increments when transitioning to Block for a new enemy turn.
    /// </summary>
    public class BattleInfo : IComponent
    {
        public Entity Owner { get; set; }
        public int TurnNumber { get; set; } = 1;
    }

    /// <summary>
    /// Generic hit points component
    /// </summary>
    public class HP : IComponent
    {
        public Entity Owner { get; set; }
        public int Max { get; set; } = 40;
        public int Current { get; set; } = 40;
        public int UnscarredMax { get; set; }
    }

    /// <summary>
    /// Shared card geometry used by rendering, layout, highlights, and overlays.
    /// </summary>
    public class CardGeometrySettings : IComponent
    {
        public const int DefaultWidth = 268;
        public const int DefaultHeight = 377;
        public const int DefaultCornerRadius = 10;
        public const int DefaultOffsetYExtra = -98;
        public const int DefaultGap = -77;
        public const int DefaultHighlightBorderThickness = 5;

        public Entity Owner { get; set; }
        public int CardWidth { get; set; }
        public int CardHeight { get; set; }
        public int CardOffsetYExtra { get; set; }
        public int CardGap { get; set; }
        public int CardCornerRadius { get; set; }
        public int HighlightBorderThickness { get; set; }
    }

    /// <summary>
    /// Shared highlight visual settings for equipment panel items.
    /// </summary>
    public class EquipmentHighlightSettings : IComponent
    {
        public Entity Owner { get; set; }
        public int GlowLayers { get; set; } = 40;
        public float GlowSpread { get; set; } = 0.006f;
        public float GlowSpreadSpeed { get; set; } = 2.5f;
        public float GlowSpreadAmplitude { get; set; } = 0.22f;
        public float MaxAlpha { get; set; } = 0.06f;
        public float GlowPulseSpeed { get; set; } = 2.0f;
        public float GlowEasingPower { get; set; } = 0.8f;
        public float GlowMinIntensity { get; set; } = 0.30f;
        public float GlowMaxIntensity { get; set; } = 0.8f;
        public int CornerRadius { get; set; } = 23;
        public int HighlightBorderThickness { get; set; } = 4;
        public int GlowColorR { get; set; } = 196;
        public int GlowColorG { get; set; } = 30;
        public int GlowColorB { get; set; } = 58;
    }

    /// <summary>
    /// Shared highlight visual settings for can-play card glow in hand.
    /// </summary>
    public class CanPlayHighlightSettings : IComponent
    {
        public Entity Owner { get; set; }
        public int GlowLayers { get; set; } = 40;
        public float GlowSpread { get; set; } = 0.006f;
        public float GlowSpreadSpeed { get; set; } = 2.5f;
        public float GlowSpreadAmplitude { get; set; } = 0.22f;
        public float MaxAlpha { get; set; } = 0.06f;
        public float GlowPulseSpeed { get; set; } = 2.0f;
        public float GlowEasingPower { get; set; } = 0.8f;
        public float GlowMinIntensity { get; set; } = 0.30f;
        public float GlowMaxIntensity { get; set; } = 0.8f;
        public int GlowColorR { get; set; } = 255;
        public int GlowColorG { get; set; } = 215;
        public int GlowColorB { get; set; } = 0;
    }

    /// <summary>
    /// A single equipped medal. Multiple instances may exist, each referencing the owning player.
    /// </summary>
    public class EquippedMedal : IComponent, IDisposable
    {
        public Entity Owner { get; set; }
        public Entity EquippedOwner { get; set; } // player entity that owns this medal
        public MedalBase Medal { get; set; }

        public void Dispose()
        {
            Medal?.Dispose();
        }
    }

    /// <summary>
    /// Enumerates the zone a card currently belongs to. Useful for transitioning toward a fully component-driven zone model.
    /// </summary>
    public enum CardZoneType
    {
        DrawPile,
        Hand,
        HandStaged,
        CostSelected,
        DiscardPile,
        ExhaustPile,
        AssignedBlock
    }

} 

// New component used to animate assigned block cards flying to the discard pile
namespace Crusaders30XX.ECS.Components
{
    /// <summary>
    /// Zone state for an equipment entity: either visible in the left panel (Default)
    /// or currently assigned as block (hidden in panel, shown via AssignedBlockCardsDisplaySystem).
    /// </summary>
    public class EquipmentZone : IComponent
    {
        public Entity Owner { get; set; }
        public EquipmentZoneType Zone { get; set; } = EquipmentZoneType.Default;
        public Microsoft.Xna.Framework.Vector2 LastPanelCenter { get; set; } = Vector2.Zero;
    }

    public enum EquipmentZoneType
    {
        Default,
        AssignedBlock
    }

    /// <summary>
    /// Marker for a card currently animating from Hand to Discard due to being played.
    /// Hand layout/draw systems should ignore cards with this component until finalize.
    /// </summary>
    public class AnimatingHandToDiscard : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marker for a card currently animating from Hand to a destination zone (Discard or Exhaust).
    /// Stores the intended destination so the animation system knows where to finalize the card.
    /// Hand layout/draw systems should ignore cards with this component until finalize.
    /// </summary>
    public class AnimatingHandToZone : IComponent
    {
        public Entity Owner { get; set; }
        public CardZoneType Destination { get; set; }
    }

    /// <summary>
    /// Marker for a card currently animating from Hand to DrawPile.
    /// Hand layout/draw systems should ignore cards with this component until finalize.
    /// </summary>
    public class AnimatingHandToDrawPile : IComponent
    {
        public Entity Owner { get; set; }
    }

    public class CardToDiscardFlight : IComponent
    {
        public Entity Owner { get; set; }
        public Microsoft.Xna.Framework.Vector2 StartPos { get; set; }
        public Microsoft.Xna.Framework.Vector2 TargetPos { get; set; }
        public float StartDelaySeconds { get; set; }
        public float DurationSeconds { get; set; }
        public float ArcHeightPx { get; set; }
        public float ElapsedSeconds { get; set; }
        public bool Started { get; set; }
        public float StartScale { get; set; } = 0.35f;
        public float EndScale { get; set; } = 0.3f;
        public bool Completed { get; set; }
    }

    /// <summary>
    /// Marks a card that has been selected to pay costs; holds tween state and original hand index.
    /// </summary>
    public class SelectedForPayment : IComponent
    {
        public Entity Owner { get; set; }
        public int OriginalHandIndex { get; set; } = -1;
        public Microsoft.Xna.Framework.Vector2 OriginalHandPos { get; set; } = Vector2.Zero;
        public Microsoft.Xna.Framework.Vector2 StartPos { get; set; } = Vector2.Zero;
        public float StartScale { get; set; } = 1f;
        public Microsoft.Xna.Framework.Vector2 TargetPos { get; set; } = Vector2.Zero;
        public float TargetScale { get; set; } = 0.5f;
        public float MoveElapsed { get; set; } = 0f;
        public float MoveDuration { get; set; } = 0.25f;
        public bool IsReturning { get; set; } = false;
        public float ReturnElapsed { get; set; } = 0f;
        public float ReturnDuration { get; set; } = 0.12f;
    }

    public class AppliedPassives : IComponent
    {
        public Entity Owner { get; set; }
        public Dictionary<AppliedPassiveType, int> Passives { get; set; } = new Dictionary<AppliedPassiveType, int>();
    }

    public enum AppliedPassiveType
    {
        Burn,
        Power,
        DowseWithHolyWater,
        Slow,
        Aegis,
        Stun,
        Armor,
        Wounded,
        Webbing,
        Inferno,
        Aggression,
        Stealth,
        Poison,
        Shield,
        Guard,
        Fear,
        Siphon,
        Thorns,
        Bleed,
        Rage,
        Intellect,
        Intimidated,
        MindFog,
        Scar,
        Channel,
        Frostbite,
        Frozen,
        Windchill,
        SubZero,
        Enflamed,
        Shackled,
        Anathema,
        Silenced,
        Sealed,
        Plunder,
        Marksman,
        Sharpen,
        Might,
        Vigor,
        CarpeDiem,
        Galvanize
    }

    public class PassiveMeterComponent : IComponent
    {
        public Entity Owner { get; set; }
        public float CurrentValue { get; set; }
        public float MaxValue { get; set; }
        public PassiveMeterDirection Direction { get; set; } = PassiveMeterDirection.Countdown;
        public bool IsActive { get; set; } = true;
    }

    public enum PassiveMeterDirection { Countdown, FillUp }

    public class MarkedForReturnToDeck : IComponent
    {
        public Entity Owner { get; set; }
    }

    public class MarkedForBottomOfDrawPile : IComponent
    {
        public Entity Owner { get; set; }
    }

    public class RelentlessStrikeBattleState : IComponent
    {
        public Entity Owner { get; set; }
    }
    public class MarkedForExhaust : IComponent
    {
        public Entity Owner { get; set; }
    }
    public class MarkedForEndOfTurnDiscard : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marker for a card that should be hidden from the hand display during the pay-cost overlay.
    /// Set by PayCostOverlaySystem; consumed by HandDisplaySystem and CanPlayCardHighlightSystem.
    /// </summary>
    public class FilteredFromHand : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marks a card as pledged (Arsenal zone). Pledged cards:
    /// - Stay in hand ignoring max hand size
    /// - Can only be played during Action phase once CanPlay is true (unlocked at end of player turn)
    /// - Cannot be played the same turn they were pledged (CanPlay starts false)
    /// - Cannot be used to block or pay for other cards' costs
    /// - Are protected from intimidate, freeze, and discard effects
    /// </summary>
    public class Pledge : IComponent
    {
        public Entity Owner { get; set; }

        /// <summary>
        /// False when pledged this turn; PledgeManagementSystem sets true when the player turn ends.
        /// When true, the card may be played during a future action phase (subject to normal rules).
        /// </summary>
        public bool CanPlay { get; set; }
    }

    public class PledgeAvailabilityState : IComponent
    {
        public Entity Owner { get; set; }
        public bool PledgedThisActionPhase { get; set; }
    }

    /// <summary>
    /// Temporary marker for a card being hovered during the Action phase before pledging.
    /// Used by PledgeDisplaySystem to show the pledge icon as a preview.
    /// </summary>
    public class PledgePreview : IComponent
    {
        public Entity Owner { get; set; }
    }

    public class CannotBlockThisAttack : IComponent
    {
        public Entity Owner { get; set; }
        public string Reason { get; set; }
    }

    /// <summary>
    /// Marks a card as plundered by an enemy (e.g., Wyvern).
    /// The card is held by the enemy and can be rescued by dealing enough damage.
    /// </summary>
    public class Plundered : IComponent
    {
        public Entity Owner { get; set; }
        /// <summary>
        /// Damage threshold required to rescue this card (4-8).
        /// </summary>
        public int DamageThreshold { get; set; }
        /// <summary>
        /// Cumulative damage dealt to the enemy this turn.
        /// </summary>
        public int DamageDealt { get; set; }
    }

    /// <summary>
    /// Animation state for the plunder "snatch" effect when a card is stolen from the deck.
    /// </summary>
    public class PlunderSnatchFlight : IComponent
    {
        public Entity Owner { get; set; }
        public PlunderSnatchPhase Phase { get; set; } = PlunderSnatchPhase.Lift;
        public Vector2 OriginPos { get; set; }
        public Vector2 LiftedPos { get; set; }
        public Vector2 TargetPos { get; set; }
        public Vector2 CurrentPos { get; set; }
        public float PhaseElapsed { get; set; }
        public float CurrentScale { get; set; } = 1f;
        public float CurrentRotation { get; set; }
    }

    public enum PlunderSnatchPhase
    {
        Lift,
        Pause,
        Arc,
        Settle,
        Complete
    }

    /// <summary>
    /// Animation state for the plunder rescue effect when a card returns to the player's hand.
    /// </summary>
    public class PlunderRescueFlight : IComponent
    {
        public Entity Owner { get; set; }
        public PlunderRescuePhase Phase { get; set; } = PlunderRescuePhase.Arc;
        public Vector2 StartPos { get; set; }
        public Vector2 TargetPos { get; set; }
        public Vector2 CurrentPos { get; set; }
        public float PhaseElapsed { get; set; }
        public float CurrentScale { get; set; } = 0.55f;
        public float CurrentRotation { get; set; }
    }

    public enum PlunderRescuePhase
    {
        Arc,
        Settle,
        Complete
    }

    /// <summary>
    /// Marks a card as targeted by the Sniper's Marksman passive.
    /// Playing the card triggers the penalty effect.
    /// </summary>
    public class Marked : IComponent
    {
        public Entity Owner { get; set; }
        public MarkEffectType EffectType { get; set; }
    }

    public enum MarkEffectType
    {
        Lose1HP,
        Lose2HP,
        Gain2Bleed,
        Gain1Burn
    }
}
