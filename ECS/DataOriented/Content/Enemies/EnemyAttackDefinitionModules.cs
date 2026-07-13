#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

[assembly: DefinitionCatalog(typeof(EnemyAttackId))]

namespace Crusaders30XX.ECS.DataOriented.Content.Enemies;

[EnemyAttackDefinition(EnemyAttackId.PummelIntoSubmission, Handler = nameof(Handle))]
public static partial class PummelIntoSubmissionEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.PummelIntoSubmission];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TreeStomp, Handler = nameof(Handle))]
public static partial class TreeStompEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TreeStomp];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SlamTrunk, Handler = nameof(Handle))]
public static partial class SlamTrunkEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SlamTrunk];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FakeOut, Handler = nameof(Handle))]
public static partial class FakeOutEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FakeOut];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Thud, Handler = nameof(Handle))]
public static partial class ThudEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Thud];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.BoneStrike, Handler = nameof(Handle))]
public static partial class BoneStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.BoneStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Sweep, Handler = nameof(Handle))]
public static partial class SweepEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Sweep];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Calcify, Handler = nameof(Handle))]
public static partial class CalcifyEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Calcify];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SkullCrusher, Handler = nameof(Handle))]
public static partial class SkullCrusherEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SkullCrusher];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.PiercingShot, Handler = nameof(Handle))]
public static partial class PiercingShotEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.PiercingShot];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.WeatheringShot, Handler = nameof(Handle))]
public static partial class WeatheringShotEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.WeatheringShot];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.QuickShot, Handler = nameof(Handle))]
public static partial class QuickShotEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.QuickShot];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Snipe, Handler = nameof(Handle))]
public static partial class SnipeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Snipe];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Slice, Handler = nameof(Handle))]
public static partial class SliceEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Slice];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Dice, Handler = nameof(Handle))]
public static partial class DiceEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Dice];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.DuskFlick, Handler = nameof(Handle))]
public static partial class DuskFlickEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.DuskFlick];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.CloakedReaver, Handler = nameof(Handle))]
public static partial class CloakedReaverEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.CloakedReaver];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SilencingStab, Handler = nameof(Handle))]
public static partial class SilencingStabEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SilencingStab];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SharpenBlade, Handler = nameof(Handle))]
public static partial class SharpenBladeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SharpenBlade];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.ShadowStep, Handler = nameof(Handle))]
public static partial class ShadowStepEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.ShadowStep];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.NightveilGuillotine, Handler = nameof(Handle))]
public static partial class NightveilGuillotineEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.NightveilGuillotine];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.RazorMaw, Handler = nameof(Handle))]
public static partial class RazorMawEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.RazorMaw];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.ScorchingClaw, Handler = nameof(Handle))]
public static partial class ScorchingClawEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.ScorchingClaw];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.InfernalExecution, Handler = nameof(Handle))]
public static partial class InfernalExecutionEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.InfernalExecution];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Pounce, Handler = nameof(Handle))]
public static partial class PounceEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Pounce];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike, Handler = nameof(Handle))]
public static partial class TutorialHordeStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike3, Handler = nameof(Handle))]
public static partial class TutorialHordeStrike3EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike3];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike5, Handler = nameof(Handle))]
public static partial class TutorialHordeStrike5EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike5];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike6, Handler = nameof(Handle))]
public static partial class TutorialHordeStrike6EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike6];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike7, Handler = nameof(Handle))]
public static partial class TutorialHordeStrike7EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike7];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike8, Handler = nameof(Handle))]
public static partial class TutorialHordeStrike8EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike8];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialHordeStrike9, Handler = nameof(Handle))]
public static partial class TutorialHordeStrike9EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialHordeStrike9];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SandBlast, Handler = nameof(Handle))]
public static partial class SandBlastEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SandBlast];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SandStorm, Handler = nameof(Handle))]
public static partial class SandStormEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SandStorm];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialSandBlast, Handler = nameof(Handle))]
public static partial class TutorialSandBlastEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialSandBlast];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TutorialSandStorm, Handler = nameof(Handle))]
public static partial class TutorialSandStormEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TutorialSandStorm];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SandPound, Handler = nameof(Handle))]
public static partial class SandPoundEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SandPound];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SandSlam, Handler = nameof(Handle))]
public static partial class SandSlamEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SandSlam];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SuffocatingSilk, Handler = nameof(Handle))]
public static partial class SuffocatingSilkEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SuffocatingSilk];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.MandibleBreaker, Handler = nameof(Handle))]
public static partial class MandibleBreakerEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.MandibleBreaker];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Entomb, Handler = nameof(Handle))]
public static partial class EntombEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Entomb];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Mummify, Handler = nameof(Handle))]
public static partial class MummifyEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Mummify];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Leprosy, Handler = nameof(Handle))]
public static partial class LeprosyEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Leprosy];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.VelvetFangs, Handler = nameof(Handle))]
public static partial class VelvetFangsEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.VelvetFangs];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SoulSiphon, Handler = nameof(Handle))]
public static partial class SoulSiphonEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SoulSiphon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.EnthrallingGaze, Handler = nameof(Handle))]
public static partial class EnthrallingGazeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.EnthrallingGaze];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.CrushingAdoration, Handler = nameof(Handle))]
public static partial class CrushingAdorationEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.CrushingAdoration];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TeasingNip, Handler = nameof(Handle))]
public static partial class TeasingNipEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TeasingNip];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SawtoothRend, Handler = nameof(Handle))]
public static partial class SawtoothRendEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SawtoothRend];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.DustStorm, Handler = nameof(Handle))]
public static partial class DustStormEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.DustStorm];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.StrangeForce, Handler = nameof(Handle))]
public static partial class StrangeForceEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.StrangeForce];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.IcyBlade, Handler = nameof(Handle))]
public static partial class IcyBladeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.IcyBlade];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FrozenClaw, Handler = nameof(Handle))]
public static partial class FrozenClawEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FrozenClaw];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FrostEater, Handler = nameof(Handle))]
public static partial class FrostEaterEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FrostEater];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.GlacialStrike, Handler = nameof(Handle))]
public static partial class GlacialStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.GlacialStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.GlacialBlast, Handler = nameof(Handle))]
public static partial class GlacialBlastEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.GlacialBlast];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Cinderbolt, Handler = nameof(Handle))]
public static partial class CinderboltEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Cinderbolt];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.InsidiousBolt, Handler = nameof(Handle))]
public static partial class InsidiousBoltEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.InsidiousBolt];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Rage, Handler = nameof(Handle))]
public static partial class RageEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Rage];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TrainingStrike, Handler = nameof(Handle))]
public static partial class TrainingStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TrainingStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.ShadowStrike, Handler = nameof(Handle))]
public static partial class ShadowStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.ShadowStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.DissipatingDarkness, Handler = nameof(Handle))]
public static partial class DissipatingDarknessEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.DissipatingDarkness];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SnuffOutTheLight, Handler = nameof(Handle))]
public static partial class SnuffOutTheLightEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SnuffOutTheLight];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.NightFall, Handler = nameof(Handle))]
public static partial class NightFallEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.NightFall];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FromTheShadows, Handler = nameof(Handle))]
public static partial class FromTheShadowsEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FromTheShadows];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.UmbraSlice, Handler = nameof(Handle))]
public static partial class UmbraSliceEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.UmbraSlice];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.TremorStrike, Handler = nameof(Handle))]
public static partial class TremorStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.TremorStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.StoneBarrage, Handler = nameof(Handle))]
public static partial class StoneBarrageEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.StoneBarrage];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.EarthenWall, Handler = nameof(Handle))]
public static partial class EarthenWallEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.EarthenWall];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.HaveNoMercy, Handler = nameof(Handle))]
public static partial class HaveNoMercyEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.HaveNoMercy];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.Gaze, Handler = nameof(Handle))]
public static partial class GazeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.Gaze];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.BasiliskGlare, Handler = nameof(Handle))]
public static partial class BasiliskGlareEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.BasiliskGlare];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.SerpentStrike, Handler = nameof(Handle))]
public static partial class SerpentStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.SerpentStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.PetrifyingGaze, Handler = nameof(Handle))]
public static partial class PetrifyingGazeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.PetrifyingGaze];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.StoneSkin, Handler = nameof(Handle))]
public static partial class StoneSkinEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.StoneSkin];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.VipersCurse, Handler = nameof(Handle))]
public static partial class VipersCurseEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.VipersCurse];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.CrumblingStone, Handler = nameof(Handle))]
public static partial class CrumblingStoneEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.CrumblingStone];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.WyvernStrike, Handler = nameof(Handle))]
public static partial class WyvernStrikeEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.WyvernStrike];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.WyvernThreat, Handler = nameof(Handle))]
public static partial class WyvernThreatEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.WyvernThreat];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdPhase1, Handler = nameof(Handle))]
public static partial class FallenShepherdPhase1EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdPhase1];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdCrooksScar, Handler = nameof(Handle))]
public static partial class FallenShepherdCrooksScarEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdCrooksScar];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdBreakFaith, Handler = nameof(Handle))]
public static partial class FallenShepherdBreakFaithEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdBreakFaith];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdBloodletting, Handler = nameof(Handle))]
public static partial class FallenShepherdBloodlettingEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdBloodletting];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdCowTheFlock, Handler = nameof(Handle))]
public static partial class FallenShepherdCowTheFlockEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdCowTheFlock];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdPhase2, Handler = nameof(Handle))]
public static partial class FallenShepherdPhase2EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdPhase2];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdShepherdsVigil, Handler = nameof(Handle))]
public static partial class FallenShepherdShepherdsVigilEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdShepherdsVigil];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdHush, Handler = nameof(Handle))]
public static partial class FallenShepherdHushEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdHush];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdPhase3, Handler = nameof(Handle))]
public static partial class FallenShepherdPhase3EnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdPhase3];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdPurgeTheHeretic, Handler = nameof(Handle))]
public static partial class FallenShepherdPurgeTheHereticEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdPurgeTheHeretic];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdFearTheShepherd, Handler = nameof(Handle))]
public static partial class FallenShepherdFearTheShepherdEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdFearTheShepherd];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}

[EnemyAttackDefinition(EnemyAttackId.FallenShepherdFinalSermon, Handler = nameof(Handle))]
public static partial class FallenShepherdFinalSermonEnemyAttackDefinitionModule
{
    public static EnemyAttackDefinitionData Definition => EnemyAttackDefinitionDataTable.Values[(int)EnemyAttackId.FallenShepherdFinalSermon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleAttack(ref context, Definition);
}


