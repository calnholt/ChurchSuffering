using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Reckoning : CardBase
    {
        private int BlockBonusUpgrade = 1;
        private int DamageUpgrade = 1;
        private List<string> CostUpgrade = ["Red", "Any"];
        public Reckoning()
        {
            CardId = "reckoning";
            Rarity = Rarity.Starter;
            Name = "Reckoning";
            Target = "Enemy";
            Cost = ["Any", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 2;

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
            };

            OnUpgrade = (entityManager, card) =>
            {
                Block += BlockBonusUpgrade;
                Damage += DamageUpgrade;
                Cost = CostUpgrade;
            };
        }
    }
}
