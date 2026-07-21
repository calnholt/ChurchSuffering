using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StLuke : MedalBase
    {
        public StLuke()
        {
            Id = "st_luke";
            Name = "St. Luke the Evangelist";
            Text = "At the start of battle, gain 1 aegis.";
            ActivationEffectRecipe = HolySupportEffect();
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
            EntityManager = entityManager;
            MedalEntity = medalEntity;
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
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Aegis, Delta = 1 });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
            Console.WriteLine($"[StLuke] Unsubscribed from ChangeBattlePhaseEvent");
        }

    }
}
