using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Dialog;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Singletons;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	public enum SceneId
	{
		TitleMenu,
		WayStation,
		Climb,
		Battle,
		Achievement,
		Snapshot,
		None
	}

	public class SceneState : IComponent
	{
		public Entity Owner { get; set; }
		public SceneId Current { get; set; } = SceneId.TitleMenu;
	}

	public class GameOverOverlayState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsActive { get; set; }
		public float Elapsed { get; set; }
		public bool SceneSwitched { get; set; }
	}

	public enum ModalAnimationPhase
	{
		Hidden,
		Entering,
		Visible,
		Exiting
	}

	public class ModalAnimation : IComponent
	{
		public Entity Owner { get; set; }
		public ModalAnimationPhase Phase { get; set; } = ModalAnimationPhase.Hidden;
		public bool RequestedVisible { get; set; }
		public float ElapsedSeconds { get; set; }
		public float EnterDurationSeconds { get; set; } = 0.28f;
		public float ExitDurationSeconds { get; set; } = 0.28f;
		public float ShellDurationSeconds { get; set; } = 0.22f;
		public float StartScale { get; set; } = 0.75f;
		public float StartBrightness { get; set; } = 1.5f;
		public float DimExitHoldPercent { get; set; } = 0.58f;
		public float VisibleShadowAlpha { get; set; } = 0.65f;
		public string InputContextId { get; set; } = string.Empty;
		public int ExitSequence { get; set; }
		public int CompletedExitSequence { get; set; }

		public bool IsHidden => Phase == ModalAnimationPhase.Hidden;
		public bool IsAnimating => Phase == ModalAnimationPhase.Entering || Phase == ModalAnimationPhase.Exiting;
	}

	public class ModalInputSuppression : IComponent
	{
		public Entity Owner { get; set; }
		public string ContextId { get; set; } = string.Empty;
	}

	public class WayStationClimbModalRoot : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationClimbModalPanel : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationClimbModalCloseButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationClimbModalWeaponChoice : IComponent
	{
		public Entity Owner { get; set; }
		public StartingWeapon Weapon { get; set; }
	}

	public class WayStationClimbModalDifficultyChoice : IComponent
	{
		public Entity Owner { get; set; }
		public RunDifficulty Difficulty { get; set; }
	}

	public class WayStationClimbModalDepartButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationSaintsMedalsModalRoot : IComponent
	{
		public Entity Owner { get; set; }
		public string SelectedMedalId { get; set; } = string.Empty;
		public int ListScrollOffset { get; set; }
		public int DetailScrollOffset { get; set; }
	}

	public class WayStationSaintsMedalsModalPanel : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationSaintsMedalsModalCloseButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class WayStationSaintsMedalsModalTile : IComponent
	{
		public Entity Owner { get; set; }
		public string MedalId { get; set; } = string.Empty;
	}

	public enum WayStationArrivalKind
	{
		Initial,
		SameVisit,
		ReturnedFromFailedClimb,
		ReturnedFromAbandonedClimb,
		ReturnedFromCompletedClimb,
	}

	public class WayStationArrivalContextState : IComponent
	{
		public Entity Owner { get; set; }
		public WayStationArrivalKind Kind { get; set; } = WayStationArrivalKind.Initial;
		public bool HasPendingContext { get; set; }
	}

	public class WayStationDialoguePoiAction : IComponent
	{
		public Entity Owner { get; set; }
		public string OfferId { get; set; } = string.Empty;
	}

	/// <summary>
	/// Marks which scene owns an entity for automatic cleanup on scene transitions.
	/// </summary>
	public class OwnedByScene : IComponent
	{
		public Entity Owner { get; set; }
		public SceneId Scene { get; set; } = SceneId.None;
	}

	/// <summary>
	/// Marker component for entities that should persist across scene transitions.
	/// </summary>
	public class DontDestroyOnLoad : IComponent
	{
		public Entity Owner { get; set; }
		public SceneId Scene { get; set; } = SceneId.None;
	}
	public class DontDestroyOnReload : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Holds a queue of enemy ids selected from the menu to spawn when battle starts.
	/// </summary>
	public class QueuedEvents : IComponent
	{
		public Entity Owner { get; set; }
		public List<QueuedEvent> Events { get; set; } = new List<QueuedEvent>();
		public int CurrentIndex = -1;
		public bool IsFirst = false;
		public bool IsClimbEncounter { get; set; }
		public string ClimbEncounterSlotId { get; set; } = string.Empty;
		public BattleLocation? BattleLocation { get; set; }
		// Encounter context for dialog lookup
		public string LocationId { get; set; } = string.Empty;
		public int QuestIndex { get; set; } = 0;
	}

	public class QueuedEvent 
	{
		public string EventId;
		public QueuedEventType EventType = QueuedEventType.Enemy;
		public List<EnemyModification> Modifications { get; set; } = new List<EnemyModification>();
	}

	public enum QueuedEventType
	{
		Enemy,
		Event,
		Shop,
		Church
	}

	public class EntityListOverlay : IComponent
	{
		public Crusaders30XX.ECS.Core.Entity Owner { get; set; }
		public bool IsOpen { get; set; } = false;
		public int PanelX { get; set; } = 40;
		public int PanelY { get; set; } = 40;
		public int PanelWidth { get; set; } = 520;
		public int PanelHeight { get; set; } = 600;
		public float TextScale { get; set; } = 0.15f;
		public int RowHeight { get; set; } = 24;
		public int Padding { get; set; } = 8;
		public float ScrollOffset { get; set; } = 0f;
	}

	/// <summary>
	/// Encounter selection overlay state for the legacy WorldMap scene.
	/// </summary>
	public class QuestSelectState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsOpen { get; set; } = false;
		public string LocationId { get; set; } = string.Empty;
		public int SelectedQuestIndex { get; set; } = 0;
	}

	/// <summary>
	/// Marker for quest selection left arrow UI.
	/// </summary>
	public class QuestArrowLeft : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Marker for quest selection right arrow UI.
	/// </summary>
	public class QuestArrowRight : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Marker for quest selection back button UI.
	/// </summary>
	public class QuestBackButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Marker for location select "Customize" button UI.
	/// </summary>
	public class LocationCustomizeButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Marker for quest selection start area (click to begin quest).
	/// </summary>
	public class QuestStartArea : IComponent
	{
		public Entity Owner { get; set; }
	}

	public enum DialogPhase { Idle, Intro, Active, Outro }

	/// <summary>
	/// Overlay state for battle dialog sequences.
	/// </summary>
	public class DialogOverlayState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsActive { get; set; } = false;
		public DialogPhase Phase { get; set; } = DialogPhase.Idle;
		public List<DialogLine> Lines { get; set; } = new List<DialogLine>();
		public int Index { get; set; } = 0;
		public bool IsCorrelatedSequence { get; set; }
		public bool BackgroundOnly { get; set; }
		public string DefinitionId { get; set; } = string.Empty;
		public string SegmentId { get; set; } = string.Empty;
		public System.Guid RequestId { get; set; }
	}

		/// <summary>
		/// Overlay state for the encounter reward modal shown after the last battle in an encounter.
		/// </summary>
		public class NarrativeEventOverlayState : IComponent
		{
			public Entity Owner { get; set; }
			public bool IsOpen { get; set; } = false;
			public string RunMapEventId { get; set; } = string.Empty;
			public string EventTypeId { get; set; } = string.Empty;
			public string ResolutionContextId { get; set; } = string.Empty;
		}

		public class QuestRewardOverlayState : IComponent
		{
			public Entity Owner { get; set; }
			public bool IsOpen { get; set; } = false;
			public string Message { get; set; } = "Encounter Complete";
			public string TitleLine1 { get; set; } = "Encounter";
			public string TitleLine2 { get; set; } = "Complete!";
			public int RewardGold { get; set; } = 0;
			public bool HasCardReward { get; set; } = false;
			public string RewardCardKey { get; set; } = string.Empty;
			public List<string> RewardCardKeys { get; set; } = new List<string>();
			public DeckRewardOfferSave DeckRewardOffer { get; set; }
			public bool HasDeckRewardOffer => DeckRewardOffer?.options != null && DeckRewardOffer.options.Count > 0;
			public bool HasMedalReward { get; set; } = false;
			public string RewardMedalId { get; set; } = string.Empty;
			public bool HasEquipmentReward { get; set; } = false;
			public string RewardEquipmentId { get; set; } = string.Empty;
			public bool IsEncounterReward { get; set; } = false;
			public ClimbResourceSave ClimbResources { get; set; }
			public bool DismissToLocation { get; set; } = true;
			public SceneId DismissScene { get; set; } = SceneId.Climb;
			public bool DismissInProgress { get; set; } = false;
			public bool CardSelectionInProgress { get; set; } = false;
			public int SelectedRewardCardIndex { get; set; } = -1;
			public float CardSelectionElapsedSeconds { get; set; } = 0f;
			public bool DeckColumnSelectionInProgress { get; set; } = false;
			public int SelectedDeckRewardColumnIndex { get; set; } = -1;
			public float DeckColumnSelectionElapsedSeconds { get; set; } = 0f;
		}

		public enum BoosterPackLootKind
		{
			Card,
			Medal,
			Equipment,
		}

		public enum BoosterPackOpeningPhase
		{
			Summon,
			Idle,
			Charge,
			Crack,
			Rupture,
			Showcase,
			Ready,
		}

		public class BoosterPackLootPreview
		{
			public BoosterPackLootKind Kind { get; set; }
			public string Id { get; set; } = string.Empty;
			public CardData.CardColor CardColor { get; set; } = CardData.CardColor.White;
			public Entity PreviewEntity { get; set; }
			public float RevealDelaySeconds { get; set; }
		}

		public class BoosterPackOpeningOverlayState : IComponent
		{
			public Entity Owner { get; set; }
			public bool IsOpen { get; set; }
			public bool CanDismiss { get; set; }
			public float ElapsedSeconds { get; set; }
			public float PreviousElapsedSeconds { get; set; }
			public float NextChargeParticleSeconds { get; set; }
			public BoosterPackOpeningPhase Phase { get; set; }
			public bool IsAuthoritativePack { get; set; }
			public List<BoosterPackLootPreview> Loot { get; set; } = new();
			public List<BoosterPackParticleFx> Particles { get; set; } = new();
			public List<BoosterPackShardFx> Shards { get; set; } = new();
		}

		public class BoosterPackParticleFx
		{
			public Vector2 Start { get; set; }
			public Vector2 Delta { get; set; }
			public float Width { get; set; }
			public float Length { get; set; }
			public Color Color { get; set; }
			public float StartSeconds { get; set; }
			public float DurationSeconds { get; set; }
			public bool IsInward { get; set; }
		}

		public class BoosterPackShardFx
		{
			public Vector2 Start { get; set; }
			public Vector2 Delta { get; set; }
			public float Width { get; set; }
			public float Height { get; set; }
			public float RotationRadians { get; set; }
			public float StartSeconds { get; set; }
			public float DurationSeconds { get; set; }
		}

	public class PendingQuestDialog : IComponent
	{
		public Entity Owner { get; set; }
		public string DialogId { get; set; } = string.Empty;
		public string SegmentId { get; set; } = string.Empty;
		public System.Guid RequestId { get; set; }
		public bool WillShowDialog { get; set; } = false;
	}

	/// <summary>
	/// Anchor entity for the Ambush intro text; position is center of the text.
	/// </summary>
	public class AmbushTextAnchor : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Anchor entity for the Ambush timer bar; position is the center of the bar.
	/// </summary>
	public class AmbushTimerAnchor : IComponent
	{
		public Entity Owner { get; set; }
	}
}
