using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Singletons;
using System.Collections.Generic;
using System;

namespace Crusaders30XX.ECS.Events
{
	public class StartBattleRequested { }
	public class OpenWayStationClimbSettingsModalEvent { }
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
		public string Message;
		public string TitleLine1;
		public string TitleLine2;
		public int RewardGold;
		public bool HasCardReward;
		public string RewardCardKey;
		public List<string> RewardCardKeys = new List<string>();
		public DeckRewardOfferSave DeckRewardOffer;
		public bool IsEncounterReward;
		public ClimbResourceSave ClimbResources;
		public SceneId DismissScene = SceneId.Climb;
	}

	public class TreasureChestOpened
	{
		public int RewardGold;
		public string RewardMedalId;
		public string RewardEquipmentId;
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
		public bool Abandoned { get; set; }
		public bool CompletedFinalBoss { get; set; }
	}

	/// <summary>Published once after the final boss is defeated in a completed climb.</summary>
	public class ClimbCompletedEvent
	{
		public string StartingWeaponId { get; set; } = "sword";
		public RunDifficulty Difficulty { get; set; } = RunDifficulty.Easy;
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
