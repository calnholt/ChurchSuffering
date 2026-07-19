using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class AnsweredPrayer : CardBase
    {
        private const int PrayerThreshold = 2;
        private const int ThresholdBonus = 3;
        private const int PerPrayerBonus = 2;

        public AnsweredPrayer()
        {
            CardId = "answered_prayer";
            Name = "Answered Prayer";
            Target = "Enemy";
            Text = GetCardText(IsUpgraded);
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 2;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity(Target),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                int prayers = GetPrayersPlayedThisTurn(entityManager);
                if (IsUpgraded)
                    return prayers * PerPrayerBonus;
                return prayers >= PrayerThreshold ? ThresholdBonus : 0;
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                    Text = GetCardText(IsUpgraded);
            };
        }

        private static string GetCardText(bool isUpgraded)
        {
            return isUpgraded
                ? $"This gains +{PerPrayerBonus} damage for each prayer card played this turn."
                : $"If you have played {PrayerThreshold}+ prayer cards this turn, this gains +{ThresholdBonus} damage.";
        }

        private static int GetPrayersPlayedThisTurn(EntityManager entityManager)
        {
            var battleState = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault()
                ?.GetComponent<BattleStateInfo>();
            if (battleState?.TurnTracking != null &&
                battleState.TurnTracking.TryGetValue(TrackingTypeEnum.PrayersPlayed.ToString(), out int prayers))
                return prayers;
            return 0;
        }
    }
}
