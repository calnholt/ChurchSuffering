using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Services;
using static ChurchSuffering.ECS.Components.CardData;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Reap : CardBase
    {
        private int DamageBonus = 2;
        private int CourageBonusUpgrade = 2;
        public Reap()
        {
            CardId = "reap";
            Name = "Reap";
            Target = "Player";
            Cost = ["Any","Any"];
            Text = $"If two red cards are discarded to play this, this gains +{DamageBonus} damage.";
            VisualEffectRecipe = PlayerAttackEffect();
            Block = 3;
            Damage = 8;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent { 
                  Source = entityManager.GetEntity("Player"), 
                  Target = entityManager.GetEntity("Enemy"), 
                  Delta = -GetDerivedDamage(entityManager, card), 
                  AttackCard = card,
 
                  DamageType = ModifyTypeEnum.Attack 
                });
                if (IsUpgraded)
                {
                    EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageBonusUpgrade, Type = ModifyCourageType.Gain });
                }
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                var cacheEntity = entityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault();
                var paymentCards = cacheEntity?.GetComponent<LastPaymentCache>()?.PaymentCards;
                var redCards = 0;
                if (paymentCards != null && paymentCards.Count > 0)
                {
                    foreach (var paymentCard in paymentCards)
                    {
                        if (CardColorQualificationService.QualifiesAs(paymentCard, CardColor.Red)) redCards++;
                    }
                }
                
                return redCards == 2 ? DamageBonus : 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"If two red cards are discarded to play this, this gains +{DamageBonus} damage and gain {CourageBonusUpgrade} courage.";
            };
        }
    }
}
