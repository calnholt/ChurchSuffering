using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Services
{
    public static class GetComponentHelper
    {
        public static EnemyAttackBase GetPlannedAttack(EntityManager entityManager)
        {
            if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(entityManager, out _, out _, out var planned))
                return null;
            return planned.AttackDefinition;
        }

        public static BattleStateInfo GetBattleStateInfo(EntityManager entityManager)
        {
            var battleStateInfo = entityManager.GetEntitiesWithComponent<BattleStateInfo>().FirstOrDefault();
            if (battleStateInfo == null) return null;
            return battleStateInfo.GetComponent<BattleStateInfo>();
        }

        public static AppliedPassives GetAppliedPassives(EntityManager entityManager, string targetId)
        {
            var target = entityManager.GetEntity(targetId);
            var appliedPassives = target.GetComponent<AppliedPassives>();
            if (appliedPassives == null) return null;
            return appliedPassives;
        }

        public static bool IsLastBattleOfQuest(EntityManager entityManager)
        {
            var queuedEvents = entityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
            if (queuedEvents == null) return false;
            var qe = queuedEvents.GetComponent<QueuedEvents>();
            return qe.Events.Count == 1 || qe.CurrentIndex == qe.Events.Count - 1;
        }

        public static Courage GetCourage(EntityManager entityManager)
        {
            var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player == null) return null;
            return player.GetComponent<Courage>();
        }

        /// <summary>
        /// Returns a list of cards in the player's hand that are not weapons and are not pledged.
        /// </summary>
        public static List<Entity> GetHandOfCards(EntityManager entityManager)
        {
            var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return null;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return null;
            var hand = deck.Hand;
            if (hand == null) return null;
            return [.. hand.Where(c => c.GetComponent<CardData>() != null && c.GetComponent<CardData>().Card.IsWeapon == false && c.GetComponent<Pledge>() == null)];
        }
    }
}
