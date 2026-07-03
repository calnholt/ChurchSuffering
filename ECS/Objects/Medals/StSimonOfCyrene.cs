using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StSimonOfCyrene : MedalBase
    {
        public StSimonOfCyrene()
        {
            Id = "st_simon_of_cyrene";
            Name = "St. Simon of Cyrene";
            Text = $"At the start of battle, the enemy gains 1 anathema.";
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
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = EntityManager.GetEntity("Enemy"),
                Type = AppliedPassiveType.Anathema,
                Delta = 1
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
