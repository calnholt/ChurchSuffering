using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class QuickWit : CardBase
    {
        private int CardDrawAmount = 1;
        private int CourageCost = 1;
        private int CourageCostUpgrade = 1;
        private int CardDrawAmountUpgrade = 1;
        public QuickWit()
        {
            CardId = "quick_wit";
            Name = "Quick Wit";
            Target = "Enemy";
            Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage. Resurrect {GetCardDrawAmount(IsUpgraded)}.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 2;
            Block = 3;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = -GetCourageCost(IsUpgraded), Type = ModifyCourageType.Spent });
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,

                    DamageType = ModifyTypeEnum.Attack
                });
                EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = GetCardDrawAmount(IsUpgraded) });
            };

            CanPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                return courage >= GetCourageCost(IsUpgraded);
            };

            OnCantPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                if (courage < GetCourageCost(IsUpgraded))
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {CourageCost} courage!" });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage. Resurrect {GetCardDrawAmount(IsUpgraded)}.";
            };

        }
        private int GetCourageCost(bool isUpgraded)
        {
            return isUpgraded ? CourageCost + CourageCostUpgrade : CourageCost;
        }
        private int GetCardDrawAmount(bool isUpgraded)
        {
            return isUpgraded ? CardDrawAmount + CardDrawAmountUpgrade : CardDrawAmount;
        }
    }
}