using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StJerome : MedalBase
    {
        public StJerome()
        {
            Id = "st_jerome";
            Name = "St. Jerome";
            Text = "Whenever you gain aggression, gain 1 courage.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
        }

        private void OnApplyPassive(ApplyPassiveEvent evt)
        {
            if (evt?.Target == null || evt.Delta <= 0) return;
            var player = EntityManager.GetEntity("Player");
            if (player == null || evt.Target != player) return;
            if (evt.Type != AppliedPassiveType.Aggression) return;

            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new ModifyCourageRequestEvent
            {
                Delta = 1,
                Reason = Id,
                Type = ModifyCourageType.Gain
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ApplyPassiveEvent>(OnApplyPassive);
        }
    }
}
