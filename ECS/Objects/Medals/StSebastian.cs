using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StSebastian : MedalBase
    {
        public StSebastian()
        {
            Id = "st_sebastian";
            Name = "St. Sebastian";
            Text = "Whenever you win a battle with 1 HP remaining, increase your max HP by 1.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            var player = EntityManager.GetEntitiesWithComponent<Player>()
                .FirstOrDefault(e => e.HasComponent<HP>());
            if (player == null) return;
            var hp = player.GetComponent<HP>();
            if (hp == null || hp.Current != 1) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new IncreaseMaxHpEvent
            {
                Target = EntityManager.GetEntity("Player"),
                Delta = 1
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }
    }
}
