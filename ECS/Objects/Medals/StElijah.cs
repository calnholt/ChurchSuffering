using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StElijah : MedalBase
    {
        private const int BurnAmount = 1;

        public StElijah()
        {
            Id = "st_elijah";
            Name = "St. Elijah";
            MaxCount = 1;
            Text = $"The first time each battle you pledge a scorched card, the enemy gains {BurnAmount} burn.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
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

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            if (CurrentCount <= 0) return;
            if (evt?.Card?.GetComponent<Scorched>() == null) return;
            CurrentCount = 0;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = EntityManager.GetEntity("Enemy"),
                Type = AppliedPassiveType.Burn,
                Delta = BurnAmount
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<PledgeAddedEvent>(OnPledgeAdded);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
