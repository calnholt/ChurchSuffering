using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Smite : CardBase
    {
        private int TemperanceUpgradeAmount = 1;
        public Smite()
        {
            CardId = "smite";
            Rarity = Rarity.Starter;
            Name = "Smite";
            Target = "Enemy";
            VisualEffectRecipe = HolyStrikeEffect();
            Damage = 3;
            Block = 3;
            Type = CardType.Attack;
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
                    EventManager.Publish(new ModifyTemperanceEvent {
                        Delta = TemperanceUpgradeAmount
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"When this is pledged, gain {TemperanceUpgradeAmount} temperance.";
            };
        }
    }
}
