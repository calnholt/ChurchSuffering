using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Graveward : CardBase
    {
        private const int AegisGained = 2;
        private const int AegisGainedUpgraded = 4;

        private bool _isSubscribed;

        public Graveward()
        {
            CardId = "graveward";
            Name = "Graveward";
            Target = "Enemy";
            Damage = 6;
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
            EventManager.Subscribe<TopCardRemovedForMillEvent>(OnTopCardRemovedForMill);
            _isSubscribed = true;
        }

        private void OnTopCardRemovedForMill(TopCardRemovedForMillEvent evt)
        {
            if (evt?.Card == null || !IsInHand()) return;

            var player = EntityManager?.GetEntity("Player");
            if (player == null) return;

            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = player,
                Type = AppliedPassiveType.Aegis,
                Delta = GetAegisGained(IsUpgraded),
            });
        }

        private bool IsInHand()
        {
            if (CardEntity == null || EntityManager == null) return false;
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            return deck?.Hand != null && deck.Hand.Contains(CardEntity);
        }

        private static int GetAegisGained(bool isUpgraded)
        {
            return isUpgraded ? AegisGainedUpgraded : AegisGained;
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
            return $"Whenever you mill a card while this card is in your hand, gain {GetAegisGained(isUpgraded)} aegis.";
        }

        public override void Dispose()
        {
            if (_isSubscribed)
            {
                EventManager.Unsubscribe<TopCardRemovedForMillEvent>(OnTopCardRemovedForMill);
                _isSubscribed = false;
            }

            base.Dispose();
        }
    }
}
