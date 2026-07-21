using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using static ChurchSuffering.ECS.Components.CardData;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StAdrian : MedalBase, ICardStatModifierProvider
    {
        private const int DamageBonus = 2;
        private const int CourageLoss = 1;

        private Entity _armedCard;

        internal int ProcChancePercent { get; set; } = 50;

        public StAdrian()
        {
            Id = "st_adrian";
            Name = "St. Adrian";
            Text = $"Whenever you play a red attack, 50% chance you lose {CourageLoss} courage and it deals +{DamageBonus} base damage.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<PlayCardRequested>(OnPlayCardRequested, priority: 1);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private void OnPlayCardRequested(PlayCardRequested evt)
        {
            if (evt?.Card == null || !evt.CostsPaid) return;

            ClearArm();
            if (!IsRedAttack(evt.Card)) return;
            if (Random.Shared.Next(0, 100) > ProcChancePercent) return;

            _armedCard = evt.Card;
            EmitActivateEvent();
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            ClearArm();
        }

        private static bool IsRedAttack(Entity card)
        {
            var cardData = card.GetComponent<CardData>();
            if (cardData?.Card?.Type != CardType.Attack) return false;
            return CardColorQualificationService.QualifiesAs(card, CardColor.Red);
        }

        private void ClearArm()
        {
            _armedCard = null;
        }

        public IEnumerable<CardStatModifier> GetStatModifiers(CardStatQuery query)
        {
            if (query?.Kind != CardStatKind.Damage) yield break;
            if (query.Mode != CardStatQueryMode.Resolution) yield break;
            if (_armedCard == null || query.Card != _armedCard) yield break;

            yield return new CardStatModifier
            {
                Delta = DamageBonus,
                Reason = Id,
                SourceId = Id,
                SourceType = "Medal",
            };
        }

        public override void Activate()
        {
            EventManager.Publish(new ModifyCourageRequestEvent
            {
                Delta = -CourageLoss,
                Reason = Id,
                Type = ModifyCourageType.Lost
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<PlayCardRequested>(OnPlayCardRequested);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
