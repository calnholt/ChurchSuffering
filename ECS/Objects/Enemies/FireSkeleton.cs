using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Enemies
{
    public class FireSkeleton : EnemyBase
    {
        private int Armor = 2;
        public FireSkeleton()
        {
            Id = EnemyId.FireSkeleton;
            Name = "Fire Skeleton";
            HP = 20;


            OnStartOfBattle = (entityManager) =>
            {
              EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
              EventQueueBridge.EnqueueTriggerAction("FireSkeleton.OnStartOfBattle", () =>
              {
                EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
              }, AppliedPassivesManagementSystem.Duration);
              EventQueueBridge.EnqueueTriggerAction("FireSkeleton.OnStartOfBattle", () =>
              {
                EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Enflamed, Delta = 2 });
              }, AppliedPassivesManagementSystem.Duration);
            };
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
          if (evt.Current != SubPhase.PlayerEnd) return;
          var courage = GetComponentHelper.GetCourage(EntityManager);
          if (courage == null || courage.Amount < 4) return;
          var passives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
          if (passives?.Passives == null) return;
          passives.Passives.TryGetValue(AppliedPassiveType.Enflamed, out int enflamedStacks);
          if (enflamedStacks <= 0) return;
          var player = EntityManager.GetEntity("Player");
          EventManager.Publish(new PassiveTriggered { Owner = player, Type = AppliedPassiveType.Enflamed });
          EventManager.Publish(new ModifyHpRequestEvent
          {
            Source = player,
            Target = player,
            Delta = -enflamedStacks,
            DamageType = ModifyTypeEnum.Effect
          });
        }

        public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
        {
            return new Skeleton().GetAttackIds(entityManager, turnNumber);
        }

        public override void Dispose()
        {
          EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
          Console.WriteLine($"[FireSkeleton] Unsubscribed from ChangeBattlePhaseEvent");
        }

    }

}