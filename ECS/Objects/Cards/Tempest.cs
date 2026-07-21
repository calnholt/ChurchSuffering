using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Tempest : CardBase
    {
        private int TemperanceAmount = 5;
        private List<string> CostUpgrade = ["Any"];
        public Tempest()
        {
            CardId = "tempest";
            Name = "Tempest";
            Target = "Enemy";
            Text = $"Gain {TemperanceAmount} temperance.";
            VisualEffectRecipe = PlayerAttackEffect();
            Cost = ["White"];
            Damage = 2;
            Block = 2;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = player, 
                    Target = enemy, 
                    Delta = -GetDerivedDamage(entityManager, card), 
                    AttackCard = card,
 
                    DamageType = ModifyTypeEnum.Attack 
                });
                EventManager.Publish(new ModifyTemperanceEvent { Delta = TemperanceAmount });
            };
            OnUpgrade = (entityManager, card) =>
            {
                Cost = CostUpgrade;
            };
        }
    }
}

