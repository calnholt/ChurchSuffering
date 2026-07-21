using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Sword : CardBase
    {
        private int CourageGained = 1;
        public Sword()
        {
            CardId = "sword";
            Name = "Sword";
            Target = "Enemy";
            Text = $"Gain {CourageGained} courage.";
            Cost = ["Black", "Any"];
            VisualEffectRecipe = LightSlashEffect();
            Damage = 5;
            IsWeapon = true;

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
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = +CourageGained, Type = ModifyCourageType.Gain });
            };
        }
    }
}

