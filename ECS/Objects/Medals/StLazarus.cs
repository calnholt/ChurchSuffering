using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StLazarus : MedalBase
    {
        private const int MillThreshold = 2;
        private const int ResurrectAmount = 1;

        public StLazarus()
        {
            Id = "st_lazarus";
            Name = "St. Lazarus";
            MaxCount = MillThreshold;
            Text = $"Whenever you mill {MillThreshold} cards, resurrect {ResurrectAmount}.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<TopCardRemovedForMillEvent>(OnTopCardRemovedForMill);
        }

        private void OnTopCardRemovedForMill(TopCardRemovedForMillEvent evt)
        {
            if (evt?.Card == null) return;
            CurrentCount++;
            if (CurrentCount >= MaxCount)
            {
                CurrentCount = 0;
                EmitActivateEvent();
            }
        }

        public override void Activate()
        {
            EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<TopCardRemovedForMillEvent>(OnTopCardRemovedForMill);
        }
    }
}
