using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class IronCovenant : CardBase
    {
        private int VigorGained = 1;

        private List<string> CostUpgrade = ["Red", "Black", "Any", "Any", "Any", "Any"];
        private int DamageUpgrade = 6;
        public IronCovenant()
        {
            CardId = CardIds.IronCovenant.ToKey();
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
