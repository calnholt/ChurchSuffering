using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Services
{
	internal static class TooltipTextService
	{
		public readonly record struct TooltipTextBlock(string Id, string Text);

		private sealed class KeywordDefinition
		{
			public string Id { get; init; }
			public string Tooltip { get; init; }
			public string[] Aliases { get; init; }
		}

		public const string ColorlessStatus =
			"Colorless: Retains its printed color, but currently qualifies as no color.";

		// --- Constants ---
		public static readonly int FrostbiteThreshold = 3;
		public static readonly int FrostbiteDamage = 3;
		public static readonly float GalvanizeBonusFraction = 0.5f;

		// --- Card status tooltips ---

		private static readonly KeywordDefinition[] KeywordDefinitions =
		[
			new() { Id = "stun", Aliases = ["stun", "stunned"], Tooltip = "X Stun - Skip the next X attack(s)." },
			new() { Id = "inferno", Aliases = ["inferno"], Tooltip = "X Inferno - At the start of the turn, gain X burn." },
			new() { Id = "slow", Aliases = ["slow"], Tooltip = "X Slow - Ambush attacks are X second faster. At the end of your turn, lose 1 slow." },
			new() { Id = "aegis", Aliases = ["aegis"], Tooltip = "X Aegis - Prevent the next X damage from any source." },
			new() { Id = "burn", Aliases = ["burn", "burns"], Tooltip = "X Burn - At the start of the turn, take X damage." },
			new() { Id = "aggression", Aliases = ["aggression"], Tooltip = "X Aggression - Your next non-weapon attack this turn gains +X damage." },
			new() { Id = "galvanize", Aliases = ["galvanize"], Tooltip = $"Galvanize - The next non-weapon attack this turn deals {GalvanizeBonusFraction * 100}% more damage. Bonus damage is rounded up." },
			new() { Id = "power", Aliases = ["power"], Tooltip = "X Power - Your attacks deal +X damage this battle." },
			new() { Id = "sharpen", Aliases = ["sharpen"], Tooltip = "X Sharpen - Your next weapon attack this turn gains +X damage." },
			new() { Id = "might", Aliases = ["might"], Tooltip = "X Might - Your attacks deal +X damage this turn." },
			new() { Id = "vigor", Aliases = ["vigor"], Tooltip = "X Vigor - The next non-weapon card with a cost you play costs X discard less." },
			new() { Id = "grace", Aliases = ["grace"], Tooltip = "X Grace - At the start of your turn (after draw), resurrect 1. Lose 1 grace." },
			new() { Id = "scar", Aliases = ["scar", "scars"], Tooltip = "X Scar - Lose X max HP. At the start of battle, lose 1 scar. Max HP is not restored until the next battle recalculates from remaining scars." },
			new() { Id = "fear", Aliases = ["fear"], Tooltip = "X Fear - All enemy attacks become ambush attacks. At the end of a battle, lose 1 fear." },
			new() { Id = "wounded", Aliases = ["wounded"], Tooltip = "X Wounded - Take X more damage from all sources this battle." },
			new() { Id = "armor", Aliases = ["armor"], Tooltip = "X Armor - Take X less damage from attacks this battle." },
			new() { Id = "guard", Aliases = ["guard"], Tooltip = "X Guard - Prevents the next X damage from attacks. Any attack damage removes all guard. Removed at the start of the enemy turn if unused." },
			new() { Id = "bleed", Aliases = ["bleed"], Tooltip = "X Bleed - While you have bleed, lose 1 HP when you block with 2 or more cards of the same color, then remove one bleed stack. Lasts for the rest of the climb." },
			new() { Id = "mill", Aliases = ["mill"], Tooltip = "Mill X - Discard the top X cards of your deck." },
			new() { Id = "resurrect", Aliases = ["resurrect"], Tooltip = "Resurrect X - draw X random cards from your discard pile." },
			new() { Id = "frostbite", Aliases = ["frostbite"], Tooltip = $"X Frostbite - When you have {FrostbiteThreshold} stacks of frostbite, take {FrostbiteDamage} damage and lose {FrostbiteThreshold} frostbite." },
			new() { Id = "frozen", Aliases = ["frozen"], Tooltip = "Frozen - When you play a frozen card, gain 1 frostbite." },
			new() { Id = "brittle", Aliases = ["brittle"], Tooltip = "Brittle - If you block an attack with only this card, mill 1." },
			new() { Id = "scorched", Aliases = ["scorched"], Tooltip = "Scorched - When pledged, lose 1 HP." },
			new() { Id = "thorned", Aliases = ["thorned"], Tooltip = "Thorned - When discarded to pay a card cost, gain 1 scar." },
			new() { Id = "poisoned", Aliases = ["poisoned"], Tooltip = "Poisoned - When used to block, lose 1 HP." },
			new() { Id = "darkness", Aliases = ["darkness"], Tooltip = "X Darkness - The enemy loses X damage when you pledge a card." },
			new() { Id = "silenced", Aliases = ["silenced"], Tooltip = "X Silenced - You cannot play pledged cards. Remove 1 silenced at the end of your action phase." },
			new() { Id = "sealed", Aliases = ["seal", "seals", "sealed"], Tooltip = "Sealed - Cannot be pledged. Lose 1 seal when used to block. At 0 seals, the card is freed." },
		];

		/// <summary>
		/// Returns tooltip blocks for a card entity: optional base text, status-effect descriptions,
		/// and recursively discovered keyword descriptions.
		/// </summary>
		public static IReadOnlyList<TooltipTextBlock> BuildTooltipBlocks(
			Entity entity,
			string baseText,
			EntityManager entityManager = null,
			string keywordSource = null)
		{
			var blocks = new List<TooltipTextBlock>();
			var shownKeywordIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			var scanTexts = new List<string>();

			if (!string.IsNullOrWhiteSpace(keywordSource))
				scanTexts.Add(keywordSource);

			if (!string.IsNullOrWhiteSpace(baseText))
			{
				blocks.Add(new TooltipTextBlock("base", baseText));
				if (scanTexts.Count == 0 || !scanTexts.Contains(baseText))
					scanTexts.Add(baseText);
			}

			AddCardStatusBlocks(entity, entityManager, blocks, shownKeywordIds);
			scanTexts.AddRange(blocks.Select(block => block.Text));
			blocks.AddRange(BuildRecursiveKeywordBlocks(scanTexts, shownKeywordIds));
			return blocks;
		}

		/// <summary>
		/// Returns the full tooltip text for compatibility with string-based callers.
		/// </summary>
		public static string BuildCardTooltip(Entity entity, string baseText, EntityManager entityManager = null)
		{
			return string.Join("\n\n", BuildTooltipBlocks(entity, baseText, entityManager).Select(block => block.Text));
		}

		private static void AddCardStatusBlocks(
			Entity entity,
			EntityManager entityManager,
			List<TooltipTextBlock> blocks,
			HashSet<string> shownKeywordIds)
		{
			if (entity == null) return;

			AddStatusBlock<Frozen>(entity, blocks, shownKeywordIds, "frozen", "This card is frozen - when played, gain 1 frostbite. Lasts for the rest of the climb.");
			AddStatusBlock<Brittle>(entity, blocks, shownKeywordIds, "brittle", "This card is brittle - if you block an attack with only this card, mill 1. Lasts for the rest of the climb.");
			AddStatusBlock<Scorched>(entity, blocks, shownKeywordIds, "scorched", "This card is scorched - when pledged, lose 1 HP. Lasts for the rest of the climb.");
			AddStatusBlock<Thorned>(entity, blocks, shownKeywordIds, "thorned", "This card is thorned - when discarded to pay a card cost, gain 1 scar. Lasts for the rest of the climb.");
			AddStatusBlock<Poisoned>(entity, blocks, shownKeywordIds, "poisoned", "This card is poisoned - when used to block, lose 1 HP.");

			if (entity.GetComponent<Colorless>() != null && !ShouldSuppressColorlessStatus(entityManager))
				blocks.Add(new TooltipTextBlock("colorless", ColorlessStatus));

			AddStatusBlock<Intimidated>(entity, blocks, shownKeywordIds, "intimidated", "This card is intimidated - cannot be used to block during the block phase.");
			AddStatusBlock<Shackle>(entity, blocks, shownKeywordIds, "shackle", "This card is shackled - shackled cards block together.");

			var pledge = entity.GetComponent<Pledge>();
			if (pledge != null)
			{
				if (!pledge.CanPlay)
					blocks.Add(new TooltipTextBlock("pledge", "This card is pledged - cannot be played until a later action phase. Does not count towards your hand size."));
				else
					blocks.Add(new TooltipTextBlock("pledge", "This card is pledged - can be played during the action phase. Does not count towards your hand size."));
			}

			if (entity.GetComponent<PledgePreview>() != null)
				blocks.Add(new TooltipTextBlock("pledge-preview", "Pledged cards cannot be played the turn they are pledged. Does not count towards your hand size."));

			if (entity.GetComponent<Sealed>() != null)
			{
				shownKeywordIds.Add("sealed");
				blocks.Add(new TooltipTextBlock("sealed", "This card is sealed - cannot be pledged. Lose 1 seal when used to block. At 0 seals, the card is freed."));
			}

			var recoil = entity.GetComponent<Recoil>();
			if (recoil != null)
				blocks.Add(new TooltipTextBlock("recoil", $"This card has Recoil {recoil.Stacks} - if you don't block with it this turn, take {recoil.Stacks} damage."));
		}

		private static void AddStatusBlock<T>(
			Entity entity,
			List<TooltipTextBlock> blocks,
			HashSet<string> shownKeywordIds,
			string id,
			string text) where T : class, IComponent
		{
			if (entity.GetComponent<T>() == null) return;
			shownKeywordIds.Add(id);
			blocks.Add(new TooltipTextBlock(id, text));
		}

		private static bool ShouldSuppressColorlessStatus(EntityManager entityManager) =>
			StateSingleton.IsTutorialActive
			|| (entityManager != null && GuidedTutorialService.IsActive(entityManager));

		// --- Passive tooltip text ---

		public static string GetPassiveText(AppliedPassiveType type, bool isPlayer, int stacks)
		{
			var text = GetPassiveTooltip(type, isPlayer, stacks);
			var suffix = " (Climb)";
			if (AppliedPassivesManagementSystem.GetTurnPassives().Contains(type))
				suffix = " (Turn)";
			else if (AppliedPassivesManagementSystem.GetBattlePassives().Contains(type))
				suffix = " (Battle)";
			return $"{text}{suffix}";
		}

		public static string GetPassiveTooltip(AppliedPassiveType type, bool isPlayer, int stacks)
		{
			switch (type)
			{
				case AppliedPassiveType.Burn:
					return $"At the start of {(isPlayer ? "your" : "the enemy's")} turn, {(isPlayer ? "you take" : "it takes")} {stacks} damage.";
				case AppliedPassiveType.Slow:
					return $"Ambush attacks are {stacks} second{(stacks == 1 ? "" : "s")} faster. At the end of your turn, lose 1 slow.";
				case AppliedPassiveType.Aegis:
					return $"Prevents the next {stacks} damage from any source.";
				case AppliedPassiveType.Stun:
					return $"Skips the next {stacks} attack{(stacks > 1 ? "s" : "")}.";
				case AppliedPassiveType.Armor:
					return $"Takes {stacks} less damage from attacks.";
				case AppliedPassiveType.Wounded:
					return $"Takes {stacks} more damage from all sources.";
				case AppliedPassiveType.Webbing:
					return $"At the start of your turn, gain {stacks} slow.";
				case AppliedPassiveType.Inferno:
					return $"At the start of your turn, gain {stacks} burn{(stacks == 1 ? "" : "s")}.";
				case AppliedPassiveType.Scar:
					return $"Lose {stacks} max HP. At the start of battle, lose 1 scar. Max HP is not restored until the next battle recalculates from remaining scars.";
				case AppliedPassiveType.Aggression:
					return $"Your next non-weapon attack this turn gains {stacks} damage.";
				case AppliedPassiveType.Galvanize:
					return $"The next non-weapon attack this turn deals {GalvanizeBonusFraction * 100}% more damage. Bonus damage is rounded up.";
				case AppliedPassiveType.Sharpen:
					return $"Your next weapon attack this turn gains {stacks} damage.";
				case AppliedPassiveType.Might:
					return $"Your attacks deal +{stacks} damage this turn.";
				case AppliedPassiveType.Vigor:
					return $"The next non-weapon card with a cost you play costs {stacks} discard less.";
				case AppliedPassiveType.Stealth:
					return "You cannot see the number of attacks this monster plans.";
				case AppliedPassiveType.Power:
					return $"{(isPlayer ? "Your" : "The enemy's")} attacks deal +{stacks} damage this battle.";
				case AppliedPassiveType.Poison:
					return "At the start of each block phase, one card in your hand becomes poisoned. Blocking with it loses 1 HP. At the end of your turn, lose 1 poison.";
				case AppliedPassiveType.Shield:
					return "Prevent all damage from the first source each turn.";
				case AppliedPassiveType.Guard:
					return $"Prevents the next {stacks} damage from attacks. Any attack damage removes all guard. Removed at the start of the enemy turn if unused.";
				case AppliedPassiveType.Fear:
					return "All enemy attacks become ambush attacks. At the end of a battle, lose 1 fear.";
				case AppliedPassiveType.Siphon:
					return $"For each point of courage this enemy removes from you, it heals {stacks * Succubus.SiphonMultiplier} HP.";
				case AppliedPassiveType.Thorns:
					return $"You gain {stacks} bleed whenever you attack this enemy.";
				case AppliedPassiveType.Bleed:
					return "When you block with 2 or more cards of the same color, lose 1HP then remove one bleed stack.";
				case AppliedPassiveType.Rage:
					return $"{(isPlayer ? "You" : "The enemy")} gain{(isPlayer ? "" : "s")} {stacks} power at the start of the {(isPlayer ? "action phase" : "block phase")}.";
				case AppliedPassiveType.Intellect:
					return $"Your max hand size and the number of cards you draw at the start of the block phase is increased by {stacks}.";
				case AppliedPassiveType.Intimidated:
					return $"At the start of the block phase, {stacks} {(stacks == 1 ? "card" : "cards")} from your hand {(stacks == 1 ? "is" : "are")} intimidated.";
				case AppliedPassiveType.MindFog:
					return "At the end of your action phase, discard all cards in your hand.";
				case AppliedPassiveType.Channel:
					return "Increases the potency of attacks.";
				case AppliedPassiveType.Frostbite:
					return $"When you have {FrostbiteThreshold} stacks of frostbite, take {FrostbiteDamage} damage and lose {FrostbiteThreshold} frostbite.";
				case AppliedPassiveType.Frozen:
					return "When you play a frozen card, gain 1 frostbite.";
				case AppliedPassiveType.SubZero:
					return "At the start of the enemy turn, freeze one card from your hand.";
				case AppliedPassiveType.Windchill:
					return "Whenever you block with a frozen card, gain 1 scar.";
				case AppliedPassiveType.Enflamed:
					return $"If you have 4+ courage at the end of the action phase, take {stacks} damage.";
				case AppliedPassiveType.Shackled:
					return "At the start of the block phase, shackle 2 cards from your hand. Remove 1 shackled stacks by blocking with them.";
				case AppliedPassiveType.Anathema:
					return $"When you pledge a card, the enemy loses {stacks} damage.";
				case AppliedPassiveType.Silenced:
					return "You cannot play pledged cards. Remove 1 silenced at the end of your action phase.";
				case AppliedPassiveType.Sealed:
					return "Sealed cards cannot be pledged. Lose 1 seal when used to block. At 0 seals, the card is freed.";
				case AppliedPassiveType.Plunder:
					return "At the start of the block phase, steals a card from your deck. Deal enough damage to rescue it.";
				case AppliedPassiveType.CarpeDiem:
					return "At the end of the turn, lose all courage.";
				case AppliedPassiveType.SwordIntoShield:
					return $"Your next non-weapon attack card this turn gains +{stacks} damage this climb.";
				case AppliedPassiveType.Grace:
					return "At the start of your turn (after draw), resurrect 1. Lose 1 grace.";
				default:
					return StringUtils.ToSentenceCase(type.ToString());
			}
		}

		// --- Keyword tooltip text ---

		/// <summary>
		/// Scans the given text for keyword mentions and returns stacked definitions for each found keyword.
		/// </summary>
		public static string GetKeywordTooltip(string text)
		{
			return string.Join("\n", GetKeywordTooltipBlocks(text).Select(block => block.Text));
		}

		public static IReadOnlyList<TooltipTextBlock> GetKeywordTooltipBlocks(string text)
		{
			return BuildRecursiveKeywordBlocks([text], new HashSet<string>(StringComparer.OrdinalIgnoreCase));
		}

		private static IReadOnlyList<TooltipTextBlock> BuildRecursiveKeywordBlocks(
			IEnumerable<string> sourceTexts,
			HashSet<string> shownKeywordIds)
		{
			var blocks = new List<TooltipTextBlock>();
			var pending = new Queue<string>(
				sourceTexts.Where(text => !string.IsNullOrWhiteSpace(text)));

			while (pending.Count > 0)
			{
				var text = pending.Dequeue();
				foreach (var match in FindKeywordMatches(text))
				{
					if (!shownKeywordIds.Add(match.Definition.Id)) continue;
					var block = new TooltipTextBlock(match.Definition.Id, match.Definition.Tooltip);
					blocks.Add(block);
					pending.Enqueue(block.Text);
				}
			}

			return blocks;
		}

		private static IEnumerable<(int Index, int RegistryIndex, KeywordDefinition Definition)> FindKeywordMatches(string text)
		{
			if (string.IsNullOrWhiteSpace(text)) yield break;

			var matches = new List<(int Index, int RegistryIndex, KeywordDefinition Definition)>();
			for (int registryIndex = 0; registryIndex < KeywordDefinitions.Length; registryIndex++)
			{
				var definition = KeywordDefinitions[registryIndex];
				int index = FindFirstAliasIndex(text, definition.Aliases);
				if (index >= 0)
					matches.Add((index, registryIndex, definition));
			}

			foreach (var match in matches
				.OrderBy(match => match.Index)
				.ThenBy(match => match.RegistryIndex))
			{
				yield return match;
			}
		}

		private static int FindFirstAliasIndex(string text, IEnumerable<string> aliases)
		{
			int best = -1;
			foreach (var alias in aliases)
			{
				int start = 0;
				while (start < text.Length)
				{
					int index = text.IndexOf(alias, start, StringComparison.OrdinalIgnoreCase);
					if (index < 0) break;
					if (IsTermBoundary(text, index - 1) && IsTermBoundary(text, index + alias.Length))
					{
						if (best < 0 || index < best)
							best = index;
						break;
					}
					start = index + alias.Length;
				}
			}
			return best;
		}

		private static bool IsTermBoundary(string text, int index)
		{
			if (index < 0 || index >= text.Length) return true;
			return !char.IsLetterOrDigit(text[index]);
		}
	}
}
