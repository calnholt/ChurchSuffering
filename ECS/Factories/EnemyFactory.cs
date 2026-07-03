using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Factories
{
    public static class EnemyFactory
    {
        private static readonly IReadOnlyDictionary<EnemyId, Func<EnemyBase>> EnemyConstructors =
            new Dictionary<EnemyId, Func<EnemyBase>>
            {
                { EnemyId.Demon, () => new Demon() },
                { EnemyId.Gleeber, () => new Gleeber() },
                { EnemyId.Mummy, () => new Mummy() },
                { EnemyId.Ninja, () => new Ninja() },
                { EnemyId.Ogre, () => new Ogre() },
                { EnemyId.SandCorpse, () => new SandCorpse() },
                { EnemyId.SandGolem, () => new SandGolem() },
                { EnemyId.Skeleton, () => new Skeleton() },
                { EnemyId.SkeletalArcher, () => new SkeletalArcher() },
                { EnemyId.Spider, () => new Spider() },
                { EnemyId.Succubus, () => new Succubus() },
                { EnemyId.Thornreaver, () => new Thornreaver() },
                { EnemyId.DustWuurm, () => new DustWuurm() },
                { EnemyId.Sorcerer, () => new Sorcerer() },
                { EnemyId.IceDemon, () => new IceDemon() },
                { EnemyId.GlacialGuardian, () => new GlacialGuardian() },
                { EnemyId.CinderboltDemon, () => new CinderboltDemon() },
                { EnemyId.FireSkeleton, () => new FireSkeleton() },
                { EnemyId.Berserker, () => new Berserker() },
                { EnemyId.Shadow, () => new Shadow() },
                { EnemyId.EarthDemon, () => new EarthDemon() },
                { EnemyId.Medusa, () => new Medusa() },
                { EnemyId.Wyvern, () => new Wyvern() },
                { EnemyId.FallenShepherd, () => new FallenShepherd() },
                { EnemyId.TrainingDemon, () => new TrainingDemon() },
            };

        public static EnemyBase Create(EnemyId enemyId)
        {
            return EnemyConstructors.TryGetValue(enemyId, out var create)
                ? create()
                : null;
        }

        public static EnemyBase Create(string enemyId)
        {
            return GameIdExtensions.TryParseEnemyId(enemyId, out var parsed)
                ? Create(parsed)
                : null;
        }

        public static bool IsRegistered(EnemyId enemyId) => EnemyConstructors.ContainsKey(enemyId);

        public static bool IsRegistered(string enemyId)
        {
            return GameIdExtensions.TryParseEnemyId(enemyId, out var parsed) && IsRegistered(parsed);
        }

        public static Dictionary<EnemyId, EnemyBase> GetAllEnemies()
        {
            return EnemyConstructors.ToDictionary(
                entry => entry.Key,
                entry => entry.Value());
        }
    }
}
