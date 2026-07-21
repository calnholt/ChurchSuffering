using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Consecrate : CardBase
    {
        private int DamageBonus = 2;
        private int CourageGain = 1;
        private List<string> CostUpgrade = ["Any"];
        public Consecrate()
        {
            CardId = "consecrate";
            Name = "Consecrate";
            Target = "Enemy";
            Cost = ["Black"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 6;
            Block = 3;
            Text = $"If this card is pledged when played, it gains +{DamageBonus} damage and gain {CourageGain} courage.";

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");

                if (card.GetComponent<Pledge>() != null)
                {
                    EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageGain, Type = ModifyCourageType.Gain });
                }

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,

                    DamageType = ModifyTypeEnum.Attack
                });
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                return card.GetComponent<Pledge>() != null ? DamageBonus : 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                Cost = CostUpgrade;
            };
        }
    }
}
