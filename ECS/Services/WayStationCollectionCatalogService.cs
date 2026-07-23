using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.Medals;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.Equipment;
using ChurchSuffering.ECS.Objects.Medals;

namespace ChurchSuffering.ECS.Services
{
	public sealed record WayStationCollectionCardEntry(string Id, CardBase Card);
	public sealed record WayStationCollectionSaintEntry(string Id, MedalBase Medal, SaintBlurbDefinition Saint);
	public sealed record WayStationCollectionEquipmentEntry(string Id, EquipmentBase Equipment);

	public sealed class WayStationCollectionCatalog
	{
		public static WayStationCollectionCatalog Empty { get; } = new([], 0, [], 0, [], 0);

		public WayStationCollectionCatalog(
			IReadOnlyList<WayStationCollectionCardEntry> cards,
			int cardTotal,
			IReadOnlyList<WayStationCollectionSaintEntry> saints,
			int saintTotal,
			IReadOnlyList<WayStationCollectionEquipmentEntry> equipment,
			int equipmentTotal)
		{
			Cards = cards ?? [];
			CardTotal = Math.Max(0, cardTotal);
			Saints = saints ?? [];
			SaintTotal = Math.Max(0, saintTotal);
			Equipment = equipment ?? [];
			EquipmentTotal = Math.Max(0, equipmentTotal);
		}

		public IReadOnlyList<WayStationCollectionCardEntry> Cards { get; }
		public int CardTotal { get; }
		public IReadOnlyList<WayStationCollectionSaintEntry> Saints { get; }
		public int SaintTotal { get; }
		public IReadOnlyList<WayStationCollectionEquipmentEntry> Equipment { get; }
		public int EquipmentTotal { get; }
		public bool HasAnyUnlocked => Cards.Count > 0 || Saints.Count > 0 || Equipment.Count > 0;
	}

	public static class WayStationCollectionCatalogService
	{
		private static readonly StringComparer IdComparer = StringComparer.OrdinalIgnoreCase;

		private static readonly IReadOnlyDictionary<string, int> EquipmentMockupOrder =
			new[]
			{
				"knightly_helm", "ivory_coif", "scarlet_coif", "oathbreaker_coif", "helm_of_seeing", "sanctified_circlet",
				"knightly_chest", "ivory_vest", "scarlet_vest", "bulwark_plate", "heartforge_cuirass", "pierced_heart_plate",
				"knightly_gauntlets", "ivory_wraps", "scarlet_wraps", "kunai_sheath", "whetstone_gauntlets",
				"purging_bracers", "warbringer_bracers",
				"knightly_grieves", "ivory_treads", "scarlet_treads", "fleetfoot_greaves", "sunderstep_treads",
			}
			.Select((id, index) => (id, index))
			.ToDictionary(item => item.id, item => item.index, IdComparer);

		public static WayStationCollectionCatalog Build(PlayerCollectionSave collection, WayStationMetaSave meta)
		{
			collection ??= new PlayerCollectionSave();
			var unlockedCards = new HashSet<string>(collection.cardIds ?? [], IdComparer);
			var unlockedMedals = new HashSet<string>(collection.medalIds ?? [], IdComparer);
			var unlockedEquipment = new HashSet<string>(collection.equipmentIds ?? [], IdComparer);

			var canonicalCards = CardFactory.GetAllCards()
				.Where(item => item.Value != null && item.Value.CanAddToLoadout && !item.Value.IsToken)
				.Select(item => new WayStationCollectionCardEntry(item.Key.ToKey(), item.Value))
				.GroupBy(item => item.Id, IdComparer)
				.Select(group => group.First())
				.ToList();
			var cards = canonicalCards
				.Where(item => item.Card.IsWeapon
					? IsWeaponUnlocked(item.Id, meta)
					: unlockedCards.Contains(item.Id))
				.OrderBy(item => item.Card.DisplayName, IdComparer)
				.ThenBy(item => item.Id, IdComparer)
				.ToArray();

			var canonicalSaints = MedalFactory.GetAllMedals()
				.Where(item => item.Value != null)
				.Select(item =>
				{
					string id = item.Key.ToKey();
					SaintBlurbDefinitionCache.TryGet(id, out var saint);
					return new WayStationCollectionSaintEntry(id, item.Value, saint);
				})
				.GroupBy(item => item.Id, IdComparer)
				.Select(group => group.First())
				.ToList();
			var saints = canonicalSaints
				.Where(item => unlockedMedals.Contains(item.Id))
				.OrderBy(item => item.Medal.Name, IdComparer)
				.ThenBy(item => item.Id, IdComparer)
				.ToArray();

			var canonicalEquipment = EquipmentFactory.GetAllEquipment()
				.Where(item => item.Value != null)
				.Select(item => new WayStationCollectionEquipmentEntry(item.Key.ToKey(), item.Value))
				.GroupBy(item => item.Id, IdComparer)
				.Select(group => group.First())
				.ToList();
			var equipment = canonicalEquipment
				.Where(item => unlockedEquipment.Contains(item.Id))
				.OrderBy(item => SlotOrder(item.Equipment.Slot))
				.ThenBy(item => EquipmentMockupOrder.TryGetValue(item.Id, out int index) ? index : int.MaxValue)
				.ThenBy(item => item.Equipment.Name, IdComparer)
				.ThenBy(item => item.Id, IdComparer)
				.ToArray();

			return new WayStationCollectionCatalog(
				cards,
				canonicalCards.Count,
				saints,
				canonicalSaints.Count,
				equipment,
				canonicalEquipment.Count);
		}

		public static IReadOnlyList<WayStationCollectionCardEntry> FilterCards(
			WayStationCollectionCatalog catalog,
			WayStationCollectionCardFilter filter)
		{
			var cards = catalog?.Cards ?? [];
			return cards.Where(item => filter switch
				{
					WayStationCollectionCardFilter.All => true,
					WayStationCollectionCardFilter.Weapon => item.Card.IsWeapon,
					WayStationCollectionCardFilter.Attack => !item.Card.IsWeapon && item.Card.Type == CardType.Attack,
					WayStationCollectionCardFilter.Block => !item.Card.IsWeapon && item.Card.Type == CardType.Block,
					WayStationCollectionCardFilter.Prayer => !item.Card.IsWeapon && item.Card.Type == CardType.Prayer,
					_ => false,
				})
				.OrderBy(item => item.Card.DisplayName, IdComparer)
				.ThenBy(item => item.Id, IdComparer)
				.ToArray();
		}

		public static bool HasAnyUnlocked(PlayerCollectionSave collection, WayStationMetaSave meta) =>
			Build(collection, meta).HasAnyUnlocked;

		private static bool IsWeaponUnlocked(string cardId, WayStationMetaSave meta)
		{
			foreach (var weapon in Enum.GetValues<StartingWeapon>())
			{
				if (!string.Equals(PenanceRules.GetWeaponId(weapon), cardId, StringComparison.OrdinalIgnoreCase)) continue;
				return ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon);
			}
			return false;
		}

		private static int SlotOrder(EquipmentSlot slot) => slot switch
			{
				EquipmentSlot.Head => 0,
				EquipmentSlot.Chest => 1,
				EquipmentSlot.Arms => 2,
				EquipmentSlot.Legs => 3,
				_ => int.MaxValue,
			};
	}

	internal static class WayStationCollectionModalLogic
	{
		private const float UpgradePreviewTriggerThreshold = 0.15f;

		public static void Reset(WayStationCollectionModalState state, WayStationCollectionCatalog catalog)
		{
			if (state == null) return;
			state.ActiveTab = WayStationCollectionTab.Cards;
			state.ActiveCardFilter = WayStationCollectionCardFilter.All;
			state.CardScrollOffset = 0;
			state.SaintListScrollOffset = 0;
			state.SaintDetailScrollOffset = 0;
			state.EquipmentScrollOffset = 0;
			state.SelectedMedalId = catalog?.Saints.FirstOrDefault()?.Id ?? string.Empty;
		}

		public static CardData.CardColor NextColor(CardData.CardColor color) => color switch
			{
				CardData.CardColor.White => CardData.CardColor.Red,
				CardData.CardColor.Red => CardData.CardColor.Black,
				_ => CardData.CardColor.White,
			};

		public static bool IsUpgradePreviewModifierHeld(PlayerInputFrame input) =>
			input.IsDown(PlayerButton.Shift)
			|| input.LeftTrigger >= UpgradePreviewTriggerThreshold;

		public static HotKey CreateCloseHotKey() => new()
		{
			Button = FaceButton.B,
			IsKeyboardMouseEnabled = false,
		};

		public static float Approach(float current, float target, float durationSeconds, float elapsedSeconds)
		{
			if (durationSeconds <= 0f || elapsedSeconds <= 0f) return durationSeconds <= 0f ? target : current;
			float rate = 6.9077554f / durationSeconds;
			float result = target + (current - target) * MathF.Exp(-rate * elapsedSeconds);
			return MathF.Abs(result - target) < 0.0001f ? target : result;
		}
	}
}
