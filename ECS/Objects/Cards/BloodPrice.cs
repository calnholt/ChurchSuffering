using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class BloodPrice : CardBase
    {
        private const int BlockAmount = 3;
        private const int ScarDamageMultiplier = 2;
        private const int MaxDamage = 10;
        private const int ScarGainedOnPlay = 1;

        public BloodPrice()
        {
            CardId = "blood_price";
            Name = "Blood Price";
            Target = "Enemy";
            Text = GetBaseText();
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 0;
            Block = BlockAmount;
            IsFreeAction = true;

            GetConditionalDamage = (entityManager, card) =>
                Math.Min(GetScarStacks(entityManager) * ScarDamageMultiplier, MaxDamage);

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");

                if (IsUpgraded)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Scar,
                        Delta = ScarGainedOnPlay
                    });
                }

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Text = GetUpgradedText();
                }
            };
        }

        private static int GetScarStacks(EntityManager entityManager)
        {
            var player = entityManager.GetEntity("Player");
            var passives = player?.GetComponent<AppliedPassives>();
            int scarStacks = 0;
            passives?.Passives.TryGetValue(AppliedPassiveType.Scar, out scarStacks);
            return scarStacks;
        }

        private static string GetBaseText() =>
            $"Deals damage equal to twice the number of scars you have (max {MaxDamage}).";

        private static string GetUpgradedText() =>
            $"Gain {ScarGainedOnPlay} scar. Deals damage equal to twice the number of scars you have (max {MaxDamage}).";
    }
}
