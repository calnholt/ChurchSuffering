using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Strike : CardBase
    {
        private int Chance = 50;
        private int CourageGained = 2;
        public Strike()
        {
            CardId = "strike";
            Name = "Strike";
            Target = "Enemy";
            Text = $"{Chance}% chance to gain {CourageGained} courage.";
            VisualEffectRecipe = LightSlashEffect();
            Damage = 3;
            Block = 3;

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
                var chance = Chance;
                var random = Random.Shared.Next(0, 100);
                if (random <= chance)
                {
                    EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageGained, Type = ModifyCourageType.Gain });
                }
            };
            OnUpgrade = (entityManager, card) =>
            {
                IsFreeAction = true;
            };
        }
    }
}

