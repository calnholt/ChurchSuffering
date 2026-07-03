using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StLouieIX : MedalBase
    {
        public StLouieIX()
        {
            Id = "st_louie_ix";
            Name = "St. Louie IX, King of France";
            Text = "At the start of every third turn, gain 1 might.";
            MaxCount = 3;
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.PlayerStart)
            {
                CurrentCount++;
                if (CurrentCount >= MaxCount)
                {
                    CurrentCount = 0;
                    EmitActivateEvent();
                }
            }
        }

        public override void Activate()
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Might, Delta = 1 });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}