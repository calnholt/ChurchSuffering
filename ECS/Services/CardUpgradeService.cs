using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;

namespace ChurchSuffering.ECS.Services
{
    public static class CardUpgradeService
    {
        internal static int UpgradeConfirmedInvokeCountForTests;

        public static void InvokeUpgradeConfirmed(string upgradedCardKey, string entryId = null)
        {
            if (!RunDeckService.TryParseCardKey(upgradedCardKey, out var cardId, out var printedColor, out var isUpgraded) || !isUpgraded)
                return;

            var card = CardFactory.Create(cardId);
            if (card == null) return;

            UpgradeConfirmedInvokeCountForTests++;
            InvokeUpgradeConfirmedOnCard(card);
            TryGrantRandomDualColorOnUpgrade(card, entryId, printedColor);
            EventManager.Publish(new CardUpgradeConfirmedEvent { CardId = cardId });
        }

        internal static void InvokeUpgradeConfirmedOnCard(CardBase card)
        {
            if (card == null) return;
            card.IsUpgraded = true;
            card.OnUpgrade?.Invoke(null, null);
        }

        private static void TryGrantRandomDualColorOnUpgrade(
            CardBase card,
            string entryId,
            CardData.CardColor printedColor)
        {
            if (card == null
                || !card.GrantsRandomDualColorOnUpgrade
                || string.IsNullOrWhiteSpace(entryId)
                || !CardColorQualificationService.IsPlayableColor(printedColor))
            {
                return;
            }

            var entry = SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, entryId);
            if (entry == null || !string.IsNullOrWhiteSpace(entry.secondaryColor))
                return;

            var secondary = CardBoonRules.RollSecondaryColor(printedColor, Random.Shared);
            SaveCache.SetRunDeckEntrySecondaryColor(
                RunDeckService.PrimaryLoadoutId,
                entryId,
                secondary.ToString());
        }
    }
}
