using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Mantlet : CardBase
    {
        private int AegisBonusUpgrade = 1;
        public Mantlet()
        {
            CardId = "mantlet";
            Rarity = Rarity.Common;
            Name = "Mantlet";
            Block = 4;
            Type = CardType.Block;

            OnDiscardedForCost = (entityManager, card) =>
            {
                EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Aegis, Delta = AegisBonusUpgrade });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"When this card is discarded to pay for a card cost, gain {AegisBonusUpgrade} aegis.";
            };
        }
    }
}
