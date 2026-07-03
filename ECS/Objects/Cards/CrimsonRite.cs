using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

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
                int rawDamage = GetDerivedDamage(entityManager, card);
                var attackPreview = new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                };
                int damageDealt = AppliedPassivesService.GetPreviewAttackDamage(attackPreview, rawDamage, ReadOnly: true);

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -rawDamage,
                    AttackCard = card,

                    DamageType = ModifyTypeEnum.Attack
                });

                if (IsUpgraded)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Aegis,
                        Delta = damageDealt
                    });
                }
                else
                { 
                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = enemy,
                        Target = player,
                        Delta = damageDealt,
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
