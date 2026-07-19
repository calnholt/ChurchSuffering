using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Climb;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Services
{
	public static class ClimbRuleService
	{
		public const int MaxTime = 32;
		public const int ShopSlotCount = 5;
		public const int EncounterSlotCount = 3;
		public const int EventSlotCount = 5;
		public const int ShopRefreshInterval = 8;
		public const int EncounterMinDuration = 2;
		public const int EncounterMaxDuration = 5;
		private const int EncounterMinDistinctEnemies = 2;
		private const int EncounterMinDistinctLocations = 2;
		private const int EncounterMaxSkeletonFamily = 1;
		private const int EncounterDiversityMaxIterations = 6;
		private const int RngSalt = unchecked((int)0xC11A1B00);
		private const int ClimbEventRngSalt = unchecked((int)0xE71E5A17);
		private const int ClimbEventResolutionSalt = unchecked((int)0x4E5A1D0B);

		private static readonly HashSet<string> BannedClimbEncounterEnemyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"horde",
			"sand_corpse",
			"training_demon",
		};

		private static readonly HashSet<string> ClimbSkeletonEnemyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			EnemyId.Skeleton.ToKey(),
			EnemyId.FireSkeleton.ToKey(),
			EnemyId.EarthSkeleton.ToKey(),
			EnemyId.FrostSkeleton.ToKey(),
			EnemyId.CursedSkeleton.ToKey(),
		};

		private static readonly CardData.CardColor[] ResourceColors =
		{
			CardData.CardColor.Red,
			CardData.CardColor.White,
			CardData.CardColor.Black
		};

		public static ClimbSaveState CreateInitialState(
			int seed,
			LoadoutDefinition loadout = null,
			RunDifficulty difficulty = RunDifficulty.Easy)
		{
			var state = new ClimbSaveState
			{
				startingWeaponId = string.IsNullOrWhiteSpace(loadout?.weaponId) ? "sword" : loadout.weaponId,
				difficulty = difficulty,
				time = 0,
				resources = new ClimbResourceSave { red = 1, white = 1, black = 1 },
				shopSlots = new List<ClimbShopSlotSave>(),
				encounterSlots = new List<ClimbEncounterSlotSave>(),
				eventSlots = new List<ClimbEventSlotSave>(),
				shownMedalIds = new List<string>(),
				shownEquipmentIds = new List<string>(),
				nextBattleBonus = new ClimbNextBattleBonusSave(),
				nextBattlePenalty = new ClimbNextBattlePenaltySave(),
			};

			RefreshShopSlots(state, seed, loadout);
			RefreshEncounterSlots(state, seed, loadout);
			RefreshEventSlots(state, seed);
			return state;
		}

		public static int ClampTime(int time) => Math.Clamp(time, 0, MaxTime);

		public static int ApplyTime(ClimbSaveState state, int delta)
		{
			if (state == null) return 0;
			int before = ClampTime(state.time);
			int after = ClampTime(before + Math.Max(0, delta));
			state.time = after;
			return after - before;
		}

		public static bool ShouldRefreshShopAtTime(int previousTime, int newTime)
		{
			previousTime = ClampTime(previousTime);
			newTime = ClampTime(newTime);
			if (newTime <= previousTime) return false;
			int nextBoundary = ((previousTime / ShopRefreshInterval) + 1) * ShopRefreshInterval;
			return nextBoundary < MaxTime && newTime >= nextBoundary;
		}

		public static bool HasPendingFinalEncounter(ClimbSaveState state)
		{
			if (state?.pendingEncounterReward?.pendingFinalEncounter == true) return true;
			return ClampTime(state?.time ?? 0) >= MaxTime;
		}

		public static bool IsEventVisible(ClimbEventSlotSave slot, int time)
		{
			return slot != null
				&& slot.status == ClimbEventStatus.Active
				&& !string.IsNullOrWhiteSpace(slot.definitionId)
				&& slot.activatedAtTime >= 0
				&& ClampTime(time) >= slot.activatedAtTime
				&& ClampTime(time) < slot.activatedAtTime + Math.Max(0, slot.duration);
		}

		public static bool IsEventExpired(ClimbEventSlotSave slot, int time)
		{
			return slot != null
				&& slot.status == ClimbEventStatus.Active
				&& slot.activatedAtTime >= 0
				&& ClampTime(time) >= slot.activatedAtTime + Math.Max(0, slot.duration);
		}

		public static bool IsEncounterExpired(ClimbEncounterSlotSave slot, int time)
		{
			if (slot == null || slot.isCompleted || slot.isFinal || string.IsNullOrWhiteSpace(slot.enemyId)) return false;
			if (slot.duration <= 0) return true;
			return ClampTime(time) >= ClampTime(slot.generatedAtTime + slot.duration);
		}

		public static bool UpdateEventLifecycle(ClimbSaveState state)
		{
			if (state?.eventSlots == null) return false;
			bool changed = false;
			int currentTime = ClampTime(state.time);

			if (currentTime >= MaxTime)
			{
				foreach (var slot in state.eventSlots)
				{
					if (slot == null || slot.status == ClimbEventStatus.Pending) continue;
					if (slot.status != ClimbEventStatus.Scheduled && slot.status != ClimbEventStatus.Active) continue;
					slot.status = ClimbEventStatus.Expired;
					changed = true;
				}
				return changed;
			}

			foreach (var slot in state.eventSlots)
			{
				if (!IsEventExpired(slot, currentTime)) continue;
				slot.status = ClimbEventStatus.Expired;
				changed = true;
			}

			foreach (var slot in state.eventSlots)
			{
				if (slot == null || slot.status != ClimbEventStatus.Scheduled) continue;
				if (slot.scheduledAppearanceTime > currentTime) continue;
				slot.status = ClimbEventStatus.Active;
				slot.activatedAtTime = currentTime;
				changed = true;
			}

			return changed;
		}

		public static bool CanAfford(ClimbResourceSave available, ClimbResourceSave cost)
		{
			available ??= new ClimbResourceSave();
			cost ??= new ClimbResourceSave { red = 0, white = 0, black = 0 };
			return available.red >= Math.Max(0, cost.red)
				&& available.white >= Math.Max(0, cost.white)
				&& available.black >= Math.Max(0, cost.black);
		}

		public static bool TrySpend(ClimbResourceSave available, ClimbResourceSave cost)
		{
			if (!CanAfford(available, cost)) return false;
			available.red -= Math.Max(0, cost?.red ?? 0);
			available.white -= Math.Max(0, cost?.white ?? 0);
			available.black -= Math.Max(0, cost?.black ?? 0);
			return true;
		}

		public static void AddResources(ClimbResourceSave target, ClimbResourceSave reward)
		{
			if (target == null || reward == null) return;
			target.red = Math.Max(0, target.red + Math.Max(0, reward.red));
			target.white = Math.Max(0, target.white + Math.Max(0, reward.white));
			target.black = Math.Max(0, target.black + Math.Max(0, reward.black));
		}

		public static ClimbResourceSave GenerateCost(Random rng, int min = 1, int max = 2)
		{
			rng ??= Random.Shared;
			var cost = new ClimbResourceSave { red = 0, white = 0, black = 0 };
			int pips = rng.Next(Math.Max(1, min), Math.Max(min, max) + 1);
			for (int i = 0; i < pips; i++)
			{
				AddColor(cost, RollResourceColor(rng), 1);
			}
			return cost;
		}

		public static ClimbResourceSave GenerateReward(Random rng, int min = 1, int max = 3)
		{
			return GenerateCost(rng, min, max);
		}

		public static void RefreshShopSlots(ClimbSaveState state, int seed, LoadoutDefinition loadout)
		{
			if (state == null) return;
			state.shopSlots = new List<ClimbShopSlotSave>(ShopSlotCount);
			var rng = CreateRng(seed, state.time, 11);

			for (int i = 0; i < ShopSlotCount; i++)
			{
				var slot = RollShopSlot(state, loadout, rng, i, ClimbShopSlotKinds.DisplayOrder[i]);
				state.shopSlots.Add(slot);
			}
		}

		public static void RefreshEncounterSlots(ClimbSaveState state, int seed, LoadoutDefinition loadout = null)
		{
			if (state == null) return;
			state.encounterSlots = new List<ClimbEncounterSlotSave>(EncounterSlotCount);
			var rng = CreateRng(seed, state.time, 23);
			for (int i = 0; i < EncounterSlotCount; i++)
			{
				var constraints = BuildEncounterRollConstraints(state.encounterSlots);
				state.encounterSlots.Add(RollEncounterSlot(state, loadout, seed, rng, $"encounter_{i}", constraints));
			}
			EnsureEncounterSlotDiversity(state, seed, loadout, rng);
		}

		public static bool ReplenishEncounterSlots(ClimbSaveState state, int seed, LoadoutDefinition loadout = null)
		{
			if (state == null) return false;
			state.encounterSlots ??= new List<ClimbEncounterSlotSave>();

			if (ClampTime(state.time) >= MaxTime) return false;

			var rng = CreateRng(seed, state.time, 23 + state.encounterSlots.Count);
			bool changed = false;
			while (state.encounterSlots.Count < EncounterSlotCount)
			{
				var constraints = BuildEncounterRollConstraints(state.encounterSlots);
				state.encounterSlots.Add(RollEncounterSlot(
					state,
					loadout,
					seed,
					rng,
					$"encounter_{state.encounterSlots.Count}",
					constraints));
				changed = true;
			}

			for (int i = 0; i < state.encounterSlots.Count; i++)
			{
				var slot = state.encounterSlots[i];
				if (slot != null
					&& !slot.isCompleted
					&& !slot.isFinal
					&& IsValidClimbEncounterEnemy(slot.enemyId)
					&& !IsEncounterExpired(slot, state.time)
					&& slot.duration >= EncounterMinDuration
					&& slot.duration <= EncounterMaxDuration
					&& slot.timeCost >= 1
					&& slot.timeCost <= 3
					&& IsValidEncounterMutationTarget(slot, loadout))
				{
					continue;
				}

				string slotId = string.IsNullOrWhiteSpace(slot?.id) ? $"encounter_{i}" : slot.id;
				var constraints = EncounterRollConstraints.ForExcludedEnemy(slot?.enemyId);
				state.encounterSlots[i] = RollEncounterSlot(state, loadout, seed, rng, slotId, constraints);
				changed = true;
			}

			if (state.encounterSlots.Count > EncounterSlotCount)
			{
				state.encounterSlots = state.encounterSlots.Take(EncounterSlotCount).ToList();
				changed = true;
			}

			if (EnsureEncounterSlotDiversity(state, seed, loadout, rng))
			{
				changed = true;
			}

			return changed;
		}

		public static int AdvanceTimeAndUpdateSlots(ClimbSaveState state, int seed, LoadoutDefinition loadout, int timeCost)
		{
			if (state == null) return 0;
			int previousTime = state.time;
			int applied = ApplyTime(state, timeCost);
			if (ShouldRefreshShopAtTime(previousTime, state.time))
			{
				RefreshShopSlots(state, seed, loadout);
			}
			UpdateEventLifecycle(state);
			ReplenishEncounterSlots(state, seed, loadout);
			if (applied > 0)
			{
				RerollEncounterMutationTargets(state, seed, loadout);
			}
			return applied;
		}

		public static ClimbEncounterPool ResolveClimbEncounterPool(int climbTime)
		{
			return ClampTime(climbTime) < MaxTime / 2
				? ClimbEncounterPool.Early
				: ClimbEncounterPool.Late;
		}

		public static IReadOnlyList<string> GetClimbEncounterEnemyPool()
		{
			return EnemyPortraitContent.GetClimbEncounterEnemyPool()
				.Where(IsValidClimbEncounterEnemy)
				.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public static IReadOnlyList<string> GetClimbEncounterEnemyPool(ClimbEncounterPool pool)
		{
			return EnemyPortraitContent.GetClimbEncounterEnemyPool(pool)
				.Where(IsValidClimbEncounterEnemy)
				.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		public static IReadOnlyList<string> GetClimbEncounterEnemyPoolForTime(int climbTime)
		{
			return GetClimbEncounterEnemyPool(ResolveClimbEncounterPool(climbTime));
		}

		public static void RefreshEventSlots(ClimbSaveState state, int seed)
		{
			if (state == null) return;
			state.eventSlots = GenerateEventSchedule(seed).ToList();
		}

		public static IReadOnlyList<ClimbEventSlotSave> GenerateEventSchedule(int seed)
		{
			var rng = CreateClimbEventRng(seed);
			var rolled = new List<ClimbEventSlotSave>(EventSlotCount);
			var hazards = ClimbEventCatalog.GetHazards();
			for (int i = 0; i < 3; i++)
			{
				var definition = hazards[rng.Next(hazards.Count)];
				rolled.Add(new ClimbEventSlotSave
				{
					definitionId = definition.DefinitionId,
					kind = ClimbEventKind.Hazard,
					hazardEffect = definition.HazardEffect,
					characterReward = ClimbCharacterRewardType.None,
					duration = rng.Next(2, 5),
					timeCost = 0,
					effectAmount = RollHazardAmount(definition.HazardEffect, rng),
					rewardResources = GenerateReward(rng, 1, 2),
					status = ClimbEventStatus.Scheduled,
				});
			}

			var characterPool = ClimbEventCatalog.GetCharacters().ToList();
			for (int i = 0; i < 2; i++)
			{
				int selectedIndex = rng.Next(characterPool.Count);
				var definition = characterPool[selectedIndex];
				characterPool.RemoveAt(selectedIndex);
				rolled.Add(new ClimbEventSlotSave
				{
					definitionId = definition.DefinitionId,
					kind = ClimbEventKind.Character,
					hazardEffect = ClimbHazardEffectType.None,
					characterReward = definition.CharacterReward,
					duration = rng.Next(3, 6),
					timeCost = 1,
					effectAmount = CharacterRewardAmount(definition.CharacterReward),
					rewardResources = new ClimbResourceSave { red = 0, white = 0, black = 0 },
					status = ClimbEventStatus.Scheduled,
				});
			}

			for (int i = rolled.Count - 1; i > 0; i--)
			{
				int swapIndex = rng.Next(i + 1);
				(rolled[i], rolled[swapIndex]) = (rolled[swapIndex], rolled[i]);
			}

			for (int i = 0; i < rolled.Count; i++)
			{
				var band = GetEventAppearanceBand(i);
				rolled[i].id = $"climb_event_{i}";
				rolled[i].scheduledAppearanceTime = rng.Next(band.Start, band.End + 1);
				rolled[i].activatedAtTime = -1;
			}

			return rolled;
		}

		public static (int Start, int End) GetEventAppearanceBand(int eventPosition)
		{
			if (eventPosition < 0 || eventPosition >= EventSlotCount)
			{
				throw new ArgumentOutOfRangeException(nameof(eventPosition));
			}

			int start = (eventPosition * MaxTime / EventSlotCount) + 1;
			int end = (eventPosition + 1) * MaxTime / EventSlotCount;
			return (start, end);
		}

		public static IReadOnlyList<LoadoutCardEntry> GetEligibleRestrictionEntries(
			LoadoutDefinition loadout,
			string restrictionName)
		{
			if (string.IsNullOrWhiteSpace(restrictionName)) return Array.Empty<LoadoutCardEntry>();
			return (loadout?.cards ?? new List<LoadoutCardEntry>())
				.Where(IsEligibleEventCard)
				.Where(entry => !(entry.restrictions ?? new List<string>()).Contains(restrictionName, StringComparer.OrdinalIgnoreCase))
				.OrderBy(entry => entry.entryId, StringComparer.Ordinal)
				.ToList();
		}

		public static IReadOnlyList<LoadoutCardEntry> GetEligibleSmithEntries(LoadoutDefinition loadout)
		{
			return (loadout?.cards ?? new List<LoadoutCardEntry>())
				.Where(IsEligibleEventCard)
				.Where(entry => !RunDeckService.IsUpgradedCardKey(entry.cardKey))
				.Where(entry => !string.IsNullOrWhiteSpace(RunDeckService.BuildUpgradedCardKey(entry.cardKey)))
				.OrderBy(entry => entry.entryId, StringComparer.Ordinal)
				.ToList();
		}

		public static LoadoutCardEntry SelectDeterministicEntry(
			IReadOnlyList<LoadoutCardEntry> eligibleEntries,
			int runSeed,
			string eventSlotId)
		{
			if (eligibleEntries == null || eligibleEntries.Count == 0) return null;
			var ordered = eligibleEntries
				.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.entryId))
				.OrderBy(entry => entry.entryId, StringComparer.Ordinal)
				.ToList();
			if (ordered.Count == 0) return null;
			var rng = new Random((runSeed == 0 ? 1 : runSeed) ^ ClimbEventResolutionSalt ^ StableHash(eventSlotId));
			return ordered[rng.Next(ordered.Count)];
		}

		public static string BuildResourceSummary(ClimbResourceSave resources)
		{
			var parts = new List<string>();
			if ((resources?.red ?? 0) > 0) parts.Add($"{resources.red} Red");
			if ((resources?.white ?? 0) > 0) parts.Add($"{resources.white} White");
			if ((resources?.black ?? 0) > 0) parts.Add($"{resources.black} Black");
			return string.Join(", ", parts);
		}

		public static string BuildHazardEffectSummary(
			ClimbHazardEffectType effect,
			int amount,
			bool hasEligibleRestrictionTarget = true)
		{
			if (IsRestrictionHazard(effect) && !hasEligibleRestrictionTarget)
			{
				return "No deck card can be affected.";
			}

			amount = Math.Max(0, amount);
			return effect switch
			{
				ClimbHazardEffectType.Colorless => "One random deck card becomes Colorless.",
				ClimbHazardEffectType.Frozen => "One random deck card becomes Frozen.",
				ClimbHazardEffectType.Brittle => "One random deck card becomes Brittle.",
				ClimbHazardEffectType.Burn => "Start the next battle with 1 Burn.",
				ClimbHazardEffectType.Fear => $"Start the next battle with {amount} Fear.",
				ClimbHazardEffectType.Shackled => $"Gain {amount} Shackled.",
				ClimbHazardEffectType.Scar when amount == 1 => "Gain 1 Scar.",
				ClimbHazardEffectType.Scar => $"Gain {amount} Scars.",
				_ => string.Empty,
			};
		}

		public static bool IsRestrictionHazard(ClimbHazardEffectType effect)
		{
			return effect == ClimbHazardEffectType.Colorless
				|| effect == ClimbHazardEffectType.Frozen
				|| effect == ClimbHazardEffectType.Brittle;
		}

		public static string GetRestrictionName(ClimbHazardEffectType effect)
		{
			return effect switch
			{
				ClimbHazardEffectType.Colorless => RunScopedStateService.RestrictionColorless,
				ClimbHazardEffectType.Frozen => RunScopedStateService.RestrictionFrozen,
				ClimbHazardEffectType.Brittle => RunScopedStateService.RestrictionBrittle,
				_ => string.Empty,
			};
		}

		public static string GetEncounterMutationRestrictionName(BattleLocation location)
		{
			return location switch
			{
				BattleLocation.Desert => RunScopedStateService.RestrictionBrittle,
				BattleLocation.Tundra => RunScopedStateService.RestrictionFrozen,
				BattleLocation.Jungle => RunScopedStateService.RestrictionThorned,
				BattleLocation.Volcano => RunScopedStateService.RestrictionScorched,
				BattleLocation.Gothic => RunScopedStateService.RestrictionCursed,
				_ => string.Empty,
			};
		}

		public static IReadOnlyList<string> BuildPreviewRestrictionNames(
			LoadoutCardEntry entry,
			string addedRestrictionName)
		{
			var names = new List<string>();
			foreach (var restriction in entry?.restrictions ?? new List<string>())
			{
				AddDistinct(names, restriction);
			}
			AddDistinct(names, addedRestrictionName);
			return names;
		}

		public static bool IsValidEncounterMutationTarget(ClimbEncounterSlotSave slot, LoadoutDefinition loadout)
		{
			if (slot == null || slot.isCompleted || slot.isFinal) return true;
			string restrictionName = GetEncounterMutationRestrictionName(slot.battleLocation);
			if (string.IsNullOrWhiteSpace(restrictionName)) return true;
			if (GetEligibleEncounterMutationEntries(loadout, restrictionName).Count == 0)
			{
				return string.IsNullOrWhiteSpace(slot.cardMutationDeckEntryId)
					&& string.IsNullOrWhiteSpace(slot.cardMutationCardKey);
			}
			if (string.IsNullOrWhiteSpace(slot.cardMutationDeckEntryId)
				|| string.IsNullOrWhiteSpace(slot.cardMutationCardKey)
				|| !string.Equals(slot.cardMutationRestrictionName, restrictionName, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			var entry = FindLoadoutEntry(loadout, slot.cardMutationDeckEntryId);
			if (entry == null) return false;
			if (!string.Equals(entry.cardKey, slot.cardMutationCardKey, StringComparison.OrdinalIgnoreCase)) return false;
			return IsEligibleEncounterMutationEntry(entry, restrictionName);
		}

		public static bool EnsureEncounterMutationTargets(ClimbSaveState state, int seed, LoadoutDefinition loadout)
		{
			return AssignEncounterMutationTargets(state, seed, loadout, rerollAll: false);
		}

		public static bool RerollEncounterMutationTargets(ClimbSaveState state, int seed, LoadoutDefinition loadout)
		{
			return AssignEncounterMutationTargets(state, seed, loadout, rerollAll: true);
		}

		private static bool IsEligibleEventCard(LoadoutCardEntry entry)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.entryId)) return false;
			if (!RunDeckService.TryParseCardKey(entry.cardKey, out var cardId, out _, out _)) return false;
			var card = CardFactory.Create(cardId);
			return card != null && card.CanAddToLoadout && !card.IsWeapon && !card.IsToken;
		}

		private static int RollHazardAmount(ClimbHazardEffectType effect, Random rng)
		{
			return effect switch
			{
				ClimbHazardEffectType.Fear => rng.Next(1, 4),
				ClimbHazardEffectType.Shackled => rng.Next(1, 5),
				ClimbHazardEffectType.Scar => rng.Next(1, 3),
				_ => 1,
			};
		}

		private static int CharacterRewardAmount(ClimbCharacterRewardType reward)
		{
			return reward switch
			{
				ClimbCharacterRewardType.Temperance => 2,
				ClimbCharacterRewardType.Courage => 2,
				ClimbCharacterRewardType.Vigor => 1,
				_ => 0,
			};
		}

		private static Random CreateClimbEventRng(int seed)
		{
			return new Random((seed == 0 ? 1 : seed) ^ ClimbEventRngSalt);
		}

		private static int StableHash(string value)
		{
			unchecked
			{
				int hash = 17;
				foreach (char character in value ?? string.Empty)
				{
					hash = hash * 31 + character;
				}
				return hash;
			}
		}

		public static string RollReplacementIncomingCardKey(Random rng, LoadoutDefinition loadout)
		{
			rng ??= Random.Shared;
			var unlockedCards = new HashSet<string>(SaveCache.GetCollection().cardIds, StringComparer.OrdinalIgnoreCase);
			var deckIds = new HashSet<string>(
				(loadout?.cards ?? new List<LoadoutCardEntry>()).Select(entry => DeckRules.ParseBaseCardId(entry.cardKey)),
				StringComparer.OrdinalIgnoreCase);
			var pool = CardFactory.GetAllCards().Values
				.Where(card => card != null && card.CanAddToLoadout && !card.IsWeapon && !card.IsToken)
				.Where(card => card.IsEligibleForWeapon(loadout?.weaponId))
				.Where(card => unlockedCards.Contains(card.CardId))
				.Where(card => !string.IsNullOrWhiteSpace(card.CardId))
				.Where(card => !deckIds.Contains(card.CardId) || card.Rarity != Rarity.Starter)
				.Select(card => card.CardId)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.OrderBy(_ => rng.Next())
				.ToList();
			if (pool.Count == 0) return string.Empty;
			string cardId = pool[0];
			var color = ResourceColors[rng.Next(ResourceColors.Length)];
			string key = RunDeckService.BuildCardKey(cardId, color);
			if (StartingDeckGeneratorService.GetAutoUpgradeCardIds(loadout?.weaponId ?? string.Empty).Contains(cardId))
			{
				key = RunDeckService.BuildUpgradedCardKey(key);
			}
			return key;
		}

		private static ClimbShopSlotSave RollShopSlot(
			ClimbSaveState state,
			LoadoutDefinition loadout,
			Random rng,
			int slotIndex,
			string kind)
		{
			var slot = new ClimbShopSlotSave
			{
				id = $"shop_{slotIndex}",
				kind = kind,
				timeCost = rng.Next(0, 3),
				generatedAtTime = state.time,
			};
			slot.cost = GenerateShopCost(rng, kind, slot.timeCost);

			if (kind == ClimbShopSlotKinds.Medal)
			{
				var unlockedMedals = new HashSet<string>(SaveCache.GetCollection().medalIds, StringComparer.OrdinalIgnoreCase);
				slot.itemId = PickUnshown(
					MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()).Where(unlockedMedals.Contains),
					state.shownMedalIds,
					rng);
				if (string.IsNullOrWhiteSpace(slot.itemId)) slot.kind = ClimbShopSlotKinds.Empty;
				else AddDistinct(state.shownMedalIds, slot.itemId);
			}
			else if (kind == ClimbShopSlotKinds.Equipment)
			{
				var occupiedSlots = GetOccupiedEquipmentSlots(loadout);
				var unlockedEquipment = new HashSet<string>(SaveCache.GetCollection().equipmentIds, StringComparer.OrdinalIgnoreCase);
				var allEquipment = EquipmentFactory.GetAllEquipment().Keys
					.Select(id => id.ToKey())
					.Where(unlockedEquipment.Contains)
					.Where(id =>
					{
						var equipment = EquipmentFactory.Create(id);
						return equipment != null && !occupiedSlots.Contains(equipment.Slot);
					});
				slot.itemId = PickUnshown(allEquipment, state.shownEquipmentIds, rng);
				if (string.IsNullOrWhiteSpace(slot.itemId)) slot.kind = ClimbShopSlotKinds.Empty;
				else AddDistinct(state.shownEquipmentIds, slot.itemId);
			}
			else if (kind == ClimbShopSlotKinds.Upgrade)
			{
				var upgrade = PickUpgradeableDeckIndex(loadout, rng);
				if (upgrade.index < 0) slot.kind = ClimbShopSlotKinds.Empty;
				else
				{
					slot.deckIndex = upgrade.index;
					slot.deckEntryId = upgrade.entryId;
					slot.cardKey = RunDeckService.BuildUpgradedCardKey(upgrade.cardKey);
				}
			}
			else if (kind == ClimbShopSlotKinds.Replacement)
			{
				slot.cardKey = RollReplacementIncomingCardKey(rng, loadout);
				if (string.IsNullOrWhiteSpace(slot.cardKey)) slot.kind = ClimbShopSlotKinds.Empty;
			}

			if (string.Equals(slot.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase))
			{
				slot.timeCost = 0;
				slot.cost = new ClimbResourceSave { red = 0, white = 0, black = 0 };
			}

			return slot;
		}

		private readonly struct EncounterRollConstraints
		{
			public IReadOnlyList<string> ExcludedEnemyIds { get; init; }
			public IReadOnlyList<BattleLocation> ExcludedLocations { get; init; }

			public static EncounterRollConstraints ForExcludedEnemy(string excludedEnemyId)
			{
				if (string.IsNullOrWhiteSpace(excludedEnemyId)) return default;
				return new EncounterRollConstraints
				{
					ExcludedEnemyIds = new[] { excludedEnemyId },
				};
			}
		}

		private static EncounterRollConstraints BuildEncounterRollConstraints(IReadOnlyList<ClimbEncounterSlotSave> existingSlots)
		{
			if (existingSlots == null || existingSlots.Count == 0) return default;

			var enemies = existingSlots
				.Select(slot => slot?.enemyId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.ToList();
			var locations = existingSlots
				.Where(slot => slot != null)
				.Select(slot => slot.battleLocation)
				.ToList();

			var constraints = new EncounterRollConstraints();
			if (enemies.Count >= 2
				&& enemies.Distinct(StringComparer.OrdinalIgnoreCase).Count() < EncounterMinDistinctEnemies)
			{
				constraints = constraints with
				{
					ExcludedEnemyIds = new[] { enemies[0] },
				};
			}

			int skeletonCount = enemies.Count(IsClimbSkeletonEnemy);
			if (skeletonCount >= EncounterMaxSkeletonFamily)
			{
				constraints = constraints with
				{
					ExcludedEnemyIds = MergeExcludedEnemyIds(constraints.ExcludedEnemyIds, ClimbSkeletonEnemyIds),
				};
			}

			if (locations.Count >= 2
				&& locations.Distinct().Count() < EncounterMinDistinctLocations)
			{
				constraints = constraints with
				{
					ExcludedLocations = new[] { locations[0] },
				};
			}

			return constraints;
		}

		private static bool SatisfiesEncounterSlotDiversity(IReadOnlyList<ClimbEncounterSlotSave> slots)
		{
			var activeSlots = (slots ?? Array.Empty<ClimbEncounterSlotSave>())
				.Where(slot => slot != null && !slot.isFinal && !string.IsNullOrWhiteSpace(slot.enemyId))
				.ToList();
			if (activeSlots.Count < EncounterSlotCount) return true;

			int distinctEnemies = activeSlots
				.Select(slot => slot.enemyId)
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.Count();
			int distinctLocations = activeSlots
				.Select(slot => slot.battleLocation)
				.Distinct()
				.Count();
			int skeletonCount = activeSlots.Count(slot => IsClimbSkeletonEnemy(slot.enemyId));
			return distinctEnemies >= EncounterMinDistinctEnemies
				&& distinctLocations >= EncounterMinDistinctLocations
				&& skeletonCount <= EncounterMaxSkeletonFamily;
		}

		private static bool EnsureEncounterSlotDiversity(
			ClimbSaveState state,
			int seed,
			LoadoutDefinition loadout,
			Random rng)
		{
			if (state?.encounterSlots == null) return false;

			bool changed = false;
			for (int iteration = 0;
				iteration < EncounterDiversityMaxIterations && !SatisfiesEncounterSlotDiversity(state.encounterSlots);
				iteration++)
			{
				var activeSlots = state.encounterSlots
					.Select((slot, index) => (slot, index))
					.Where(entry => entry.slot != null && !entry.slot.isFinal && !string.IsNullOrWhiteSpace(entry.slot.enemyId))
					.ToList();
				if (activeSlots.Count < EncounterSlotCount) break;

				int distinctEnemies = activeSlots
					.Select(entry => entry.slot.enemyId)
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.Count();
				int distinctLocations = activeSlots
					.Select(entry => entry.slot.battleLocation)
					.Distinct()
					.Count();
				int skeletonCount = activeSlots.Count(entry => IsClimbSkeletonEnemy(entry.slot.enemyId));

				if (distinctEnemies < EncounterMinDistinctEnemies)
				{
					string majorityEnemy = activeSlots
						.GroupBy(entry => entry.slot.enemyId, StringComparer.OrdinalIgnoreCase)
						.OrderByDescending(group => group.Count())
						.ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
						.First()
						.Key;
					var candidates = activeSlots
						.Where(entry => string.Equals(entry.slot.enemyId, majorityEnemy, StringComparison.OrdinalIgnoreCase))
						.ToList();
					var (slot, index) = candidates[rng.Next(candidates.Count)];
					string slotId = string.IsNullOrWhiteSpace(slot.id) ? $"encounter_{index}" : slot.id;
					slot.enemyId = RollClimbEncounterEnemyId(rng, new[] { majorityEnemy }, state.time);
					changed = true;
				}
				else if (skeletonCount > EncounterMaxSkeletonFamily)
				{
					var candidates = activeSlots
						.Where(entry => IsClimbSkeletonEnemy(entry.slot.enemyId))
						.ToList();
					if (candidates.Count == 0) break;
					var (slot, _) = candidates[rng.Next(candidates.Count)];
					slot.enemyId = RollClimbEncounterEnemyId(rng, ClimbSkeletonEnemyIds, state.time);
					changed = true;
				}
				else if (distinctLocations < EncounterMinDistinctLocations)
				{
					BattleLocation majorityLocation = activeSlots
						.GroupBy(entry => entry.slot.battleLocation)
						.OrderByDescending(group => group.Count())
						.ThenBy(group => group.Key)
						.First()
						.Key;
					var candidates = activeSlots
						.Where(entry => entry.slot.battleLocation == majorityLocation)
						.ToList();
					var (slot, index) = candidates[rng.Next(candidates.Count)];
					string slotId = string.IsNullOrWhiteSpace(slot.id) ? $"encounter_{index}" : slot.id;
					slot.battleLocation = BattleLocationAssetService.RollClimbEncounterLocation(rng, new[] { majorityLocation });
					AssignEncounterMutationTarget(state, slot, seed, loadout, slotId);
					changed = true;
				}
				else
				{
					break;
				}
			}

			return changed;
		}

		internal static bool IsClimbSkeletonEnemy(string enemyId) =>
			!string.IsNullOrWhiteSpace(enemyId) && ClimbSkeletonEnemyIds.Contains(enemyId);

		private static IReadOnlyList<string> MergeExcludedEnemyIds(
			IReadOnlyList<string> existing,
			IEnumerable<string> additional)
		{
			var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			if (existing != null)
			{
				foreach (string id in existing)
				{
					if (!string.IsNullOrWhiteSpace(id)) merged.Add(id);
				}
			}
			if (additional != null)
			{
				foreach (string id in additional)
				{
					if (!string.IsNullOrWhiteSpace(id)) merged.Add(id);
				}
			}
			return merged.Count == 0 ? null : merged.ToList();
		}

		private static ClimbEncounterSlotSave RollEncounterSlot(
			ClimbSaveState state,
			LoadoutDefinition loadout,
			int seed,
			Random rng,
			string slotId,
			EncounterRollConstraints constraints = default)
		{
			int timeCost = rng.Next(1, 4);
			var slot = new ClimbEncounterSlotSave
			{
				id = slotId ?? string.Empty,
				enemyId = RollClimbEncounterEnemyId(rng, constraints.ExcludedEnemyIds, state?.time ?? 0),
				generatedAtTime = ClampTime(state?.time ?? 0),
				duration = rng.Next(EncounterMinDuration, EncounterMaxDuration + 1),
				timeCost = timeCost,
				battleLocation = BattleLocationAssetService.RollClimbEncounterLocation(rng, constraints.ExcludedLocations),
				rewardResources = GenerateReward(rng, timeCost, timeCost),
				hasDeckReward = true,
				isFinal = false,
				isCompleted = false,
			};
			AssignEncounterMutationTarget(state, slot, seed, loadout, slotId);
			return slot;
		}

		internal static CardData.CardColor RollResourceColorForTests(int roll)
		{
			return ResolveResourceColorRoll(roll);
		}

		internal static ClimbResourceSave GenerateShopCostForTests(string kind, int timeCost)
		{
			return GenerateShopCost(new Random(1), kind, timeCost);
		}

		private static ClimbResourceSave GenerateShopCost(Random rng, string kind, int timeCost)
		{
			rng ??= Random.Shared;
			var dominant = CardData.CardColor.Red;
			int baseline = 3;
			int reductionPerExtraTime = 1;

			if (string.Equals(kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase))
			{
				dominant = CardData.CardColor.White;
				baseline = 6;
				reductionPerExtraTime = 2;
			}
			else if (string.Equals(kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase))
			{
				dominant = CardData.CardColor.Black;
				baseline = 7;
				reductionPerExtraTime = 2;
			}

			int total = Math.Max(0, baseline - Math.Max(0, timeCost) * reductionPerExtraTime);
			return GenerateDominantCost(rng, total, dominant);
		}

		private static ClimbResourceSave GenerateDominantCost(Random rng, int total, CardData.CardColor dominant)
		{
			var cost = new ClimbResourceSave { red = 0, white = 0, black = 0 };
			if (total <= 0) return cost;

			int dominantPips = 1;
			while (dominantPips < total && dominantPips <= (int)Math.Ceiling((total - dominantPips) / 2f))
			{
				dominantPips++;
			}

			AddColor(cost, dominant, dominantPips);

			var offColors = ResourceColors
				.Where(color => color != dominant)
				.OrderBy(_ => rng.Next())
				.ToArray();
			int remaining = total - dominantPips;
			AddColor(cost, offColors[0], remaining / 2 + remaining % 2);
			AddColor(cost, offColors[1], remaining / 2);
			return cost;
		}

		private static string RollClimbEncounterEnemyId(Random rng, IEnumerable<string> excludedEnemyIds, int climbTime)
		{
			rng ??= Random.Shared;
			var pool = GetClimbEncounterEnemyPoolForTime(climbTime).ToList();
			if (pool.Count == 0) return ResolveClimbEncounterPool(climbTime) == ClimbEncounterPool.Early
				? "skeleton"
				: "spider";

			var excluded = (excludedEnemyIds ?? Array.Empty<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			if (excluded.Count > 0 && pool.Count > excluded.Count)
			{
				pool = pool
					.Where(id => !excluded.Contains(id))
					.ToList();
			}

			return pool[rng.Next(pool.Count)];
		}

		private static bool IsValidClimbEncounterEnemy(string enemyId)
		{
			if (string.IsNullOrWhiteSpace(enemyId)) return false;
			if (BannedClimbEncounterEnemyIds.Contains(enemyId)) return false;
			var enemy = EnemyFactory.Create(enemyId);
			return enemy != null
				&& !enemy.IsBoss
				&& !enemy.IsTutorialOnly
				&& enemy.ClimbPool != ClimbEncounterPool.None
				&& EnemyPortraitContent.HasPortrait(enemyId);
		}

		private static bool AssignEncounterMutationTargets(
			ClimbSaveState state,
			int seed,
			LoadoutDefinition loadout,
			bool rerollAll)
		{
			if (state?.encounterSlots == null) return false;
			bool changed = false;
			for (int i = 0; i < state.encounterSlots.Count; i++)
			{
				var slot = state.encounterSlots[i];
				if (slot == null || slot.isCompleted || slot.isFinal) continue;
				if (!rerollAll && IsValidEncounterMutationTarget(slot, loadout)) continue;
				string before = EncounterMutationSnapshot(slot);
				AssignEncounterMutationTarget(state, slot, seed, loadout, string.IsNullOrWhiteSpace(slot.id) ? $"encounter_{i}" : slot.id);
				changed |= !string.Equals(before, EncounterMutationSnapshot(slot), StringComparison.Ordinal);
			}
			return changed;
		}

		private static void AssignEncounterMutationTarget(
			ClimbSaveState state,
			ClimbEncounterSlotSave slot,
			int seed,
			LoadoutDefinition loadout,
			string slotId)
		{
			if (slot == null) return;
			string restrictionName = GetEncounterMutationRestrictionName(slot.battleLocation);
			slot.cardMutationRestrictionName = restrictionName;
			slot.cardMutationDeckEntryId = string.Empty;
			slot.cardMutationCardKey = string.Empty;
			if (string.IsNullOrWhiteSpace(restrictionName)) return;

			var eligible = GetEligibleEncounterMutationEntries(loadout, restrictionName);
			var target = SelectDeterministicEntry(
				eligible,
				seed,
				$"{slotId}|{ClimbRuleService.ClampTime(state?.time ?? 0)}|{slot.battleLocation}|{restrictionName}");
			if (target == null) return;

			slot.cardMutationDeckEntryId = target.entryId ?? string.Empty;
			slot.cardMutationCardKey = target.cardKey ?? string.Empty;
		}

		private static IReadOnlyList<LoadoutCardEntry> GetEligibleEncounterMutationEntries(
			LoadoutDefinition loadout,
			string restrictionName)
		{
			if (string.IsNullOrWhiteSpace(restrictionName)) return Array.Empty<LoadoutCardEntry>();
			return (loadout?.cards ?? new List<LoadoutCardEntry>())
				.Where(entry => IsEligibleEncounterMutationEntry(entry, restrictionName))
				.OrderBy(entry => entry.entryId, StringComparer.Ordinal)
				.ToList();
		}

		private static bool IsEligibleEncounterMutationEntry(LoadoutCardEntry entry, string restrictionName)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.entryId)) return false;
			if ((entry.restrictions ?? new List<string>()).Contains(restrictionName, StringComparer.OrdinalIgnoreCase)) return false;
			if (!RunDeckService.TryParseCardKey(entry.cardKey, out var cardId, out _, out _)) return false;
			var card = CardFactory.Create(cardId);
			return card != null && card.CanAddToLoadout && !card.IsWeapon && !card.IsToken;
		}

		private static LoadoutCardEntry FindLoadoutEntry(LoadoutDefinition loadout, string entryId)
		{
			if (string.IsNullOrWhiteSpace(entryId)) return null;
			return (loadout?.cards ?? new List<LoadoutCardEntry>())
				.FirstOrDefault(entry => string.Equals(entry?.entryId, entryId, StringComparison.Ordinal));
		}

		private static string EncounterMutationSnapshot(ClimbEncounterSlotSave slot)
		{
			return $"{slot?.cardMutationRestrictionName ?? string.Empty}|{slot?.cardMutationDeckEntryId ?? string.Empty}|{slot?.cardMutationCardKey ?? string.Empty}";
		}

		private static (int index, string entryId, string cardKey) PickUpgradeableDeckIndex(LoadoutDefinition loadout, Random rng)
		{
			var eligible = new List<(int index, string entryId, string cardKey)>();
			var cards = loadout?.cards;
			if (cards == null) return (-1, string.Empty, string.Empty);
			for (int i = 0; i < cards.Count; i++)
			{
				var entry = cards[i];
				if (entry == null || !RunDeckService.TryParseCardKey(entry.cardKey, out var cardId, out _, out bool isUpgraded)) continue;
				if (isUpgraded) continue;
				var card = CardFactory.Create(cardId);
				if (card == null || card.IsWeapon || card.IsToken || !card.CanAddToLoadout) continue;
				eligible.Add((i, entry.entryId, entry.cardKey));
			}

			if (eligible.Count == 0) return (-1, string.Empty, string.Empty);
			return eligible[rng.Next(eligible.Count)];
		}

		private static HashSet<EquipmentSlot> GetOccupiedEquipmentSlots(LoadoutDefinition loadout)
		{
			var occupied = new HashSet<EquipmentSlot>();
			if (loadout == null) return occupied;
			if (!string.IsNullOrWhiteSpace(loadout.headId)) occupied.Add(EquipmentSlot.Head);
			if (!string.IsNullOrWhiteSpace(loadout.chestId)) occupied.Add(EquipmentSlot.Chest);
			if (!string.IsNullOrWhiteSpace(loadout.armsId)) occupied.Add(EquipmentSlot.Arms);
			if (!string.IsNullOrWhiteSpace(loadout.legsId)) occupied.Add(EquipmentSlot.Legs);
			return occupied;
		}

		private static string PickUnshown(IEnumerable<string> allIds, List<string> shownIds, Random rng)
		{
			var shown = new HashSet<string>(shownIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
			var pool = allIds
				.Where(id => !string.IsNullOrWhiteSpace(id) && !shown.Contains(id))
				.OrderBy(_ => rng.Next())
				.ToList();
			return pool.Count == 0 ? string.Empty : pool[0];
		}

		private static CardData.CardColor RollResourceColor(Random rng)
		{
			return ResolveResourceColorRoll(rng.Next(100));
		}

		private static CardData.CardColor ResolveResourceColorRoll(int roll)
		{
			if (roll < 25) return CardData.CardColor.Red;
			if (roll < 60) return CardData.CardColor.White;
			return CardData.CardColor.Black;
		}

		private static void AddColor(ClimbResourceSave resources, CardData.CardColor color, int amount)
		{
			switch (color)
			{
				case CardData.CardColor.Red:
					resources.red += amount;
					break;
				case CardData.CardColor.White:
					resources.white += amount;
					break;
				case CardData.CardColor.Black:
					resources.black += amount;
					break;
			}
		}

		private static void AddDistinct(List<string> list, string value)
		{
			if (list == null || string.IsNullOrWhiteSpace(value)) return;
			if (!list.Contains(value, StringComparer.OrdinalIgnoreCase)) list.Add(value);
		}

		private static bool IsPendingEventSlot(ClimbSaveState state, ClimbEventSlotSave slot)
		{
			if (state?.pendingEvent == null || slot == null) return false;
			return string.Equals(state.pendingEvent.eventSlotId, slot.id, StringComparison.OrdinalIgnoreCase);
		}

		private static Random CreateRng(int seed, int time, int salt)
		{
			int value = seed == 0 ? 1 : seed;
			return new Random(value ^ RngSalt ^ (time * 397) ^ salt);
		}
	}
}
