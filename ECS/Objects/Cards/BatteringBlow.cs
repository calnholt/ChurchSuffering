using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class BatteringBlow : CardBase
    {
        private int CourageGain = 3;

        public BatteringBlow()
        {
            CardId = "battering_blow";
            Name = "Battering Blow";
            Target = "Enemy";
            Text = $"If no cards were discarded to play this, gain {CourageGain} courage.";
            Cost = ["White"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 6;
            Block = 3;
            IsFreeAction = false;
            EligibleWeapons = [EligibleWeapon.Hammer];

            OnPlay = (entityManager, card) =>
            {
                var cacheEntity = entityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault();
                var paymentCards = cacheEntity?.GetComponent<LastPaymentCache>()?.PaymentCards;
                if (paymentCards == null || paymentCards.Count == 0)
                {
                    EventManager.Publish(new ModifyCourageRequestEvent
                    {
                        Delta = CourageGain,
                        Type = ModifyCourageType.Gain
                    });
                }

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity("Enemy"),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };
            OnUpgrade = (entityManager, card) =>
            {
                IsFreeAction = true;
            };
        }
    }
}
