using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Retaliate : CardBase
    {
        private const int BurnAmount = 1;

        public Retaliate()
        {
            CardId = CardIds.Retaliate.ToKey();
            Name = "Retaliate";
            Target = "Enemy";
            Text = $"If you took damage during your action phase, the enemy gains {BurnAmount} burn.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });

                var battleState = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault()
                    ?.GetComponent<BattleStateInfo>();
                if (battleState?.PhaseTracking != null &&
                    battleState.PhaseTracking.TryGetValue(TrackingTypeEnum.DamageTaken.ToString(), out int taken) &&
                    taken > 0)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = enemy,
                        Type = AppliedPassiveType.Burn,
                        Delta = BurnAmount
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                IsFreeAction = true;
            };
        }
    }
}
