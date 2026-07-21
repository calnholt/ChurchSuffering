using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Vindicate : CardBase
    {
        private int CourageThreshold = 5;
        private int DamageBonus = 7;

        private int DamageUpgrade = 3;
        public Vindicate()
        {
            CardId = "vindicate";
            Name = "Vindicate";
            Target = "Enemy";
            Text = $"If you have {CourageThreshold} or more courage, this attack gains +{DamageBonus} damage and lose all courage.";
            Cost = ["Red", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                var damage = GetDerivedDamage(entityManager, card);
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = player, 
                    Target = enemy, 
                    Delta = -damage, 
                    AttackCard = card,
 
                    DamageType = ModifyTypeEnum.Attack 
                });
                EventManager.Publish(new SetCourageEvent { Amount = 0 });
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courage = player.GetComponent<Courage>().Amount;
                return courage >= CourageThreshold ? DamageBonus : 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
            };
        }
    }
}

