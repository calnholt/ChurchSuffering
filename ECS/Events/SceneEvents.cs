using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using System.Collections.Generic;

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
