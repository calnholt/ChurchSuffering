using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StRita : MedalBase
    {
        private const int ResurrectAmount = 2;

        public StRita()
        {
            Id = "st_rita";
            Name = "St. Rita of Cascia";
            MaxCount = 1;
            Text = $"The first time you remove a curse in battle, resurrect {ResurrectAmount}.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<TrackingEvent>(OnTrackingEvent);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        public override void OnAcquire()
        {
            CurrentCount = MaxCount;
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.StartBattle) return;
            CurrentCount = MaxCount;
        }

        private void OnTrackingEvent(TrackingEvent evt)
        {
            if (CurrentCount <= 0) return;
            if (evt?.Type != TrackingTypeEnum.CursesRemoved.ToString()) return;
            if (evt.Delta <= 0) return;
            CurrentCount = 0;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<TrackingEvent>(OnTrackingEvent);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
