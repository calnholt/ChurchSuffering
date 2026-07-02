using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class UnburdenedStrike : CardBase
    {
        private int DamageBonus = 3;
        private int DamageBonusUpgrade = 1;
        private int BlockUpgrade = 1;
        public UnburdenedStrike()
        {
            CardId = "unburdened_strike";
            Rarity = Rarity.Uncommon;
            Name = "Unburdened Strike";
            Target = "Enemy";
            Text = $"If no cards were discarded to play this, this gains +{GetDamageBonus(IsUpgraded)} damage.";
            Cost = ["White", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var bonusDamage = 0;
                var cacheEntity = entityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault();
                var paymentCards = cacheEntity?.GetComponent<LastPaymentCache>()?.PaymentCards;
                if (paymentCards == null || paymentCards.Count == 0)
                {
                    bonusDamage = GetDamageBonus(IsUpgraded);
                }

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity("Enemy"),
                    Delta = -GetDerivedDamage(entityManager, card) - bonusDamage,
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"If no cards were discarded to play this, this gains +{GetDamageBonus(IsUpgraded)} damage.";
                Block += BlockUpgrade;
            };

        }
        private int GetDamageBonus(bool isUpgraded)
        {
            return isUpgraded ? DamageBonus + DamageBonusUpgrade : DamageBonus;
        }
    }
}
