using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class UnburdenedStrike : CardBase
    {
        private const int DamageBonus = 3;
        private const int DamageBonusUpgrade = 1;
        private const int BlockUpgrade = 1;
        public UnburdenedStrike()
        {
            CardId = "unburdened_strike";
            Rarity = Rarity.Uncommon;
            Name = "Unburdened Strike";
            Target = "Enemy";
            Text = $"If no cards were discarded to play this, this gains +{GetDamageBonus(IsUpgraded)} damage.";
            Cost = ["White", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 2;
            EligibleWeapons = [EligibleWeapon.Hammer];

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
                var playContext = card?.GetComponent<CardPlayStatContext>();
                if (playContext != null)
                {
                    return playContext.PaymentCards.Count == 0
                        ? GetDamageBonus(IsUpgraded)
                        : 0;
                }

                int vigorStacks = VigorService.GetPlayerVigorStacks(entityManager);
                return VigorService.GetEffectiveCost(this, vigorStacks).Count == 0
                    ? GetDamageBonus(IsUpgraded)
                    : 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"If no cards were discarded to play this, this gains +{GetDamageBonus(IsUpgraded)} damage.";
                Block += BlockUpgrade;
            };

        }
        private int GetDamageBonus(bool isUpgraded)
        {
            return isUpgraded ? DamageBonus + DamageBonusUpgrade : DamageBonus;
        }
    }
}
