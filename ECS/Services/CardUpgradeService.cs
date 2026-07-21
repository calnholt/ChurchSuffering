using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;

namespace ChurchSuffering.ECS.Services
{
    public static class CardUpgradeService
    {
        internal static int UpgradeConfirmedInvokeCountForTests;

        public static void InvokeUpgradeConfirmed(string upgradedCardKey)
        {
            if (!RunDeckService.TryParseCardKey(upgradedCardKey, out var cardId, out _, out var isUpgraded) || !isUpgraded)
                return;

            var card = CardFactory.Create(cardId);
            if (card == null) return;

            UpgradeConfirmedInvokeCountForTests++;
            InvokeUpgradeConfirmedOnCard(card);
            EventManager.Publish(new CardUpgradeConfirmedEvent { CardId = cardId });
        }

        internal static void InvokeUpgradeConfirmedOnCard(CardBase card)
        {
            if (card == null) return;
            card.IsUpgraded = true;
            card.OnUpgrade?.Invoke(null, null);
        }
    }
}
