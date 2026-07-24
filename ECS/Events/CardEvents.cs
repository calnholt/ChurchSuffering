using ChurchSuffering.ECS.Core;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using ChurchSuffering.ECS.Components;
using static ChurchSuffering.ECS.Systems.MustBeBlockedSystem;

namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Event published when a card should be rendered
    /// </summary>
    public class CardRenderEvent
    {
        public const int BaseRendererPriority = 100;

        public Entity Card { get; set; }
        public Vector2 Position { get; set; }
        public bool IsInHand { get; set; }
        /// <summary>
        /// Reuses a renderer-owned base-card surface when the card presentation is static.
        /// Highlights and status effects are still rendered live.
        /// </summary>
        public bool PreferCachedBase { get; set; } = false;
    }

    /// <summary>
    /// Event published to render a card at a position with a uniform scale factor
    /// </summary>
    public class CardRenderScaledEvent
    {
        public Entity Card { get; set; }
        public Vector2 Position { get; set; }
        public float Scale { get; set; } = 1f;
        public float Alpha { get; set; } = 1f;
        public float Rotation { get; set; } = 0f;
        /// <summary>
        /// Reuses a renderer-owned base-card surface when the card presentation is static.
        /// Highlights and status effects are still rendered live.
        /// </summary>
        public bool PreferCachedBase { get; set; } = false;
        /// <summary>
        /// Optional clip rectangle. If set, the card will be clipped to this region using scissor test.
        /// </summary>
        public Rectangle? ClipRect { get; set; }
    }

    /// <summary>
    /// Event to render a card at a position with a uniform scale factor, preserving the card's current rotation.
    /// </summary>
    public class CardRenderScaledRotatedEvent
    {
        public Entity Card { get; set; }
        public Vector2 Position { get; set; }
        public float Scale { get; set; } = 1f;
    }

    /// <summary>
    /// Unified highlight render event for both cards and equipment. Carries the UIElement and Transform for accurate bounds/rotation.
    /// </summary>
    public class HighlightRenderEvent
    {
        public Entity Entity { get; set; }
        public Transform Transform { get; set; }
        public UIElement UI { get; set; }
    }

    // Legacy events retained for compatibility. Prefer HighlightRenderEvent.
    public class CardHighlightRenderEvent { public Entity Card { get; set; } }
    public class EquipmentHighlightRenderEvent { public Entity Equipment { get; set; } }
    
    /// <summary>
    /// Event published when cards in hand need to be positioned
    /// </summary>
    public class HandLayoutEvent
    {
        public System.Collections.Generic.List<Entity> CardsInHand { get; set; }
        public float ScreenWidth { get; set; }
        public float ScreenHeight { get; set; }
    }

    /// <summary>
    /// Event published when a debug UI button is clicked
    /// </summary>
    public class DebugCommandEvent
    {
        public string Command { get; set; }
    }

    public class ApplyDualColorEvent
    {
        public Entity Card { get; set; }
        public CardData.CardColor SecondaryColor { get; set; }
    }

    /// <summary>
    /// Increases or decreases the player's Courage by Delta
    /// </summary>
    public class ModifyCourageRequestEvent
    {
        public int Delta { get; set; } = 0;
        public string Reason { get; set; } = "";
        public ModifyCourageType Type {get; set;}

    }
    public enum ModifyCourageType
    {
        Gain,
        Spent,
        Lost
    }
    public class ModifyCourageEvent
    {
        public int Delta { get; set; } = 0;
        public string Reason { get; set; } = "";
    }
    /// <summary>
    /// Sets the player's Courage
    /// </summary>
    public class SetCourageEvent
    {
        public int Amount { get; set; } = 0;
    }

    /// <summary>
    /// Event to request discarding the hand, shuffling, and drawing a fresh hand
    /// </summary>
    public class RedrawHandEvent
    {
        public int DrawCount { get; set; } = 4;
    }

    public class OpenCardListModalEvent
    {
        public string Title { get; set; }
        public List<Entity> Cards { get; set; }
        public bool IsSelectable { get; set; } = false;
        public string SelectionContext { get; set; } = string.Empty;
        public CardListModalMode Mode { get; set; } = CardListModalMode.Auto;
    }

    public static class CardListSelectionContexts
    {
        public const string ClimbReplacement = "climb.replacement";
    }

    public class CloseCardListModalEvent { }

    public class CardListModalCardSelectedEvent
    {
        public Entity Card { get; set; }
        public int CardIndex { get; set; } = -1;
        public string SelectionContext { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Event published when deck shuffling and drawing is requested
    /// </summary>
    public class DeckShuffleDrawEvent
    {
        public Entity Deck { get; set; }
        public int DrawCount { get; set; }
    }
        /// <summary>
    /// Event published when deck shuffling is requested
    /// </summary>
    public class DeckShuffleEvent
    {
        public Entity Deck { get; set; }
    }
    
    /// <summary>
    /// Event to reset the deck: move all cards from Hand and Discard back into the Draw pile,
    /// excluding the equipped weapon card.
    /// </summary>
    public class ResetDeckEvent
    {
        // If null, the first entity with a Deck will be used
        public Entity Deck { get; set; }
    }
    
    /// <summary>
    /// Event published when cards are drawn from deck
    /// </summary>
    public class CardsDrawnEvent
    {
        public Entity Deck { get; set; }
        public System.Collections.Generic.List<Entity> DrawnCards { get; set; }
    }

    /// <summary>
    /// Event published after the start-of-turn draw request sequence has attempted to resolve.
    /// </summary>
    public class StartOfTurnDrawResolvedEvent
    {
        public Entity Player { get; set; }
        public Entity Deck { get; set; }
        public SubPhase Phase { get; set; }
        public int RequestedDrawCount { get; set; }
    }

    /// <summary>
    /// Event to request drawing N cards from the current deck without reshuffling
    /// </summary>
    public class RequestDrawCardsEvent
    {
        public int Count { get; set; } = 1;
    }

    /// <summary>
    /// Event published when a draw attempt finds an empty draw pile.
    /// Handlers may synchronously refill the deck before the draw attempt returns.
    /// </summary>
    public class DrawPileEmptyEvent
    {
        public Entity Deck { get; set; }
    }

    /// <summary>
    /// Event to request shuffling random cards from discard back into the draw pile.
    /// Moves up to Amount non-weapon cards; partial if discard is short.
    /// </summary>
    public class ShuffleRandomCardsFromDiscardToDrawPileEvent
    {
        public Entity Deck { get; set; }
        public int Amount { get; set; } = 1;
    }

    /// <summary>
    /// Event to request drawing random cards from the discard pile into the hand.
    /// Moves up to Amount cards; partial if discard is short.
    /// </summary>
    public class DrawRandomCardFromDiscardEvent
    {
        public int Amount { get; set; } = 1;
    }

    /// <summary>
    /// Event to request discarding all cards in the hand
    /// </summary>
    public class DiscardAllCardsEvent
    {
    }

    /// <summary>
    /// Event to request moving a card into a destination zone. A single authoritative system should handle this.
    /// </summary>
    public class CardMoveRequested
    {
        public Entity Card { get; set; }
        public Entity Deck { get; set; }
        public CardZoneType Destination { get; set; }
        public string ContextId { get; set; }
        public string Reason { get; set; }
        public int? InsertIndex { get; set; }
    }

    /// <summary>
    /// Event published after a card has been moved between zones.
    /// </summary>
    public class CardMoved
    {
        public Entity Card { get; set; }
        public Entity Deck { get; set; }
        public CardZoneType From { get; set; }
        public CardZoneType To { get; set; }
        public string ContextId { get; set; }
    }

    /// <summary>
    /// Request an animated move of a played card from Hand to Discard. Zone mutation is deferred until finalize.
    /// </summary>
    public class PlayCardToDiscardAnimationRequested
    {
        public Entity Card { get; set; }
        public Entity Deck { get; set; }
        public string ContextId { get; set; }
    }

    /// <summary>
    /// Request an animated move of a card from Hand to DrawPile. Zone mutation is deferred until finalize.
    /// </summary>
    public class PlayCardToDrawPileAnimationRequested
    {
        public Entity Card { get; set; }
        public Entity Deck { get; set; }
        public string ContextId { get; set; }
    }

    /// <summary>
    /// Request to finalize a deferred CardMove by mutating zones and publishing CardMoved.
    /// </summary>
    public class CardMoveFinalizeRequested
    {
        public Entity Card { get; set; }
        public Entity Deck { get; set; }
        public CardZoneType Destination { get; set; }
        public string ContextId { get; set; }
    }

    /// <summary>
    /// Request to play a card during the Action phase.
    /// The handling system should validate phase/zone and resolve effects.
    /// </summary>
    public class PlayCardRequested
    {
        public Entity Card { get; set; }
        // When true, downstream systems should not prompt for resource/cost payment.
        public bool CostsPaid { get; set; } = false;
        public List<Entity> PaymentCards { get; set; } = new();
    }

    /// <summary>
    /// Increase or decrease the player's Action Points (AP) by Delta.
    /// Clamped at zero; no explicit max is enforced here.
    /// </summary>
    public class ModifyActionPointsEvent
    {
        public int Delta { get; set; } = 0;
    }

    public class SetActionPointsEvent
    {
        public int Amount { get; set; } = 0;
    }

    /// <summary>
    /// Opens the pay-cost overlay for a specific card with required costs.
    /// </summary>
    public class OpenPayCostOverlayEvent
    {
        public Entity CardToPlay { get; set; }
        public System.Collections.Generic.List<string> RequiredCosts { get; set; } = new(); // values: "Red","White","Black","Any"
        public PayCostOverlayType Type { get; set; } = PayCostOverlayType.ColorDiscard;
    }

    public enum PayCostOverlayType
    {
        ColorDiscard,
        SelectOneCard,
    }

    /// <summary>
    /// Closes the pay-cost overlay if open.
    /// </summary>
    public class ClosePayCostOverlayEvent { }

    /// <summary>
    /// Emitted by input when a card is clicked while the pay-cost overlay is open.
    /// </summary>
    public class PayCostCandidateClicked
    {
        public Entity Card { get; set; }
    }

    /// <summary>
    /// User requested to cancel paying costs and abort the play.
    /// </summary>
    public class PayCostCancelRequested { }

    /// <summary>
    /// Emitted when cost payment has been satisfied and the selected cards have been discarded.
    /// Re-dispatches play of the original card with CostsPaid=true.
    /// </summary>
    public class PayCostSatisfied
    {
        public Entity CardToPlay { get; set; }
        public System.Collections.Generic.List<Entity> PaymentCards { get; set; } = new();
    }

    /// <summary>
    /// Emitted when the player requests to pledge a card during the Action phase.
    /// </summary>
    public class PledgeCardRequested
    {
        public Entity Card { get; set; }
    }

    /// <summary>
    /// Request that the pledge owner system applies pledge state to a card.
    /// Used by card effects that pledge directly rather than by player input.
    /// </summary>
    public class ApplyPledgeToCardRequested
    {
        public Entity Card { get; set; }
        public bool MarkPledgedThisActionPhase { get; set; } = true;
    }

    /// <summary>
    /// Request that the pledge owner system move a random discard card to hand and pledge it.
    /// </summary>
    public class PledgeRandomCardFromDiscardRequested { }

    /// <summary>
    /// Request that the pledge owner system removes pledge state from a card.
    /// </summary>
    public class RemovePledgeFromCardRequested
    {
        public Entity Card { get; set; }
    }

    /// <summary>
    /// Emitted when the player adds a pledge to a card.
    /// </summary>
    public class PledgeAddedEvent
    {
        public Entity Card { get; set; }
    }

    /// <summary>
    /// UI message indicating why a card cannot be played.
    /// Systems can subscribe to display a transient on-screen notification.
    /// </summary>
    public class CantPlayCardMessage
    {
        public string Message { get; set; } = "";
    }

    /// <summary>
    /// Emitted when an equipment's ability triggers, for UI pulse/feedback.
    /// </summary>
    public class EquipmentAbilityTriggered
    {
        public Entity Equipment { get; set; }
        public string EquipmentId { get; set; }
    }

    /// <summary>
    /// Emitted when an equipment use should be counted (e.g., after resolving attack where it was used to block)
    /// </summary>
    public class EquipmentUseResolved
    {
        public string EquipmentId { get; set; }
        public int Delta { get; set; } = 1;
    }

    /// <summary>
    /// Emitted after equipment remaining uses change so the loadout save can stay in sync.
    /// </summary>
    public class EquipmentRemainingUsesChanged
    {
        public string EquipmentId { get; set; }
        public EquipmentSlot Slot { get; set; }
        public int RemainingUses { get; set; }
    }

    /// <summary>
    /// Emitted when an equipment is destroyed by activation.
    /// </summary>
    public class EquipmentDestroyed
    {
        public string EquipmentId { get; set; }
    }

    /// <summary>
    /// Emitted when a player activates an equipment's Activate ability this turn.
    /// </summary>
    public class EquipmentActivated
    {
        public string EquipmentId { get; set; }
    }

    /// <summary>
    /// Emitted when a medal triggers so UI can animate the corresponding icon.
    /// </summary>
    public class MedalTriggered
    {
        public Entity MedalEntity { get; set; }
        public string MedalId { get; set; }
    }

    /// <summary>
    /// Supported music tracks for background playback.
    /// </summary>
    public enum MusicTrack
    {
        None = 0,
        Menu = 1,
        DesertBattle = 2,
        Customize = 3,
        Map = 4,
        QuestComplete = 5,
        Achievements = 6,
        FrozenBattle = 7,
        TundraBattle = 8,
        JungleBattle = 9,
        TheGateBattle = 10,
        Climb = 11,
        VolcanoBattle = 12,
        GothicBattle = 13,
        Title = 14,
        WayStation = 15,
    }

    /// <summary>
    /// Request to change currently playing music.
    /// </summary>
    public class ChangeMusicTrack
    {
        public MusicTrack Track { get; set; } = MusicTrack.None;
        public float Volume { get; set; } = 0.2f; // 0..1
        public bool Loop { get; set; } = true;
        public bool Fade { get; set; } = true;
        public float FadeSeconds { get; set; } = 0.5f;
    }

    /// <summary>
    /// Request to stop current music playback (with optional fade out).
    /// </summary>
    public class StopMusic
    {
        public bool Fade { get; set; } = true;
        public float FadeSeconds { get; set; } = 0.5f;
    }

    /// <summary>
    /// Supported sound effect tracks for playback.
    /// </summary>
    public enum SfxTrack
    {
        None = 0,
        SwordAttack = 1,
        SwordImpact = 2,
        SwordUnsheath = 3,
        SwordWhoosh = 4,
        Equip = 5,
        BashShield = 6,
        CardHover = 7,
        ApplyCard = 8,
        CoinBag = 9,
        CashRegister = 10,
        Firebuff = 11,
        BagHandle = 12,
        Interface = 13,
        Confirm = 14,
        PhaseChange = 15,
        Transition = 16,
        Prayer = 17,
        GainAegis = 18,
        EnemyAttackIntro = 19,
        ActiveDialogue = 20,
        ApplyBrittle = 21,
        ApplyCurse = 22,
        ApplyScorched = 23,
        ApplyThorns = 24,
        ApplyFrozen = 25,
        ClimbMenuEnter = 26,
        ClimbWidgetLeave = 27,
        OpenInventory = 28,
        CloseInventory = 29,
        DrawCard = 30,
        EnemyDeath = 31,
        GainCourage = 32,
        GainTemperance = 33,
        MedalActivated = 34,
        PledgeCard = 35,
        Purchase = 36,
        SaintInfo = 37,
        Temperance = 38,
        TakeReward = 39,
        UpgradeCard = 40,
        BoosterPackReveal = 41,
        DeckShuffle = 42,
		ShieldBlock = 43,
		GemDrop = 44,
		ClimbPointsTier = 45,
		ClimbPointsTotal = 46,
		GameOver = 47,
		AchievementReveal = 48,
		BoosterPackCharge = 49,
		LightSpellCast = 50,
		RaySpellCast = 51,
		RaySpellImpact = 52,
		EatenBite = 53,
		EarthSpell = 54,
		HealStinger = 55,
		IceSpellLayer = 56,
		FireSpellLayer = 57,
		BellLowMagical = 58,
		ChoirChord = 59,
		ClimbWhoosh = 60,
    }

    /// <summary>
    /// Request to play a sound effect.
    /// </summary>
    public class PlaySfxEvent
    {
        public SfxTrack Track { get; set; } = SfxTrack.None;
        public float Volume { get; set; } = 1.0f; // 0..1
        public float Pitch { get; set; } = 0.0f; // -1..1
        public float Pan { get; set; } = 0.0f; // -1..1
        public bool Loop { get; set; } = false;
    }

    /// <summary>
    /// Request to stop a looping sound effect.
    /// </summary>
    public class StopSfxEvent
    {
        public SfxTrack Track { get; set; } = SfxTrack.None;
    }

    /// <summary>
    /// Event published when a hotkey hold-to-activate completes
    /// </summary>
    public class HotKeyHoldCompletedEvent
    {
        public Entity Entity { get; set; }
    }

    public class UnassignCardAsBlockRequested
    {
        public Entity CardEntity;
    }

    public class AssignCardAsBlockRequested
    {
        public Entity Card { get; set; }
    }

    public class AssignEquipmentAsBlockRequested
    {
        public Entity EquipmentEntity;
    }

    public class ActivateEquipmentRequested
    {
        public Entity EquipmentEntity;
    }
    public class QuestSelectRequested 
    { 
        public Entity Entity;
    }

    /// <summary>
    /// Request to mill the top card of the player's draw pile with a flyout animation.
    /// If Deck is null, the first entity with a Deck will be used.
    /// </summary>
    public class MillCardEvent
    {
    }

    /// <summary>
    /// Coordination event: request that the deck manager remove the top card of DrawPile
    /// but do not place it in another zone yet (animation will run first).
    /// </summary>
    public class RemoveTopCardFromDrawPileRequested
    {
    }

    /// <summary>
    /// Response event: published after the top card has been removed from DrawPile.
    /// Carries the removed card entity for animation systems.
    /// </summary>
    public class TopCardRemovedForMillEvent
    {
        public Entity Deck { get; set; }
        public Entity Card { get; set; }
    }

    public class EndTurnDisplayEvent
    {
        public bool ShowButton;
    }

    /// <summary>
    /// Event published each frame indicating which card in hand is currently hovered.
    /// Card is null if no card is hovered.
    /// </summary>
    public class CardInHandHoveredEvent
    {
        public Entity Card { get; set; }
    }

    public class IntimidateEvent
    {
        public int Amount { get; set; }
    }

    /// <summary>
    /// Emitted when a specific card becomes intimidated.
    /// </summary>
    public class CardIntimidatedEvent
    {
        public Entity Card { get; set; }
    }

    public class ShackleEvent
    {
        public int Amount { get; set; }
    }

    public class MustBeBlockedEvent
    {
        public int Threshold { get; set; }
        public MustBeBlockedByType Type { get; set; }
    }

    public class MarkedForSpecificDiscardEvent
    {
        public int Amount { get; set; }
    }

    public class DiscardMarkedForSpecificDiscardEvent
    {
    }

    public class ApplyCardApplicationEvent
    {
        public Entity Card { get; set; }
        public int Amount { get; set; }
        /// <summary>Number of stacks applied to each selected card. Used by stacked restrictions such as Sealed.</summary>
        public int StacksPerCard { get; set; } = 1;
        public CardApplicationType Type { get; set; }
        public CardApplicationTarget Target { get; set; }
    }

    public class CardRestrictionMutationAnimationRequested
    {
        public Entity TargetCard { get; set; }
        public int StacksPerCard { get; set; } = 1;
        public CardApplicationType Type { get; set; }
    }

    public class RemoveCardApplication
    {
        public Entity Card { get; set; }
        public CardApplicationType Type { get; set; }
    }

    public class RemoveCardApplications
    {
        public int Amount { get; set; }
        public CardApplicationType Type { get; set; }
        public CardApplicationTarget Target { get; set; }
    }

    public enum CardApplicationType
    {
        Frozen,
        Brittle,
        Scorched,
        Thorned,
        Colorless,
        Sealed,
        Cursed,
        Hex,
    }

    public class RemoveRandomCardEvent
    {
        public int Amount { get; set; }
    }

    public class ApplyRecoilEvent
    {
        public int Amount { get; set; }
    }

    public enum CardApplicationTarget
    {
        HandAndDrawPile,
        TopXCards,
        DrawPile,
        DrawPileAndDiscard,
        Hand,
        Deck,
    }

    public class CardPlayedEvent
    {
        public Entity Card { get; set; }
        public int VigorStacksAtPlay { get; set; }
        public bool PlayedAsCurse { get; set; }
    }

    public class CardUpgradeConfirmedEvent
    {
        public string CardId { get; set; }
    }

    public class CardBlockedEvent
    {
        public Entity Card { get; set; }
    }

    public class CardDiscardedForCostEvent
    {
        public Entity Card { get; set; }
    }

    public class EndTurnRequested { }

    public class ConfirmBlocksRequested { }

} 
