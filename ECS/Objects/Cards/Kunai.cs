using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Kunai : CardBase
    {
        private int RequiredAttackHits = 4;
        private int RequiredAttackHitsUpgrade = 1;
        public Kunai()
        {
            CardId = "kunai";
            Name = "Kunai";
            Target = "Enemy";
            Text = $"Wounds the enemy if you have dealt attack damage {RequiredAttackHits} times this action phase. Exhaust on play or at the end of your turn";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 1;
            ExhaustsOnEndTurn = true;
            CanAddToLoadout = false;
            IsToken = true;
            CardTooltip = "kunai";

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                var battleState = player?.GetComponent<BattleStateInfo>();
                if (battleState != null && battleState.PlayerActionPhaseAttackHits >= GetRequiredAttackHits(IsUpgraded))
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = enemy, Type = AppliedPassiveType.Wounded, Delta = +1 });
                }
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
                entityManager.AddComponent(card, new MarkedForExhaust { Owner = card });
            };

            OnUpgrade = (entityManager, card) =>
            {
                RequiredAttackHits = GetRequiredAttackHits(IsUpgraded);
                Text = $"Wounds the enemy if you have dealt attack damage {RequiredAttackHits} times this action phase. Exhaust on play or at the end of your turn";
            };
        }

        private int GetRequiredAttackHits(bool isUpgraded)
        {
            return isUpgraded ? RequiredAttackHits - RequiredAttackHitsUpgrade : RequiredAttackHits;
        }
    }
}
