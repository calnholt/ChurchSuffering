using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.Save;

namespace ChurchSuffering.ECS.Data.Climb
{
	public sealed class ClimbEventDefinition
	{
		public string DefinitionId { get; init; } = string.Empty;
		public ClimbEventKind Kind { get; init; }
		public string Title { get; init; } = string.Empty;
		public string NarrativeBody { get; init; } = string.Empty;
		public ClimbHazardEffectType HazardEffect { get; init; }
		public ClimbCharacterRewardType CharacterReward { get; init; }
		public string Actor { get; init; } = string.Empty;
		public string PortraitAsset { get; init; } = string.Empty;
		public string DialogueSegmentId { get; init; } = "climb_event";
		public IReadOnlyList<ClimbEventDialogueLine> DialogueLines { get; init; } = Array.Empty<ClimbEventDialogueLine>();
		public string SummaryTitle { get; init; } = string.Empty;
		public string SummaryBody { get; init; } = string.Empty;
		public string NoTargetSummaryBody { get; init; } = string.Empty;
		public string GainLine1 { get; init; } = string.Empty;
		public string GainLine2 { get; init; } = string.Empty;
	}

	public sealed class ClimbEventDialogueLine
	{
		public string Actor { get; init; } = string.Empty;
		public string Text { get; init; } = string.Empty;
	}

	public static class ClimbEventCatalog
	{
		private static readonly IReadOnlyList<ClimbEventDefinition> Hazards = new List<ClimbEventDefinition>
		{
			Hazard("bleached_standard", "The Bleached Standard", ClimbHazardEffectType.Colorless,
				"A torn battle standard rises from the sand with no army beneath it. Its colors drain as you approach, and the blank cloth strains toward your deck like a hand seeking a name."),
			Hazard("winter_reliquary", "The Winter Reliquary", ClimbHazardEffectType.Frozen,
				"An iron reliquary lies cold beneath the noon sun. When its seal breaks, a breath of winter coils around your deck and settles between the cards."),
			Hazard("glass_psalm", "The Glass Psalm", ClimbHazardEffectType.Brittle,
				"Wind uncovers a chapel made of glass-thin stone. A single hymn rings from its empty nave. One card answers, its fibers hardening until they sound like bone."),
			Hazard("cinders_in_the_censer", "Cinders in the Censer", ClimbHazardEffectType.Burn,
				"A censer swings from a dead tree though the air is still. Its coals flare when you pass, searing a mark into your flesh before dropping bright offerings into the dust."),
			Hazard("second_footsteps", "The Second Footsteps", ClimbHazardEffectType.Fear,
				"At dusk, a second set of footprints appears beside yours. They stop when you stop. They begin again one step before you move."),
			Hazard("penitents_chain", "The Penitent's Chain", ClimbHazardEffectType.Shackled,
				"Half-buried chains rattle beneath a weathered shrine. When you pull free the offering tangled among them, the loose links coil around your wrists and vanish beneath the skin."),
			Hazard("saint_of_old_wounds", "The Saint of Old Wounds", ClimbHazardEffectType.Scar,
				"A cracked saint's effigy kneels beside a dry well. The stone bears wounds that match no martyr's tale. When you take the coins at its feet, the cuts open across your own skin."),
		};

		private static readonly IReadOnlyList<ClimbEventDefinition> Characters = new List<ClimbEventDefinition>
		{
			Character(
				"nun_counsel", "Nun", "character/nun", ClimbCharacterRewardType.Temperance,
				"Measured Grace",
				"The nun's measured counsel steadies your spirit. Gain 2 Temperance in the next battle.",
				"+2 TEMPERANCE", "NEXT BATTLE",
				("Nun", "You carry every wound as if suffering were proof of purpose. Take two measured breaths before you draw steel."),
				("Crusader", "Pain is easier to trust than mercy. But I will take the breaths.")),
			Character(
				"reverent_crusader_counsel", "Reverent Crusader", "character/reverent_crusader", ClimbCharacterRewardType.Courage,
				"A Steadier Heart",
				"The reverent crusader's conviction hardens your resolve. Gain 2 Courage in the next battle.",
				"+2 COURAGE", "NEXT BATTLE",
				("Reverent Crusader", "Your guard is sound, but your heart enters battle after your blade. Courage is command over doubt. Remember that."),
				("Crusader", "My blade has fewer doubts. I will teach my heart to follow.")),
			Character(
				"revered_crusader_training", "Revered Crusader", "character/revered_crusader", ClimbCharacterRewardType.Vigor,
				"Strength Without Waste",
				"The revered crusader strips wasted motion from your stance. Gain 1 Vigor in the next battle.",
				"+1 VIGOR", "NEXT BATTLE",
				("Revered Crusader", "You waste strength fighting the weight of your own armor. Set your feet, loosen your shoulders, and let it serve you."),
				("Crusader", "Armor is meant to be carried. Show me how to carry it well.")),
			Character(
				"smith_forging", "Smith", "character/smith", ClimbCharacterRewardType.RandomCardUpgrade,
				"The Smith's Work",
				"The smith raises his hammer over your deck. A random eligible card gains an upgrade.",
				"RANDOM CARD", "UPGRADE",
				("Smith", "That card has seen hard use. I cannot mend it, but I can make it worthy of your hand."),
				("Crusader", "Then strike while the iron still fears you."),
				"The smith studies every card, then lowers his hammer. Nothing remains for him to improve."),
		};

		private static readonly IReadOnlyDictionary<string, ClimbEventDefinition> ById = Hazards
			.Concat(Characters)
			.ToDictionary(definition => definition.DefinitionId, StringComparer.OrdinalIgnoreCase);

		public static IReadOnlyList<ClimbEventDefinition> GetHazards() => Hazards;
		public static IReadOnlyList<ClimbEventDefinition> GetCharacters() => Characters;

		public static ClimbEventDefinition Get(string definitionId)
		{
			return !string.IsNullOrWhiteSpace(definitionId) && ById.TryGetValue(definitionId, out var definition)
				? definition
				: null;
		}

		private static ClimbEventDefinition Hazard(
			string id,
			string title,
			ClimbHazardEffectType effect,
			string body)
		{
			return new ClimbEventDefinition
			{
				DefinitionId = id,
				Kind = ClimbEventKind.Hazard,
				Title = title,
				NarrativeBody = body,
				HazardEffect = effect,
			};
		}

		private static ClimbEventDefinition Character(
			string id,
			string actor,
			string portraitAsset,
			ClimbCharacterRewardType reward,
			string summaryTitle,
			string summaryBody,
			string gainLine1,
			string gainLine2,
			(string Actor, string Text) firstLine,
			(string Actor, string Text) secondLine,
			string noTargetSummaryBody = "")
		{
			return new ClimbEventDefinition
			{
				DefinitionId = id,
				Kind = ClimbEventKind.Character,
				Actor = actor,
				PortraitAsset = portraitAsset,
				CharacterReward = reward,
				SummaryTitle = summaryTitle,
				SummaryBody = summaryBody,
				NoTargetSummaryBody = noTargetSummaryBody,
				GainLine1 = gainLine1,
				GainLine2 = gainLine2,
				DialogueLines = new List<ClimbEventDialogueLine>
				{
					new ClimbEventDialogueLine { Actor = firstLine.Actor, Text = firstLine.Text },
					new ClimbEventDialogueLine { Actor = secondLine.Actor, Text = secondLine.Text },
				},
			};
		}
	}
}
