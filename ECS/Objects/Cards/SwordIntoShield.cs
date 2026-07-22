using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class SwordIntoShield : CardBase
    {
        private const int DamageGain = 1;

        public SwordIntoShield()
        {
            CardId = CardIds.SwordIntoShield.ToKey();
            Name = "Sword Into Shield";
            Target = "Player";
            Text = $"The next non-weapon attack card you play this turn gains +{DamageGain} damage this climb, then this becomes a textless block card.";
            IsFreeAction = true;
            Type = CardType.Prayer;
            Block = 3;
            VisualEffectRecipe = PlayerBuffEffect();

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = entityManager.GetEntity("Player"),
                    Type = AppliedPassiveType.SwordIntoShield,
                    Delta = DamageGain
                });

                Type = CardType.Block;
                Text = string.Empty;
            };
        }
    }
}
