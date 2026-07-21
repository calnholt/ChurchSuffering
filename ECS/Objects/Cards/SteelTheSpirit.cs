using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class SteelTheSpirit : CardBase
    {
        private int CourageCost = 3;
        private int VigorGained = 2;
        private int CourageCostUpgrade = 1;
        public SteelTheSpirit()
        {
            CardId = "steel_the_spirit";
            Name = "Steel the Spirit";
            Target = "Player";
            Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage. Gain {VigorGained} vigor.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = -GetCourageCost(IsUpgraded), Type = ModifyCourageType.Spent });
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Vigor,
                    Delta = VigorGained
                });
            };

            CanPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                return (courageCmp?.Amount ?? 0) >= GetCourageCost(IsUpgraded);
            };

            OnCantPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                if ((courageCmp?.Amount ?? 0) < GetCourageCost(IsUpgraded))
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {GetCourageCost(IsUpgraded)} courage!" });
                }
            };
            OnUpgrade = (entityManager, card) =>
            {
                Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage. Gain {VigorGained} vigor.";
            };
        }
        private int GetCourageCost(bool isUpgraded)
        {
            return isUpgraded ? CourageCost - CourageCostUpgrade : CourageCost;
        }
    }
}
