using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StIgnatius : MedalBase
    {
        private const int CourageThreshold = 5;
        private const int AggressionAmount = 2;

        public StIgnatius()
        {
            Id = "st_ignatius";
            Name = "St. Ignatius of Loyola";
            MaxCount = 1;
            Text = $"The first time each battle you start your action phase with {CourageThreshold} or more courage, gain {AggressionAmount} aggression.";
            ActivationEffectRecipe = HolySupportEffect();
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        public override void OnAcquire()
        {
            CurrentCount = MaxCount;
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.StartBattle)
            {
                CurrentCount = MaxCount;
                return;
            }

            if (evt.Current != SubPhase.Action) return;
            if (CurrentCount <= 0) return;
            if (GetPlayerCourage() < CourageThreshold) return;

            CurrentCount = 0;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = EntityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Aggression,
                Delta = AggressionAmount
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private int GetPlayerCourage()
        {
            var player = EntityManager.GetEntity("Player");
            return player?.GetComponent<Courage>()?.Amount ?? 0;
        }
    }
}
