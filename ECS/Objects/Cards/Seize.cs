using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Seize : CardBase
    {
        private int DamageBonus = 2;

        public Seize()
        {
            CardId = "seize";
            Rarity = Rarity.Common;
            Name = "Seize";
            Target = "Enemy";
            Damage = 2;
            Block = 3;
            Text = $"If you have lost courage during this action phase, this gains +{DamageBonus} damage.";
            VisualEffectRecipe = PlayerAttackEffect();

            OnPlay = (entityManager, card) =>
            {
                var damage = GetDerivedDamage(entityManager, card);
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = entityManager.GetEntity("Player"), 
                    Target = entityManager.GetEntity("Enemy"), 
                    Delta = -damage, 
                    AttackCard = card,
 
                    DamageType = ModifyTypeEnum.Attack 
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                IsFreeAction = true;
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                var phase = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
                if (phase?.Sub != SubPhase.Action) return 0;

                var battleState = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault()
                    ?.GetComponent<BattleStateInfo>();
                if (battleState?.PhaseTracking != null &&
                    battleState.PhaseTracking.TryGetValue(TrackingTypeEnum.CourageLost.ToString(), out int lost) &&
                    lost > 0)
                    return DamageBonus;
                return 0;
            };
        }
    }
}
