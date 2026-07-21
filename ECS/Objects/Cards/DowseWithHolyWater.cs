using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class DowseWithHolyWater : CardBase
    {
        private int Might = 3;
        private int CourageThreshold = 5;
        private int MightUpgrade = 1;
        public DowseWithHolyWater()
        {
            CardId = "dowse_with_holy_water";
            Name = "Douse with Holy Water";
            Target = "Player";
            Text = $"If you have {CourageThreshold}+ courage, gain {GetMight(IsUpgraded)} might.";
            IsFreeAction = true;
            VisualEffectRecipe = HolySupportEffect();
            Type = CardType.Prayer;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var courage = player?.GetComponent<Courage>()?.Amount ?? 0;
                if (courage >= CourageThreshold)
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Might, Delta = GetMight(IsUpgraded) });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"If you have {CourageThreshold}+ courage, gain {GetMight(IsUpgraded)} might.";
            };
        }
        private int GetMight(bool isUpgraded)
        {
            return isUpgraded ? Might + MightUpgrade : Might;
        }

        
    }
}

