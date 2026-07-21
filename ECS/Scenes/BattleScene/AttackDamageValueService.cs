using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using System;
using ChurchSuffering.ECS.Factories;

namespace ChurchSuffering.ECS.Systems
{
    internal static class AttackDamageValueService
    {
        public static void ApplyDelta(Entity card, int delta, string reason)
        {
          var modifiedDamage = card.GetComponent<ModifiedDamage>();
          modifiedDamage.Modifications.Add(new Modification { Delta = delta, Reason = reason });
        }

        public static void RemoveModification(Entity card, string reason)
        {
          var modifiedDamage = card.GetComponent<ModifiedDamage>();
          modifiedDamage.Modifications.RemoveAll(m => m.Reason == reason);
        }

        public static int GetTotalDelta(Entity card)
        {
          var modifiedDamage = card.GetComponent<ModifiedDamage>();
          return modifiedDamage?.Modifications?.Sum(m => m.Delta) ?? 0;
        }

        public static int GetTotalDamageValue(Entity card)
        {
          var entityManager = card?.GetComponent<CardData>()?.Card?.EntityManager;
          return CardStatModifierService.GetCardDamage(entityManager, card).TotalValue;
        }

        public static int GetBaseDamageValue(Entity card)
        {
            var cd = card.GetComponent<CardData>();
            return cd.Card.Damage;
        }
    }
}
