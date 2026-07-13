#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

[assembly: DefinitionCatalog(typeof(EnemyId))]

namespace Crusaders30XX.ECS.DataOriented.Content.Enemies;

[EnemyDefinition(EnemyId.Demon, Handler = nameof(Handle))]
public static partial class DemonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Demon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Horde, Handler = nameof(Handle))]
public static partial class HordeEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Horde];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Mummy, Handler = nameof(Handle))]
public static partial class MummyEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Mummy];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Ninja, Handler = nameof(Handle))]
public static partial class NinjaEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Ninja];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Ogre, Handler = nameof(Handle))]
public static partial class OgreEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Ogre];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.SandCorpse, Handler = nameof(Handle))]
public static partial class SandCorpseEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.SandCorpse];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.SandGolem, Handler = nameof(Handle))]
public static partial class SandGolemEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.SandGolem];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Skeleton, Handler = nameof(Handle))]
public static partial class SkeletonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Skeleton];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.SkeletalArcher, Handler = nameof(Handle))]
public static partial class SkeletalArcherEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.SkeletalArcher];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Spider, Handler = nameof(Handle))]
public static partial class SpiderEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Spider];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Succubus, Handler = nameof(Handle))]
public static partial class SuccubusEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Succubus];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Thornreaver, Handler = nameof(Handle))]
public static partial class ThornreaverEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Thornreaver];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.DustWuurm, Handler = nameof(Handle))]
public static partial class DustWuurmEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.DustWuurm];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Sorcerer, Handler = nameof(Handle))]
public static partial class SorcererEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Sorcerer];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.IceDemon, Handler = nameof(Handle))]
public static partial class IceDemonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.IceDemon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.GlacialGuardian, Handler = nameof(Handle))]
public static partial class GlacialGuardianEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.GlacialGuardian];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.CinderboltDemon, Handler = nameof(Handle))]
public static partial class CinderboltDemonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.CinderboltDemon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.FireSkeleton, Handler = nameof(Handle))]
public static partial class FireSkeletonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.FireSkeleton];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Berserker, Handler = nameof(Handle))]
public static partial class BerserkerEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Berserker];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Shadow, Handler = nameof(Handle))]
public static partial class ShadowEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Shadow];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.EarthDemon, Handler = nameof(Handle))]
public static partial class EarthDemonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.EarthDemon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Medusa, Handler = nameof(Handle))]
public static partial class MedusaEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Medusa];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.Wyvern, Handler = nameof(Handle))]
public static partial class WyvernEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.Wyvern];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.FallenShepherd, Handler = nameof(Handle))]
public static partial class FallenShepherdEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.FallenShepherd];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}

[EnemyDefinition(EnemyId.TrainingDemon, Handler = nameof(Handle))]
public static partial class TrainingDemonEnemyDefinitionModule
{
    public static EnemyDefinitionData Definition => EnemyDefinitionDataTable.Values[(int)EnemyId.TrainingDemon];

    public static void Handle(ref EnemyHandlerContext context) => EnemyContentBehavior.HandleEnemy(ref context, Definition);
}


