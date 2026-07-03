using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Data.Climb;
using Crusaders30XX.Diagnostics;

namespace Crusaders30XX.ECS.Data.Save
{
	public static class SaveCache
	{
		private static SaveFile _save;
		private static string _filePath;
		private static readonly object _lock = new object();

		public static SaveFile GetAll()
		{
			EnsureLoaded();
			return _save;
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
			string temperanceId)
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = CreateDefaultRunSave();
				EnsureRunMap();
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

				_save.climb = ClimbRuleService.CreateInitialState(_save.runMapSeed, savedLoadout);
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

		public static int GetValueOrDefault(string locationId, int defaultValue = 0)
		{
			EnsureLoaded();
			EnsureRunMap();
			if (_save?.runMapNodes == null) return defaultValue;
			int count = 0;
			foreach (var node in _save.runMapNodes)
			{
				if (node != null && node.isCompleted) count++;
			}
			return count;
		}

		public static void EnsureRunMap()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				if (!_save.isRunActive) return;
				if (_save.runMapNodes != null && _save.runMapNodes.Count > 0)
				{
					if (SanitizeRunMapEnemyIds())
					{
						Persist();
					}
					return;
				}
				var (seed, nodes, shops, treasures, events) = GenerateRunMapForSave();
				_save.runMapSeed = seed;
				_save.runMapNodes = nodes;
				_save.runMapShops = shops;
				_save.runMapTreasures = treasures;
				_save.runMapEvents = events;
				if (string.IsNullOrEmpty(_save.lastLocation) && nodes.Count > 0)
				{
					_save.lastLocation = nodes[0].id;
				}
				Persist();
			}
		}

		public static IReadOnlyList<RunMapNode> GetRunMapNodes()
		{
			EnsureLoaded();
			if (_save?.isRunActive != true) return new List<RunMapNode>();
			EnsureRunMap();
			return _save?.runMapNodes ?? new List<RunMapNode>();
		}

		public static int RunMapNodeCount => LocationMapConstants.NodeCount;

		public static string GetStartNodeId()
		{
			EnsureRunMap();
			if (_save?.runMapNodes == null || _save.runMapNodes.Count == 0) return "run_0";
			return _save.runMapNodes[0].id;
		}

		public static bool TryGetRunNode(string nodeId, out RunMapNode node, out int index)
		{
			node = null;
			index = -1;
			EnsureRunMap();
			if (_save?.runMapNodes == null || string.IsNullOrEmpty(nodeId)) return false;
			for (int i = 0; i < _save.runMapNodes.Count; i++)
			{
				var n = _save.runMapNodes[i];
				if (n != null && string.Equals(n.id, nodeId, System.StringComparison.OrdinalIgnoreCase))
				{
					node = n;
					index = i;
					return true;
				}
			}
			return false;
		}

		public static bool TryGetRunNode(int index, out RunMapNode node)
		{
			node = null;
			EnsureRunMap();
			if (_save?.runMapNodes == null || index < 0 || index >= _save.runMapNodes.Count) return false;
			node = _save.runMapNodes[index];
			return node != null;
		}

		public static void SetRunNodeRevealed(string nodeId, bool revealed = true)
		{
			if (!TryGetRunNode(nodeId, out var node, out _)) return;
			lock (_lock)
			{
				node.isRevealed = revealed;
				Persist();
			}
		}

		public static void SetRunNodeCompleted(string nodeId, bool completed = true)
		{
			if (!TryGetRunNode(nodeId, out var node, out _)) return;
			lock (_lock)
			{
				node.isCompleted = completed;
				Persist();
			}
		}

		private static bool SanitizeRunMapEnemyIds()
		{
			if (_save?.runMapNodes == null || _save.runMapNodes.Count == 0) return false;
			var pool = EnemyPortraitContent.GetRunMapEnemyPool().ToList();
			if (pool.Count == 0) return false;
			var rng = new System.Random(_save.runMapSeed != 0 ? _save.runMapSeed : 1);
			bool changed = false;
			foreach (var node in _save.runMapNodes)
			{
				if (node == null) continue;
				if (!string.IsNullOrEmpty(node.enemyId) && !EnemyPortraitContent.HasPortrait(node.enemyId))
				{
					node.enemyId = pool[rng.Next(pool.Count)];
					changed = true;
				}

				if (node.battleEnemyIds == null || node.battleEnemyIds.Count == 0) continue;
				for (int i = 0; i < node.battleEnemyIds.Count; i++)
				{
					string id = node.battleEnemyIds[i];
					if (string.IsNullOrEmpty(id) || EnemyPortraitContent.HasPortrait(id)) continue;
					node.battleEnemyIds[i] = pool[rng.Next(pool.Count)];
					changed = true;
				}
			}
			return changed;
		}

		public static bool TryRevealRunNode(string nodeId)
		{
			if (!TryGetRunNode(nodeId, out var node, out _)) return false;
			if (node.isRevealed) return false;
			lock (_lock)
			{
				node.isRevealed = true;
				Persist();
			}
			return true;
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

		public static bool IsQuestCompleted(string locationId, string questId)
		{
			EnsureRunMap();
			if (string.IsNullOrEmpty(questId)) return false;
			return TryGetRunNode(questId, out var node, out _) && node.isCompleted;
		}

		public static bool IsStartQuestCompleted()
		{
			return IsQuestCompleted(null, GetStartNodeId());
		}

		public static void SetQuestCompleted(string locationId, string questId, bool completed)
		{
			SetRunNodeCompleted(questId, completed);
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

		public static void SetLastLocation(string locationId)
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				_save.lastLocation = locationId ?? string.Empty;
				Persist();
			}
		}

		public static void SetPendingBattleNode(string nodeId)
		{
			if (string.IsNullOrEmpty(nodeId)) return;
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				_save.pendingBattleNodeId = nodeId;
				Persist();
			}
		}

		public static void ClearPendingBattle()
		{
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return;
				if (string.IsNullOrEmpty(_save.pendingBattleNodeId)) return;
				_save.pendingBattleNodeId = string.Empty;
				Persist();
			}
		}

		public static bool TryGetResumableBattleNode(out string nodeId)
		{
			nodeId = string.Empty;
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return false;
				nodeId = _save.pendingBattleNodeId ?? string.Empty;
			}

			if (string.IsNullOrEmpty(nodeId)) return false;
			if (!TryGetRunNode(nodeId, out var node, out _))
			{
				ClearPendingBattle();
				return false;
			}

			if (!node.isRevealed || node.isCompleted || node.ResolveBattleEnemyIds().Count == 0)
			{
				ClearPendingBattle();
				return false;
			}

			return true;
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
					EnsureRunMap();
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
			var mastery = prior?.cardMastery;
			var achievements = prior?.achievements;
			var seenTutorials = prior?.seenTutorials;
			int musicVolumeLevel = ClampAudioVolumeLevel(prior?.musicVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL);
			int sfxVolumeLevel = ClampAudioVolumeLevel(prior?.sfxVolumeLevel ?? SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL);
			var save = CreateDefaultRunSave();
			save.cardMastery = mastery ?? new Dictionary<string, CardMastery>();
			save.achievements = achievements ?? new Dictionary<string, AchievementProgress>();
			save.seenTutorials = seenTutorials ?? new List<string>();
			save.guidedTutorialCompleted = prior?.guidedTutorialCompleted == true;
			save.musicVolumeLevel = musicVolumeLevel;
			save.sfxVolumeLevel = sfxVolumeLevel;
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
				runMapSeed = 0,
				runMapNodes = new List<RunMapNode>(),
				runMapShops = new List<RunMapShop>(),
				runMapTreasures = new List<RunMapTreasure>(),
				runMapEvents = new List<RunMapEvent>(),
				items = new List<SaveItem>(),
				lastLocation = string.Empty,
				pendingBattleNodeId = string.Empty,
				loadouts = new List<LoadoutDefinition>(),
				nextRunDeckEntryId = 0,
				runLongPassives = new Dictionary<string, int>(),
				pendingDeckRewardOffer = null,
				climb = new ClimbSaveState(),
				cardMastery = prior?.cardMastery ?? new Dictionary<string, CardMastery>(),
				achievements = prior?.achievements ?? new Dictionary<string, AchievementProgress>(),
				seenTutorials = prior?.seenTutorials ?? new List<string>(),
				guidedTutorialCompleted = prior?.guidedTutorialCompleted == true,
			};
		}

		private static int ClampAudioVolumeLevel(int value)
		{
			return Math.Clamp(value, 0, 100);
		}

		private static SaveFile CreateDefaultRunSave()
		{
			var (seed, nodes, shops, treasures, events) = GenerateRunMapForSave();
			var startingDeck = StartingDeckGeneratorService.Generate(
				StartingDeckGeneratorService.DefaultStarterCardPool,
				seed);
			var save = new SaveFile
			{
				version = SaveFile.CURRENT_VERSION,
				isRunActive = true,
				gold = 4,
				runMapSeed = seed,
				runMapNodes = nodes,
				runMapShops = shops,
				runMapTreasures = treasures,
				runMapEvents = events,
				items = new List<SaveItem>(),
				lastLocation = nodes.Count > 0 ? nodes[0].id : "run_0",
				pendingBattleNodeId = string.Empty,
				pendingDeckRewardOffer = null,
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

		private static (int seed, List<RunMapNode> nodes, List<RunMapShop> shops, List<RunMapTreasure> treasures, List<RunMapEvent> events) GenerateRunMapForSave()
		{
			var (seed, nodes) = LocationMapGeneratorService.Generate();
			var shops = RunMapShopGeneratorService.Generate(seed, nodes);
			var treasures = RunMapTreasureGeneratorService.Generate(seed, nodes, shops);
			var events = RunMapEventGeneratorService.Generate(seed, nodes, shops, treasures);
#if DEBUG
			RunMapGeneratorLog.Append(LocationMapGeneratorService.ComputeSpreadMetrics(seed, nodes));
#endif
			return (seed, nodes, shops, treasures, events);
		}

		public static IReadOnlyList<RunMapShop> GetRunMapShops()
		{
			EnsureLoaded();
			EnsureRunMap();
			return _save?.runMapShops ?? new List<RunMapShop>();
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

		public static bool TryGetRunShop(string shopId, out RunMapShop shop, out int index)
		{
			shop = null;
			index = -1;
			EnsureRunMap();
			return RunMapShopService.TryGetShop(shopId, _save?.runMapShops, out shop, out index);
		}

		public static IReadOnlyList<RunMapTreasure> GetRunMapTreasures()
		{
			EnsureLoaded();
			EnsureRunMap();
			return _save?.runMapTreasures ?? new List<RunMapTreasure>();
		}

		public static bool TryGetRunTreasure(string treasureId, out RunMapTreasure treasure, out int index)
		{
			treasure = null;
			index = -1;
			EnsureRunMap();
			return RunMapTreasureService.TryGetTreasure(treasureId, _save?.runMapTreasures, out treasure, out index);
		}

		public static IReadOnlyList<RunMapEvent> GetRunMapEvents()
		{
			EnsureLoaded();
			EnsureRunMap();
			return _save?.runMapEvents ?? new List<RunMapEvent>();
		}

		public static bool TryGetRunEvent(string eventId, out RunMapEvent mapEvent, out int index)
		{
			mapEvent = null;
			index = -1;
			EnsureRunMap();
			return RunMapEventService.TryGetEvent(eventId, _save?.runMapEvents, out mapEvent, out index);
		}

		public static bool TryCompleteRunMapEvent(string eventId)
		{
			if (string.IsNullOrWhiteSpace(eventId)) return false;

			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return false;
				if (!TryGetRunEvent(eventId, out var mapEvent, out _)) return false;
				if (mapEvent == null || mapEvent.isCompleted) return false;

				mapEvent.isCompleted = true;
				Persist();
				return true;
			}
		}

		public static bool TryClaimRunMapTreasure(
			string treasureId,
			EntityManager entityManager,
			out int rewardGold,
			out string rewardMedalId,
			out string rewardEquipmentId)
		{
			rewardGold = 0;
			rewardMedalId = string.Empty;
			rewardEquipmentId = string.Empty;
			if (string.IsNullOrWhiteSpace(treasureId)) return false;

			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return false;
				if (!TryGetRunTreasure(treasureId, out var treasure, out int index)) return false;
				if (treasure == null || treasure.isClaimed) return false;

				var rng = new Random((_save.runMapSeed ^ 0x71EA5A71) + index);
				rewardGold = System.Math.Max(0, treasure.rewardGold);

				EnsurePrimaryLoadout(_save);
				var loadout = _save.loadouts[0];

				if (treasure.grantsEquipmentReward)
				{
					rewardEquipmentId = RunMapEquipmentPoolService.PickRandomEquipment(
						rng,
						loadout,
						_save.runMapShops,
						excludeShopOffers: true);
					RunMapEquipmentPoolService.ApplyEquipmentToLoadout(loadout, rewardEquipmentId);
				}
				else
				{
					rewardMedalId = RunMapTreasureMedalPoolService.PickRandomMedal(rng, entityManager);
					if (loadout.medalIds == null) loadout.medalIds = new List<string>();
					loadout.medalIds.Add(rewardMedalId);
				}

				treasure.isClaimed = true;
				AddGold(rewardGold);
				Persist();
				return true;
			}
		}

		public static bool TryPurchaseRunMapShopItem(string shopId, int slotIndex, out int newGold)
		{
			newGold = 0;
			if (string.IsNullOrWhiteSpace(shopId) || slotIndex < 0) return false;

			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) return false;
				if (!TryGetRunShop(shopId, out var shop, out _) || shop?.items == null) return false;
				if (slotIndex >= shop.items.Count) return false;

				var item = shop.items[slotIndex];
				if (item == null || item.isPurchased) return false;
				if (string.IsNullOrWhiteSpace(item.cardId)) return false;
				if (!item.IsMedal && !item.IsEquipment && string.IsNullOrWhiteSpace(item.color)) return false;

				int price = System.Math.Max(0, item.price);
				if (_save.gold < price) return false;

				EnsurePrimaryLoadout(_save);
				var loadout = _save.loadouts[0];
				if (loadout.cards == null) loadout.cards = new List<LoadoutCardEntry>();
				if (loadout.medalIds == null) loadout.medalIds = new List<string>();

				if (item.IsEquipment && IsItemOwned(item.cardId, ForSaleItemType.Equipment))
				{
					return false;
				}

				_save.gold = System.Math.Max(0, _save.gold - price);
				item.isPurchased = true;
				if (item.IsMedal)
				{
					loadout.medalIds.Add(item.cardId);
				}
				else if (item.IsEquipment)
				{
					RunMapEquipmentPoolService.ApplyEquipmentToLoadout(loadout, item.cardId);
				}
				else
				{
					string cardKey = $"{item.cardId}|{item.color}";
					var entry = CreateEntryLocked(cardKey, isStarter: false, countsAsTraded: false);
					loadout.cards.Add(entry);
					Persist();
					EventManager.Publish(new LoadoutCardAdded { LoadoutId = loadout.id, EntryId = entry.entryId, CardKey = cardKey });
					newGold = _save.gold;
					return true;
				}

				Persist();
				newGold = _save.gold;
				return true;
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

		private const int PointsToMaster = 50;

		public static CardMastery GetMasteryData(string cardId)
		{
			if (TestFightRuntime.IsActive) return null;
			if (string.IsNullOrEmpty(cardId)) return null;
			EnsureLoaded();
			if (_save == null || _save.cardMastery == null) return null;
			_save.cardMastery.TryGetValue(cardId, out var mastery);
			return mastery;
		}

		public static void AddMasteryPoints(string cardId, int points)
		{
			if (TestFightRuntime.IsActive) return;
			if (string.IsNullOrEmpty(cardId) || points <= 0) return;
			EnsureLoaded();
			lock (_lock)
			{
				if (_save == null) _save = new SaveFile();
				if (_save.cardMastery == null) _save.cardMastery = new Dictionary<string, CardMastery>();

				if (!_save.cardMastery.TryGetValue(cardId, out var mastery))
				{
					mastery = new CardMastery { cardId = cardId, level = 0, points = 0 };
					_save.cardMastery[cardId] = mastery;
				}

				int oldLevel = mastery.level;
				mastery.points += points;

				// Check for level up
				while (mastery.points >= PointsToMaster)
				{
					mastery.points -= PointsToMaster;
					mastery.level++;
				}

				Persist();

				// Publish event if leveled up
				if (mastery.level > oldLevel)
				{
					Crusaders30XX.ECS.Core.EventManager.Publish(new Crusaders30XX.ECS.Events.CardMasteredEvent
					{
						CardId = cardId,
						Level = mastery.level
					});
				}
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

		private static ClimbSaveState CloneClimbState(ClimbSaveState state)
		{
			if (state == null) return null;
			return new ClimbSaveState
			{
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
