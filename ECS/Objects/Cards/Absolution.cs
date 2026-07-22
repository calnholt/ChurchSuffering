using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Absolution : CardBase
    {
        private int CourageUpgradeAmount = 2;
        public Absolution()
        {
            CardId = CardIds.Absolution.ToKey();
            Rarity = Rarity.Starter;
            Name = "Absolution";
            Target = "Enemy";
            Cost = ["Any", "Any", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 10;
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
            };

            OnPledged = (entityManager, card) =>
            {
                if (IsUpgraded)
                {
                    EventManager.Publish(new ModifyCourageRequestEvent {
                        Delta = CourageUpgradeAmount,
                        Type = ModifyCourageType.Gain
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"When this is pledged, gain {CourageUpgradeAmount} courage.";
            };
        }
    }
}
