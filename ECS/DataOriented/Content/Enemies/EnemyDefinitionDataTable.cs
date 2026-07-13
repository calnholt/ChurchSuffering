#nullable enable

using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.DataOriented.Content.Enemies;

internal static class EnemyDefinitionDataTable
{
    public static readonly EnemyDefinitionData[] Values =
    [
        E(EnemyId.Demon, "Demon", 29, EnemyPlanningProfile.Weighted, P(1, EnemyAttackId.RazorMaw, EnemyAttackId.ScorchingClaw, EnemyAttackId.InfernalExecution)),
        E(EnemyId.Horde, "Horde", 14, EnemyPlanningProfile.Tutorial, P(1, EnemyAttackId.Pounce, EnemyAttackId.TutorialHordeStrike, EnemyAttackId.TutorialHordeStrike3, EnemyAttackId.TutorialHordeStrike5, EnemyAttackId.TutorialHordeStrike6, EnemyAttackId.TutorialHordeStrike7, EnemyAttackId.TutorialHordeStrike8, EnemyAttackId.TutorialHordeStrike9), EnemyDefinitionFlags.TutorialOnly),
        E(EnemyId.Mummy, "Mummy", 26, EnemyPlanningProfile.Weighted, P(1, EnemyAttackId.Entomb, EnemyAttackId.Mummify, EnemyAttackId.Leprosy)),
        E(EnemyId.Ninja, "Ninja", 22, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.Slice, EnemyAttackId.Dice, EnemyAttackId.DuskFlick, EnemyAttackId.CloakedReaver, EnemyAttackId.SilencingStab, EnemyAttackId.SharpenBlade, EnemyAttackId.ShadowStep, EnemyAttackId.NightveilGuillotine, EnemyAttackId.HaveNoMercy)),
        E(EnemyId.Ogre, "Ogre", 31, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.PummelIntoSubmission, EnemyAttackId.TreeStomp, EnemyAttackId.SlamTrunk, EnemyAttackId.FakeOut, EnemyAttackId.Thud, EnemyAttackId.HaveNoMercy)),
        E(EnemyId.SandCorpse, "Sand Corpse", 16, EnemyPlanningProfile.RandomPool, P(1, EnemyAttackId.SandBlast, EnemyAttackId.SandStorm, EnemyAttackId.TutorialSandBlast, EnemyAttackId.TutorialSandStorm), EnemyDefinitionFlags.TutorialOnly),
        E(EnemyId.SandGolem, "Sand Golem", 30, EnemyPlanningProfile.Alternating, P(1, EnemyAttackId.SandPound, EnemyAttackId.SandSlam)),
        E(EnemyId.Skeleton, "Skeleton", 26, EnemyPlanningProfile.Weighted, P(1, EnemyAttackId.BoneStrike, EnemyAttackId.Sweep, EnemyAttackId.Calcify, EnemyAttackId.SkullCrusher, EnemyAttackId.HaveNoMercy)),
        E(EnemyId.SkeletalArcher, "Skeletal Archer", 23, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.PiercingShot, EnemyAttackId.WeatheringShot, EnemyAttackId.QuickShot, EnemyAttackId.Snipe)),
        E(EnemyId.Spider, "Spider", 28, EnemyPlanningProfile.Weighted, P(1, EnemyAttackId.SuffocatingSilk, EnemyAttackId.MandibleBreaker)),
        E(EnemyId.Succubus, "Succubus", 26, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.VelvetFangs, EnemyAttackId.SoulSiphon, EnemyAttackId.EnthrallingGaze, EnemyAttackId.CrushingAdoration, EnemyAttackId.TeasingNip), startingBelowMax: 3),
        E(EnemyId.Thornreaver, "Thornreaver", 34, EnemyPlanningProfile.Fixed, P(1, EnemyAttackId.SawtoothRend)),
        E(EnemyId.DustWuurm, "Dust Wuurm", 31, EnemyPlanningProfile.Fixed, P(1, EnemyAttackId.DustStorm)),
        E(EnemyId.Sorcerer, "Sorcerer", 25, EnemyPlanningProfile.Fixed, P(1, EnemyAttackId.StrangeForce)),
        E(EnemyId.IceDemon, "Ice Demon", 33, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.IcyBlade, EnemyAttackId.FrozenClaw, EnemyAttackId.FrostEater)),
        E(EnemyId.GlacialGuardian, "Glacial Guardian", 24, EnemyPlanningProfile.RandomPool, P(1, EnemyAttackId.GlacialStrike, EnemyAttackId.GlacialBlast)),
        E(EnemyId.CinderboltDemon, "Cinderbolt Demon", 30, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.Cinderbolt, EnemyAttackId.InsidiousBolt)),
        E(EnemyId.FireSkeleton, "Fire Skeleton", 20, EnemyPlanningProfile.Weighted, P(1, EnemyAttackId.BoneStrike, EnemyAttackId.Sweep, EnemyAttackId.Calcify, EnemyAttackId.SkullCrusher, EnemyAttackId.HaveNoMercy)),
        E(EnemyId.Berserker, "Berserker", 31, EnemyPlanningProfile.Fixed, P(1, EnemyAttackId.Rage)),
        E(EnemyId.Shadow, "Shadow", 42, EnemyPlanningProfile.Alternating, P(1, EnemyAttackId.ShadowStrike, EnemyAttackId.DissipatingDarkness, EnemyAttackId.SnuffOutTheLight, EnemyAttackId.NightFall, EnemyAttackId.FromTheShadows, EnemyAttackId.UmbraSlice)),
        E(EnemyId.EarthDemon, "Earth Demon", 32, EnemyPlanningProfile.RandomPool, P(1, EnemyAttackId.TremorStrike, EnemyAttackId.StoneBarrage, EnemyAttackId.EarthenWall)),
        E(EnemyId.Medusa, "Medusa", 37, EnemyPlanningProfile.Stateful, P(1, EnemyAttackId.Gaze, EnemyAttackId.BasiliskGlare, EnemyAttackId.SerpentStrike, EnemyAttackId.PetrifyingGaze, EnemyAttackId.StoneSkin, EnemyAttackId.VipersCurse, EnemyAttackId.CrumblingStone)),
        E(EnemyId.Wyvern, "Wyvern", 28, EnemyPlanningProfile.Alternating, P(1, EnemyAttackId.WyvernStrike, EnemyAttackId.WyvernThreat)),
        E(EnemyId.FallenShepherd, "Fallen Shepherd", 29, EnemyPlanningProfile.PhaseBoss, P(1, EnemyAttackId.FallenShepherdPhase1, EnemyAttackId.FallenShepherdCrooksScar, EnemyAttackId.FallenShepherdBreakFaith, EnemyAttackId.FallenShepherdBloodletting, EnemyAttackId.FallenShepherdCowTheFlock), EnemyDefinitionFlags.Boss, P(2, EnemyAttackId.FallenShepherdPhase2, EnemyAttackId.FallenShepherdShepherdsVigil, EnemyAttackId.FallenShepherdHush, EnemyAttackId.FallenShepherdCrooksScar, EnemyAttackId.FallenShepherdCowTheFlock), P(3, EnemyAttackId.FallenShepherdPhase3, EnemyAttackId.FallenShepherdPurgeTheHeretic, EnemyAttackId.FallenShepherdFearTheShepherd, EnemyAttackId.FallenShepherdFinalSermon)),
        E(EnemyId.TrainingDemon, "Training Demon", 26, EnemyPlanningProfile.Fixed, P(1, EnemyAttackId.TrainingStrike)),
    ];

    private static EnemyDefinitionData E(
        EnemyId id,
        string name,
        int health,
        EnemyPlanningProfile planning,
        EnemyPhaseDefinition phase,
        EnemyDefinitionFlags flags = EnemyDefinitionFlags.None,
        EnemyPhaseDefinition? phase2 = null,
        EnemyPhaseDefinition? phase3 = null,
        int startingBelowMax = 0)
    {
        EnemyPhaseDefinition[] phases = phase3 is not null
            ? [phase, phase2!, phase3]
            : phase2 is not null ? [phase, phase2] : [phase];
        return new EnemyDefinitionData(id, name, health, startingBelowMax, flags, planning, phases);
    }

    private static EnemyPhaseDefinition P(int phase, params EnemyAttackId[] arsenal) => new(phase, arsenal);
}
