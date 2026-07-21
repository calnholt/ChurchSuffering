using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Impale : CardBase
    {
        private int CourageCost = 3;

        private int DamageUpgrade = 1;
        public Impale()
        {
            CardId = "impale";
            Name = "Impale";
            Target = "Enemy";
            Text = $"As an additional cost, lose {CourageCost} courage.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 6;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = -CourageCost, Type = ModifyCourageType.Spent });
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity(Target),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,

                    DamageType = ModifyTypeEnum.Attack
                });
            };

            CanPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                if (courage < CourageCost) return false;
                return true;
            };
            OnCantPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var courageCmp = player?.GetComponent<Courage>();
                int courage = courageCmp?.Amount ?? 0;
                if (courage < CourageCost)
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {CourageCost} courage!" });
            };
            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
            };
        }
    }
}
