using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StBartholomew : MedalBase
    {
        private const int DamageThreshold = 8;
        private const int WoundedAmount = 1;

        public StBartholomew()
        {
            Id = "st_bartholomew";
            Name = "St. Bartholomew";
            MaxCount = 1;
            Text = $"The first time you deal {DamageThreshold} or more damage to the enemy with a single attack, the enemy gains {WoundedAmount} wounded.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<ModifyHpRequestEvent>(OnModifyHpRequest);
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

        private void OnModifyHpRequest(ModifyHpRequestEvent evt)
        {
            if (CurrentCount <= 0) return;
            if (evt.DamageType != ModifyTypeEnum.Attack || evt.Delta >= 0) return;
            if (evt.Source?.HasComponent<Player>() != true) return;
            if (evt.Target?.HasComponent<Enemy>() != true) return;

            int rawDamage = Math.Abs(evt.Delta);
            var preview = new ModifyHpRequestEvent
            {
                Source = evt.Source,
                Target = evt.Target,
                AttackCard = evt.AttackCard,
                DamageType = evt.DamageType
            };
            int damageDealt = AppliedPassivesService.GetPreviewAttackDamage(preview, rawDamage, ReadOnly: true);
            if (damageDealt < DamageThreshold) return;

            CurrentCount = 0;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = EntityManager.GetEntity("Enemy"),
                Type = AppliedPassiveType.Wounded,
                Delta = WoundedAmount
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<ModifyHpRequestEvent>(OnModifyHpRequest);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
