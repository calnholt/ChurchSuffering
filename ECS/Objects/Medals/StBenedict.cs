using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StBenedict : MedalBase
    {
        public StBenedict()
        {
            Id = "st_benedict";
            Name = "St. Benedict";
            MaxCount = 3;
            Text = $"Whenever you pledge {MaxCount} cards, gain 1 vigor.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
        }

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            CurrentCount++;
            if (CurrentCount >= MaxCount)
            {
                CurrentCount = 0;
                EmitActivateEvent();
            }
        }

        public override void Activate()
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = EntityManager.GetEntity("Player"),
                Type = AppliedPassiveType.Vigor,
                Delta = 1
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<PledgeAddedEvent>(OnPledgeAdded);
        }
    }
}
