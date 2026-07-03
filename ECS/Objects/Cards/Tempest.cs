using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
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

