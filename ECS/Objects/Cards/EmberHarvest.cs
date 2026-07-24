using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class EmberHarvest : CardBase
    {
        private const int MightGained = 2;
        private const int MightUpgrade = 1;
        private const int DamageUpgrade = 1;
        private const int BlockUpgrade = 1;

        public EmberHarvest()
        {
            CardId = CardIds.EmberHarvest.ToKey();
            Name = "Ember Harvest";
            Target = "Enemy";
            Cost = ["Any"];
            Text = $"If a scorched card was discarded to play this, gain {GetMightGained(IsUpgraded)} might.";
            VisualEffectRecipe = PlayerAttackEffect();
            IsFreeAction = true;
            Damage = 7;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = entityManager.GetEntity("Enemy"),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });

                var cacheEntity = entityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault();
                var paymentCards = cacheEntity?.GetComponent<LastPaymentCache>()?.PaymentCards;
                if (AnyScorchedPayment(paymentCards))
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Might,
                        Delta = GetMightGained(IsUpgraded)
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
                Block += BlockUpgrade;
                Text = $"If a scorched card was discarded to play this, gain {GetMightGained(IsUpgraded)} might.";
            };
        }

        private static bool AnyScorchedPayment(IEnumerable<Entity> paymentCards) =>
            paymentCards?.Any(p => p?.GetComponent<Scorched>() != null) == true;

        private int GetMightGained(bool isUpgraded) =>
            isUpgraded ? MightGained + MightUpgrade : MightGained;
    }
}
