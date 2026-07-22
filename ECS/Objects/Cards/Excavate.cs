using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Excavate : CardBase
    {
        private const int MillThreshold = 2;
        private const int DamageBonus = 3;
        private const int DamageBonusUpgrade = 5;
        private readonly List<string> CostUpgrade = ["Any", "Any"];

        public Excavate()
        {
            CardId = CardIds.Excavate.ToKey();
            Name = "Excavate";
            Target = "Enemy";
            Text = $"If you have milled {MillThreshold} or more cards this battle, this attack gains +{GetDamageBonus(IsUpgraded)} damage.";
            Cost = ["Black", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 9;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity("Enemy"),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                var battleState = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault()
                    ?.GetComponent<BattleStateInfo>();
                if (battleState?.BattleTracking != null &&
                    battleState.BattleTracking.TryGetValue(TrackingTypeEnum.CardsMilled.ToString(), out int milled) &&
                    milled >= MillThreshold)
                {
                    return GetDamageBonus(IsUpgraded);
                }

                return 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Cost = CostUpgrade;
                    Text = $"If you have milled {MillThreshold} or more cards this battle, this attack gains +{GetDamageBonus(IsUpgraded)} damage.";
                }
            };
        }

        private static int GetDamageBonus(bool isUpgraded) => isUpgraded ? DamageBonusUpgrade : DamageBonus;
    }
}
