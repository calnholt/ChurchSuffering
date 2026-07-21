using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Purge : CardBase
    {
        private static readonly (CardApplicationType Type, Func<Entity, bool> IsPresent)[] LocationModifiers =
        [
            (CardApplicationType.Frozen, card => card.HasComponent<Frozen>()),
            (CardApplicationType.Brittle, card => card.HasComponent<Brittle>()),
            (CardApplicationType.Scorched, card => card.HasComponent<Scorched>()),
            (CardApplicationType.Thorned, card => card.HasComponent<Thorned>()),
            (CardApplicationType.Cursed, card => card.HasComponent<Cursed>()),
        ];

        private bool _isSubscribed;

        public Purge()
        {
            CardId = "purge";
            Name = "Purge";
            Target = "Enemy";
            Damage = 3;
            Block = 3;
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            RefreshText();

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity(Target),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    RefreshText();
                }
            };
        }

        public override void Initialize(EntityManager entityManager, Entity cardEntity)
        {
            base.Initialize(entityManager, cardEntity);
            RefreshText();
            if (_isSubscribed) return;
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
            _isSubscribed = true;
        }

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            if (evt?.Card == null || !IsInHand()) return;

            var typesToRemove = GetPresentLocationModifiers(evt.Card);
            if (typesToRemove.Count == 0) return;

            foreach (var type in typesToRemove)
            {
                EventManager.Publish(new RemoveCardApplication
                {
                    Card = evt.Card,
                    Type = type,
                });
            }

            if (!IsUpgraded) return;

            var player = EntityManager?.GetEntity("Player");
            if (player == null) return;

            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = player,
                Type = AppliedPassiveType.Might,
                Delta = typesToRemove.Count,
            });
        }

        private bool IsInHand()
        {
            if (CardEntity == null || EntityManager == null) return false;
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            return deck?.Hand != null && deck.Hand.Contains(CardEntity);
        }

        private static List<CardApplicationType> GetPresentLocationModifiers(Entity card)
        {
            var types = new List<CardApplicationType>();
            foreach (var (type, isPresent) in LocationModifiers)
            {
                if (isPresent(card))
                {
                    types.Add(type);
                }
            }
            return types;
        }

        private void RefreshText()
        {
            Text = GetText(IsUpgraded);
            Tooltip = Text;
            var ui = CardEntity?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.Tooltip = string.Empty;
                ui.TooltipKeywordSource = Text ?? string.Empty;
            }
        }

        private static string GetText(bool isUpgraded)
        {
            const string baseText = "While this card is in your hand, when you pledge a card, remove all location modifiers from that card.";
            return isUpgraded
                ? $"{baseText}\n\nGain 1 might for each modification removed."
                : baseText;
        }

        public override void Dispose()
        {
            if (_isSubscribed)
            {
                EventManager.Unsubscribe<PledgeAddedEvent>(OnPledgeAdded);
                _isSubscribed = false;
            }

            base.Dispose();
        }
    }
}
