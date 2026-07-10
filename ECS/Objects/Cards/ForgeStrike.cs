using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class ForgeStrike : CardBase
    {
        private int MightGainedUpgrade = 2;
        public ForgeStrike()
        {
            CardId = "forge_strike";
            Rarity = Rarity.Starter;
            Name = "Forge Strike";
            Target = "Enemy";
            VisualEffectRecipe = HeavyHammerEffect();
            Damage = 7;
            Cost = ["Any", "Any"];
            Block = 3;
            IsFreeAction = true;

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
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = entityManager.GetEntity("Player"),
                    Type = AppliedPassiveType.Might,
                    Delta = MightGainedUpgrade
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Gain {MightGainedUpgrade} might.";
            };
        }
    }
}
