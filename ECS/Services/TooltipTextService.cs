using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Services
{
	internal static class TooltipTextService
	{
		public const string ColorlessStatus =
			"Colorless: Retains its printed color, but currently qualifies as no color.";

		// --- Constants ---
		public static readonly int FrostbiteThreshold = 3;
		public static readonly int FrostbiteDamage = 3;
		public static readonly float GalvanizeBonusFraction = 0.5f;

		// --- Card status tooltips ---

		/// <summary>
		/// Returns the full tooltip text for a card entity: base tooltip text plus any appended
		/// status-effect descriptions (Frozen, Brittle, Scorched, Thorned, Colorless, Intimidated, Shackle, Pledge, Sealed, Recoil).
		/// </summary>
		public static string BuildCardTooltip(Entity entity, string baseText, EntityManager entityManager = null)
		{
			string text = baseText;

			if (entity.GetComponent<Frozen>() != null)
				text += Sep(text) + "This card is frozen - when played, gain 1 frostbite. Lasts for the rest of the climb.";

			if (entity.GetComponent<Brittle>() != null)
				text += Sep(text) + "This card is brittle - if you block an attack with only this card, mill 1. Lasts for the rest of the climb.";

			if (entity.GetComponent<Scorched>() != null)
				text += Sep(text) + "This card is scorched - when pledged, lose 1 HP. Lasts for the rest of the climb.";

			if (entity.GetComponent<Thorned>() != null)
				text += Sep(text) + "This card is thorned - when discarded to pay a card cost, gain 1 scar. Lasts for the rest of the climb.";

			if (entity.GetComponent<Colorless>() != null && !ShouldSuppressColorlessStatus(entityManager))
				text += Sep(text) + ColorlessStatus;

			if (entity.GetComponent<Intimidated>() != null)
				text += Sep(text) + "This card is intimidated - cannot be used to block during the block phase.";

			if (entity.GetComponent<Shackle>() != null)
				text += Sep(text) + "This card is shackled - shackled cards block together.";

			var pledge = entity.GetComponent<Pledge>();
			if (pledge != null)
			{
				if (!pledge.CanPlay)
					text += Sep(text) + "This card is pledged - cannot be played until a later action phase. Does not count towards your hand size.";
				else
					text += Sep(text) + "This card is pledged - can be played during the action phase. Does not count towards your hand size.";
			}

			if (entity.GetComponent<PledgePreview>() != null)
				text += Sep(text) + "Pledged cards cannot be played the turn they are pledged. Does not count towards your hand size.";

			if (entity.GetComponent<Sealed>() != null)
				text += Sep(text) + "This card is sealed - costs HP equal to remaining seals to play. Seals decrease: -1 per block, -1 per card played. At 0 seals, card is freed. Cannot be pledged.";

			var recoil = entity.GetComponent<Recoil>();
			if (recoil != null)
				text += Sep(text) + $"This card has Recoil {recoil.Stacks} — if you don't block with it this turn, take {recoil.Stacks} damage.";

			return text;
		}

		private static string Sep(string text) => string.IsNullOrWhiteSpace(text) ? "" : "\n\n";

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
					return $"{(isPlayer ? "Your" : "The enemy's")} attacks deal +{stacks} damage.";
				case AppliedPassiveType.Poison:
					return "Every 60 seconds, lose 1 HP.";
				case AppliedPassiveType.Shield:
					return "Prevent all damage from the first source each turn.";
				case AppliedPassiveType.Guard:
					return $"Prevents the next {stacks} damage from attacks. Any damage removes all guard. At the start of the enemy turn, converts to 1 aggression.";
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
					return "Sealed cards cost HP equal to remaining seals when played or discarded to pay for costs. Seals decrease: -1 per block, -1 per card played. At 0 seals, card is freed.";
				case AppliedPassiveType.Plunder:
					return "At the start of the block phase, steals a card from your deck. Deal enough damage to rescue it.";
				case AppliedPassiveType.Marksman:
					return "Each turn a random card in your hand is marked. Playing a marked card removes the mark and applies the negative effect. Blocking with a marked card moves the mark to a different card and changes the negative effect.";
				case AppliedPassiveType.CarpeDiem:
					return "At the end of the turn, lose all courage.";
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
			if (text == null) return string.Empty;
			if (string.IsNullOrWhiteSpace(text)) return string.Empty;
			var lowerText = text.ToLowerInvariant();
			var matches = new List<(int Index, string Tooltip)>();

			int i;
			i = lowerText.IndexOf("stun");
			if (i >= 0) matches.Add((i, "X Stun - Skip the next X attack(s)."));
			i = lowerText.IndexOf("inferno");
			if (i >= 0) matches.Add((i, "X Inferno- At the start of the turn, gain X burn."));
			i = lowerText.IndexOf("slow");
			if (i >= 0) matches.Add((i, "X Slow - Ambush attacks are X second faster. At the end of your turn, lose 1 slow."));
			i = lowerText.IndexOf("aegis");
			if (i >= 0) matches.Add((i, " X Aegis - Prevent the next X damage from any source."));
			i = lowerText.IndexOf("burn");
			if (i >= 0) matches.Add((i, "X Burn - At the start of the turn, take X damage."));
			i = lowerText.IndexOf("aggression");
			if (i >= 0) matches.Add((i, "X Aggression - Your next non-weapon attack this turn gains +X damage."));
			i = lowerText.IndexOf("galvanize");
			if (i >= 0) matches.Add((i, $"Galvanize - The next non-weapon attack this turn deals {GalvanizeBonusFraction * 100}% more damage. Bonus damage is rounded up."));
			i = lowerText.IndexOf("power");
			if (i >= 0) matches.Add((i, "X Power - Your attacks deal +X damage."));
			i = lowerText.IndexOf("sharpen");
			if (i >= 0) matches.Add((i, "X Sharpen - Your next weapon attack this turn gains +X damage."));
			i = lowerText.IndexOf("might");
			if (i >= 0) matches.Add((i, "X Might - Your attacks deal +X damage this turn."));
			i = lowerText.IndexOf("vigor");
			if (i >= 0) matches.Add((i, "X Vigor - The next non-weapon card with a cost you play costs X discard less."));
			var showScar = lowerText.IndexOf("scar ") >= 0 || lowerText.IndexOf("scars") >= 0 || lowerText.IndexOf("scars ") >= 0 || lowerText.IndexOf("scar.") >= 0;
			if (showScar)
			{
				i = lowerText.IndexOf("scar");
				matches.Add((i, "X Scar - Lose X max HP. At the start of battle, lose 1 scar. Max HP is not restored until the next battle recalculates from remaining scars."));
			}
			i = lowerText.IndexOf("fear");
			if (i >= 0) matches.Add((i, "X Fear - All enemy attacks become ambush attacks. At the end of a battle, lose 1 fear."));
			i = lowerText.IndexOf("wounded");
			if (i >= 0) matches.Add((i, "X Wounded - Take X more damage from all sources this battle."));
			i = lowerText.IndexOf("armor");
			if (i >= 0) matches.Add((i, "X Armor - Take X less damage from attacks this battle."));
			i = lowerText.IndexOf("guard");
			if (i >= 0) matches.Add((i, "X Guard - Prevents the next X damage from attacks. Any damage removes all guard. At the start of the enemy turn, converts to 1 aggression."));
			i = lowerText.IndexOf("bleed");
			if (i >= 0) matches.Add((i, "X Bleed - While you have bleed, lose 1 HP when you block with 2 or more cards of the same color, then remove one bleed stack. Lasts for the rest of the climb."));
			i = lowerText.IndexOf("mill");
			if (i >= 0) matches.Add((i, "Mill X - Discard the top X cards of your deck."));
			i = lowerText.IndexOf("resurrect");
			if (i >= 0) matches.Add((i, "Resurrect X - draw X random cards from your discard pile."));
			i = lowerText.IndexOf("frostbite");
			if (i >= 0) matches.Add((i, $"X Frostbite - When you have 3 stacks of frostbite, take {FrostbiteDamage} damage and lose 3 frostbite."));
			i = lowerText.IndexOf("frozen");
			if (i >= 0) matches.Add((i, "Frozen - When you play a frozen card, gain 1 frostbite."));
			i = lowerText.IndexOf("brittle");
			if (i >= 0) matches.Add((i, "Brittle - If you block an attack with only this card, mill 1."));
			i = lowerText.IndexOf("scorched");
			if (i >= 0) matches.Add((i, "Scorched - When pledged, lose 1 HP."));
			i = lowerText.IndexOf("thorned");
			if (i >= 0) matches.Add((i, "Thorned - When discarded to pay a card cost, gain 1 scar."));
			if (matches.Count == 0) return string.Empty;
			i = lowerText.IndexOf("darkness");
			if (i >= 0) matches.Add((i, "X Darkness - The enemy loses X damage when you pledge a card."));
			i = lowerText.IndexOf("silenced");
			if (i >= 0) matches.Add((i, "X Silenced - You cannot play pledged cards. Remove 1 silenced at the end of your action phase."));
			i = lowerText.IndexOf("seal");
			if (i >= 0) matches.Add((i, "Sealed - Sealed cards cost HP equal to remaining seals when played or discarded to pay for costs. Seals decrease: -1 per block, -1 per card played. At 0 seals, card is freed."));
			matches.Sort((a, b) => a.Index.CompareTo(b.Index));
			var parts = new string[matches.Count];
			for (int j = 0; j < matches.Count; j++) parts[j] = matches[j].Tooltip;
			return string.Join("\n", parts);
		}
	}
}
