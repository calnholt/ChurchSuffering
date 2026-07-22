using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Exaltation : CardBase
    {
        private int CourageCost = 3;
        private int CourageCostUpgrade = 1;

        public Exaltation()
        {
            CardId = CardIds.Exaltation.ToKey();
            Rarity = Rarity.Uncommon;
            Name = "Exaltation";
            Target = "Enemy";
            Text = $"As an additional cost, lose {CourageCost} courage.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 7;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = -CourageCost, Type = ModifyCourageType.Spent });
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
                Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage.";
            };
        }

        private int GetCourageCost(bool isUpgraded)
        {
            return isUpgraded ? CourageCost - CourageCostUpgrade : CourageCost;
        }
    }
}
