using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class IronCovenant : CardBase
    {
        private int VigorGained = 1;

        private List<string> CostUpgrade = ["Red", "Black", "Any", "Any", "Any", "Any"];
        private int DamageUpgrade = 6;
        public IronCovenant()
        {
            CardId = "iron_covenant";
            Name = "Iron Covenant";
            Target = "Enemy";
            Text = $"When this is pledged, gain {VigorGained} vigor.";
            Cost = ["Red", "Black", "Any", "Any", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 15;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            OnPledged = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Vigor,
                    Delta = VigorGained
                });
            };
            OnUpgrade = (entityManager, card) =>
            {
                Cost = CostUpgrade;
                Damage += DamageUpgrade;
            };
        }
    }
}
