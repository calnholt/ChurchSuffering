using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Stab : CardBase
    {
        private int CourageCost = 2;
        private int CourageCostUpgrade = 1;
        public Stab()
        {
            CardId = "stab";
            Rarity = Rarity.Common;
            Name = "Stab";
            Target = "Enemy";
            Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 5;
            Block = 2;

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
                if (courage < GetCourageCost(IsUpgraded)    )
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {CourageCost} courage!" });
            };
            OnUpgrade = (entityManager, card) =>
            {
                Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage.";
            };
        }
        private int GetCourageCost(bool isUpgraded)
        {
            return isUpgraded ? CourageCost - CourageCostUpgrade : CourageCost;
        }
    }
}

