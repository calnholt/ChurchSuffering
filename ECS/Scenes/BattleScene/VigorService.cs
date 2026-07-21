using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;

namespace ChurchSuffering.ECS.Systems
{
    internal static class VigorService
    {
        public static int GetPlayerVigorStacks(EntityManager entityManager)
        {
            var player = entityManager.GetEntity("Player");
            if (player == null) return 0;
            var ap = player.GetComponent<AppliedPassives>();
            if (ap == null) return 0;
            ap.Passives.TryGetValue(AppliedPassiveType.Vigor, out int stacks);
            return stacks;
        }

        public static int GetWaivedPipCount(CardBase card, int vigorStacks)
        {
            if (card == null || card.IsWeapon || vigorStacks <= 0) return 0;
            int costCount = card.Cost?.Count ?? 0;
            if (costCount == 0) return 0;
            return Math.Min(vigorStacks, costCount);
        }

        public static List<string> GetEffectiveCost(CardBase card, int vigorStacks)
        {
            var printed = card?.Cost ?? new List<string>();
            if (printed.Count == 0) return new List<string>();

            int waived = GetWaivedPipCount(card, vigorStacks);
            if (waived <= 0) return printed.ToList();

            return printed.Take(printed.Count - waived).ToList();
        }

        public static bool IsWaivedPipIndex(int pipIndex, int pipCount, int waivedCount)
        {
            if (waivedCount <= 0 || pipCount <= 0) return false;
            return pipIndex >= pipCount - waivedCount;
        }
    }
}
