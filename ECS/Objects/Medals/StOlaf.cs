using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StOlaf : MedalBase, IReplacementEffectProvider
    {
        public StOlaf()
        {
            Id = "st_olaf";
            Name = "St. Olaf";
            Text = $"Each time frostbite triggers, the enemy takes {TooltipTextService.FrostbiteDamage} damage instead of you.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public bool TryReplace(ReplaceableEffectRequest request)
        {
            if (request == null) return false;
            if (request.Kind != ReplaceableEffectKind.FrostbiteThresholdDamage) return false;
            if (request.OriginalTarget != EntityManager.GetEntity("Player")) return false;

            request.IsHandled = true;
            request.HandlingMedalEntity = MedalEntity;
            request.HandlingMedalId = Id;

            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>()
                .FirstOrDefault(entity => entity.HasComponent<HP>());
            if (enemy != null)
            {
                request.Actions.Add(new ReplacementEffectAction
                {
                    Type = ReplacementEffectActionType.ModifyHp,
                    Source = request.OriginalTarget,
                    Target = enemy,
                    Delta = -TooltipTextService.FrostbiteDamage,
                    DamageType = ModifyTypeEnum.Effect
                });
            }

            return true;
        }
    }
}
