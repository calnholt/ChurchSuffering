using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Data.Ids;

namespace ChurchSuffering.ECS.Systems
{
    internal enum GuardianAmbientMessageType
    {
        StartOfBattle,
        ActionPhase,
        Temperance,
    }

    internal static class GuardianAngelMessageService
    {
        private static readonly Dictionary<string, int> LastSelections = [];

        private static readonly IReadOnlyDictionary<GuardianAmbientMessageType, string[]> AmbientMessages =
            new Dictionary<GuardianAmbientMessageType, string[]>
            {
                [GuardianAmbientMessageType.StartOfBattle] = ["Stay close. We have this!", "Wings ready. Heart steady!"],
                [GuardianAmbientMessageType.ActionPhase] = ["Your turn. Make it count!", "I am right beside you!"],
                [GuardianAmbientMessageType.Temperance] = ["Easy now. Find your balance.", "Steady heart, steady hands."],
            };

        private static readonly IReadOnlyDictionary<CardId, string[]> CardMessages =
            new Dictionary<CardId, string[]>
            {
                [CardId.Absolution] = ["A clean heart fights lighter!", "Leave that burden behind!"],
                [CardId.AboundingGrace] = ["Grace rises with the dawn!", "What was lost returns to hand!"],
                [CardId.AnsweredPrayer] = ["Heaven heard you. Strike!", "Your prayers return as power!"],
                [CardId.ArkOfTheCovenant] = ["The Ark goes before us!", "Stand firm beneath His promise!"],
                [CardId.BatteringBlow] = ["Open the way!", "That guard will not hold!"],
                [CardId.BattleScars] = ["Those scars still shine!", "Every scar taught us courage!"],
                [CardId.BlessedOnslaught] = ["Five holy blows! Keep the rhythm!", "Blessed steel, again and again!"],
                [CardId.BloodPrice] = ["Careful. That strength costs dearly!", "A hard price. Spend it well!"],
                [CardId.Burn] = ["Hot hands! Let it fly!", "A little fire for them!"],
                [CardId.CarpeDiem] = ["This is our moment!", "Take the day by the reins!"],
                [CardId.Comeback] = ["They cannot keep you down!", "Rise again and answer!"],
                [CardId.Consecrate] = ["Make this ground holy!", "Let no darkness stand here!"],
                [CardId.Courageous] = ["That is the brave heart I know!", "Courage looks good on you!"],
                [CardId.CrimsonRite] = ["A solemn bargain. Stay true!", "Let the sacrifice mean something!"],
                [CardId.Crusade] = ["Forward with faithful hearts!", "Our cause carries us onward!"],
                [CardId.Curse] = ["Do not let it cling to you!", "We can outlast this darkness!"],
                [CardId.Hex] = ["Shake that hex loose!", "Do not let the mark take hold!"],
                [CardId.Dagger] = ["Quick and clean!", "A small blade finds its mark!"],
                [CardId.DeusVult] = ["Then let His will be done!", "Answer boldly!"],
                [CardId.DivineProtection] = ["You are sheltered!", "Grace stands between you and harm!"],
                [CardId.DowseWithHolyWater] = ["A blessed splash!", "Wash that wickedness away!"],
                [CardId.EmberHarvest] = ["Gather every glowing spark!", "The ashes still have work to do!"],
                [CardId.EvenTemper] = ["Keep your cool!", "Steady heart, steady hand!"],
                [CardId.Exaltation] = ["Lift your heart higher!", "Let your spirit rise!"],
                [CardId.Excavate] = ["There is something useful down there!", "Dig deep. We need an answer!"],
                [CardId.Fervor] = ["Let that fire carry you!", "Faith has found its feet!"],
                [CardId.ForgeStrike] = ["Hammer it bright!", "Straight from the forge!"],
                [CardId.FullForce] = ["Put everything into it!", "Nothing held back!"],
                [CardId.Fury] = ["Hold the reins on that fury!", "Strong, but still in control!"],
                [CardId.Graveward] = ["The grave gets nothing today!", "Keep watch over every soul!"],
                [CardId.HoldTheLine] = ["Not one step back!", "This line belongs to us!"],
                [CardId.Hammer] = ["Bring the hammer down!", "A sturdy answer!"],
                [CardId.HiddenKunai] = ["They never saw that coming!", "A secret edge, right on time!"],
                [CardId.Impale] = ["Straight through!", "That point found its purpose!"],
                [CardId.IncreaseFaith] = ["Faith makes room for miracles!", "Let your trust grow strong!"],
                [CardId.IronCovenant] = ["A promise made of iron!", "That vow will hold!"],
                [CardId.Kunai] = ["Swift as a little silver wing!", "There it goes!"],
                [CardId.Lacerate] = ["Leave them no easy footing!", "A sharp lesson!"],
                [CardId.LitanyOfWrath] = ["Let every word strike true!", "A fierce prayer for a fierce hour!"],
                [CardId.Mantlet] = ["Shelter behind it!", "A little wall at the right time!"],
                [CardId.MaleficRite] = ["Dark work. Keep your soul guarded!", "Use it, but do not trust it!"],
                [CardId.MarkOfAnathema] = ["Mark them for judgment!", "Let the curse cling to our foe!"],
                [CardId.OathGuard] = ["Your oath steadies this shield!", "Keep the pledge. Hold firm!"],
                [CardId.QuickWit] = ["A clever answer!", "Good thinking, quick as light!"],
                [CardId.RallyTheFaithful] = ["Hearts together, now!", "Call them back to courage!"],
                [CardId.RelentlessStrike] = ["Again, and do not falter!", "Keep the pressure true!"],
                [CardId.PierceThrough] = ["No armor is perfect!", "Find the opening!"],
                [CardId.PouchOfKunai] = ["You brought the whole pouch!", "Plenty of little surprises!"],
                [CardId.Purge] = ["Out with the poison!", "Cleanse it all away!"],
                [CardId.Ravage] = ["Leave their ranks in tatters!", "Break their wicked formation!"],
                [CardId.RazorStorm] = ["A storm with an edge!", "Duck after you throw those!"],
                [CardId.Reckoning] = ["The account comes due!", "Now answer for it!"],
                [CardId.Reap] = ["Gather what the battle sowed!", "The harvest is ours!"],
                [CardId.RecklessBarrage] = ["Wild and costly. Make it count!", "Throw caution. Keep the faith!"],
                [CardId.RenounceAndHone] = ["Cast it off and sharpen your purpose!", "Less burden, keener edge!"],
                [CardId.Retaliate] = ["Hurt them back!", "They struck first. Answer in kind!"],
                [CardId.Sacrifice] = ["Give only what you must!", "Let this gift bring hope!"],
                [CardId.SerpentCrush] = ["No room left to slither!", "Pin that serpent down!"],
                [CardId.Seize] = ["Take the opening!", "Ours now!"],
                [CardId.ShieldbearersVigil] = ["Your shield steadies every hand!", "Stand watch. We guard together!"],
                [CardId.ShieldOfFaith] = ["Faith before fear!", "That shield is more than steel!"],
                [CardId.Smite] = ["A bright little thunderbolt!", "Smite with purpose!"],
                [CardId.Stab] = ["Right where it hurts!", "A quick point well made!"],
                [CardId.SteadfastResolve] = ["Nothing shakes that heart!", "Plant your feet and mean it!"],
                [CardId.Stalwart] = ["Solid as the chapel doors!", "You will not be moved!"],
                [CardId.SteelPrayer] = ["Steel your heart in prayer!", "Courage rises with every word!"],
                [CardId.SteelTheSpirit] = ["Make your spirit stronger than steel!", "Let courage take an edge!"],
                [CardId.StokedAssault] = ["The furnace is roaring now!", "Strike while the coals are bright!"],
                [CardId.Strike] = ["A good honest strike!", "Simple and true!"],
                [CardId.SuddenThrust] = ["Quick, through the gap!", "A flash of steel!"],
                [CardId.StokeTheFurnace] = ["Feed the fire carefully!", "Keep those coals awake!"],
                [CardId.Sword] = ["Trust the blade you know!", "Steel forward!"],
                [CardId.SwordIntoShield] = ["Let that edge become your shelter!", "A keen blade can still guard the faithful!"],
                [CardId.TemperTheBlade] = ["Heat, hammer, patience!", "Make that edge worthy!"],
                [CardId.Tempest] = ["Ride the storm!", "Let the whole sky answer!"],
                [CardId.Thaw] = ["Warmth is winning!", "Shake off that frost!"],
                [CardId.UnburdenedStrike] = ["Light hands strike fast!", "Nothing holding you back now!"],
                [CardId.VanguardsPromise] = ["The vanguard keeps its word!", "First forward, last to flee!"],
                [CardId.Vindicate] = ["Let the truth strike back!", "Set the record right!"],
                [CardId.WardingPledge] = ["Your pledge turns wounds into wards!", "Scars become shelter while you vow!"],
                [CardId.Whirlwind] = ["Mind your footing!", "Steel in every direction!"],
                [CardId.ZealousVow] = ["A fierce vow. Keep it faithfully!", "Let devotion lend you strength!"],
            };

        private static readonly IReadOnlyDictionary<MedalId, string[]> MedalMessages =
            new Dictionary<MedalId, string[]>
            {
                [MedalId.StLuke] = ["Saint Luke, tend these wounds!", "A healer arrives right on time!"],
                [MedalId.StMichael] = ["Saint Michael, guard our flank!", "The archangel stands with us!"],
                [MedalId.StMonica] = ["Saint Monica, keep hope alive!", "Patient prayer bears fruit!"],
                [MedalId.StNicholas] = ["Saint Nicholas brings a timely gift!", "A generous hand helps again!"],
                [MedalId.StPeter] = ["Saint Peter, make us steadfast!", "The rock holds firm!"],
                [MedalId.StPaulMiki] = ["Saint Paul Miki, lend us courage!", "A fearless witness stands near!"],
                [MedalId.StLouieIX] = ["Saint Louis, lead with honor!", "A just king answers the call!"],
                [MedalId.StSebastian] = ["Saint Sebastian, keep us standing!", "Endure the volley!"],
                [MedalId.StFrancisDeSales] = ["Saint Francis, give us gentle strength!", "A kind word can still win battles!"],
                [MedalId.StGeorge] = ["Saint George, face the beast!", "The dragon slayer rides again!"],
                [MedalId.StHomobonus] = ["Saint Homobonus, bless honest work!", "Good work brings good aid!"],
                [MedalId.StIgnatius] = ["Saint Ignatius, sharpen our purpose!", "Discern the path, then charge!"],
                [MedalId.StClare] = ["Saint Clare, make the way clear!", "A clear light guides us!"],
                [MedalId.StElijah] = ["Saint Elijah, call down courage!", "A fiery prophet answers!"],
                [MedalId.StJoanOfArc] = ["Saint Joan, raise the banner!", "Stand brave beside the Maid!"],
                [MedalId.StJoseph] = ["Saint Joseph, shelter us through this blow!", "The guardian of the Holy Family stands firm!"],
                [MedalId.StJerome] = ["Saint Jerome, find the right words!", "Wisdom has entered the fray!"],
                [MedalId.StLonginus] = ["Saint Longinus, guide the spear!", "Let the point find truth!"],
                [MedalId.StBenedict] = ["Saint Benedict, bar the darkness!", "That holy shield holds fast!"],
                [MedalId.StSimonOfCyrene] = ["Saint Simon, help bear the weight!", "No burden is carried alone!"],
                [MedalId.StThomasAquinas] = ["Saint Thomas, light the mind!", "A clear thought cuts through confusion!"],
                [MedalId.StAugustine] = ["Saint Augustine, turn our hearts true!", "Restless hearts, find your strength!"],
                [MedalId.StAnthonyOfPadua] = ["Saint Anthony, find what we need!", "Nothing useful stays lost for long!"],
                [MedalId.StBartholomew] = ["Saint Bartholomew, make us bold!", "Steady us through the trial!"],
                [MedalId.StOlaf] = ["Saint Olaf, break the wicked line!", "A kingly blow turns the tide!"],
                [MedalId.StRita] = ["Saint Rita, help the impossible!", "No cause is lost yet!"],
                [MedalId.StChristopher] = ["Saint Christopher, carry us through!", "Strong shoulders, safe passage!"],
                [MedalId.StLawrence] = ["Saint Lawrence, keep us bright!", "Even the fire cannot dim us!"],
                [MedalId.StLazarus] = ["Saint Lazarus, rise once more!", "The grave does not get the last word!"],
                [MedalId.StAdrian] = ["Saint Adrian, risk it all!", "Courage spent can still buy victory!"],
            };

        private static readonly IReadOnlyDictionary<EnemyAttackId, string[]> EnemyAttackMessages =
            new Dictionary<EnemyAttackId, string[]>
            {
                [EnemyAttackId.PummelIntoSubmission] = ["That is a heavy flurry. Brace!", "Guard high. He means to overwhelm us!"],
                [EnemyAttackId.TreeStomp] = ["Big foot coming down!", "Move before the ground jumps!"],
                [EnemyAttackId.SlamTrunk] = ["That trunk is swinging wide!", "Duck the timber!"],
                [EnemyAttackId.FakeOut] = ["Wait for the real swing!", "Do not bite on the feint!"],
                [EnemyAttackId.Thud] = ["Short swing, heavy landing!", "That one will rattle us!"],
                [EnemyAttackId.BoneStrike] = ["A sharp bone is still a blade!", "Guard against that jagged strike!"],
                [EnemyAttackId.Sweep] = ["Low sweep. Lift your feet!", "It is coming across the ground!"],
                [EnemyAttackId.Calcify] = ["Do not let your joints stiffen!", "Keep moving through the stone curse!"],
                [EnemyAttackId.SkullCrusher] = ["Protect your head!", "That club wants a dreadful landing!"],
                [EnemyAttackId.PiercingShot] = ["A piercing arrow. Narrow guard!", "That shot means to punch through!"],
                [EnemyAttackId.WeatheringShot] = ["That arrow will wear us down!", "Save strength for what follows!"],
                [EnemyAttackId.QuickShot] = ["Arrow already flying!", "Quick guard, now!"],
                [EnemyAttackId.Snipe] = ["He has taken careful aim!", "Do not give that shot a clean line!"],
                [EnemyAttackId.Slice] = ["Blade from the side!", "A clean slice is coming!"],
                [EnemyAttackId.Dice] = ["Two cuts, close together!", "One blade was not enough for him!"],
                [EnemyAttackId.DuskFlick] = ["A little blade from the shadows!", "Watch his wrist!"],
                [EnemyAttackId.CloakedReaver] = ["The cloak hides a cruel edge!", "Track the blade, not the cloth!"],
                [EnemyAttackId.SilencingStab] = ["That stab means to steal our voice!", "Keep your prayer through the pain!"],
                [EnemyAttackId.SharpenBlade] = ["He is making the next cut worse!", "Do not give him time to hone it!"],
                [EnemyAttackId.ShadowStep] = ["He moved. Check the shadows!", "Behind us, perhaps!"],
                [EnemyAttackId.NightveilGuillotine] = ["That is the finishing stroke!", "Everything into this guard!"],
                [EnemyAttackId.RazorMaw] = ["Those teeth are all edges!", "Keep clear of that maw!"],
                [EnemyAttackId.ScorchingClaw] = ["Claws and flame together!", "Brace for a burning rake!"],
                [EnemyAttackId.InfernalExecution] = ["This is his cruelest blow!", "Stand firm against the executioner!"],
                [EnemyAttackId.Pounce] = ["Here comes the whole horde!", "They are leaping at once!"],
                [EnemyAttackId.TutorialHordeStrike] = ["A small rush. You can stop it!", "Meet them with a steady guard!"],
                [EnemyAttackId.TutorialHordeStrike3] = ["Three little terrors incoming!", "Count them, then block them!"],
                [EnemyAttackId.TutorialHordeStrike5] = ["Five are rushing us now!", "Keep calm through the crowd!"],
                [EnemyAttackId.TutorialHordeStrike6] = ["Six attackers. Hold together!", "A busy guard, but not impossible!"],
                [EnemyAttackId.TutorialHordeStrike7] = ["Seven sets of claws!", "This is the big lesson. Brace!"],
                [EnemyAttackId.TutorialHordeStrike8] = ["Eight are piling in!", "Make every bit of guard count!"],
                [EnemyAttackId.TutorialHordeStrike9] = ["Nine at once. Stay brave!", "The whole pack is coming!"],
                [EnemyAttackId.SandBlast] = ["Cover your eyes!", "A wall of sand is coming!"],
                [EnemyAttackId.SandStorm] = ["The whole sky is turning to sand!", "Stay close inside the storm!"],
                [EnemyAttackId.TutorialSandBlast] = ["Sand ahead. Guard and breathe!", "You know what to do. Eyes down!"],
                [EnemyAttackId.TutorialSandStorm] = ["A bigger storm this time!", "Hold your ground in the sand!"],
                [EnemyAttackId.SandPound] = ["Stone fist incoming!", "That punch carries half the desert!"],
                [EnemyAttackId.SandSlam] = ["Both hands are coming down!", "The golem means to flatten us!"],
                [EnemyAttackId.SuffocatingSilk] = ["Do not let that silk close in!", "Cut yourself breathing room!"],
                [EnemyAttackId.MandibleBreaker] = ["Those jaws can crack armor!", "Keep clear of the bite!"],
                [EnemyAttackId.Entomb] = ["The wrappings are reaching for us!", "Do not let them seal you in!"],
                [EnemyAttackId.Mummify] = ["More bandages. Keep moving!", "Tear free before they tighten!"],
                [EnemyAttackId.Leprosy] = ["A foul sickness rides that touch!", "Do not let the curse settle!"],
                [EnemyAttackId.VelvetFangs] = ["Soft steps, sharp fangs!", "Do not trust that gentle approach!"],
                [EnemyAttackId.SoulSiphon] = ["Hold tight to your spirit!", "She means to drink our strength!"],
                [EnemyAttackId.EnthrallingGaze] = ["Eyes away from hers!", "Do not listen to that gaze!"],
                [EnemyAttackId.CrushingAdoration] = ["That embrace is a trap!", "No hugs from the demon!"],
                [EnemyAttackId.TeasingNip] = ["A playful bite still bites!", "Do not let her toy with us!"],
                [EnemyAttackId.SawtoothRend] = ["Those teeth will tear on the way out!", "Brace for a ragged cut!"],
                [EnemyAttackId.DustStorm] = ["Something huge moves in that dust!", "Plant your feet before it hits!"],
                [EnemyAttackId.StrangeForce] = ["The air itself is twisting!", "I do not like that strange pull!"],
                [EnemyAttackId.IcyBlade] = ["Cold steel coming through!", "Guard before it freezes you!"],
                [EnemyAttackId.FrozenClaw] = ["Those claws carry winter!", "Keep the frost away from your hands!"],
                [EnemyAttackId.FrostEater] = ["It feeds on the cold around us!", "Do not let it swallow our warmth!"],
                [EnemyAttackId.GlacialStrike] = ["A glacier learned to punch!", "That icy blow is enormous!"],
                [EnemyAttackId.GlacialBlast] = ["A frozen wave is building!", "Shelter from the blast!"],
                [EnemyAttackId.Cinderbolt] = ["A hot bolt, straight at us!", "Knock that ember aside!"],
                [EnemyAttackId.InsidiousBolt] = ["That bolt carries something worse!", "Guard against the hidden sting!"],
                [EnemyAttackId.Rage] = ["Anger is making him reckless!", "Weather the rage, then answer!"],
                [EnemyAttackId.TrainingStrike] = ["A practice blow. Guard properly!", "Show me a sturdy defense!"],
                [EnemyAttackId.ShadowStrike] = ["A fist from the dark!", "Watch where the light ends!"],
                [EnemyAttackId.DissipatingDarkness] = ["The darkness is spreading thin and wide!", "Keep a light in your heart!"],
                [EnemyAttackId.SnuffOutTheLight] = ["It wants every light gone!", "Our flame stays lit!"],
                [EnemyAttackId.NightFall] = ["Night is dropping all at once!", "Stay close until the dark passes!"],
                [EnemyAttackId.FromTheShadows] = ["Something is lunging from behind!", "The shadows just moved!"],
                [EnemyAttackId.UmbraSlice] = ["A black edge cuts the air!", "Guard the unseen blade!"],
                [EnemyAttackId.TremorStrike] = ["The ground will carry that blow!", "Brace from your boots upward!"],
                [EnemyAttackId.StoneBarrage] = ["A whole quarry is flying at us!", "Cover up from the stones!"],
                [EnemyAttackId.EarthenWall] = ["It is hiding behind the earth!", "That wall will make it stubborn!"],
                [EnemyAttackId.HaveNoMercy] = ["It means to finish this cruelly!", "Give everything to this defense!"],
                [EnemyAttackId.WardenSeal] = ["Break the ward with your shield!", "Block with it to crack the seal!"],
				[EnemyAttackId.VenomLash] = ["Keep that venomous tongue away!", "One firm guard stops the poison!"],
				[EnemyAttackId.ToxicDeluge] = ["The whole spray is poison!", "Put enough shield into this one!"],
                [EnemyAttackId.WyvernStrike] = ["Talons diving from above!", "The wyvern is committed now!"],
                [EnemyAttackId.WyvernThreat] = ["It is gathering itself for worse!", "That warning is no bluff!"],
                [EnemyAttackId.FallenShepherdPhase1] = ["His crooked staff is raised!", "Do not follow that false shepherd!"],
                [EnemyAttackId.FallenShepherdCrooksScar] = ["The crook is sweeping for a scar!", "Keep clear of that hooked staff!"],
                [EnemyAttackId.FallenShepherdBreakFaith] = ["He wants doubt to do his work!", "Hold fast to what you know!"],
                [EnemyAttackId.FallenShepherdBloodletting] = ["A cruel cut is coming!", "Do not feed his wicked lesson!"],
                [EnemyAttackId.FallenShepherdCowTheFlock] = ["He means to frighten us small!", "Stand tall against his threat!"],
                [EnemyAttackId.FallenShepherdPhase2] = ["His false sermon is growing louder!", "The second trial begins. Stay true!"],
                [EnemyAttackId.FallenShepherdShepherdsVigil] = ["He is watching for every weakness!", "Do not give that vigil an opening!"],
                [EnemyAttackId.FallenShepherdHush] = ["He wants our prayers silent!", "Keep the truth in your heart!"],
                [EnemyAttackId.FallenShepherdPhase3] = ["This is his last, darkest stand!", "All our courage, right now!"],
                [EnemyAttackId.FallenShepherdPurgeTheHeretic] = ["He has named us for the fire!", "His judgment is false. Stand firm!"],
                [EnemyAttackId.FallenShepherdFearTheShepherd] = ["Fear is the only crook he has left!", "We bow to no false shepherd!"],
                [EnemyAttackId.FallenShepherdFinalSermon] = ["His final sermon ends here!", "Let truth have the last word!"],
                [EnemyAttackId.ChronoSlice] = ["That blade is cutting through time!", "Guard carefully. It will bury the first card!"],
                [EnemyAttackId.AeonWard] = ["The ice is hardening around him!", "Strike through that frozen ward!"],
            };

        public static string GetMessage(GuardianAmbientMessageType type) => Select($"ambient:{type}", AmbientMessages[type]);

        public static string GetCardMessage(CardId id) => Select($"card:{id}", GetCardMessages(id));

        public static string GetMedalMessage(MedalId id) => Select($"medal:{id}", GetMedalMessages(id));

        public static string GetEnemyAttackMessage(EnemyAttackId id) => Select($"attack:{id}", GetEnemyAttackMessages(id));

        internal static IReadOnlyList<string> GetCardMessages(CardId id) =>
            CardMessages.TryGetValue(id, out var messages) ? messages : ["A good play. Keep going!", "Well chosen. Stay ready!"];

        internal static IReadOnlyList<string> GetMedalMessages(MedalId id) =>
            MedalMessages.TryGetValue(id, out var messages) ? messages : ["A saint lends us aid!", "Help has arrived!"];

        internal static IReadOnlyList<string> GetEnemyAttackMessages(EnemyAttackId id) =>
            EnemyAttackMessages.TryGetValue(id, out var messages) ? messages : ["Something dangerous is coming!", "Stay close and guard well!"];

        internal static bool HasCardMessages(CardId id) => CardMessages.ContainsKey(id);

        internal static bool HasMedalMessages(MedalId id) => MedalMessages.ContainsKey(id);

        internal static bool HasEnemyAttackMessages(EnemyAttackId id) => EnemyAttackMessages.ContainsKey(id);

        private static string Select(string key, IReadOnlyList<string> messages)
        {
            if (messages.Count == 0) return "Stay ready!";
            int previous = LastSelections.TryGetValue(key, out var last) ? last : -1;
            int selected = messages.Count == 1 ? 0 : Random.Shared.Next(messages.Count - 1);
            if (selected >= previous) selected++;
            LastSelections[key] = selected;
            return messages[selected];
        }
    }
}
