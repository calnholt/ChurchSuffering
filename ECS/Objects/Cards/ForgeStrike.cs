using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class ForgeStrike : CardBase
    {
        private int MightGainedUpgrade = 2;
        public ForgeStrike()
        {
            CardId = CardIds.ForgeStrike.ToKey();
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
