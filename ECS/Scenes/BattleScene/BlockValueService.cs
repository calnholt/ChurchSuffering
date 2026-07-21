using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using System;
using ChurchSuffering.ECS.Factories;

namespace ChurchSuffering.ECS.Systems
{
    internal static class BlockValueService
    {
        public static void ApplyDelta(Entity card, int delta, string reason)
        {
          var modifiedBlock = card.GetComponent<ModifiedBlock>();
          modifiedBlock.Modifications.Add(new Modification { Delta = delta, Reason = reason });
        }

        public static void RemoveModification(Entity card, string reason)
        {
          var modifiedBlock = card.GetComponent<ModifiedBlock>();
          modifiedBlock.Modifications.RemoveAll(m => m.Reason == reason);
        }

        public static int GetTotalBlockValue(Entity card)
        {
          var entityManager = card?.GetComponent<CardData>()?.Card?.EntityManager;
          return CardStatModifierService.GetCardBlock(entityManager, card).TotalValue;
        }

        public static int GetBaseBlockValue(Entity card)
        {
            var cd = card.GetComponent<CardData>();
            return cd.Card.Block;
        }
    }
}
