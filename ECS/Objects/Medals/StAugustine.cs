using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StAugustine : MedalBase
    {
        public int HpIncrease { get; set; } = 1;
        public int MillCount { get; set; } = 1;
        public StAugustine()
        {
            Id = "st_augustine";
            Name = "St. Augustine of Hippo";
            Text = $"At the start of battle, mill {MillCount} card. Increase your max HP by {HpIncrease} when this is acquired.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        public override void OnAcquire()
        {
            EventManager.Publish(new IncreaseMaxHpEvent
            {
                Target = EntityManager.GetEntity("Player"),
                Delta = HpIncrease
            });
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.StartBattle)
            {
                EmitActivateEvent();
            }
        }

        public override void Activate()
        {
            EventManager.Publish(new MillCardEvent());
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
