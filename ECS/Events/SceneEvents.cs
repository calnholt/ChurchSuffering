using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using System.Collections.Generic;
using System;

namespace ChurchSuffering.ECS.Events
{
	public class StartBattleRequested { }
	public class OpenWayStationClimbSettingsModalEvent { }
	public class WayStationPenanceSelectionChangedEvent
	{
		public int OldLevel { get; set; }
		public int NewLevel { get; set; }
		public bool WeaponChanged { get; set; }
	}
	public class OpenWayStationSaintsMedalsModalEvent { }

	public class WayStationDialoguePoiSelectedEvent
	{
		public string OfferId { get; set; } = string.Empty;
	}

	public class LoadSceneEvent {
		public SceneId Scene;
		public SceneId PreviousScene { get; set; } = SceneId.None;
	}

	public class SceneTransitionRequested
	{
		public Guid PreparationId { get; set; }
		public SceneId From { get; set; }
		public SceneId To { get; set; }
	}

	public class PrepareSceneEvent
	{
		public Guid PreparationId { get; set; }
		public SceneId Scene { get; set; }
	}

	public class ScenePreparationReady
	{
		public Guid PreparationId { get; set; }
		public SceneId Scene { get; set; }
	}

	public class SceneDeactivating
	{
		public SceneId From { get; set; }
		public SceneId To { get; set; }
	}

	public class SceneActivating
	{
		public Guid PreparationId { get; set; }
		public SceneId From { get; set; }
		public SceneId To { get; set; }
	}

	public class SceneActivated
	{
		public Guid PreparationId { get; set; }
		public SceneId Scene { get; set; }
	}

	public class PrepareMusicTrackEvent
	{
		public MusicTrack Track { get; set; }
	}

	public class DeleteCachesEvent { public SceneId Scene; }

	public class QuestSelected
	{
		public string LocationId;
		public int QuestIndex;
		public string QuestId;
	}

	public class ShowQuestRewardOverlay
	{
		public DeckRewardOfferSave DeckRewardOffer;
		public bool IsEncounterReward;
		public ClimbResourceSave ClimbResources;
		public SceneId DismissScene = SceneId.Climb;
	}

	public class ShowBoosterPackOpeningOverlayEvent
	{
		public BoosterPackSave Pack { get; set; }
	}

	public class CloseBoosterPackOpeningOverlayEvent { }

	public class BoosterPackOpeningDismissedEvent
	{
		public bool WasAuthoritativePack { get; set; }
	}

	public class ClaimPendingClimbPointsEvent { }

	public class ClimbPointsSegmentAwardedEvent
	{
		public int NewTotalPoints { get; set; }
		public bool TriggeredLevelComplete { get; set; }
	}

	public class ClimbEndedEvent
	{
		public int TimeReached { get; set; }
		public int ShopRefreshInterval { get; set; } = PenanceRules.BaseShopRefreshInterval;
		public bool Abandoned { get; set; }
		public bool CompletedFinalBoss { get; set; }
	}

	public class ClimbPointsAwardOverlayDismissedEvent
	{
		public bool WasAuthoritative { get; set; }
	}

	/// <summary>Published once after the final boss is defeated in a completed climb.</summary>
	public class ClimbCompletedEvent
	{
		public string StartingWeaponId { get; set; } = "sword";
		public int PenanceLevel { get; set; }
	}

	public class ShowNarrativeEventOverlay
	{
		public string RunMapEventId;
		public string EventTypeId;
		public string ResolutionContextId { get; set; } = string.Empty;
		public NarrativeModalContent Content { get; set; }
	}

	public class NarrativeEventOverlayClosedEvent
	{
		public string RunMapEventId;
		public string EventTypeId;
		public int OptionIndex;
	}
}
