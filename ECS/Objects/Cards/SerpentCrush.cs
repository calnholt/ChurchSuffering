using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class SerpentCrush : CardBase
    {
        private int CourageCost = 2;
        private int ActionPointAmount = 1;
        private int DrawAmount = 1;

        private int CourageCostUpgrade = 1;
        public SerpentCrush()
        {
            CardId = "serpent_crush";
            Name = "Serpent Crush";
            Target = "Enemy";
            Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage. Gain {ActionPointAmount} action point and resurrect {DrawAmount}.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Type = CardType.Attack;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = entityManager.GetEntity("Player"), 
                    Target = entityManager.GetEntity("Enemy"), 
                    Delta = -GetDerivedDamage(entityManager, card), 
                    AttackCard = card,
 
                    DamageType = ModifyTypeEnum.Attack 
                });
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = -GetCourageCost(IsUpgraded), Type = ModifyCourageType.Spent });
                EventManager.Publish(new ModifyActionPointsEvent { Delta = ActionPointAmount });
                EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = DrawAmount });
            };

            CanPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                if (courage < GetCourageCost(IsUpgraded)) return false;
                return true;
            };
            OnCantPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                if (courage < GetCourageCost(IsUpgraded))
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {GetCourageCost(IsUpgraded)} courage!" });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage. Gain {ActionPointAmount} action point and resurrect {DrawAmount}.";
            };
        }

        private int GetCourageCost(bool isUpgraded)
        {
            return isUpgraded ? CourageCost - CourageCostUpgrade : CourageCost;
        }
    }
}
