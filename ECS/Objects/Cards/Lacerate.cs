using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Lacerate : CardBase
    {
        private const int WoundedDamageBonus = 3;
        private const int WoundedGained = 1;
        private const int BlockUpgrade = 1;

        public Lacerate()
        {
            CardId = "lacerate";
            Name = "Lacerate";
            Target = "Enemy";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 2;
            Block = 2;
            Text = $"If this attack deals {Damage + WoundedDamageBonus} or more damage, the enemy gains {WoundedGained} wounded.";

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                int rawDamage = GetDerivedDamage(entityManager, card);
                int woundedDamageThreshold = Damage + WoundedDamageBonus;
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

                if (damageDealt >= woundedDamageThreshold)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = enemy,
                        Type = AppliedPassiveType.Wounded,
                        Delta = WoundedGained
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Block += BlockUpgrade;
                }
            };
        }
    }
}
