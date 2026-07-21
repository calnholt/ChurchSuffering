using System;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Dialog
{
	public static class WayStationDialogueCatalog
	{
		public const string KeeperCharacterId = "keeper";
		public const string EliasCharacterId = "elias";
		public const string OldConfessorCharacterId = "old_confessor";
		public const string MaraCharacterId = "mara";
		public const string RookCharacterId = "rook";

		public const string KeeperDefinitionId = "waystation_keeper";
		public const string EliasDefinitionId = "waystation_elias";
		public const string OldConfessorDefinitionId = "waystation_old_confessor";
		public const string MaraDefinitionId = "waystation_mara";
		public const string RookDefinitionId = "waystation_rook";

		public const string KeeperIntroSegmentId = "intro";
		public const string KeeperEarlyReturnSegmentId = "early_return";
		public const string RookTutorialSegment1Id = "tutorial_1";
		public const string RookTutorialSegment2Id = "tutorial_2";
		public const string RookTutorialSegment3Id = "tutorial_3";

		public static readonly IReadOnlyList<string> NpcCharacterIds =
		[
			EliasCharacterId,
			OldConfessorCharacterId,
			MaraCharacterId,
			RookCharacterId,
		];

		public static readonly IReadOnlyList<string> RookTutorialSegmentIds =
		[
			RookTutorialSegment1Id,
			RookTutorialSegment2Id,
			RookTutorialSegment3Id,
		];

		private static readonly IReadOnlyDictionary<string, string> DefinitionIds =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				[KeeperCharacterId] = KeeperDefinitionId,
				[EliasCharacterId] = EliasDefinitionId,
				[OldConfessorCharacterId] = OldConfessorDefinitionId,
				[MaraCharacterId] = MaraDefinitionId,
				[RookCharacterId] = RookDefinitionId,
			};

		private static readonly IReadOnlyDictionary<string, string> DisplayNames =
			new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				[KeeperCharacterId] = "Keeper",
				[EliasCharacterId] = "Elias",
				[OldConfessorCharacterId] = "Old Confessor",
				[MaraCharacterId] = "Mara",
				[RookCharacterId] = "Rook",
			};

		private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> OrderedSegments =
			new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
			{
				[KeeperCharacterId] =
				[
					KeeperIntroSegmentId,
					KeeperEarlyReturnSegmentId,
				],
				[EliasCharacterId] =
				[
					"dialogue_1",
					"dialogue_2",
					"dialogue_3",
				],
				[OldConfessorCharacterId] =
				[
					"dialogue_1",
				],
				[MaraCharacterId] =
				[
					"dialogue_1",
					"dialogue_2",
					"dialogue_3",
					"dialogue_4",
				],
				[RookCharacterId] =
				[
					"dialogue_1",
				],
			};

		public static bool TryGetDefinitionId(string characterId, out string definitionId) =>
			DefinitionIds.TryGetValue(characterId ?? string.Empty, out definitionId);

		public static bool TryGetDisplayName(string characterId, out string displayName) =>
			DisplayNames.TryGetValue(characterId ?? string.Empty, out displayName);

		public static IReadOnlyList<string> GetOrderedSegments(string characterId) =>
			OrderedSegments.TryGetValue(characterId ?? string.Empty, out var segments)
				? segments
				: Array.Empty<string>();
	}
}
