using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StMonica : MedalBase
    {
        private const int ResurrectAmount = 1;

        public StMonica()
        {
            Id = "st_monica";
            Name = "St. Monica";
            Text = $"Whenever you trigger your temperance ability, resurrect {ResurrectAmount}.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<TriggerTemperance>(OnTriggerTemperance);
        }

        private void OnTriggerTemperance(TriggerTemperance evt)
        {
            if (evt?.Owner == null) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<TriggerTemperance>(OnTriggerTemperance);
        }
    }
}
