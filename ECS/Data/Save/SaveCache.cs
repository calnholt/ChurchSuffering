using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Data.Climb;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Data.Save
{
	public static class SaveCache
	{
		private const int WayStationNpcDialogueCounterThreshold = 3;
		private static SaveFile _save;
		private static string _filePath;
		private static readonly object _lock = new object();

		public static SaveFile GetAll()
		{
			EnsureLoaded();
			return _save;
		}

		public static PlayerCollectionSave GetCollection()
		{
			EnsureLoaded();
			lock (_lock)
			{
				return CloneCollection(_save?.collection) ?? CreateInitialCollection();
			}
		}

		public static void SaveCollection(PlayerCollectionSave collection)
		{
			if (collection == null) return;
			EnsureLoaded();
			lock (_lock)
			{
				_save ??= new SaveFile();
				_save.collection = CloneCollection(collection) ?? CreateInitialCollection();
				Persist();
			}
		}

		public static void UnlockAllCollectionItems()
		{
			EnsureLoaded();
			lock (_lock)
			{
				_save ??= CreateInactiveSavePreservingMeta(null);
				var collection = CloneCollection(_save.collection) ?? CreateInitialCollection();
				AddMissingCollectionItems(
					collection.cardIds,
					CardFactory.GetAllCards()
						.Where(entry => entry.Value != null && entry.Value.CanAddToLoadout && !entry.Value.IsWeapon && !entry.Value.IsToken)
						.Select(entry => entry.Key.ToKey()));
				AddMissingCollectionItems(
					collection.medalIds,
					MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()));
				AddMissingCollectionItems(
					collection.equipmentIds,
					EquipmentFactory.GetAllEquipment().Keys.Select(id => id.ToKey()));
				_save.collection = collection;
				Persist();
			}
		}

		public static void UnlockAllRunSetupOptions()
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				foreach (var weapon in Enum.GetValues<StartingWeapon>())
				{
					string weaponId = weapon.ToString().ToLowerInvariant();
					RecordCompletedClimbLocked(weaponId, RunDifficulty.Easy);
					RecordCompletedClimbLocked(weaponId, RunDifficulty.Normal);
				}

				int prerequisiteCompletionCount = Enum.GetValues<StartingWeapon>().Length * 2;
				_save.waystation.climbCompletions = Math.Max(
					_save.waystation.climbCompletions,
					prerequisiteCompletionCount);
				Persist();
			}
		}

		public static bool IsCollectionItemUnlocked(string itemId, ForSaleItemType itemType)
		{
			if (string.IsNullOrWhiteSpace(itemId)) return false;
			var collection = GetCollection();
			return itemType switch
			{
				ForSaleItemType.Card => collection.cardIds.Contains(itemId, StringComparer.OrdinalIgnoreCase),
				ForSaleItemType.Medal => collection.medalIds.Contains(itemId, StringComparer.OrdinalIgnoreCase),
				ForSaleItemType.Equipment => collection.equipmentIds.Contains(itemId, StringComparer.OrdinalIgnoreCase),
				_ => false,
			};
		}

		public static int GetMusicVolumeLevel()
		{
			EnsureLoaded();
			lock (_lock)
			{
				return ClampAudioVolumeLevel(_save?.musicVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL);
			}
		}

		public static int GetSfxVolumeLevel()
		{
			EnsureLoaded();
			lock (_lock)
			{
				return ClampAudioVolumeLevel(_save?.sfxVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL);
			}
		}

		public static bool GetRumbleEnabled()
		{
			EnsureLoaded();
			lock (_lock)
			{
				return _save?.rumbleEnabled ?? true;
			}
		}

		public static void SetRumbleEnabled(bool enabled)
		{
			bool changed;
			lock (_lock)
			{
				EnsureLoaded();
				_save ??= new SaveFile();
				if (_save.rumbleEnabled == enabled) return;
				_save.rumbleEnabled = enabled;
				changed = Persist();
			}

			if (changed)
			{
				EventManager.Publish(new RumbleSettingsChangedEvent { Enabled = enabled });
			}
		}

		public static void SetMusicVolumeLevel(int value)
		{
			SetAudioVolumeLevels(musicVolumeLevel: value, sfxVolumeLevel: null);
		}

		public static void SetSfxVolumeLevel(int value)
		{
			SetAudioVolumeLevels(musicVolumeLevel: null, sfxVolumeLevel: value);
		}

		private static void SetAudioVolumeLevels(int? musicVolumeLevel, int? sfxVolumeLevel)
		{
			bool changed = false;
			int resolvedMusic = SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL;
			int resolvedSfx = SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL;
			lock (_lock)
			{
				EnsureLoaded();
				if (_save == null) _save = new SaveFile();

				resolvedMusic = ClampAudioVolumeLevel(musicVolumeLevel ?? _save.musicVolumeLevel);
				resolvedSfx = ClampAudioVolumeLevel(sfxVolumeLevel ?? _save.sfxVolumeLevel);
				if (_save.musicVolumeLevel == resolvedMusic && _save.sfxVolumeLevel == resolvedSfx) return;

				_save.musicVolumeLevel = resolvedMusic;
				_save.sfxVolumeLevel = resolvedSfx;
				changed = Persist();
			}

			if (changed)
			{
				EventManager.Publish(new AudioSettingsChangedEvent
				{
					MusicVolumeLevel = resolvedMusic,
					SfxVolumeLevel = resolvedSfx,
				});
			}
		}

		public static LoadoutDefinition GetLoadout(string id)
		{
			EnsureLoaded();
			if (_save == null || _save.loadouts == null) return null;
			lock (_lock)
			{
				return CloneLoadout(_save.loadouts.FirstOrDefault(l => l.id == id));
			}
		}

		public static List<LoadoutDefinition> GetAllLoadouts()
		{
			EnsureLoaded();
			if (_save == null || _save.loadouts == null) return new List<LoadoutDefinition>();
			lock (_lock)
			{
				return _save.loadouts.Select(CloneLoadout).Where(loadout => loadout != null).ToList();
			}
		}

		public static void SaveLoadout(LoadoutDefinition def)
		{
			if (def == null || string.IsNullOrEmpty(def.id)) return;
			lock (_lock)
			{
				EnsureLoaded();
				if (_save == null) _save = new SaveFile();
				if (_save.loadouts == null) _save.loadouts = new List<LoadoutDefinition>();
				
				var existing = _save.loadouts.FirstOrDefault(l => l.id == def.id);
				if (existing != null)
				{
					_save.loadouts.Remove(existing);
				}
				_save.loadouts.Add(CloneLoadout(def));
				Persist();
			}
		}

		public static void ConfigurePrimaryRunSetup(
			string weaponId,
			string temperanceId,
			RunDifficulty difficulty)
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = CreateDefaultRunSave();
				EnsurePrimaryLoadout(_save);

				var resolvedWeaponId = string.IsNullOrWhiteSpace(weaponId) ? "sword" : weaponId;
				var loadout = StartingDeckGeneratorService.BuildStartingLoadout(
					resolvedWeaponId,
					_save.runMapSeed,
					"loadout_1");

				_save.nextRunDeckEntryId = 0;
				var savedLoadout = _save.loadouts[0];
				savedLoadout.cards = loadout.cards
					.Select(entry => CreateEntryLocked(entry.cardKey, isStarter: true, countsAsTraded: false))
					.ToList();
				savedLoadout.weaponId = resolvedWeaponId;
				savedLoadout.temperanceId = string.IsNullOrWhiteSpace(temperanceId)
					? loadout.temperanceId
					: temperanceId;
				if (string.IsNullOrWhiteSpace(savedLoadout.name)) savedLoadout.name = "Deck";
				if (string.IsNullOrWhiteSpace(savedLoadout.id)) savedLoadout.id = "loadout_1";
				savedLoadout.chestId ??= string.Empty;
				savedLoadout.legsId ??= string.Empty;
				savedLoadout.armsId ??= string.Empty;
				savedLoadout.headId ??= string.Empty;
				savedLoadout.medalIds ??= new List<string>();

				_save.climb = ClimbRuleService.CreateInitialState(_save.runMapSeed, savedLoadout, difficulty);
				Persist();
			}
		}

		public static HashSet<string> GetOwnedCardIds()
		{
			EnsureLoaded();
			var loadout = GetLoadout("loadout_1");
			if (loadout?.cards == null || loadout.cards.Count == 0)
			{
				return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}
			var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var entry in loadout.cards)
			{
				string key = entry?.cardKey;
				string baseId = DeckRules.ParseBaseCardId(key);
				if (!string.IsNullOrEmpty(baseId)) ids.Add(baseId);
			}
			return ids;
		}

		public static bool IsCardOwned(string cardId)
		{
			if (string.IsNullOrWhiteSpace(cardId)) return false;
			return GetOwnedCardIds().Contains(cardId);
		}

		public static bool IsItemOwned(string itemId, ForSaleItemType itemType)
		{
			if (string.IsNullOrWhiteSpace(itemId)) return false;
			EnsureLoaded();
			var loadout = GetLoadout("loadout_1");
			if (loadout == null) return false;

			switch (itemType)
			{
				case ForSaleItemType.Card:
					return IsCardOwned(itemId);
				case ForSaleItemType.Weapon:
					return string.Equals(loadout.weaponId, itemId, StringComparison.OrdinalIgnoreCase);
				case ForSaleItemType.Medal:
					return loadout.medalIds != null && loadout.medalIds.Any(m => string.Equals(m, itemId, StringComparison.OrdinalIgnoreCase));
				case ForSaleItemType.Equipment:
					return string.Equals(loadout.headId, itemId, StringComparison.OrdinalIgnoreCase)
						|| string.Equals(loadout.chestId, itemId, StringComparison.OrdinalIgnoreCase)
						|| string.Equals(loadout.armsId, itemId, StringComparison.OrdinalIgnoreCase)
						|| string.Equals(loadout.legsId, itemId, StringComparison.OrdinalIgnoreCase);
				default:
					return false;
			}
		}

		public static int GetGold()
		{
			EnsureLoaded();
			return _save?.gold ?? 0;
		}

		public static void AddGold(int amount)
		{
			EnsureLoaded();
			if (amount <= 0) return;
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				long newValue = (_save.gold) + (long)amount;
				_save.gold = newValue < 0 ? 0 : (int)System.Math.Min(int.MaxValue, newValue);
				Persist();
			}
		}

		public static void Reload()
		{
			lock (_lock)
			{
				var path = ResolveFilePath();
				_save = SaveRepository.Load(path);
				ApplyVersionPolicy(persist: true);
			}
		}

		private static bool Persist()
		{
			try
			{
				var path = ResolveFilePath();
				if (string.IsNullOrEmpty(path)) return false;
				return SaveRepository.Save(path, _save);
			}
			catch
			{
				return false;
			}
		}

		private static void EnsureLoaded()
		{
			if (_save != null) return;
			lock (_lock)
			{
				if (_save == null)
				{
					var path = ResolveFilePath();
					if (!string.IsNullOrEmpty(path) && File.Exists(path))
					{
						_save = SaveRepository.Load(path);
						ApplyVersionPolicy(persist: true);
					}
					else
					{
						// Optional migration: if no LocalApplicationData save exists yet,
						// but a legacy project save file does, load that and persist it
						// to the new location so existing progress is preserved.
						string legacyPath = ResolveLegacyFilePath();
						if (!string.IsNullOrEmpty(legacyPath) && File.Exists(legacyPath))
						{
							_save = SaveRepository.Load(legacyPath) ?? CreateInactiveSavePreservingMeta(null);
							ApplyVersionPolicy(persist: true);
						}
						else
						{
							// New profiles start inactive; WayStation Depart creates the first active run.
							_save = CreateInactiveSavePreservingMeta(null);
							Persist();
						}
					}
				}
			}
		}

		public static void StartNewRun()
		{
			EnsureLoaded();
			lock (_lock)
			{
				_save = CreateFreshRunPreservingMeta(_save);
				_save.isRunActive = true;
				Persist();
				CardUsageTelemetryRuntime.StartNewRun(_save.runMapSeed);
			}
		}

		public static void StartWayStationClimbAttempt()
		{
			EnsureLoaded();
			lock (_lock)
			{
				var prior = _save;
				var waystation = CloneWayStationMeta(prior?.waystation);
				waystation.climbAttempts = Math.Max(0, waystation.climbAttempts) + 1;
				waystation.currentVisit = new WayStationVisitSave();
				_save = CreateFreshRunPreservingMeta(prior);
				_save.waystation = waystation;
				_save.isRunActive = true;
				Persist();
				CardUsageTelemetryRuntime.StartNewRun(_save.runMapSeed);
			}
		}

		public static WayStationMetaSave GetWayStationMeta()
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				return CloneWayStationMeta(_save.waystation);
			}
		}

		public static WayStationVisitSave GetWayStationVisit()
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				return CloneWayStationVisit(_save.waystation.currentVisit);
			}
		}

		public static List<string> GetPurchasedWayStationMedalIds()
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				return CloneStringList(_save.waystation.purchasedMedalIds);
			}
		}

		public static void MarkWayStationMedalPurchased(string medalId)
		{
			if (string.IsNullOrWhiteSpace(medalId)) return;
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				_save.waystation.purchasedMedalIds ??= new List<string>();
				if (_save.waystation.purchasedMedalIds.Contains(medalId, StringComparer.OrdinalIgnoreCase)) return;
				_save.waystation.purchasedMedalIds.Add(medalId);
				Persist();
			}
		}

		public static void SaveWayStationVisit(WayStationVisitSave visit)
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				_save.waystation.currentVisit = CloneWayStationVisit(visit) ?? new WayStationVisitSave();
				Persist();
			}
		}

		public static void ClearWayStationVisit()
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				_save.waystation.currentVisit = new WayStationVisitSave();
				Persist();
			}
		}

		public static void RecordWayStationClimbReturn(WayStationArrivalKind arrivalKind)
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				if (arrivalKind == WayStationArrivalKind.ReturnedFromCompletedClimb)
				{
					RecordCompletedClimbLocked(_save.climb);
					_save.waystation.climbCompletions = Math.Max(0, _save.waystation.climbCompletions) + 1;
				}

				_save.waystation.currentVisit = new WayStationVisitSave();
				if (arrivalKind == WayStationArrivalKind.ReturnedFromAbandonedClimb)
				{
					Persist();
					return;
				}

				if (arrivalKind == WayStationArrivalKind.ReturnedFromCompletedClimb || DidCurrentClimbReachNpcDialogueThreshold())
				{
					_save.waystation.pendingNpcDialogueOffer = true;
					Persist();
					return;
				}

				if (arrivalKind == WayStationArrivalKind.ReturnedFromFailedClimb)
				{
					_save.waystation.deferredNpcDialogueCounter = Math.Min(
						WayStationNpcDialogueCounterThreshold,
						Math.Max(0, _save.waystation.deferredNpcDialogueCounter) + 1);
					if (_save.waystation.deferredNpcDialogueCounter >= WayStationNpcDialogueCounterThreshold)
					{
						_save.waystation.pendingNpcDialogueOffer = true;
					}
					Persist();
				}
			}
		}

		public static void MarkWayStationNpcDialogueOfferQueued()
		{
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				_save.waystation.pendingNpcDialogueOffer = false;
				_save.waystation.deferredNpcDialogueCounter = 0;
				Persist();
			}
		}

		public static void RecordWayStationClimbCompletion()
		{
			RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromCompletedClimb);
		}

		public static bool HasSeenWayStationDialogueSegment(string characterId, string segmentId)
		{
			if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(segmentId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				return _save.waystation.completedDialogueSegments.TryGetValue(characterId, out var segments)
					&& segments != null
					&& segments.Contains(segmentId, StringComparer.OrdinalIgnoreCase);
			}
		}

		public static void MarkWayStationDialogueSegmentSeen(string characterId, string segmentId)
		{
			if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(segmentId)) return;
			EnsureLoaded();
			lock (_lock)
			{
				EnsureWayStationMetaLocked();
				if (!_save.waystation.completedDialogueSegments.TryGetValue(characterId, out var segments) || segments == null)
				{
					segments = new List<string>();
					_save.waystation.completedDialogueSegments[characterId] = segments;
				}

				if (!segments.Contains(segmentId, StringComparer.OrdinalIgnoreCase))
				{
					segments.Add(segmentId);
					Persist();
				}
			}
		}

		public static bool IsRunActive()
		{
			EnsureLoaded();
			return _save?.isRunActive == true;
		}

		public static void MarkRunInactive()
		{
			EnsureLoaded();
			lock (_lock)
			{
				_save = CreateInactiveSavePreservingMeta(_save);
				Persist();
			}
		}

		/// <summary>
		/// Removes on-disk save files (primary and legacy locations) and clears the in-memory cache.
		/// The next <see cref="EnsureLoaded"/> creates a fresh default save.
		/// </summary>
		public static void DeleteSaveFilesIfPresent()
		{
			lock (_lock)
			{
				_save = null;
				bool deletedAny = false;
				foreach (string path in EnumerateSaveFilePaths())
				{
					if (!File.Exists(path)) continue;
					File.Delete(path);
					System.Console.WriteLine($"[SaveCache] Deleted save file: {path}");
					deletedAny = true;
				}
				if (!deletedAny)
				{
					System.Console.WriteLine("[SaveCache] No save file found to delete.");
				}
			}
		}

		/// <summary>
		/// Any save whose version is not CURRENT_VERSION is discarded and replaced with a new default save.
		/// No migration between versions.
		/// </summary>
		private static void ApplyVersionPolicy(bool persist)
		{
			if (_save == null || _save.version != SaveFile.CURRENT_VERSION)
			{
				int found = _save?.version ?? 0;
				System.Console.WriteLine($"[SaveCache] Save version {found} != {SaveFile.CURRENT_VERSION}; creating a new save file.");
				_save = CreateInactiveSavePreservingMeta(null);
				if (persist) Persist();
			}
		}

		private static SaveFile CreateFreshRunPreservingMeta(SaveFile prior)
		{
			var achievements = prior?.achievements;
			var seenTutorials = prior?.seenTutorials;
			int musicVolumeLevel = ClampAudioVolumeLevel(prior?.musicVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL);
			int sfxVolumeLevel = ClampAudioVolumeLevel(prior?.sfxVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL);
			bool rumbleEnabled = prior?.rumbleEnabled ?? true;
			var save = CreateDefaultRunSave();
			save.achievements = achievements ?? new Dictionary<string, AchievementProgress>();
			save.seenTutorials = seenTutorials ?? new List<string>();
			save.guidedTutorialCompleted = prior?.guidedTutorialCompleted == true;
			save.waystation = CloneWayStationMeta(prior?.waystation);
			save.collection = CloneCollection(prior?.collection) ?? CreateInitialCollection();
			save.musicVolumeLevel = musicVolumeLevel;
			save.sfxVolumeLevel = sfxVolumeLevel;
			save.rumbleEnabled = rumbleEnabled;
			return save;
		}

		private static SaveFile CreateInactiveSavePreservingMeta(SaveFile prior)
		{
			return new SaveFile
			{
				version = SaveFile.CURRENT_VERSION,
				isRunActive = false,
				gold = 0,
				musicVolumeLevel = ClampAudioVolumeLevel(prior?.musicVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL),
					sfxVolumeLevel = ClampAudioVolumeLevel(prior?.sfxVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL),
					rumbleEnabled = prior?.rumbleEnabled ?? true,
				runMapSeed = 0,
				items = new List<SaveItem>(),
				lastLocation = string.Empty,
				loadouts = new List<LoadoutDefinition>(),
				nextRunDeckEntryId = 0,
				runLongPassives = new Dictionary<string, int>(),
				pendingDeckRewardOffer = null,
				climb = new ClimbSaveState(),
				achievements = prior?.achievements ?? new Dictionary<string, AchievementProgress>(),
				collection = CloneCollection(prior?.collection) ?? CreateInitialCollection(),
				seenTutorials = prior?.seenTutorials ?? new List<string>(),
				guidedTutorialCompleted = prior?.guidedTutorialCompleted == true,
				waystation = CloneWayStationMeta(prior?.waystation),
			};
		}

		private static int ClampAudioVolumeLevel(int value)
		{
			return Math.Clamp(value, 0, 100);
		}

		private static SaveFile CreateDefaultRunSave()
		{
			int seed = Random.Shared.Next();
			var startingDeck = StartingDeckGeneratorService.Generate(
				StartingDeckGeneratorService.DefaultStarterCardPool,
				seed);
			var save = new SaveFile
			{
				version = SaveFile.CURRENT_VERSION,
				isRunActive = true,
				gold = 4,
				runMapSeed = seed,
				items = new List<SaveItem>(),
				lastLocation = string.Empty,
				pendingDeckRewardOffer = null,
				collection = CreateInitialCollection(),
				nextRunDeckEntryId = startingDeck.Count,
				loadouts = new List<LoadoutDefinition>
				{
					new LoadoutDefinition
					{
						id = "loadout_1",
						name = "Deck",
						cards = startingDeck.Select((cardKey, index) => new LoadoutCardEntry
						{
							entryId = $"run_card_{index}",
							cardKey = cardKey,
							isStarter = true,
							countsAsTraded = false,
							restrictions = new List<string>(),
						}).ToList(),
						weaponId = "sword",
						temperanceId = "angelic_aura",
						chestId = "",
						legsId = "",
						armsId = "",
						headId = "",
						medalIds = new List<string>()
					}
				}
			};
			save.climb = ClimbRuleService.CreateInitialState(seed, save.loadouts[0]);
			return save;
		}

		private static void EnsurePrimaryLoadout(SaveFile save)
		{
			if (save.loadouts == null) save.loadouts = new List<LoadoutDefinition>();
			var loadout = save.loadouts.FirstOrDefault(l => l.id == "loadout_1");
			if (loadout == null)
			{
				loadout = new LoadoutDefinition { id = "loadout_1", name = "Deck", medalIds = new List<string>() };
				save.loadouts.Add(loadout);
			}
			if (loadout.cards == null) loadout.cards = new List<LoadoutCardEntry>();
			if (loadout.medalIds == null) loadout.medalIds = new List<string>();
		}

		public static string GetSaveDirectory()
		{
			string path = ResolveFilePath();
			if (string.IsNullOrEmpty(path)) return string.Empty;
			return Path.GetDirectoryName(path);
		}

		public static ClimbSaveState GetClimbState()
		{
			EnsureLoaded();
			EnsureClimbState();
			lock (_lock)
			{
				return CloneClimbState(_save?.climb);
			}
		}

		public static void SaveClimbState(ClimbSaveState state)
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				_save.climb = CloneClimbState(state) ?? ClimbRuleService.CreateInitialState(_save.runMapSeed, GetLoadout("loadout_1"));
				Persist();
			}
		}

		public static bool TryUpdateClimbEventLifecycle(out ClimbSaveState updatedState)
		{
			updatedState = null;
			EnsureLoaded();
			lock (_lock)
			{
				if (_save?.climb == null) return false;
				var backup = CaptureClimbEventTransactionBackup();
				bool changed = ClimbRuleService.UpdateEventLifecycle(_save.climb);
				if (changed && !Persist())
				{
					RestoreClimbEventTransactionBackup(backup);
					updatedState = CloneClimbState(_save.climb);
					return false;
				}
				updatedState = CloneClimbState(_save.climb);
				return changed;
			}
		}

		public static bool TryBeginClimbEvent(
			string eventSlotId,
			ClimbEventFlowPhase phase,
			string dialogueRequestId,
			out ClimbEventSlotSave pendingSlot)
		{
			pendingSlot = null;
			if (string.IsNullOrWhiteSpace(eventSlotId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var climb = _save?.climb;
				if (climb?.pendingEvent != null) return false;
				var slot = FindClimbEventSlot(climb, eventSlotId);
				if (slot == null || slot.status != ClimbEventStatus.Active) return false;
				if (phase == ClimbEventFlowPhase.HazardConfirmation && slot.kind != ClimbEventKind.Hazard) return false;
				if (phase == ClimbEventFlowPhase.CharacterDialogue && slot.kind != ClimbEventKind.Character) return false;
				if (phase != ClimbEventFlowPhase.HazardConfirmation && phase != ClimbEventFlowPhase.CharacterDialogue) return false;
				var backup = CaptureClimbEventTransactionBackup();

				slot.status = ClimbEventStatus.Pending;
				climb.pendingEvent = new ClimbPendingEventSave
				{
					eventSlotId = slot.id,
					phase = phase,
					dialogueRequestId = dialogueRequestId ?? string.Empty,
				};
				if (!Persist())
				{
					RestoreClimbEventTransactionBackup(backup);
					return false;
				}
				pendingSlot = CloneClimbEventSlot(slot);
				return true;
			}
		}

		public static bool TrySetClimbCharacterSummaryPhase(string eventSlotId, string dialogueRequestId)
		{
			if (string.IsNullOrWhiteSpace(eventSlotId) || string.IsNullOrWhiteSpace(dialogueRequestId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var climb = _save?.climb;
				var pending = climb?.pendingEvent;
				var slot = FindClimbEventSlot(climb, eventSlotId);
				if (pending == null
					|| slot == null
					|| slot.kind != ClimbEventKind.Character
					|| slot.status != ClimbEventStatus.Pending
					|| pending.phase != ClimbEventFlowPhase.CharacterDialogue
					|| !string.Equals(pending.eventSlotId, slot.id, StringComparison.OrdinalIgnoreCase)
					|| !string.Equals(pending.dialogueRequestId, dialogueRequestId, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}

				var backup = CaptureClimbEventTransactionBackup();
				pending.phase = ClimbEventFlowPhase.CharacterSummary;
				if (!Persist())
				{
					RestoreClimbEventTransactionBackup(backup);
					return false;
				}
				return true;
			}
		}

		public static bool TryResolveClimbHazard(string eventSlotId, out ClimbEventMutationResult result)
		{
			result = new ClimbEventMutationResult();
			if (string.IsNullOrWhiteSpace(eventSlotId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var climb = _save?.climb;
				var slot = FindClimbEventSlot(climb, eventSlotId);
				if (slot?.kind != ClimbEventKind.Hazard) return false;
				if (slot.status == ClimbEventStatus.Completed)
				{
					result = new ClimbEventMutationResult
					{
						Succeeded = true,
						AlreadyResolved = true,
						EventSlotId = slot.id,
					};
					return true;
				}

				var pending = climb?.pendingEvent;
				if (slot.status != ClimbEventStatus.Pending
					|| pending?.phase != ClimbEventFlowPhase.HazardConfirmation
					|| !string.Equals(pending.eventSlotId, slot.id, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				var backup = CaptureClimbEventTransactionBackup();

				climb.resources ??= new ClimbResourceSave { red = 0, white = 0, black = 0 };
				ClimbRuleService.AddResources(climb.resources, slot.rewardResources);

				string restrictedEntryId = string.Empty;
				string restrictionName = ClimbRuleService.GetRestrictionName(slot.hazardEffect);
				bool runLongPassivesChanged = false;
				string runLongPassiveType = string.Empty;
				int runLongPassiveAmount = 0;
				int runLongPassiveTotal = 0;
				if (!string.IsNullOrWhiteSpace(restrictionName))
				{
					EnsurePrimaryLoadout(_save);
					var loadout = _save.loadouts.First(loadout => loadout.id == RunDeckService.PrimaryLoadoutId);
					var target = ClimbRuleService.SelectDeterministicEntry(
						ClimbRuleService.GetEligibleRestrictionEntries(loadout, restrictionName),
						_save.runMapSeed,
						slot.id);
					if (target != null)
					{
						target.restrictions ??= new List<string>();
						if (!target.restrictions.Contains(restrictionName, StringComparer.OrdinalIgnoreCase))
						{
							target.restrictions.Add(restrictionName);
						}
						restrictedEntryId = target.entryId;
					}
				}
				else if (slot.hazardEffect == ClimbHazardEffectType.Burn)
				{
					climb.nextBattlePenalty ??= new ClimbNextBattlePenaltySave();
					climb.nextBattlePenalty.burn += Math.Max(0, slot.effectAmount);
				}
				else if (slot.hazardEffect == ClimbHazardEffectType.Fear)
				{
					climb.nextBattlePenalty ??= new ClimbNextBattlePenaltySave();
					climb.nextBattlePenalty.fear += Math.Max(0, slot.effectAmount);
				}
				else if (slot.hazardEffect == ClimbHazardEffectType.Shackled)
				{
					runLongPassiveType = AppliedPassiveType.Shackled.ToString();
					runLongPassiveAmount = Math.Max(0, slot.effectAmount);
					runLongPassiveTotal = AddRunLongPassiveStacksLocked(runLongPassiveType, runLongPassiveAmount);
					runLongPassivesChanged = true;
				}
				else if (slot.hazardEffect == ClimbHazardEffectType.Scar)
				{
					runLongPassiveType = AppliedPassiveType.Scar.ToString();
					runLongPassiveAmount = Math.Max(0, slot.effectAmount);
					runLongPassiveTotal = AddRunLongPassiveStacksLocked(runLongPassiveType, runLongPassiveAmount);
					runLongPassivesChanged = true;
				}

				slot.status = ClimbEventStatus.Completed;
				climb.pendingEvent = null;
				if (!Persist())
				{
					RestoreClimbEventTransactionBackup(backup);
					return false;
				}
				result = new ClimbEventMutationResult
				{
					Succeeded = true,
					EventSlotId = slot.id,
					ResourcesGained = CloneClimbResources(slot.rewardResources),
					RestrictedEntryId = restrictedEntryId,
					RestrictionName = restrictionName,
					RunLongPassivesChanged = runLongPassivesChanged,
					RunLongPassiveType = runLongPassiveType,
					RunLongPassiveAmount = runLongPassiveAmount,
					RunLongPassiveTotal = runLongPassiveTotal,
				};
				return true;
			}
		}

		public static bool TryResolveClimbCharacter(string eventSlotId, out ClimbEventMutationResult result)
		{
			result = new ClimbEventMutationResult();
			if (string.IsNullOrWhiteSpace(eventSlotId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var climb = _save?.climb;
				var slot = FindClimbEventSlot(climb, eventSlotId);
				if (slot?.kind != ClimbEventKind.Character) return false;
				if (slot.status == ClimbEventStatus.Completed)
				{
					result = new ClimbEventMutationResult
					{
						Succeeded = true,
						AlreadyResolved = true,
						EventSlotId = slot.id,
						ReachedFinalTime = ClimbRuleService.ClampTime(climb?.time ?? 0) >= ClimbRuleService.MaxTime,
					};
					return true;
				}

				var pending = climb?.pendingEvent;
				if (slot.status != ClimbEventStatus.Pending
					|| pending?.phase != ClimbEventFlowPhase.CharacterSummary
					|| !string.Equals(pending.eventSlotId, slot.id, StringComparison.OrdinalIgnoreCase))
				{
					return false;
				}
				var backup = CaptureClimbEventTransactionBackup();

				climb.nextBattleBonus ??= new ClimbNextBattleBonusSave();
				string upgradedEntryId = string.Empty;
				string upgradedCardKey = string.Empty;
				if (slot.characterReward == ClimbCharacterRewardType.Courage)
				{
					climb.nextBattleBonus.courage += Math.Max(0, slot.effectAmount);
				}
				else if (slot.characterReward == ClimbCharacterRewardType.Temperance)
				{
					climb.nextBattleBonus.temperance += Math.Max(0, slot.effectAmount);
				}
				else if (slot.characterReward == ClimbCharacterRewardType.Vigor)
				{
					climb.nextBattleBonus.vigor += Math.Max(0, slot.effectAmount);
				}
				else if (slot.characterReward == ClimbCharacterRewardType.RandomCardUpgrade)
				{
					EnsurePrimaryLoadout(_save);
					var loadout = _save.loadouts.First(loadout => loadout.id == RunDeckService.PrimaryLoadoutId);
					var target = ClimbRuleService.SelectDeterministicEntry(
						ClimbRuleService.GetEligibleSmithEntries(loadout),
						_save.runMapSeed,
						slot.id);
					if (target != null)
					{
						string proposedKey = RunDeckService.BuildUpgradedCardKey(target.cardKey);
						if (!string.IsNullOrWhiteSpace(proposedKey))
						{
							target.cardKey = proposedKey;
							upgradedEntryId = target.entryId;
							upgradedCardKey = target.cardKey;
						}
					}
				}

				slot.status = ClimbEventStatus.Completed;
				climb.pendingEvent = null;
				int previousTime = climb.time;
				int appliedTime = ClimbRuleService.ApplyTime(climb, 1);
				EnsurePrimaryLoadout(_save);
				var primaryLoadout = _save.loadouts.First(loadout => loadout.id == RunDeckService.PrimaryLoadoutId);
				if (ClimbRuleService.ShouldRefreshShopAtTime(previousTime, climb.time))
				{
					ClimbRuleService.RefreshShopSlots(climb, _save.runMapSeed, primaryLoadout);
				}
				ClimbRuleService.UpdateEventLifecycle(climb);
				ClimbRuleService.ReplenishEncounterSlots(climb, _save.runMapSeed, primaryLoadout);
				if (appliedTime > 0)
				{
					ClimbRuleService.RerollEncounterMutationTargets(climb, _save.runMapSeed, primaryLoadout);
				}
				bool reachedFinalTime = ClimbRuleService.ClampTime(climb.time) >= ClimbRuleService.MaxTime;
				if (!Persist())
				{
					RestoreClimbEventTransactionBackup(backup);
					return false;
				}

				result = new ClimbEventMutationResult
				{
					Succeeded = true,
					EventSlotId = slot.id,
					UpgradedEntryId = upgradedEntryId,
					UpgradedCardKey = upgradedCardKey,
					ReachedFinalTime = reachedFinalTime,
				};
				return true;
			}
		}

		public static void EnsureClimbState()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				if (!_save.isRunActive) return;
				if (_save.climb != null
					&& _save.climb.shopSlots != null
					&& _save.climb.shopSlots.Count == ClimbRuleService.ShopSlotCount
					&& _save.climb.encounterSlots != null
					&& _save.climb.encounterSlots.Count == ClimbRuleService.EncounterSlotCount
					&& _save.climb.eventSlots != null
					&& _save.climb.eventSlots.Count == ClimbRuleService.EventSlotCount)
				{
					return;
				}

				EnsurePrimaryLoadout(_save);
				_save.climb = ClimbRuleService.CreateInitialState(_save.runMapSeed, _save.loadouts[0]);
				Persist();
			}
		}

		private static IEnumerable<string> EnumerateSaveFilePaths()
		{
			string primary = ResolvePrimarySaveFilePath(ensureSaveDirectory: false);
			if (!string.IsNullOrEmpty(primary)) yield return primary;
			string legacy = ResolveLegacyFilePath();
			if (!string.IsNullOrEmpty(legacy)) yield return legacy;
		}

		private static string ResolveFilePath()
		{
			string path = ResolvePrimarySaveFilePath(ensureSaveDirectory: true);
			if (!string.IsNullOrEmpty(path)) _filePath = path;
			return path;
		}

		private static string ResolvePrimarySaveFilePath(bool ensureSaveDirectory)
		{
			if (!string.IsNullOrEmpty(_filePath)) return _filePath;
			try
			{
				var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData);
				if (string.IsNullOrEmpty(appData)) return string.Empty;
				var saveDir = Path.Combine(appData, "Crusaders30XX");
				if (ensureSaveDirectory && !Directory.Exists(saveDir)) Directory.CreateDirectory(saveDir);
				return Path.Combine(saveDir, "save_file.json");
			}
			catch
			{
				return string.Empty;
			}
		}

		/// <summary>
		/// Legacy project-relative save path used by older builds.
		/// Kept for one-time migration into the LocalApplicationData location.
		/// </summary>
		private static string ResolveLegacyFilePath()
		{
			try
			{
				string root = FindProjectRootContaining("Crusaders30XX.csproj");
				if (string.IsNullOrEmpty(root)) return string.Empty;
				return Path.Combine(root, "ECS", "Data", "save_file.json");
			}
			catch
			{
				return string.Empty;
			}
		}

		private static string FindProjectRootContaining(string filename)
		{
			try
			{
				var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
				for (int i = 0; i < 6 && dir != null; i++)
				{
					var candidate = Path.Combine(dir.FullName, filename);
					if (File.Exists(candidate)) return dir.FullName;
					dir = dir.Parent;
				}
			}
			catch { }
			return null;
		}

		public static string AllocateRunDeckEntryId()
		{
			EnsureLoaded();
			lock (_lock)
			{
				string entryId = AllocateRunDeckEntryIdLocked();
				Persist();
				return entryId;
			}
		}

		public static LoadoutCardEntry GetRunDeckEntry(string loadoutId, string entryId)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId)) return null;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save.loadouts.FirstOrDefault(l => l.id == loadoutId);
				return CloneLoadoutEntry(FindEntry(loadout, entryId));
			}
		}

		public static LoadoutCardEntry AddRunDeckEntry(
			string loadoutId,
			string cardKey,
			bool isStarter = false,
			bool countsAsTraded = false,
			bool publishChange = true)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(cardKey)) return null;
			EnsureLoaded();
			lock (_lock)
			{
				_save ??= new SaveFile();
				_save.loadouts ??= new List<LoadoutDefinition>();
				var loadout = _save.loadouts.FirstOrDefault(l => l.id == loadoutId);
				if (loadout == null)
				{
					loadout = new LoadoutDefinition { id = loadoutId, name = loadoutId };
					_save.loadouts.Add(loadout);
				}
				loadout.cards ??= new List<LoadoutCardEntry>();
				var entry = CreateEntryLocked(cardKey, isStarter, countsAsTraded);
				loadout.cards.Add(entry);
				Persist();
				if (publishChange)
				{
					EventManager.Publish(new LoadoutCardAdded { LoadoutId = loadoutId, EntryId = entry.entryId, CardKey = cardKey });
				}
				return CloneLoadoutEntry(entry);
			}
		}

		public static bool TryRemoveRunDeckEntry(
			string loadoutId,
			string entryId,
			out LoadoutCardEntry removedEntry,
			bool publishChange = true)
		{
			removedEntry = null;
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				int index = FindEntryIndex(loadout, entryId);
				if (index < 0) return false;
				var removed = loadout.cards[index];
				loadout.cards.RemoveAt(index);
				Persist();
				if (publishChange)
				{
					EventManager.Publish(new LoadoutCardRemoved { LoadoutId = loadoutId, EntryId = removed.entryId, CardKey = removed.cardKey });
				}
				removedEntry = CloneLoadoutEntry(removed);
				return true;
			}
		}

		public static bool TryReplaceRunDeckEntry(
			string loadoutId,
			string outgoingEntryId,
			string incomingCardKey,
			out LoadoutCardEntry replacementEntry,
			bool countsAsTraded = true,
			bool publishChange = true)
		{
			replacementEntry = null;
			if (string.IsNullOrWhiteSpace(loadoutId)
				|| string.IsNullOrWhiteSpace(outgoingEntryId)
				|| string.IsNullOrWhiteSpace(incomingCardKey)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				int index = FindEntryIndex(loadout, outgoingEntryId);
				if (index < 0) return false;
				var outgoing = loadout.cards[index];
				var incoming = CreateEntryLocked(incomingCardKey, isStarter: false, countsAsTraded);
				loadout.cards[index] = incoming;
				Persist();
				if (publishChange)
				{
					EventManager.Publish(new LoadoutCardRemoved { LoadoutId = loadoutId, EntryId = outgoing.entryId, CardKey = outgoing.cardKey });
					EventManager.Publish(new LoadoutCardAdded { LoadoutId = loadoutId, EntryId = incoming.entryId, CardKey = incoming.cardKey });
				}
				replacementEntry = CloneLoadoutEntry(incoming);
				return true;
			}
		}

		public static bool TryUpgradeRunDeckEntry(
			string loadoutId,
			string entryId,
			string upgradedCardKey,
			out LoadoutCardEntry upgradedEntry)
		{
			upgradedEntry = null;
			if (string.IsNullOrWhiteSpace(loadoutId)
				|| string.IsNullOrWhiteSpace(entryId)
				|| string.IsNullOrWhiteSpace(upgradedCardKey)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				var entry = FindEntry(loadout, entryId);
				if (entry == null || RunDeckService.IsUpgradedCardKey(entry.cardKey)) return false;
				string expected = RunDeckService.BuildUpgradedCardKey(entry.cardKey);
				if (!string.Equals(expected, upgradedCardKey, StringComparison.OrdinalIgnoreCase)) return false;
				string previousCardKey = entry.cardKey;
				entry.cardKey = upgradedCardKey;
				Persist();
				EventManager.Publish(new LoadoutCardRemoved { LoadoutId = loadoutId, EntryId = entry.entryId, CardKey = previousCardKey });
				EventManager.Publish(new LoadoutCardAdded { LoadoutId = loadoutId, EntryId = entry.entryId, CardKey = entry.cardKey });
				upgradedEntry = CloneLoadoutEntry(entry);
				return true;
			}
		}

		public static DeckRewardOfferSave GetPendingDeckRewardOffer()
		{
			EnsureLoaded();
			lock (_lock)
			{
				return CloneDeckRewardOffer(_save?.pendingDeckRewardOffer);
			}
		}

		public static int GetAcceptedDeckRewardMutationCount()
		{
			EnsureLoaded();
			lock (_lock)
			{
				return Math.Max(0, _save?.acceptedDeckRewardMutations ?? 0);
			}
		}

		public static void RecordAcceptedDeckRewardMutation()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return;
				_save.acceptedDeckRewardMutations = Math.Max(0, _save.acceptedDeckRewardMutations) + 1;
				Persist();
			}
		}

		public static void SetPendingDeckRewardOffer(DeckRewardOfferSave offer)
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				_save.pendingDeckRewardOffer = CloneDeckRewardOffer(offer);
				Persist();
			}
		}

		public static void ClearPendingDeckRewardOffer()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null || _save.pendingDeckRewardOffer == null) return;
				_save.pendingDeckRewardOffer = null;
				Persist();
			}
		}

		private static DeckRewardOfferSave CloneDeckRewardOffer(DeckRewardOfferSave offer)
		{
			if (offer == null) return null;
			var clone = new DeckRewardOfferSave
			{
				rewardGold = offer.rewardGold,
				options = new List<DeckRewardOfferOptionSave>()
			};
			if (offer.options == null) return clone;
			foreach (var option in offer.options)
			{
				if (option == null) continue;
				clone.options.Add(new DeckRewardOfferOptionSave
				{
					kind = option.kind ?? string.Empty,
					loadoutIndex = option.loadoutIndex,
					outgoingEntryId = option.outgoingEntryId ?? string.Empty,
					outgoingCardKey = option.outgoingCardKey ?? string.Empty,
					incomingCardKey = option.incomingCardKey ?? string.Empty,
					upgradedCardKey = option.upgradedCardKey ?? string.Empty,
				});
			}
			return clone;
		}

		public static bool TrySpendGoldAndAddToCollection(string itemId, int price, ForSaleItemType itemType, out int newGold)
		{
			newGold = 0;
			if (price < 0) price = 0;

			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return false;
				if (_save.gold < price) return false;

				EnsurePrimaryLoadout(_save);
				var loadout = _save.loadouts[0];

				if (!string.IsNullOrWhiteSpace(itemId) && IsItemOwned(itemId, itemType))
				{
					return false;
				}

				string shopCardKey = null;
				if (itemType == ForSaleItemType.Card && !string.IsNullOrWhiteSpace(itemId))
				{
					shopCardKey = PickShopCardKey(itemId, loadout.cards.Select(entry => entry.cardKey).ToList());
					if (string.IsNullOrEmpty(shopCardKey)) return false;
				}

				_save.gold = System.Math.Max(0, _save.gold - price);

				if (!string.IsNullOrWhiteSpace(itemId))
				{
					if (itemType == ForSaleItemType.Card)
					{
						loadout.cards.Add(CreateEntryLocked(shopCardKey, isStarter: false, countsAsTraded: false));
					}
					else if (itemType == ForSaleItemType.Weapon)
					{
						loadout.weaponId = itemId;
					}
					else if (itemType == ForSaleItemType.Medal)
					{
						loadout.medalIds.Add(itemId);
					}
					else if (itemType == ForSaleItemType.Equipment)
					{
						EquipmentBase equipment = EquipmentFactory.Create(itemId);
						switch (equipment.Slot)
						{
							case EquipmentSlot.Chest:
								loadout.chestId = itemId;
								break;
							case EquipmentSlot.Legs:
								loadout.legsId = itemId;
								break;
							case EquipmentSlot.Arms:
								loadout.armsId = itemId;
								break;
							case EquipmentSlot.Head:
								loadout.headId = itemId;
								break;
						}
					}
				}

				Persist();
				if (!string.IsNullOrEmpty(shopCardKey))
				{
					var added = loadout.cards.Last();
					EventManager.Publish(new LoadoutCardAdded { LoadoutId = loadout.id, EntryId = added.entryId, CardKey = shopCardKey });
				}
				newGold = _save.gold;
				return true;
			}
		}

		private static string PickShopCardKey(string cardId, List<string> deckKeys)
		{
			if (DeckRules.CountCardIdInDeck(deckKeys, cardId) >= DeckRules.MaxCopiesPerCardId) return null;
			var deckKeySet = new HashSet<string>(deckKeys ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
			string[] colors = { "Red", "White", "Black" };
			var eligible = new List<string>();
			foreach (var color in colors)
			{
				string key = $"{cardId}|{color}";
				if (!deckKeySet.Contains(key)) eligible.Add(key);
			}
			if (eligible.Count == 0) return null;
			return eligible[System.Random.Shared.Next(eligible.Count)];
		}

		public static bool HasSeenTutorial(string key)
		{
			if (string.IsNullOrEmpty(key)) return false;
			EnsureLoaded();
			if (_save == null || _save.seenTutorials == null) return false;
			return _save.seenTutorials.Contains(key);
		}

		public static void MarkTutorialSeen(string key)
		{
			if (string.IsNullOrEmpty(key)) return;
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				if (_save.seenTutorials == null) _save.seenTutorials = new List<string>();
				if (!_save.seenTutorials.Contains(key))
				{
					_save.seenTutorials.Add(key);
					Persist();
				}
			}
		}

		public static bool IsGuidedTutorialCompleted()
		{
			EnsureLoaded();
			return _save?.guidedTutorialCompleted == true;
		}

		public static void CompleteGuidedTutorial()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = CreateInactiveSavePreservingMeta(null);
				_save.guidedTutorialCompleted = true;
				_save.seenTutorials ??= new List<string>();
				foreach (string key in GuidedTutorialDefinitions.CoveredTutorialKeys.Append(GuidedTutorialDefinitions.CompletionTutorialKey))
				{
					if (!_save.seenTutorials.Contains(key))
					{
						_save.seenTutorials.Add(key);
					}
				}
				Persist();
			}
		}

		/// <summary>
		/// Persist achievement data to disk.
		/// Called by AchievementManager when progress is updated.
		/// </summary>
		public static void PersistAchievements()
		{
			lock (_lock)
			{
				Persist();
			}
		}

		public static void ClearRunScopedState()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				_save.runLongPassives = new Dictionary<string, int>();
				foreach (var loadout in _save.loadouts ?? new List<LoadoutDefinition>())
				{
					foreach (var entry in loadout?.cards ?? new List<LoadoutCardEntry>())
					{
						entry.restrictions = new List<string>();
					}
				}
				Persist();
			}
		}

		public static IReadOnlyDictionary<string, int> GetRunLongPassivesSnapshot()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save?.runLongPassives == null) return new Dictionary<string, int>();
				return new Dictionary<string, int>(_save.runLongPassives);
			}
		}

		public static void SetRunLongPassive(string passiveTypeName, int stacks)
		{
			if (string.IsNullOrWhiteSpace(passiveTypeName)) return;
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				if (_save.runLongPassives == null) _save.runLongPassives = new Dictionary<string, int>();
				if (stacks <= 0) _save.runLongPassives.Remove(passiveTypeName);
				else _save.runLongPassives[passiveTypeName] = stacks;
				Persist();
			}
		}

		public static List<string> GetRunDeckEntryRestrictions(string loadoutId, string entryId)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId)) return new List<string>();
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				var entry = FindEntry(loadout, entryId);
				return CloneStringList(entry?.restrictions);
			}
		}

		public static bool AddRunDeckEntryRestriction(string loadoutId, string entryId, string restrictionName)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(restrictionName)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				var entry = FindEntry(loadout, entryId);
				if (entry == null) return false;
				entry.restrictions ??= new List<string>();
				if (!entry.restrictions.Contains(restrictionName, StringComparer.OrdinalIgnoreCase))
				{
					entry.restrictions.Add(restrictionName);
					Persist();
				}
				return true;
			}
		}

		public static bool RemoveRunDeckEntryRestriction(string loadoutId, string entryId, string restrictionName)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId) || string.IsNullOrWhiteSpace(restrictionName)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				var entry = FindEntry(loadout, entryId);
				if (entry == null) return false;
				entry.restrictions ??= new List<string>();
				int removed = entry.restrictions.RemoveAll(r => string.Equals(r, restrictionName, StringComparison.OrdinalIgnoreCase));
				if (removed > 0) Persist();
				return true;
			}
		}

		public static bool SetRunDeckEntryRestrictions(string loadoutId, string entryId, IReadOnlyCollection<string> restrictionNames)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				var entry = FindEntry(loadout, entryId);
				if (entry == null) return false;
				entry.restrictions = restrictionNames == null
					? new List<string>()
					: restrictionNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				Persist();
				return true;
			}
		}

		public static bool SetRunDeckEntryRestrictionState(
			string loadoutId,
			string entryId,
			IReadOnlyCollection<string> restrictionNames,
			IReadOnlyDictionary<string, int> restrictionStacks)
		{
			if (string.IsNullOrWhiteSpace(loadoutId) || string.IsNullOrWhiteSpace(entryId)) return false;
			EnsureLoaded();
			lock (_lock)
			{
				var loadout = _save?.loadouts?.FirstOrDefault(l => l.id == loadoutId);
				var entry = FindEntry(loadout, entryId);
				if (entry == null) return false;
				entry.restrictions = restrictionNames == null
					? new List<string>()
					: restrictionNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
				entry.restrictionStacks = restrictionStacks == null
					? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
					: restrictionStacks
						.Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0)
						.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
				Persist();
				return true;
			}
		}

		private static ClimbSaveState CloneClimbState(ClimbSaveState state)
		{
			if (state == null) return null;
			return new ClimbSaveState
			{
				startingWeaponId = string.IsNullOrWhiteSpace(state.startingWeaponId) ? "sword" : state.startingWeaponId,
				difficulty = state.difficulty,
				time = ClimbRuleService.ClampTime(state.time),
				resources = CloneClimbResources(state.resources),
				shopSlots = CloneClimbShopSlots(state.shopSlots),
				encounterSlots = CloneClimbEncounterSlots(state.encounterSlots),
				eventSlots = CloneClimbEventSlots(state.eventSlots),
				shownMedalIds = CloneStringList(state.shownMedalIds),
				shownEquipmentIds = CloneStringList(state.shownEquipmentIds),
				pendingReplacementOffer = CloneClimbReplacementOffer(state.pendingReplacementOffer),
				pendingEncounterReward = CloneClimbEncounterReward(state.pendingEncounterReward),
				pendingEvent = CloneClimbPendingEvent(state.pendingEvent),
				nextBattleBonus = CloneClimbNextBattleBonus(state.nextBattleBonus),
				nextBattlePenalty = CloneClimbNextBattlePenalty(state.nextBattlePenalty),
			};
		}

		private static ClimbResourceSave CloneClimbResources(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = Math.Max(0, resources?.red ?? 0),
				white = Math.Max(0, resources?.white ?? 0),
				black = Math.Max(0, resources?.black ?? 0),
			};
		}

		private static List<ClimbShopSlotSave> CloneClimbShopSlots(List<ClimbShopSlotSave> slots)
		{
			var clone = new List<ClimbShopSlotSave>();
			if (slots == null) return clone;
			foreach (var slot in slots)
			{
				if (slot == null) continue;
				clone.Add(new ClimbShopSlotSave
				{
					id = slot.id ?? string.Empty,
					kind = slot.kind ?? ClimbShopSlotKinds.Empty,
					itemId = slot.itemId ?? string.Empty,
					cardKey = slot.cardKey ?? string.Empty,
					deckEntryId = slot.deckEntryId ?? string.Empty,
					deckIndex = slot.deckIndex,
					cost = CloneClimbResources(slot.cost),
					timeCost = Math.Max(0, slot.timeCost),
					isSold = slot.isSold,
					generatedAtTime = ClimbRuleService.ClampTime(slot.generatedAtTime),
				});
			}
			return clone;
		}

		private static List<ClimbEncounterSlotSave> CloneClimbEncounterSlots(List<ClimbEncounterSlotSave> slots)
		{
			var clone = new List<ClimbEncounterSlotSave>();
			if (slots == null) return clone;
			foreach (var slot in slots)
			{
				if (slot == null) continue;
				clone.Add(new ClimbEncounterSlotSave
				{
					id = slot.id ?? string.Empty,
					enemyId = slot.enemyId ?? string.Empty,
					generatedAtTime = ClimbRuleService.ClampTime(slot.generatedAtTime),
					duration = Math.Max(0, slot.duration),
					timeCost = Math.Max(0, slot.timeCost),
					battleLocation = slot.battleLocation,
					rewardResources = CloneClimbResources(slot.rewardResources),
					hasDeckReward = slot.hasDeckReward,
					isCompleted = slot.isCompleted,
					isFinal = slot.isFinal,
					cardMutationRestrictionName = slot.cardMutationRestrictionName ?? string.Empty,
					cardMutationDeckEntryId = slot.cardMutationDeckEntryId ?? string.Empty,
					cardMutationCardKey = slot.cardMutationCardKey ?? string.Empty,
				});
			}
			return clone;
		}

		private static List<ClimbEventSlotSave> CloneClimbEventSlots(List<ClimbEventSlotSave> slots)
		{
			var clone = new List<ClimbEventSlotSave>();
			if (slots == null) return clone;
			foreach (var slot in slots)
			{
				if (slot == null) continue;
				clone.Add(new ClimbEventSlotSave
				{
					id = slot.id ?? string.Empty,
					definitionId = slot.definitionId ?? string.Empty,
					kind = slot.kind,
					hazardEffect = slot.hazardEffect,
					characterReward = slot.characterReward,
					scheduledAppearanceTime = ClimbRuleService.ClampTime(slot.scheduledAppearanceTime),
					activatedAtTime = slot.activatedAtTime < 0 ? -1 : ClimbRuleService.ClampTime(slot.activatedAtTime),
					duration = Math.Max(0, slot.duration),
					timeCost = Math.Max(0, slot.timeCost),
					effectAmount = Math.Max(0, slot.effectAmount),
					rewardResources = CloneClimbResources(slot.rewardResources),
					status = slot.status,
				});
			}
			return clone;
		}

		private static ClimbEventSlotSave CloneClimbEventSlot(ClimbEventSlotSave slot)
		{
			return CloneClimbEventSlots(slot == null ? null : new List<ClimbEventSlotSave> { slot }).FirstOrDefault();
		}

		private static ClimbEventSlotSave FindClimbEventSlot(ClimbSaveState climb, string eventSlotId)
		{
			return climb?.eventSlots?.FirstOrDefault(slot => slot != null
				&& string.Equals(slot.id, eventSlotId, StringComparison.OrdinalIgnoreCase));
		}

		private sealed class ClimbEventTransactionBackup
		{
			public ClimbSaveState Climb { get; init; }
			public LoadoutDefinition PrimaryLoadout { get; init; }
			public Dictionary<string, int> RunLongPassives { get; init; }
		}

		private static ClimbEventTransactionBackup CaptureClimbEventTransactionBackup()
		{
			return new ClimbEventTransactionBackup
			{
				Climb = CloneClimbState(_save?.climb),
				PrimaryLoadout = CloneLoadout(_save?.loadouts?.FirstOrDefault(loadout =>
					string.Equals(loadout.id, RunDeckService.PrimaryLoadoutId, StringComparison.OrdinalIgnoreCase))),
				RunLongPassives = _save?.runLongPassives == null
					? new Dictionary<string, int>()
					: new Dictionary<string, int>(_save.runLongPassives),
			};
		}

		private static void RestoreClimbEventTransactionBackup(ClimbEventTransactionBackup backup)
		{
			if (_save == null || backup == null) return;
			_save.climb = CloneClimbState(backup.Climb);
			_save.runLongPassives = new Dictionary<string, int>(backup.RunLongPassives ?? new Dictionary<string, int>());
			_save.loadouts ??= new List<LoadoutDefinition>();
			int index = _save.loadouts.FindIndex(loadout => loadout != null
				&& string.Equals(loadout.id, RunDeckService.PrimaryLoadoutId, StringComparison.OrdinalIgnoreCase));
			if (backup.PrimaryLoadout == null)
			{
				if (index >= 0) _save.loadouts.RemoveAt(index);
				return;
			}

			var restored = CloneLoadout(backup.PrimaryLoadout);
			if (index >= 0) _save.loadouts[index] = restored;
			else _save.loadouts.Add(restored);
		}

		private static int AddRunLongPassiveStacksLocked(string passiveTypeName, int amount)
		{
			if (string.IsNullOrWhiteSpace(passiveTypeName) || amount <= 0) return 0;
			_save.runLongPassives ??= new Dictionary<string, int>();
			string existingKey = _save.runLongPassives.Keys
				.FirstOrDefault(key => string.Equals(key, passiveTypeName, StringComparison.OrdinalIgnoreCase));
			string key = string.IsNullOrWhiteSpace(existingKey) ? passiveTypeName : existingKey;
			_save.runLongPassives.TryGetValue(key, out int current);
			int total = Math.Max(0, current) + amount;
			_save.runLongPassives[key] = total;
			return total;
		}

		private static ClimbReplacementOfferSave CloneClimbReplacementOffer(ClimbReplacementOfferSave offer)
		{
			if (offer == null) return null;
			return new ClimbReplacementOfferSave
			{
				shopSlotIndex = offer.shopSlotIndex,
				incomingCardKey = offer.incomingCardKey ?? string.Empty,
				cost = CloneClimbResources(offer.cost),
			};
		}

		private static ClimbEncounterRewardSave CloneClimbEncounterReward(ClimbEncounterRewardSave reward)
		{
			if (reward == null) return null;
			return new ClimbEncounterRewardSave
			{
				encounterSlotId = reward.encounterSlotId ?? string.Empty,
				resources = CloneClimbResources(reward.resources),
				deckRewardOffer = CloneDeckRewardOffer(reward.deckRewardOffer),
				pendingFinalEncounter = reward.pendingFinalEncounter,
			};
		}

		private static ClimbPendingEventSave CloneClimbPendingEvent(ClimbPendingEventSave pending)
		{
			if (pending == null) return null;
			return new ClimbPendingEventSave
			{
				eventSlotId = pending.eventSlotId ?? string.Empty,
				phase = pending.phase,
				dialogueRequestId = pending.dialogueRequestId ?? string.Empty,
			};
		}

		private static ClimbNextBattleBonusSave CloneClimbNextBattleBonus(ClimbNextBattleBonusSave bonus)
		{
			return new ClimbNextBattleBonusSave
			{
				courage = Math.Max(0, bonus?.courage ?? 0),
				temperance = Math.Max(0, bonus?.temperance ?? 0),
				vigor = Math.Max(0, bonus?.vigor ?? 0),
			};
		}

		private static ClimbNextBattlePenaltySave CloneClimbNextBattlePenalty(ClimbNextBattlePenaltySave penalty)
		{
			return new ClimbNextBattlePenaltySave
			{
				burn = Math.Max(0, penalty?.burn ?? 0),
				fear = Math.Max(0, penalty?.fear ?? 0),
			};
		}

		private static List<string> CloneStringList(List<string> list)
		{
			return list == null
				? new List<string>()
				: list.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		private static void AddMissingCollectionItems(List<string> collectionIds, IEnumerable<string> itemIds)
		{
			if (collectionIds == null || itemIds == null) return;
			var existing = new HashSet<string>(collectionIds, StringComparer.OrdinalIgnoreCase);
			foreach (var itemId in itemIds)
			{
				if (!string.IsNullOrWhiteSpace(itemId) && existing.Add(itemId))
				{
					collectionIds.Add(itemId);
				}
			}
		}

		private static PlayerCollectionSave CreateInitialCollection()
		{
			var collection = new PlayerCollectionSave
			{
				cardIds = new List<string>
				{
					"consecrate", "crimson_rite", "crusade", "divine_protection",
					"dowse_with_holy_water", "fury", "impale", "lacerate", "pierce_through",
					"pouch_of_kunai", "quick_wit", "ravage", "reap", "shield_of_faith",
					"strike", "sudden_thrust", "temper_the_blade", "tempest", "zealous_vow",
				},
				medalIds = new List<string>
				{
					"st_augustine", "st_bartholomew", "st_clare", "st_francis_de_sales",
					"st_homobonus", "st_joan_of_arc", "st_luke", "st_michael", "st_sebastian",
				},
				equipmentIds = new List<string>
				{
					"scarlet_coif", "scarlet_treads", "scarlet_vest", "scarlet_wraps",
					"ivory_coif", "ivory_treads", "ivory_vest", "ivory_wraps",
					"knightly_chest", "knightly_grieves", "knightly_gauntlets", "knightly_helm",
				},
			};
			foreach (var cardId in StartingDeckGeneratorService.DefaultStarterCardPool)
			{
				if (!collection.cardIds.Contains(cardId, StringComparer.OrdinalIgnoreCase))
					collection.cardIds.Add(cardId);
			}
			return CloneCollection(collection);
		}

		private static PlayerCollectionSave CloneCollection(PlayerCollectionSave collection)
		{
			if (collection == null) return null;
			var clone = new PlayerCollectionSave
			{
				cardIds = CloneStringList(collection.cardIds),
				medalIds = CloneStringList(collection.medalIds),
				equipmentIds = CloneStringList(collection.equipmentIds),
				totalPoints = Math.Max(0, collection.totalPoints),
				pendingClimbPoints = Math.Max(0, collection.pendingClimbPoints),
				processedRewardLevels = Math.Max(0, collection.processedRewardLevels),
				pendingBoosterPacks = new List<BoosterPackSave>(),
			};
			foreach (var pack in collection.pendingBoosterPacks ?? new List<BoosterPackSave>())
			{
				if (pack?.rewards == null || pack.rewards.Count == 0) continue;
				clone.pendingBoosterPacks.Add(new BoosterPackSave
				{
					rewards = pack.rewards
						.Where(reward => reward != null && !string.IsNullOrWhiteSpace(reward.kind) && !string.IsNullOrWhiteSpace(reward.id))
						.Select(reward => new BoosterPackRewardSave
						{
							kind = reward.kind,
							id = reward.id,
							cardColor = string.IsNullOrWhiteSpace(reward.cardColor) ? "White" : reward.cardColor,
						})
						.ToList(),
				});
			}
			return clone;
		}

		private static void EnsureWayStationMetaLocked()
		{
			if (_save == null) _save = new SaveFile();
			_save.waystation = CloneWayStationMeta(_save.waystation);
		}

		private static bool DidCurrentClimbReachNpcDialogueThreshold()
		{
			int threshold = Math.Max(1, (ClimbRuleService.MaxTime + 1) / 2);
			return ClimbRuleService.ClampTime(_save?.climb?.time ?? 0) >= threshold;
		}

		private static WayStationMetaSave CloneWayStationMeta(WayStationMetaSave meta)
		{
			var clone = new WayStationMetaSave
			{
				climbAttempts = Math.Max(0, meta?.climbAttempts ?? 0),
				climbCompletions = Math.Max(0, meta?.climbCompletions ?? 0),
				completedClimbs = CloneCompletedClimbs(meta?.completedClimbs),
				deferredNpcDialogueCounter = Math.Clamp(meta?.deferredNpcDialogueCounter ?? 0, 0, WayStationNpcDialogueCounterThreshold),
				pendingNpcDialogueOffer = meta?.pendingNpcDialogueOffer == true,
				purchasedMedalIds = CloneStringList(meta?.purchasedMedalIds),
				completedDialogueSegments = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase),
				currentVisit = CloneWayStationVisit(meta?.currentVisit) ?? new WayStationVisitSave(),
			};

			if (meta?.completedDialogueSegments != null)
			{
				foreach (var kvp in meta.completedDialogueSegments)
				{
					if (string.IsNullOrWhiteSpace(kvp.Key)) continue;
					clone.completedDialogueSegments[kvp.Key] = CloneStringList(kvp.Value);
				}
			}

			return clone;
		}

		private static void RecordCompletedClimbLocked(ClimbSaveState climb)
		{
			if (climb == null) return;
			string weaponId = string.IsNullOrWhiteSpace(climb.startingWeaponId)
				? "sword"
				: climb.startingWeaponId.Trim().ToLowerInvariant();
			RecordCompletedClimbLocked(weaponId, climb.difficulty);
		}

		private static void RecordCompletedClimbLocked(string weaponId, RunDifficulty difficulty)
		{
			_save.waystation.completedClimbs ??= new List<CompletedClimbSave>();
			if (_save.waystation.completedClimbs.Any(entry =>
				entry != null
					&& string.Equals(entry.startingWeaponId, weaponId, StringComparison.OrdinalIgnoreCase)
					&& entry.difficulty == difficulty))
			{
				return;
			}

			_save.waystation.completedClimbs.Add(new CompletedClimbSave
			{
				startingWeaponId = weaponId,
				difficulty = difficulty,
			});
		}

		private static List<CompletedClimbSave> CloneCompletedClimbs(IEnumerable<CompletedClimbSave> completedClimbs)
		{
			if (completedClimbs == null) return new List<CompletedClimbSave>();
			return completedClimbs
				.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.startingWeaponId))
				.GroupBy(
					entry => $"{entry.startingWeaponId.Trim().ToLowerInvariant()}|{entry.difficulty}",
					StringComparer.OrdinalIgnoreCase)
				.Select(group => group.First())
				.Select(entry => new CompletedClimbSave
				{
					startingWeaponId = entry.startingWeaponId.Trim().ToLowerInvariant(),
					difficulty = entry.difficulty,
				})
				.ToList();
		}

		private static WayStationVisitSave CloneWayStationVisit(WayStationVisitSave visit)
		{
			if (visit == null) return new WayStationVisitSave();
			var clone = new WayStationVisitSave
			{
				initialized = visit.initialized,
				offers = new List<WayStationDialogueOfferSave>(),
			};

			if (visit.offers == null) return clone;
			foreach (var offer in visit.offers)
			{
				if (offer == null || string.IsNullOrWhiteSpace(offer.offerId)) continue;
				clone.offers.Add(new WayStationDialogueOfferSave
				{
					offerId = offer.offerId ?? string.Empty,
					characterId = offer.characterId ?? string.Empty,
					definitionId = offer.definitionId ?? string.Empty,
					segmentId = offer.segmentId ?? string.Empty,
					screenX = offer.screenX,
					screenY = offer.screenY,
					visible = offer.visible,
				});
			}

			return clone;
		}

		private static string AllocateRunDeckEntryIdLocked()
		{
			_save ??= new SaveFile();
			int next = Math.Max(0, _save.nextRunDeckEntryId);
			_save.nextRunDeckEntryId = next + 1;
			return $"run_card_{next}";
		}

		private static LoadoutCardEntry CreateEntryLocked(string cardKey, bool isStarter, bool countsAsTraded)
		{
			return new LoadoutCardEntry
			{
				entryId = AllocateRunDeckEntryIdLocked(),
				cardKey = cardKey?.Trim() ?? string.Empty,
				isStarter = isStarter,
				countsAsTraded = countsAsTraded,
				restrictions = new List<string>(),
				restrictionStacks = new Dictionary<string, int>(),
			};
		}

		private static int FindEntryIndex(LoadoutDefinition loadout, string entryId)
		{
			if (loadout?.cards == null || string.IsNullOrWhiteSpace(entryId)) return -1;
			return loadout.cards.FindIndex(entry => entry != null
				&& string.Equals(entry.entryId, entryId, StringComparison.Ordinal));
		}

		private static LoadoutCardEntry FindEntry(LoadoutDefinition loadout, string entryId)
		{
			int index = FindEntryIndex(loadout, entryId);
			return index < 0 ? null : loadout.cards[index];
		}

		private static LoadoutCardEntry CloneLoadoutEntry(LoadoutCardEntry entry)
		{
			if (entry == null) return null;
			return new LoadoutCardEntry
			{
				entryId = entry.entryId ?? string.Empty,
				cardKey = entry.cardKey ?? string.Empty,
				isStarter = entry.isStarter,
				countsAsTraded = entry.countsAsTraded,
				restrictions = CloneStringList(entry.restrictions),
				restrictionStacks = entry.restrictionStacks == null
					? new Dictionary<string, int>()
					: new Dictionary<string, int>(entry.restrictionStacks, StringComparer.OrdinalIgnoreCase),
			};
		}

		private static LoadoutDefinition CloneLoadout(LoadoutDefinition loadout)
		{
			if (loadout == null) return null;
			return new LoadoutDefinition
			{
				id = loadout.id,
				name = loadout.name,
				cards = (loadout.cards ?? new List<LoadoutCardEntry>()).Select(CloneLoadoutEntry).Where(entry => entry != null).ToList(),
				weaponId = loadout.weaponId,
				temperanceId = loadout.temperanceId,
				chestId = loadout.chestId,
				legsId = loadout.legsId,
				armsId = loadout.armsId,
				headId = loadout.headId,
				medalIds = CloneStringList(loadout.medalIds),
			};
		}
	}
}
