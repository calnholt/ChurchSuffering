using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StClare : MedalBase
    {
        public const string MedalId = "st_clare";
        private const int DamageAmount = 2;

        public StClare()
        {
            Id = MedalId;
            Name = "St. Clare of Assisi";
            Text = $"At the start of battle, deal {DamageAmount} damage to the enemy.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.StartBattle) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new ModifyHpRequestEvent
            {
                Source = EntityManager.GetEntity("Player"),
                Target = EntityManager.GetEntity("Enemy"),
                Delta = -DamageAmount,
                DamageType = ModifyTypeEnum.Effect
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
