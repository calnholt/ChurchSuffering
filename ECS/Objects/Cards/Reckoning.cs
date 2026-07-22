using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Reckoning : CardBase
    {
        private int BlockBonusUpgrade = 1;
        private int DamageUpgrade = 1;
        private List<string> CostUpgrade = ["Red", "Any"];
        public Reckoning()
        {
            CardId = CardIds.Reckoning.ToKey();
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
