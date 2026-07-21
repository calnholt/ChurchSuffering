using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Dialog;
using ChurchSuffering.ECS.Data.Save;

namespace ChurchSuffering.ECS.Services
{
	public static class WayStationDialoguePlanner
	{
		public const string KeeperOfferId = "keeper";
		public const string NpcOfferId = "npc";
		public const string RookTutorialOfferId = "rook_tutorial";

		public class DialogueOfferPlan
		{
			public string OfferId { get; set; } = string.Empty;
			public string CharacterId { get; set; } = string.Empty;
			public string DefinitionId { get; set; } = string.Empty;
			public string SegmentId { get; set; } = string.Empty;
		}

		public static DialogueOfferPlan TryGetAutoDialogue(WayStationMetaSave meta)
		{
			if (HasSeen(meta, WayStationDialogueCatalog.KeeperCharacterId, WayStationDialogueCatalog.KeeperIntroSegmentId))
			{
				return null;
			}

			return CreateOffer(
				"keeper_auto",
				WayStationDialogueCatalog.KeeperCharacterId,
				WayStationDialogueCatalog.KeeperIntroSegmentId);
		}

		public static IReadOnlyList<DialogueOfferPlan> BuildVisit(
			WayStationMetaSave meta,
			WayStationArrivalKind arrivalKind,
			Random rng)
		{
			var offers = new List<DialogueOfferPlan>();
			var keeper = TryGetKeeperPoiDialogue(meta, arrivalKind);
			if (keeper != null) offers.Add(keeper);

			var rookTutorial = TryGetRookTutorialDialogue(meta);
			if (rookTutorial != null) offers.Add(rookTutorial);

			var npc = TryGetNpcDialogue(meta, rng ?? Random.Shared);
			if (npc != null) offers.Add(npc);

			return offers;
		}

		public static DialogueOfferPlan TryGetKeeperPoiDialogue(
			WayStationMetaSave meta,
			WayStationArrivalKind arrivalKind)
		{
			bool introSeen = HasSeen(meta, WayStationDialogueCatalog.KeeperCharacterId, WayStationDialogueCatalog.KeeperIntroSegmentId);
			bool earlyReturnSeen = HasSeen(meta, WayStationDialogueCatalog.KeeperCharacterId, WayStationDialogueCatalog.KeeperEarlyReturnSegmentId);
			if (introSeen
				&& !earlyReturnSeen
				&& (arrivalKind == WayStationArrivalKind.ReturnedFromFailedClimb
					|| arrivalKind == WayStationArrivalKind.ReturnedFromAbandonedClimb))
			{
				return CreateOffer(
					KeeperOfferId,
					WayStationDialogueCatalog.KeeperCharacterId,
					WayStationDialogueCatalog.KeeperEarlyReturnSegmentId);
			}

			return null;
		}

		public static DialogueOfferPlan TryGetNpcDialogue(WayStationMetaSave meta, Random rng)
		{
			if (meta?.pendingNpcDialogueOffer != true) return null;
			var available = WayStationDialogueCatalog.NpcCharacterIds
				.Where(characterId => !string.Equals(characterId, WayStationDialogueCatalog.RookCharacterId, StringComparison.OrdinalIgnoreCase)
					|| IsRookRandomNpcEligible(meta))
				.Where(characterId => TryGetNextUnseenSegment(meta, characterId, out _))
				.ToList();
			if (available.Count == 0) return null;

			var characterId = available[(rng ?? Random.Shared).Next(available.Count)];
			return TryGetNextUnseenSegment(meta, characterId, out string segmentId)
				? CreateOffer(NpcOfferId, characterId, segmentId)
				: null;
		}

		public static DialogueOfferPlan TryGetRookTutorialDialogue(WayStationMetaSave meta)
		{
			if (!IsRookTutorialWindow(meta)) return null;
			foreach (var candidate in WayStationDialogueCatalog.RookTutorialSegmentIds)
			{
				if (HasSeen(meta, WayStationDialogueCatalog.RookCharacterId, candidate)) continue;
				return CreateOffer(
					RookTutorialOfferId,
					WayStationDialogueCatalog.RookCharacterId,
					candidate);
			}

			return null;
		}

		public static bool TryGetNextUnseenSegment(
			WayStationMetaSave meta,
			string characterId,
			out string segmentId)
		{
			segmentId = string.Empty;
			foreach (var candidate in WayStationDialogueCatalog.GetOrderedSegments(characterId))
			{
				if (HasSeen(meta, characterId, candidate)) continue;
				segmentId = candidate;
				return true;
			}

			return false;
		}

		public static bool HasSeen(WayStationMetaSave meta, string characterId, string segmentId)
		{
			if (string.IsNullOrWhiteSpace(characterId) || string.IsNullOrWhiteSpace(segmentId)) return false;
			return meta?.completedDialogueSegments != null
				&& meta.completedDialogueSegments.TryGetValue(characterId, out var completed)
				&& completed != null
				&& completed.Contains(segmentId, StringComparer.OrdinalIgnoreCase);
		}

		private static DialogueOfferPlan CreateOffer(string offerId, string characterId, string segmentId)
		{
			if (!WayStationDialogueCatalog.TryGetDefinitionId(characterId, out string definitionId)) return null;
			return new DialogueOfferPlan
			{
				OfferId = offerId,
				CharacterId = characterId,
				DefinitionId = definitionId,
				SegmentId = segmentId,
			};
		}

		private static bool IsRookTutorialWindow(WayStationMetaSave meta)
		{
			return Math.Max(0, meta?.climbAttempts ?? 0) < WayStationDialogueCatalog.RookTutorialSegmentIds.Count;
		}

		private static bool IsRookRandomNpcEligible(WayStationMetaSave meta)
		{
			return !IsRookTutorialWindow(meta);
		}
	}
}
