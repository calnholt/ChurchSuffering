using System;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Data.Dialog
{
	public static class DialogCatalog
	{
		private static readonly IReadOnlyDictionary<string, DialogDefinition> Definitions =
			new Dictionary<string, DialogDefinition>(StringComparer.OrdinalIgnoreCase)
			{
				["guided_tutorial"] = new DialogDefinition
				{
					id = "guided_tutorial",
					segments = new Dictionary<string, List<DialogLine>>
					{
						["intro"] =
						[
							new() { actor = "Remiel", message = "There you are, eyes open. Now I need you awake a great deal faster, because we have a problem brewing about thirty feet to your left." },
							new() { actor = "Crusader", message = "...Where is my sword?" },
							new() { actor = "Remiel", message = "It came with you, but it is out of reach. Come on, up you get! Quick recap: you died, took a spear, very gallant..." },
							new() { actor = "Crusader", message = "And woke to sulphur and something breathing in the dark. So this is Hell. I always suspected I had earned it." },
							new() { actor = "Remiel", message = "Hell? No, no, no. This is Purgatory. How would an angel end up in Hell with you?" },
							new() { actor = "Crusader", message = "...Then tell me, angel. Why is something like that here?" },
							new() { actor = "Remiel", message = "...Yeah. That is the part keeping me from enjoying being right." },
							new() { actor = "Crusader", message = "Then it does not need explaining. It needs to be put down." },
						],
						["catch_breath"] =
						[
							new() { actor = "Remiel", message = "Enough! Stop a moment. You are bleeding badly. Let me tend those wounds." },
							new() { actor = "Crusader", message = "I can still fight." },
							new() { actor = "Remiel", message = "You will fight better if you are not half-dead. Trust me. Just breathe." },
						],
						["sword_retrieved"] =
						[
							new() { actor = "Crusader", message = "There. My sword. Now I remember why I carried it through every campaign." },
							new() { actor = "Remiel", message = "Good. Then let us finish this properly." },
						],
						["last_of_them"] =
						[
							new() { actor = "Remiel", message = "I think that was the last of them. For now, at least." },
							new() { actor = "Crusader", message = "Then we keep moving. Purgatory will not cleanse itself." },
						],
					},
				},
				["fallen_shepherd"] = new DialogDefinition
				{
					id = "fallen_shepherd",
					segments = new Dictionary<string, List<DialogLine>>
					{
						["intro"] = [new() { actor = "Fallen Shepherd", message = "..." }],
						["phase_1_end"] = [new() { actor = "Fallen Shepherd", message = "..." }],
						["phase_2_end"] = [new() { actor = "Fallen Shepherd", message = "..." }],
						["victory"] = [new() { actor = "Fallen Shepherd", message = "..." }],
					},
				},
				["nun_counsel"] = Segment("nun_counsel", "climb_event",
					("Nun", "You carry every wound as if suffering were proof of purpose. Take two measured breaths before you draw steel."),
					("Crusader", "Pain is easier to trust than mercy. But I will take the breaths.")),
				["reverent_crusader_counsel"] = Segment("reverent_crusader_counsel", "climb_event",
					("Reverent Crusader", "Your guard is sound, but your heart enters battle after your blade. Courage is command over doubt. Remember that."),
					("Crusader", "My blade has fewer doubts. I will teach my heart to follow.")),
				["revered_crusader_training"] = Segment("revered_crusader_training", "climb_event",
					("Revered Crusader", "You waste strength fighting the weight of your own armor. Set your feet, loosen your shoulders, and let it serve you."),
					("Crusader", "Armor is meant to be carried. Show me how to carry it well.")),
				["smith_forging"] = Segment("smith_forging", "climb_event",
					("Smith", "That card has seen hard use. I cannot mend it, but I can make it worthy of your hand."),
					("Crusader", "Then strike while the iron still fears you.")),
				["desert_1"] = Lines("desert_1",
					("Angel", "We've been in this desert so long I have sand stuck in my halo!"),
					("Crusader", "...we just got here, Replacement."),
					("Angel", "What? No way. The heat must be getting to me... or maybe it's all this sand stuck in my [slow factor=0.01]-[/slow] [jitter]COUGH[/jitter] [slow factor=0.01]-[/slow] throat."),
					("Crusader", "Saints above, you're winded from talking."),
					("Angel", "Blegh. Anyways, remind me why we're in this AWFUL place again?"),
					("Crusader", "Orders from the Holy See. Too many demonic reports out here - reeks of a new Hellrift."),
					("Angel", "You mean that [nod]adorable[/nod] thing over there?"),
					("Crusader", "No. That's what crawled out of one."),
					("Horde", "[shake]HORDE[/shake]!"),
					("Angel", "Oh come on, that thing's way too cute to be evil!"),
					("Crusader", "You'll learn fast - even demons can wear friendly faces. *grips sword* First test of the desert. Ready yourself."),
					("Angel", "Death to the cute demon! Hold on, let me just [slow factor=0.01]-[/slow] [jitter]COUGH[/jitter] [slow factor=0.01]-[/slow] okay, ready!")),
				["desert_2"] = Lines("desert_2",
					("Angel", "You ever notice how quiet it is out here? No birds, no wind - just silence heavy enough to choke on."),
					("Crusader", "A cursed silence. The land remembers every drop of blood spilled over gold and water. Greed made the sand itself hungry."),
					("Angel", "Greed? That's what birthed this Hellrift?"),
					("Crusader", "Hmhmmm. Men built shrines of wealth in a place that had none. When they finally turned on each other, Hell heard the prayers meant for treasure."),
					("Angel", "So the desert didn't just die - it was [jitter]consumed[/jitter]."),
					("Crusader", "And it's still feeding. The Rift's heart lies beneath the dunes somewhere. We'll know we're close when the sand starts to glimmer."),
					("Angel", "Glimmer? Like gold?"),
					("Crusader", "Exactly. That's Hell's joke - temptation lighting your path to damnation."),
					("Sand Corpse", "[shake][slow factor=0.1]GOLD... MINE...[/slow][/shake]"),
					("Angel", "[jitter]Ahhhhh![/jitter]")),
				["desert_3"] = Lines("desert_3",
					("Angel", "Wait... are those graves? There must be hundreds of them."),
					("Crusader", "Thousands. The Battle of Ashenfell Ridge. Fought here two centuries ago."),
					("Angel", "Two centuries? But the markers look almost... fresh."),
					("Crusader", "The desert preserves what it should let rot. Wood doesn't decay. Iron doesn't rust. Even the bones stay [slow factor=0.5]whole[/slow]."),
					("Angel", "That's... unsettling. What were they fighting for?"),
					("Crusader", "Everything. Nothing. Two kingdoms claimed the same oasis. By the time the battle ended, both armies had bled the water source dry."),
					("Angel", "They killed the very thing they were fighting over?"),
					("Crusader", "Pride does that. Turns men into weapons that destroy their own purpose. The survivors buried their dead and left. Never came back."),
					("Angel", "Do you think they... [slow factor=0.3]regretted[/slow] it?"),
					("Crusader", "I think they realized too late that some victories cost more than defeat ever could."),
					("Angel", "Should we... say something? For them?"),
					("Crusader", "*stops, removes helmet* Eternal rest grant unto them, O Lord. May they find in death the peace they never knew in life. *replaces helmet* Their souls still need our prayers, Replacement. But right now, the living need our swords."),
					("Angel", "...right behind you.")),
				["desert_4"] = Lines("desert_4",
					("Angel", "Oh thank the Saints! An oasis! We can finally rest and -"),
					("Crusader", "No."),
					("Angel", "What do you mean [jitter]no[/jitter]? It's right there! Water, shade, probably some nice cool -"),
					("Crusader", "We rest when the mission's done. Not before."),
					("Angel", "But I can barely feel my wings! And you've been marching in full armor for [slow factor=0.5]hours[/slow]!"),
					("Crusader", "I've marched for days in worse. The Hellrift won't close itself because we're [slow factor=0.3]tired[/slow]."),
					("Angel", "You know what? You're the most stubborn person I've ever met!"),
					("Crusader", "Good. Stubbornness keeps you alive when everything else fails. *continues walking* Now stop whining and keep up."),
					("Angel", "*sighs* This is going to be a [jitter]long[/jitter] partnership...")),
				["desert_5"] = Lines("desert_5",
					("Crusader", "What did you just say?"),
					("Angel", "Huh? I didn't say anything."),
					("Crusader", "You absolutely did. Something about my armor."),
					("Angel", "I was literally just breathing! I didn't -"),
					("Crusader", "I [jitter]heard[/jitter] you, Replacement. This helmet doesn't muffle [slow factor=0.5]everything[/slow]."),
					("Angel", "Okay but what if it does? What if that's [nod]exactly[/nod] what it does?"),
					("Crusader", "......"),
					("Angel", "Just saying, you've been wearing it in desert heat for like three hours -"),
					("Crusader", "My hearing is [shake]fine[/shake]. March.")),
			};

		public static IReadOnlyDictionary<string, DialogDefinition> GetAll() => Definitions;

		public static bool TryGet(string id, out DialogDefinition definition) =>
			Definitions.TryGetValue(id ?? string.Empty, out definition);

		private static DialogDefinition Lines(string id, params (string Actor, string Message)[] lines)
		{
			var definition = new DialogDefinition { id = id };
			foreach (var line in lines)
			{
				definition.lines.Add(new DialogLine { actor = line.Actor, message = line.Message });
			}
			return definition;
		}

		private static DialogDefinition Segment(
			string id,
			string segmentId,
			params (string Actor, string Message)[] lines)
		{
			var definition = new DialogDefinition { id = id };
			definition.segments[segmentId] = new List<DialogLine>();
			foreach (var line in lines)
			{
				definition.segments[segmentId].Add(new DialogLine { actor = line.Actor, message = line.Message });
			}
			return definition;
		}
	}
}
