using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Fervor : CardBase
    {
        private int CourageThreshold = 5;
        private int DamageBonus = 3;
        private List<string> CostUpgrade = ["Any"];
        public Fervor()
        {
            CardId = "fervor";
            Rarity = Rarity.Common;
            Name = "Fervor";
            Target = "Enemy";
            Cost = ["Red"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 6;
            Block = 2;
            Type = CardType.Attack;
            Text = $"If you have {CourageThreshold}+ courage, this attack gains +{DamageBonus} damage.";

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

            GetConditionalDamage = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                return courage >= CourageThreshold ? DamageBonus : 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                Block += 1;
                Cost = CostUpgrade;
            };

        }
    }
}
