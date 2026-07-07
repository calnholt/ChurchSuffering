using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;

namespace Crusaders30XX.ECS.Data.Tutorials
{
	public sealed record TutorialCardDefinition(string CardId, CardData.CardColor Color, bool IsColorless = false);

	public sealed record TutorialTurnDefinition(
		IReadOnlyList<TutorialCardDefinition> StockHand,
		IReadOnlyList<string> AttackIds);

	public sealed record TutorialSectionDefinition(
		int Section,
		int EnemyHp,
		string EnemyAttackId,
		int PlayerHp,
		bool IsTeachSection,
		IReadOnlyList<string> TeachMessageKeys,
		IReadOnlyList<TutorialTurnDefinition> Turns,
		bool ShowDrawPile = false,
		string PendingDialogKey = "");

	public static class GuidedTutorialDefinitions
	{
		public const string CompletionTutorialKey = "guided_tutorial";

		public static readonly IReadOnlyList<string> CoveredTutorialKeys =
		[
			"teach_win",
			"teach_loss",
			"teach_enemy_attack",
			"teach_black_block",
			"teach_weapon",
			"teach_red_courage",
			"teach_courage_hud",
			"teach_white_temperance",
			"teach_temperance_hud",
			"teach_intent_pips",
			"teach_pledge",
			"teach_action_points",
			"teach_free_actions",
			"teach_reckoning_discard",
		];

		public static readonly IReadOnlyList<TutorialDefinition> GuidedMessages =
		[
			new() { key = "teach_win", text = "Reduce the enemy's HP to zero to win the battle.", targetType = "entity_name", targetId = "Enemy", bubbleOrientation = "bottom" },
			new() { key = "teach_loss", text = "If your HP reaches zero, you lose.", targetType = "entity_name", targetId = "UI_PlayerHudHealth", bubbleOrientation = "right" },
			new() { key = "teach_enemy_attack", text = "Attacks show their DAMAGE value. Block incoming damage by assigning cards.", targetType = "ui_region", targetId = "enemy_attack_display", bubbleOrientation = "top" },
			new() { key = "teach_black_block", text = "Black cards receive one additional BLOCK.", targetType = "ui_region", targetId = "first_black_card", bubbleOrientation = "top" },
			new() { key = "teach_weapon", text = "Your weapon can be played once each turn.", targetType = "entity_name", targetId = "Weapon", bubbleOrientation = "top" },
			new() { key = "teach_red_courage", text = "Blocking with a red card grants 1 courage.", targetType = "ui_region", targetId = "first_red_card", bubbleOrientation = "top" },
			new() { key = "teach_courage_hud", text = "Your current courage is displayed here.", targetType = "entity_name", targetId = "UI_PlayerHudCourage", bubbleOrientation = "right" },
			new() { key = "teach_white_temperance", text = "Blocking with a white card grants 1 temperance.", targetType = "ui_region", targetId = "first_white_card", bubbleOrientation = "top" },
			new() { key = "teach_temperance_hud", text = "Your temperance meter is shown here. Hover over it to see your ability details.", targetType = "entity_name", targetId = "UI_PlayerHudTemperance", bubbleOrientation = "right" },
			new() { key = "teach_intent_pips", text = "Intent pips show the number of incoming enemy attacks for this turn, and the next turn.", targetType = "entity_name", targetId = "EnemyIntentPips", bubbleOrientation = "bottom" },
			new() { key = "teach_pledge", text = "Pledge one card to keep it for a later turn. Pledged cards cannot block or pay costs.", targetType = "entity_name", targetId = "UI_PlayerHudPledge", bubbleOrientation = "right" },
			new() { key = "teach_action_points", text = "You get one Action Point to spend during your Action phase. Cards that cost AP show their cost.", targetType = "entity_name", targetId = "UI_PlayerHudActionPoint", bubbleOrientation = "right", condition = "has_non_free_card" },
			new() { key = "teach_free_actions", text = "FREE ACTIONS do not consume your action point.", targetType = "ui_region", targetId = "litany_of_wrath", bubbleOrientation = "top", condition = "has_litany_of_wrath_in_hand" },
			new() { key = "teach_reckoning_discard", text = "This card requires you to DISCARD two other cards from your hand to play.", targetType = "ui_region", targetId = "reckoning", bubbleOrientation = "top", condition = "has_reckoning_in_hand" },
		];

		private static TutorialCardDefinition C(string id, bool colorless = true) => new(id, CardData.CardColor.Black, colorless);
		private static TutorialCardDefinition B(string id) => new(id, CardData.CardColor.Black);
		private static TutorialCardDefinition R(string id) => new(id, CardData.CardColor.Red);
		private static TutorialCardDefinition W(string id) => new(id, CardData.CardColor.White);

		private static readonly IReadOnlyList<TutorialSectionDefinition> Sections =
		[
			new(1, 3,  "tutorial_horde_strike_3", 1, true,
				["teach_win", "teach_loss", "teach_enemy_attack"],
				[Turn([C("smite"), C("smite")], ["tutorial_horde_strike_3"])]),

			new(2, 6,  "tutorial_horde_strike_3", 1, true,
				["teach_free_actions"],
				[Turn([C("smite"), C("litany_of_wrath"), C("smite")], ["tutorial_horde_strike_3"])]),

			new(3, 8,  "tutorial_horde_strike_3", 1, true,
				["teach_reckoning_discard"],
				[Turn([C("smite"), C("litany_of_wrath"), C("smite"), C("reckoning")], ["tutorial_horde_strike_3"])],
				PendingDialogKey: "catch_breath"),

			new(4, 10, "tutorial_horde_strike_8", 9, false,
				[],
				[Turn([C("absolution"), C("litany_of_wrath"), C("smite"), C("reckoning")], ["tutorial_horde_strike_8"])],
				PendingDialogKey: "sword_retrieved"),

			new(5, 5,  "tutorial_horde_strike_8", 1, true,
				["teach_black_block", "teach_weapon"],
				[Turn([B("smite"), B("smite"), B("smite"), B("smite")], ["tutorial_horde_strike_8"])]),

			new(6, 8,  "tutorial_horde_strike_6", 1, true,
				["teach_red_courage", "teach_courage_hud"],
				[Turn([B("stab"), R("smite"), W("smite"), W("smite")], ["tutorial_horde_strike_6"])]),

			new(7, 10, "tutorial_horde_strike_6", 1, true,
				["teach_white_temperance", "teach_temperance_hud"],
				[Turn([W("smite"), W("smite"), B("smite"), B("smite")], ["tutorial_horde_strike_6"])]),

			new(8, 12, "tutorial_horde_strike_8", 1, true,
				["teach_intent_pips", "teach_pledge"],
				[
					Turn([B("courageous"), B("smite"), B("smite"), B("fervor")], ["tutorial_horde_strike_8"]),
					Turn([R("litany_of_wrath"), R("absolution"), B("reckoning"), R("smite")], ["tutorial_horde_strike_6"]),
				],
				ShowDrawPile: true,
				PendingDialogKey: "last_of_them"),
		];

		public static TutorialSectionDefinition GetSection(int section)
		{
			int index = Math.Clamp(section - 1, 0, Sections.Count - 1);
			return Sections[index];
		}

		public static TutorialTurnDefinition GetTurn(int section, int turn)
		{
			var def = GetSection(section);
			int index = Math.Clamp(turn - 1, 0, def.Turns.Count - 1);
			return def.Turns[index];
		}

		public static int GetTurnCount(int section) => GetSection(section).Turns.Count;

		public static IReadOnlyList<string> GetMessageKeys(
			int section,
			int turn,
			SubPhase phase,
			int confirmedAttackCount)
		{
			var sec = GetSection(section);
			if (!sec.IsTeachSection || sec.TeachMessageKeys.Count == 0)
				return Array.Empty<string>();

			if (section == 1)
			{
				if (phase == SubPhase.Block)
					return ["teach_win", "teach_loss", "teach_enemy_attack"];
				if (phase == SubPhase.Action)
					return ["teach_action_points"];
				return Array.Empty<string>();
			}

			if (section == 2)
			{
				if (phase == SubPhase.Action)
					return ["teach_free_actions"];
				return Array.Empty<string>();
			}

			if (section == 3)
			{
				if (phase == SubPhase.Action)
					return ["teach_reckoning_discard"];
				return Array.Empty<string>();
			}

			if (section == 5)
			{
				if (phase == SubPhase.Block)
					return ["teach_black_block"];
				if (phase == SubPhase.Action)
					return ["teach_weapon"];
				return Array.Empty<string>();
			}

			if (section == 6 && phase == SubPhase.Block)
				return [.. sec.TeachMessageKeys];

			if (section == 7 && phase == SubPhase.Block)
				return [.. sec.TeachMessageKeys];

			if (section == 8)
			{
				if (phase == SubPhase.Block)
					return ["teach_intent_pips"];
				if (phase == SubPhase.Action)
					return ["teach_pledge"];
				return Array.Empty<string>();
			}

			return Array.Empty<string>();
		}

		private static TutorialTurnDefinition Turn(
			IReadOnlyList<TutorialCardDefinition> hand,
			IReadOnlyList<string> attacks) =>
			new(hand, attacks);
	}
}
