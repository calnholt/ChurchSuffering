using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class PierceThrough : CardBase
    {
        public PierceThrough()
        {
            CardId = "pierce_through";
            Name = "Pierce Through";
            Target = "Enemy";
            Cost = ["Any", "Any"];
            Text = "Remove all guard from the enemy.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");

                EventManager.Publish(new RemovePassive { Owner = enemy, Type = AppliedPassiveType.Guard });
                if (IsUpgraded)
                {
                    EventManager.Publish(new RemovePassive { Owner = enemy, Type = AppliedPassiveType.Armor });
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

            OnUpgrade = (entityManager, card) =>
            {
                Text = "Remove all guard and armor from the enemy.";
            };
        }
    }
}
