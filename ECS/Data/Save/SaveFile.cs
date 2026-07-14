using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Data.Save
{
	public class SaveFile
	{
		public const int CURRENT_VERSION = 24;
		public const int DEFAULT_AUDIO_VOLUME_LEVEL = 50;

		public int version { get; set; } = 0;
		public bool isRunActive { get; set; }
        public int gold { get; set; } = 0;
		public int musicVolumeLevel { get; set; } = DEFAULT_AUDIO_VOLUME_LEVEL;
		public int sfxVolumeLevel { get; set; } = DEFAULT_AUDIO_VOLUME_LEVEL;
		public bool rumbleEnabled { get; set; } = true;
		public int runMapSeed { get; set; }
		public List<SaveItem> items { get; set; } = new List<SaveItem>();
		public string lastLocation { get; set; } = string.Empty;
		public List<LoadoutDefinition> loadouts { get; set; } = new List<LoadoutDefinition>();
		public int nextRunDeckEntryId { get; set; }
		public List<string> seenTutorials { get; set; } = new List<string>();
		public bool guidedTutorialCompleted { get; set; }
		public Dictionary<string, AchievementProgress> achievements { get; set; } = new Dictionary<string, AchievementProgress>();
		/// <summary>Persistent unlocks and reward-progression state shared by all future climbs.</summary>
		public PlayerCollectionSave collection { get; set; } = new PlayerCollectionSave();
		/// <summary>Run-long applied passive type name to stack count (e.g. Frostbite).</summary>
		public Dictionary<string, int> runLongPassives { get; set; } = new Dictionary<string, int>();
		public WayStationMetaSave waystation { get; set; } = new WayStationMetaSave();
		/// <summary>Exact unresolved deck reward offer shown after a quest reward.</summary>
		public DeckRewardOfferSave pendingDeckRewardOffer { get; set; }
		/// <summary>Accepted replacement or upgrade selections from quest deck-reward offers in this run.</summary>
		public int acceptedDeckRewardMutations { get; set; }
		public ClimbSaveState climb { get; set; } = new ClimbSaveState();
	}

	public class PlayerCollectionSave
	{
		public List<string> cardIds { get; set; } = new List<string>();
		public List<string> medalIds { get; set; } = new List<string>();
		public List<string> equipmentIds { get; set; } = new List<string>();
		public int totalPoints { get; set; }
		public int pendingClimbPoints { get; set; }
		/// <summary>Reward thresholds already processed, including thresholds reached after the collection is full.</summary>
		public int processedRewardLevels { get; set; }
		public List<BoosterPackSave> pendingBoosterPacks { get; set; } = new List<BoosterPackSave>();
	}

	public class BoosterPackSave
	{
		public List<BoosterPackRewardSave> rewards { get; set; } = new List<BoosterPackRewardSave>();
	}

	public class BoosterPackRewardSave
	{
		public string kind { get; set; } = string.Empty;
		public string id { get; set; } = string.Empty;
		/// <summary>Cosmetic printed color used when a card reward is displayed.</summary>
		public string cardColor { get; set; } = "White";
	}

	public class WayStationMetaSave
	{
		public int climbAttempts { get; set; }
		public int climbCompletions { get; set; }
		public List<CompletedClimbSave> completedClimbs { get; set; } = new List<CompletedClimbSave>();
		public int deferredNpcDialogueCounter { get; set; }
		public bool pendingNpcDialogueOffer { get; set; }
		public List<string> purchasedMedalIds { get; set; } = new List<string>();
		public Dictionary<string, List<string>> completedDialogueSegments { get; set; } = new Dictionary<string, List<string>>();
		public WayStationVisitSave currentVisit { get; set; } = new WayStationVisitSave();
	}

	public class CompletedClimbSave
	{
		public string startingWeaponId { get; set; } = "sword";
		public RunDifficulty difficulty { get; set; } = RunDifficulty.Easy;
	}

	public class WayStationVisitSave
	{
		public bool initialized { get; set; }
		public List<WayStationDialogueOfferSave> offers { get; set; } = new List<WayStationDialogueOfferSave>();
	}

	public class WayStationDialogueOfferSave
	{
		public string offerId { get; set; } = string.Empty;
		public string characterId { get; set; } = string.Empty;
		public string definitionId { get; set; } = string.Empty;
		public string segmentId { get; set; } = string.Empty;
		public float screenX { get; set; }
		public float screenY { get; set; }
		public bool visible { get; set; }
	}

	public class ClimbSaveState
	{
		public string startingWeaponId { get; set; } = "sword";
		public RunDifficulty difficulty { get; set; } = RunDifficulty.Easy;
		public int time { get; set; }
		public ClimbResourceSave resources { get; set; } = new ClimbResourceSave();
		public List<ClimbShopSlotSave> shopSlots { get; set; } = new List<ClimbShopSlotSave>();
		public List<ClimbEncounterSlotSave> encounterSlots { get; set; } = new List<ClimbEncounterSlotSave>();
		public List<ClimbEventSlotSave> eventSlots { get; set; } = new List<ClimbEventSlotSave>();
		public List<string> shownMedalIds { get; set; } = new List<string>();
		public List<string> shownEquipmentIds { get; set; } = new List<string>();
		public ClimbReplacementOfferSave pendingReplacementOffer { get; set; }
		public ClimbEncounterRewardSave pendingEncounterReward { get; set; }
		public ClimbPendingEventSave pendingEvent { get; set; }
		public ClimbNextBattleBonusSave nextBattleBonus { get; set; } = new ClimbNextBattleBonusSave();
		public ClimbNextBattlePenaltySave nextBattlePenalty { get; set; } = new ClimbNextBattlePenaltySave();

	}

	public class ClimbResourceSave
	{
		public int red { get; set; } = 1;
		public int white { get; set; } = 1;
		public int black { get; set; } = 1;
	}

	public class ClimbShopSlotSave
	{
		public string id { get; set; } = string.Empty;
		public string kind { get; set; } = ClimbShopSlotKinds.Empty;
		public string itemId { get; set; } = string.Empty;
		public string cardKey { get; set; } = string.Empty;
		public string deckEntryId { get; set; } = string.Empty;
		public int deckIndex { get; set; } = -1;
		public ClimbResourceSave cost { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public int timeCost { get; set; }
		public bool isSold { get; set; }
		public int generatedAtTime { get; set; }
	}

	public class ClimbEncounterSlotSave
	{
		public string id { get; set; } = string.Empty;
		public string enemyId { get; set; } = string.Empty;
		public int generatedAtTime { get; set; }
		public int duration { get; set; }
		public int timeCost { get; set; }
		public BattleLocation battleLocation { get; set; } = BattleLocation.Desert;
		public ClimbResourceSave rewardResources { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public bool hasDeckReward { get; set; } = true;
		public bool isCompleted { get; set; }
		public bool isFinal { get; set; }
		public string cardMutationRestrictionName { get; set; } = string.Empty;
		public string cardMutationDeckEntryId { get; set; } = string.Empty;
		public string cardMutationCardKey { get; set; } = string.Empty;
	}

	public class ClimbEventSlotSave
	{
		public string id { get; set; } = string.Empty;
		public string definitionId { get; set; } = string.Empty;
		public ClimbEventKind kind { get; set; }
		public ClimbHazardEffectType hazardEffect { get; set; }
		public ClimbCharacterRewardType characterReward { get; set; }
		public int scheduledAppearanceTime { get; set; }
		public int activatedAtTime { get; set; } = -1;
		public int duration { get; set; }
		public int timeCost { get; set; }
		public int effectAmount { get; set; }
		public ClimbResourceSave rewardResources { get; set; }
			= new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public ClimbEventStatus status { get; set; } = ClimbEventStatus.Scheduled;
	}

	public class ClimbReplacementOfferSave
	{
		public int shopSlotIndex { get; set; } = -1;
		public string incomingCardKey { get; set; } = string.Empty;
		public ClimbResourceSave cost { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
	}

	public class ClimbEncounterRewardSave
	{
		public string encounterSlotId { get; set; } = string.Empty;
		public ClimbResourceSave resources { get; set; } = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		public DeckRewardOfferSave deckRewardOffer { get; set; }
		public bool pendingFinalEncounter { get; set; }
	}

	public class ClimbPendingEventSave
	{
		public string eventSlotId { get; set; } = string.Empty;
		public ClimbEventFlowPhase phase { get; set; }
		public string dialogueRequestId { get; set; } = string.Empty;
	}

	public class ClimbNextBattleBonusSave
	{
		public int courage { get; set; }
		public int temperance { get; set; }
		public int vigor { get; set; }
	}

	public class ClimbNextBattlePenaltySave
	{
		public int burn { get; set; }
		public int fear { get; set; }
	}

	public enum ClimbEventKind
	{
		Hazard,
		Character,
	}

	public enum ClimbHazardEffectType
	{
		None,
		Colorless,
		Frozen,
		Brittle,
		Burn,
		Fear,
		Shackled,
		Scar,
	}

	public enum ClimbCharacterRewardType
	{
		None,
		Temperance,
		Courage,
		Vigor,
		RandomCardUpgrade,
	}

	public enum ClimbEventStatus
	{
		Scheduled,
		Active,
		Pending,
		Completed,
		Expired,
	}

	public enum ClimbEventFlowPhase
	{
		None,
		HazardConfirmation,
		CharacterDialogue,
		CharacterSummary,
	}

	public static class ClimbShopSlotKinds
	{
		public const string Empty = "empty";
		public const string Medal = "medal";
		public const string Equipment = "equipment";
		public const string Upgrade = "upgrade";
		public const string Replacement = "replacement";

		public static readonly string[] DisplayOrder =
		{
			Upgrade,
			Replacement,
			Medal,
			Medal,
			Equipment,
		};

		public static int GetDisplayIndex(string kind)
		{
			for (int i = 0; i < DisplayOrder.Length; i++)
			{
				if (string.Equals(kind, DisplayOrder[i], StringComparison.OrdinalIgnoreCase))
					return i;
			}
			return DisplayOrder.Length - 1;
		}
	}

	public class DeckRewardOfferSave
	{
		public int rewardGold { get; set; }
		public List<DeckRewardOfferOptionSave> options { get; set; } = new List<DeckRewardOfferOptionSave>();
	}

	public class DeckRewardOfferOptionSave
	{
		public string kind { get; set; } = DeckRewardOfferKinds.Exchange;
		public int loadoutIndex { get; set; } = -1;
		public string outgoingEntryId { get; set; } = string.Empty;
		public string outgoingCardKey { get; set; } = string.Empty;
		public string incomingCardKey { get; set; } = string.Empty;
		public string upgradedCardKey { get; set; } = string.Empty;
	}

	public static class DeckRewardOfferKinds
	{
		public const string Exchange = "exchange";
		public const string Upgrade = "upgrade";
	}

	public class SaveItem
	{
		public string id { get; set; }
		public int amount { get; set; }
	}

}
