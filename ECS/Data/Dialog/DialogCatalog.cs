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
				["waystation_keeper"] = new DialogDefinition
				{
					id = "waystation_keeper",
					segments = new Dictionary<string, List<DialogLine>>
					{
						["intro"] =
						[
							new() { actor = "Crusader", message = "What is this place?" },
							new() { actor = "Keeper", message = "The Waystation. Purgatory. You're dead, you've been judged, and you're bound for Heaven once your soul is cleansed. That's the simple part, thank God." },
							new() { actor = "Crusader", message = "And the rest?" },
							new() { actor = "Keeper", message = "Heaven's Gate and the climbs to get there are misbehaving. Souls leave for purgation, demons appear in their path. Demons in Purgatory...never thought I'd see the day." },
							new() { actor = "Remiel", message = "Don't mind him. He's been given a disaster, a desk, and no helpful instructions." },
							new() { actor = "Keeper", message = "That is painfully accurate." },
							new() { actor = "Crusader", message = "How did demons enter Purgatory?" },
							new() { actor = "Keeper", message = "I don't know. No one here knows. Something is bending the climbs, and I'm the angel currently failing to make sense of it." },
							new() { actor = "Crusader", message = "Then I'll climb and end this." },
							new() { actor = "Keeper", message = "You're armed. You're steady. And you're asking for a death sentence before I've finished warning you. You may be exactly what I need." },
							new() { actor = "Remiel", message = "Don't mind him, he processes grief by finding doors with enemies behind them." },
							new() { actor = "Keeper", message = "Fine. Fight what blocks the route. If the climb breaks, you'll return here. That's the plan, which is a generous name for what we have." },
						],
						["early_return"] =
						[
							new() { actor = "Keeper", message = "You came back. That's excellent. Terrible for my assumptions, but excellent." },
							new() { actor = "Crusader", message = "The climb threw me out." },
							new() { actor = "Keeper", message = "Yes, and you remember it. Most reports come back as fragments or contradictions. You came back with a route I can actually mark." },
							new() { actor = "Remiel", message = "Looks like you've become his most reliable filing system!" },
							new() { actor = "Keeper", message = "Don't make this anomaly sound small, this is the best lead I've received since stationed here." },
							new() { actor = "Crusader", message = "Then mark the route and send me again." },
							new() { actor = "Keeper", message = "I will. Fight what blocks you, remember what changes, and return before the climb breaks you beyond usefulness." },
							new() { actor = "Remiel", message = "He means that warmly of course. Administrative angels have a gift for making concern sound like inventory." },
							new() { actor = "Keeper", message = "I'm concerned and taking inventory, both are necessary." },
						],
					},
				},
				["waystation_elias"] = new DialogDefinition
				{
					id = "waystation_elias",
					segments = new Dictionary<string, List<DialogLine>>
					{
						["dialogue_1"] =
						[
							new() { actor = "Remiel", message = "Crusader, the man in the back pew. Try not to arrive like a siege tower." },
							new() { actor = "Elias", message = "Don't trouble yourself, knight. I'm only waiting until I can bear the sight of the gate." },
							new() { actor = "Crusader", message = "What are you hiding under your cloak?" },
							new() { actor = "Elias", message = "A battle standard. I carried it for my company in life. When the enemy broke through, I dropped it and ran while better men died holding the line." },
							new() { actor = "Crusader", message = "Then stand and carry it now." },
							new() { actor = "Elias", message = "I thought death would make me brave. It's only made me honest." },
							new() { actor = "Remiel", message = "Honesty's a good start. Most men take years to remove that much armor." },
							new() { actor = "Elias", message = "Is honesty enough?" },
							new() { actor = "Crusader", message = "No. But it's where men begin." },
							new() { actor = "Elias", message = "Then pray I don't run from the beginning too." },
						],
						["dialogue_2"] =
						[
							new() { actor = "Elias", message = "I moved three pews closer while you were gone. I wanted to call it progress, but that felt too flattering." },
							new() { actor = "Crusader", message = "Call it obedience." },
							new() { actor = "Remiel", message = "That's his favorite word. Be careful with it." },
							new() { actor = "Elias", message = "I uncovered the banner. There's a bloodstain near the staff. For years I told myself it was mine." },
							new() { actor = "Crusader", message = "It wasn't." },
							new() { actor = "Elias", message = "No. I knew whose it was. I preferred the kinder lie." },
							new() { actor = "Remiel", message = "Lies are rarely kind. They just speak softly at first." },
							new() { actor = "Elias", message = "I think that's why the gate frightens me. Nothing soft can pass through it unless it's true." },
							new() { actor = "Crusader", message = "Then keep walking toward it." },
							new() { actor = "Elias", message = "I will. One pew at a time, if God permits it." },
						],
						["dialogue_3"] =
						[
							new() { actor = "Elias", message = "I remembered Gerard's mother today. After the war, I brought her his medal and let her thank me as if I'd been loyal to him. I told myself silence was mercy because she was already grieving." },
							new() { actor = "Crusader", message = "You wanted her blessing." },
							new() { actor = "Elias", message = "I did. I wanted one person connected to that day to look at me kindly." },
							new() { actor = "Remiel", message = "That's a very human way to steal comfort. Ugly, but common." },
							new() { actor = "Elias", message = "I've lit a candle for her. That feels small." },
							new() { actor = "Crusader", message = "It is small." },
							new() { actor = "Remiel", message = "Small can still be sincere." },
							new() { actor = "Elias", message = "Then I'll keep it lit. I can't give her the truth now, but I can stop hiding from the man who needed it." },
						],
					},
				},
				["waystation_old_confessor"] = new DialogDefinition
				{
					id = "waystation_old_confessor",
					segments = new Dictionary<string, List<DialogLine>>
					{
						["dialogue_1"] =
						[
							new() { actor = "Old Confessor", message = "You've found the quiet corner, knight. Men usually come here after the gate has frightened them." },
							new() { actor = "Crusader", message = "Are you a priest?" },
							new() { actor = "Old Confessor", message = "I was. Now I listen. I can't absolve anyone here, but I remember how mercy sounds." },
							new() { actor = "Crusader", message = "Then why do souls come to you?" },
							new() { actor = "Old Confessor", message = "Because telling the truth still steadies the heart." },
							new() { actor = "Crusader", message = "My heart has had enough steadying. It needs to be made clean." },
							new() { actor = "Old Confessor", message = "That may be why God led you to a chair instead of another battlefield." },
							new() { actor = "Crusader", message = "You speak boldly for a man without authority." },
							new() { actor = "Old Confessor", message = "I have patience, memory, and time enough to listen." },
						],
					},
				},
				["waystation_mara"] = new DialogDefinition
				{
					id = "waystation_mara",
					segments = new Dictionary<string, List<DialogLine>>
					{
						["dialogue_1"] =
						[
							new() { actor = "Crusader", message = "I see you keep to one side." },
							new() { actor = "Mara", message = "It leaves room for souls with louder grief." },
							new() { actor = "Crusader", message = "Yours is quiet?" },
							new() { actor = "Mara", message = "It learned to be." },
							new() { actor = "Crusader", message = "What brought you here?" },
							new() { actor = "Mara", message = "The same thing that brings anyone. Something in me still needs mercy. I try not to make a display of it." },
						],
						["dialogue_2"] =
						[
							new() { actor = "Crusader", message = "You said you try not to make a display of needing mercy. Who taught you that?" },
							new() { actor = "Mara", message = "No one sat me down and said it. It just... worked better when I didn't make things complicated." },
							new() { actor = "Crusader", message = "\"Worked better\"?" },
							new() { actor = "Mara", message = "When people asked what I wanted, I learned to give them the answer that kept things smooth." },
							new() { actor = "Crusader", message = "And your own answer?" },
							new() { actor = "Mara", message = "It stopped feeling relevant." },
						],
						["dialogue_3"] =
						[
							new() { actor = "Crusader", message = "You looked at me before you answered, like you were checking something." },
							new() { actor = "Mara", message = "I suppose I was." },
							new() { actor = "Crusader", message = "What were you checking?" },
							new() { actor = "Mara", message = "Whether my answer would make things harder." },
							new() { actor = "Crusader", message = "For me?" },
							new() { actor = "Mara", message = "For anyone nearby." },
							new() { actor = "Crusader", message = "And if it would?" },
							new() { actor = "Mara", message = "Then I change it." },
						],
						["dialogue_4"] =
						[
							new() { actor = "Crusader", message = "Would you like me to sit with you for a while?" },
							new() { actor = "Mara", message = "I don't mind." },
							new() { actor = "Remiel", message = "That tells him he can. It doesn't tell him if you want him to." },
							new() { actor = "Mara", message = "I know. I'm trying not to ask too much." },
							new() { actor = "Remiel", message = "He offered." },
							new() { actor = "Mara", message = "People offer things to be kind. That doesn't mean I should take them." },
							new() { actor = "Crusader", message = "This isn't complicated." },
							new() { actor = "Mara", message = "I know. Yes, I'd like company." },
						],
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
