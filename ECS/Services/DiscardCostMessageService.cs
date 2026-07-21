using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.ECS.Services
{
    public static class DiscardCostMessageService
    {
        private static readonly string[] ColorOrder = ["Red", "White", "Black"];

        public static string GetUnsatisfiableCostMessage(IReadOnlyList<string> requiredCosts)
        {
            if (requiredCosts == null || requiredCosts.Count == 0)
                return "You need another card in your hand to pay for the discard cost";

            var colorCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int anyCount = 0;
            foreach (var cost in requiredCosts)
            {
                if (string.Equals(cost, "Any", StringComparison.OrdinalIgnoreCase))
                {
                    anyCount++;
                    continue;
                }

                if (ColorOrder.Contains(cost, StringComparer.OrdinalIgnoreCase))
                {
                    colorCounts[cost] = colorCounts.GetValueOrDefault(cost) + 1;
                }
            }

            bool allAny = anyCount > 0 && colorCounts.Count == 0 && anyCount == requiredCosts.Count;
            var parts = new List<string>();

            foreach (var color in ColorOrder)
            {
                if (!colorCounts.TryGetValue(color, out int count) || count <= 0)
                    continue;

                parts.Add(FormatColorPhrase(color, count, inYourHand: false));
            }

            if (anyCount > 0)
                parts.Add(FormatAnyPhrase(anyCount, inYourHand: allAny));

            if (parts.Count == 0)
                return "You need another card in your hand to pay for the discard cost";

            if (parts.Count == 1 && colorCounts.Count == 1 && anyCount == 0)
            {
                var onlyColor = colorCounts.Keys.First();
                return $"You need {FormatColorPhrase(onlyColor, colorCounts[onlyColor], inYourHand: true)} to pay for the discard cost";
            }

            return $"You need {JoinParts(parts)} to pay for the discard cost";
        }

        private static string FormatColorPhrase(string color, int count, bool inYourHand)
        {
            string colorLower = color.ToLowerInvariant();
            string phrase = count switch
            {
                1 => $"a {colorLower} card",
                _ => $"{CountWord(count)} {colorLower} cards",
            };

            return inYourHand ? $"{phrase} in your hand" : phrase;
        }

        private static string FormatAnyPhrase(int count, bool inYourHand)
        {
            string phrase = count switch
            {
                1 => "another card",
                _ => $"{CountWord(count)} other cards",
            };

            return inYourHand ? $"{phrase} in your hand" : phrase;
        }

        private static string CountWord(int count) => count switch
        {
            2 => "two",
            3 => "three",
            _ => count.ToString(),
        };

        private static string JoinParts(IReadOnlyList<string> parts)
        {
            if (parts.Count == 1) return parts[0];
            if (parts.Count == 2) return $"{parts[0]} and {parts[1]}";
            return string.Join(", ", parts.Take(parts.Count - 1)) + $", and {parts[^1]}";
        }
    }
}
