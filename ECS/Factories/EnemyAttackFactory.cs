using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Factories
{
    public static class EnemyAttackFactory
    {
        private static readonly IReadOnlyDictionary<EnemyAttackId, Func<EnemyAttackBase>> AttackConstructors =
            new Dictionary<EnemyAttackId, Func<EnemyAttackBase>>
            {
                { EnemyAttackId.PummelIntoSubmission, () => new PummelIntoSubmission() },
                { EnemyAttackId.TreeStomp, () => new TreeStomp() },
                { EnemyAttackId.SlamTrunk, () => new SlamTrunk() },
                { EnemyAttackId.FakeOut, () => new FakeOut() },
                { EnemyAttackId.Thud, () => new Thud() },
                { EnemyAttackId.BoneStrike, () => new BoneStrike() },
                { EnemyAttackId.BurialStrike, () => new BurialStrike() },
                { EnemyAttackId.SearingStrike, () => new SearingStrike() },
                { EnemyAttackId.RimeStrike, () => new RimeStrike() },
                { EnemyAttackId.DreadStrike, () => new DreadStrike() },
                { EnemyAttackId.Sweep, () => new Sweep() },
                { EnemyAttackId.Calcify, () => new Calcify() },
                { EnemyAttackId.SkullCrusher, () => new SkullCrusher() },
                { EnemyAttackId.PiercingShot, () => new PiercingShot() },
                { EnemyAttackId.WeatheringShot, () => new WeatheringShot() },
                { EnemyAttackId.QuickShot, () => new QuickShot() },
                { EnemyAttackId.Snipe, () => new Snipe() },
                { EnemyAttackId.Slice, () => new Slice() },
                { EnemyAttackId.Dice, () => new Dice() },
                { EnemyAttackId.DuskFlick, () => new DuskFlick() },
                { EnemyAttackId.CloakedReaver, () => new CloakedReaver() },
                { EnemyAttackId.SilencingStab, () => new SilencingStab() },
                { EnemyAttackId.SharpenBlade, () => new SharpenBlade() },
                { EnemyAttackId.ShadowStep, () => new ShadowStep() },
                { EnemyAttackId.NightveilGuillotine, () => new NightveilGuillotine() },
                { EnemyAttackId.RazorMaw, () => new RazorMaw() },
                { EnemyAttackId.ScorchingClaw, () => new ScorchingClaw() },
                { EnemyAttackId.InfernalExecution, () => new InfernalExecution() },
                { EnemyAttackId.Pounce, () => new Pounce() },
                { EnemyAttackId.TutorialHordeStrike, () => new TutorialHordeStrike() },
                { EnemyAttackId.TutorialHordeStrike3, () => new TutorialHordeStrike3() },
                { EnemyAttackId.TutorialHordeStrike5, () => new TutorialHordeStrike5() },
                { EnemyAttackId.TutorialHordeStrike6, () => new TutorialHordeStrike6() },
                { EnemyAttackId.TutorialHordeStrike7, () => new TutorialHordeStrike7() },
                { EnemyAttackId.TutorialHordeStrike8, () => new TutorialHordeStrike8() },
                { EnemyAttackId.TutorialHordeStrike9, () => new TutorialHordeStrike9() },
                { EnemyAttackId.SandBlast, () => new SandBlast() },
                { EnemyAttackId.SandStorm, () => new SandStorm() },
                { EnemyAttackId.TutorialSandBlast, () => new TutorialSandBlast() },
                { EnemyAttackId.TutorialSandStorm, () => new TutorialSandStorm() },
                { EnemyAttackId.SandPound, () => new SandPound() },
                { EnemyAttackId.SandSlam, () => new SandSlam() },
                { EnemyAttackId.SuffocatingSilk, () => new SuffocatingSilk() },
                { EnemyAttackId.MandibleBreaker, () => new MandibleBreaker() },
                { EnemyAttackId.Entomb, () => new Entomb() },
                { EnemyAttackId.Mummify, () => new Mummify() },
                { EnemyAttackId.Leprosy, () => new Leprosy() },
                { EnemyAttackId.VelvetFangs, () => new VelvetFangs() },
                { EnemyAttackId.SoulSiphon, () => new SoulSiphon() },
                { EnemyAttackId.EnthrallingGaze, () => new EnthrallingGaze() },
                { EnemyAttackId.CrushingAdoration, () => new CrushingAdoration() },
                { EnemyAttackId.TeasingNip, () => new TeasingNip() },
                { EnemyAttackId.SawtoothRend, () => new SawtoothRend() },
                { EnemyAttackId.DustStorm, () => new DustStorm() },
                { EnemyAttackId.StrangeForce, () => new StrangeForce() },
                { EnemyAttackId.IcyBlade, () => new IcyBlade() },
                { EnemyAttackId.FrozenClaw, () => new FrozenClaw() },
                { EnemyAttackId.FrostEater, () => new FrostEater() },
                { EnemyAttackId.GlacialStrike, () => new GlacialStrike() },
                { EnemyAttackId.GlacialBlast, () => new GlacialBlast() },
                { EnemyAttackId.Cinderbolt, () => new Cinderbolt() },
                { EnemyAttackId.InsidiousBolt, () => new InsidiousBolt() },
                { EnemyAttackId.Rage, () => new Rage() },
                { EnemyAttackId.TrainingStrike, () => new TrainingStrike() },
                { EnemyAttackId.ShadowStrike, () => new ShadowStrike() },
                { EnemyAttackId.DissipatingDarkness, () => new EncroachingDarkness() },
                { EnemyAttackId.SnuffOutTheLight, () => new SnuffOutTheLight() },
                { EnemyAttackId.NightFall, () => new NightFall() },
                { EnemyAttackId.FromTheShadows, () => new FromTheShadows() },
                { EnemyAttackId.UmbraSlice, () => new UmbraSlice() },
                { EnemyAttackId.TremorStrike, () => new TremorStrike() },
                { EnemyAttackId.StoneBarrage, () => new StoneBarrage() },
                { EnemyAttackId.EarthenWall, () => new EarthenWall() },
                { EnemyAttackId.HaveNoMercy, () => new HaveNoMercy() },
                { EnemyAttackId.WardenSeal, () => new WardenSeal() },
				{ EnemyAttackId.VenomLash, () => new VenomLash() },
				{ EnemyAttackId.ToxicDeluge, () => new ToxicDeluge() },
                { EnemyAttackId.WyvernStrike, () => new WyvernStrike() },
                { EnemyAttackId.WyvernThreat, () => new WyvernThreat() },
                { EnemyAttackId.FallenShepherdPhase1, () => new FallenShepherdPhase1() },
                { EnemyAttackId.FallenShepherdCrooksScar, () => new FallenShepherdCrooksScar() },
                { EnemyAttackId.FallenShepherdBreakFaith, () => new FallenShepherdBreakFaith() },
                { EnemyAttackId.FallenShepherdBloodletting, () => new FallenShepherdBloodletting() },
                { EnemyAttackId.FallenShepherdCowTheFlock, () => new FallenShepherdCowTheFlock() },
                { EnemyAttackId.FallenShepherdPhase2, () => new FallenShepherdPhase2() },
                { EnemyAttackId.FallenShepherdShepherdsVigil, () => new FallenShepherdShepherdsVigil() },
                { EnemyAttackId.FallenShepherdHush, () => new FallenShepherdHush() },
                { EnemyAttackId.FallenShepherdPhase3, () => new FallenShepherdPhase3() },
                { EnemyAttackId.FallenShepherdPurgeTheHeretic, () => new FallenShepherdPurgeTheHeretic() },
                { EnemyAttackId.FallenShepherdFearTheShepherd, () => new FallenShepherdFearTheShepherd() },
                { EnemyAttackId.FallenShepherdFinalSermon, () => new FallenShepherdFinalSermon() },
                { EnemyAttackId.WritOfMalice, () => new WritOfMalice() },
                { EnemyAttackId.ChronoSlice, () => new ChronoSlice() },
                { EnemyAttackId.AeonWard, () => new AeonWard() },
            };

        public static EnemyAttackBase Create(EnemyAttackId attackId)
        {
            return AttackConstructors.TryGetValue(attackId, out var create)
                ? create()
                : null;
        }

        public static EnemyAttackBase Create(string attackId)
        {
            return GameIdExtensions.TryParseEnemyAttackId(attackId, out var parsed)
                ? Create(parsed)
                : null;
        }

        public static Dictionary<EnemyAttackId, EnemyAttackBase> GetAllAttacks()
        {
            return AttackConstructors.ToDictionary(
                entry => entry.Key,
                entry => entry.Value());
        }

        public static bool IsRegistered(EnemyAttackId attackId) => AttackConstructors.ContainsKey(attackId);
    }
}
