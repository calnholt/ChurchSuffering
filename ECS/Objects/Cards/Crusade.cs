using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Crusade : CardBase
    {
        private int ActionPointGain = 1;
        private int MightGain = 2;
        private List<string> CostUpgrade = [];
        private int DamageUpgradeAmount = 2;
        public Crusade()
        {
            CardId = "crusade";
            Name = "Crusade";
            Target = "Enemy";
            Text = $"If this card is pledged when played, gain {ActionPointGain} action point and {MightGain} might.";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 5;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");

                var isPledged = card.GetComponent<Pledge>() != null;

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });

                if (isPledged)
                {
                    EventManager.Publish(new ModifyActionPointsEvent { Delta = ActionPointGain });
                    EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Might, Delta = MightGain });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Cost = CostUpgrade;
                Damage -= DamageUpgradeAmount;
                Text = $"If this card is pledged when played, gain {ActionPointGain}AP and {MightGain} might.";
            };
        }
    }
}
