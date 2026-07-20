using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	public enum ClimbSlotKind
	{
		Shop,
		Encounter,
		Event,
	}

	public enum ClimbColumnKind
	{
		Shop,
		Encounter,
		Event,
	}

	public enum ClimbColumnTransitionPhase
	{
		Idle,
		EnteringEvents,
		LeavingEvents,
	}

	public enum ClimbSlotRefreshPhase
	{
		Idle,
		Animating,
	}

	public enum ClimbResourceType
	{
		Red,
		White,
		Black,
	}

	public enum ClimbV2SectionKind
	{
		Shop,
		Encounter,
		Event,
	}

	public enum ClimbChoiceRailOutcomeKind
	{
		None,
		Price,
		Reward,
	}

	public enum ClimbV2MotionPhase
	{
		Settled,
		AshesExiting,
		Entering,
		Purchasing,
		AwaitingPurchaseReconciliation,
	}

	public class ClimbSceneRoot : IComponent
	{
		public Entity Owner { get; set; }
	}

	public sealed class ClimbV2SceneState : IComponent
	{
		public Entity Owner { get; set; }
		public bool FreshEntranceRequested { get; set; }
		public bool FreshEntranceStarted { get; set; }
		public bool IsInputSuppressed { get; set; }
	}

	public sealed class ClimbV2TitlePresentation : IComponent
	{
		public Entity Owner { get; set; }
	}

	public sealed class DistanceClimbedTimelinePresentation : IComponent
	{
		public Entity Owner { get; set; }
	}

	public sealed class PlayerResourcesPresentation : IComponent
	{
		public Entity Owner { get; set; }
	}

	public sealed class ClimbOverviewButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	public sealed class ClimbV2SectionPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public ClimbV2SectionKind Kind { get; set; }
	}

	public sealed class ClimbShopItemPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public string ItemKind { get; set; } = string.Empty;
		public string ItemAsset { get; set; } = string.Empty;
		public string TooltipFingerprint { get; set; } = string.Empty;
	}

	public sealed class ClimbEncounterPresentation : IComponent
	{
		public Entity Owner { get; set; }
	}

	public sealed class ClimbEventPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public string Description { get; set; } = string.Empty;
	}

	public sealed class ClimbChoiceRailPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public string SourceSlotId { get; set; } = string.Empty;
		public ClimbChoiceRailOutcomeKind OutcomeKind { get; set; }
		public ClimbResourceSave Resources { get; set; } = new();
		public int Time { get; set; }
		public bool ShowTime { get; set; } = true;
		public int Stays { get; set; } = -1;
		public int ProjectedStays { get; set; } = -1;
		public float Opacity { get; set; } = 1f;
	}

	public sealed class ClimbChoiceExpiryPreviewPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsActive { get; set; }
		public float PulseElapsedSeconds { get; set; }
		public float Strength { get; set; }
		public float OpacityMultiplier { get; set; } = 1f;
		public float Grayscale { get; set; }
	}

	public sealed class ClimbV2ChoiceMotion : IComponent
	{
		public Entity Owner { get; set; }
		public ClimbV2MotionPhase Phase { get; set; } = ClimbV2MotionPhase.Settled;
		public float ElapsedSeconds { get; set; }
		public float DelaySeconds { get; set; }
		public Vector2 Offset { get; set; }
		public float Opacity { get; set; } = 1f;
		public float Brightness { get; set; } = 1f;
		public float Grayscale { get; set; }
		public float Sepia { get; set; }
		public float Blur { get; set; }
		public string Fingerprint { get; set; } = string.Empty;
		public bool Initialized { get; set; }
	}

	public sealed class ClimbV2InputSuppression : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class ClimbPreviewState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsActive { get; set; }
		public string SourceSlotId { get; set; } = string.Empty;
		public int Amount { get; set; }
		public int ProjectedUsedTime { get; set; }
		public int ProjectedRemainingTime { get; set; }
		public ClimbResourceSave ProjectedResources { get; set; } = new ClimbResourceSave();
		public HashSet<string> WouldVanishSlotIds { get; set; } = new HashSet<string>();
		public HashSet<string> AffordableShopSlotIds { get; set; } = new HashSet<string>();

		public void Clear()
		{
			IsActive = false;
			SourceSlotId = string.Empty;
			Amount = 0;
			ProjectedUsedTime = 0;
			ProjectedRemainingTime = 0;
			ProjectedResources = new ClimbResourceSave();
			WouldVanishSlotIds.Clear();
			AffordableShopSlotIds.Clear();
		}
	}

	public class ClimbColumnTransitionState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsInitialized { get; set; }
		public bool CurrentShowEvents { get; set; }
		public bool TargetShowEvents { get; set; }
		public ClimbColumnTransitionPhase Phase { get; set; } = ClimbColumnTransitionPhase.Idle;
		public float ElapsedSeconds { get; set; }
		public List<ClimbEventSlotSave> CachedEventSlots { get; set; } = new List<ClimbEventSlotSave>();

		public bool IsAnimating => Phase == ClimbColumnTransitionPhase.EnteringEvents
			|| Phase == ClimbColumnTransitionPhase.LeavingEvents;
	}

	public class ClimbColumnTransitionInputSuppression : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class ClimbSlotRefreshTransitionState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsInitialized { get; set; }
		public ClimbSlotRefreshPhase Phase { get; set; } = ClimbSlotRefreshPhase.Idle;
		public float ElapsedSeconds { get; set; }
		public List<ClimbSlotRefreshJob> Jobs { get; set; } = new List<ClimbSlotRefreshJob>();
		public List<ClimbSlotVisualSnapshot> PreviousSnapshots { get; set; } = new List<ClimbSlotVisualSnapshot>();

		public bool IsAnimating => Phase == ClimbSlotRefreshPhase.Animating && Jobs.Count > 0;
	}

	public class ClimbSlotRefreshJob
	{
		public ClimbSlotKind Kind { get; set; }
		public int SlotIndex { get; set; } = -1;
		public float StaggerSeconds { get; set; }
		public ClimbSlotVisualSnapshot Outgoing { get; set; }
		public ClimbSlotVisualSnapshot Incoming { get; set; }

		public bool HasOutgoing => Outgoing?.IsVisible == true;
		public bool HasIncoming => Incoming?.IsVisible == true;
	}

	public class ClimbSlotVisualSnapshot
	{
		public ClimbSlotKind Kind { get; set; }
		public int SlotIndex { get; set; } = -1;
		public string SlotId { get; set; } = string.Empty;
		public string Fingerprint { get; set; } = string.Empty;
		public bool IsVisible { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
		public string Meta { get; set; } = string.Empty;
		public int GeneratedAtTime { get; set; }
		public int Duration { get; set; }
		public int TimeCost { get; set; }
		public ClimbResourceSave Cost { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public ClimbResourceSave Reward { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public bool IsSold { get; set; }
		public bool IsCompleted { get; set; }
		public bool IsUnavailable { get; set; }
		public bool IsAffordable { get; set; } = true;
		public bool IsFinal { get; set; }
		public BattleLocation BattleLocation { get; set; } = BattleLocation.Desert;
		public string PortraitAsset { get; set; } = string.Empty;
		public ClimbEventKind EventKind { get; set; }
		public string GainLine1 { get; set; } = string.Empty;
		public string GainLine2 { get; set; } = string.Empty;
		public float Opacity { get; set; } = 1f;
	}

	public class ClimbHeaderElement : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class ClimbTimelineElement : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class ClimbResourceBarElement : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class ClimbLoadoutButton : IComponent
	{
		public Entity Owner { get; set; }
	}

	public class ClimbColumnPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public ClimbColumnKind Kind { get; set; }
		public string Title { get; set; } = string.Empty;
		public string Subtitle { get; set; } = string.Empty;
		public Rectangle InnerBounds { get; set; }
		public bool IsVisible { get; set; } = true;
		public float Opacity { get; set; } = 1f;
	}

	public class ClimbSlotPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public ClimbSlotKind Kind { get; set; }
		public string SlotId { get; set; } = string.Empty;
		public int SlotIndex { get; set; } = -1;
		public string Title { get; set; } = string.Empty;
		public string Label { get; set; } = string.Empty;
		public string Meta { get; set; } = string.Empty;
		public int GeneratedAtTime { get; set; }
		public int Duration { get; set; }
		public int TimeCost { get; set; }
		public ClimbResourceSave Cost { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public ClimbResourceSave Reward { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public bool IsSold { get; set; }
		public bool IsCompleted { get; set; }
		public bool IsUnavailable { get; set; }
		public bool IsAffordable { get; set; } = true;
		public bool IsFinal { get; set; }
		public BattleLocation BattleLocation { get; set; } = BattleLocation.Desert;
		public string PortraitAsset { get; set; } = string.Empty;
		public ClimbEventKind EventKind { get; set; }
		public string GainLine1 { get; set; } = string.Empty;
		public string GainLine2 { get; set; } = string.Empty;
		public float Opacity { get; set; } = 1f;
		public float AnimationOffsetX { get; set; }
		public float AnimationOpacityMultiplier { get; set; } = 1f;
		public bool ClipToBounds { get; set; }
		public bool IsRefreshShadow { get; set; }
	}

	public class ClimbEncounterSlotAction : IComponent
	{
		public Entity Owner { get; set; }
		public string SlotId { get; set; } = string.Empty;
	}

	public class ClimbEventSlotAction : IComponent
	{
		public Entity Owner { get; set; }
		public string SlotId { get; set; } = string.Empty;
	}

	public class ClimbShopTooltipSource : IComponent
	{
		public Entity Owner { get; set; }
		public string EquipmentId { get; set; } = string.Empty;
	}

	public class ClimbMedalTooltipSource : IComponent
	{
		public Entity Owner { get; set; }
		public string MedalId { get; set; } = string.Empty;
	}

	public class ClimbMedalTooltipAnchor : IComponent
	{
		public Entity Owner { get; set; }
		public Rectangle IconBounds { get; set; }
	}
}
