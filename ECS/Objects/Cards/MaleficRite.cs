using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class MaleficRite : CardBase
    {
        private const int AggressionGained = 10;
        private const int AggressionGainedUpgrade = 12;
        private const int BlockAmount = 2;
        private const int BlockUpgrade = 3;

        public MaleficRite()
        {
            CardId = "malefic_rite";
            Name = "Malefic Rite";
            Target = "Player";
            Text = GetText(IsUpgraded);
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = BlockAmount;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");

                EventManager.Publish(new ApplyCardApplicationEvent
                {
                    Amount = 1,
                    Type = CardApplicationType.Cursed,
                    Target = CardApplicationTarget.Deck,
                });

                var battleState = player?.GetComponent<BattleStateInfo>();
                if (battleState?.BattleTracking != null &&
                    battleState.BattleTracking.TryGetValue(TrackingTypeEnum.CursesRemoved.ToString(), out int removed) &&
                    removed > 0)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Aggression,
                        Delta = GetAggressionGained(IsUpgraded)
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Block = BlockUpgrade;
                    Text = GetText(IsUpgraded);
                }
            };
        }

        private static int GetAggressionGained(bool isUpgraded) => isUpgraded ? AggressionGainedUpgrade : AggressionGained;

        private static string GetText(bool isUpgraded) =>
            $"A random card in your deck becomes cursed.\n\nIf you removed a curse this battle, gain {GetAggressionGained(isUpgraded)} aggression.";
    }
}
