using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class CrimsonRite : CardBase
    {
        private List<string> CostUpgrade = ["Any, Any"];
        public CrimsonRite()
        {
            CardId = "crimson_rite";
            Name = "Crimson Rite";
            Target = "Enemy";
            Cost = ["Black", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Block = 3;
            Text = "Heal X HP where X is the damage dealt from this attack.";

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                int damage = GetDerivedDamage(entityManager, card);

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -damage,
                    AttackCard = card,

                    DamageType = ModifyTypeEnum.Attack
                });

                if (IsUpgraded)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Aegis,
                        Delta = 1
                    });
                }
                else
                { 
                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = enemy,
                        Target = player,
                        Delta = damage,
                        DamageType = ModifyTypeEnum.Heal
                    });
                }
                
            };

            OnUpgrade = (entityManager, card) =>
            {
                Cost = CostUpgrade;
                Text = "Gain X aegis where X is the damage dealt from this attack.";
            };
        }
    }
}
