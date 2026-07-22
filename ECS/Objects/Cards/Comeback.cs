using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Comeback : CardBase
    {
        private const int ResurrectAmount = 1;
        private const int BlockUpgrade = 1;
        private bool _isSubscribed;

        public Comeback()
        {
            CardId = CardIds.Comeback.ToKey();
            Name = "Comeback";
            Target = "Enemy";
            Text = $"If this card becomes intimidated, resurrect {ResurrectAmount}.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Block = 3;

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
                    Block += BlockUpgrade;
            };
        }

        public override void Initialize(EntityManager entityManager, Entity cardEntity)
        {
            base.Initialize(entityManager, cardEntity);
            if (_isSubscribed) return;
            EventManager.Subscribe<CardIntimidatedEvent>(OnCardIntimidated);
            _isSubscribed = true;
        }

        private void OnCardIntimidated(CardIntimidatedEvent evt)
        {
            if (evt?.Card == null || evt.Card != CardEntity) return;
            EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
        }

        public override void Dispose()
        {
            if (_isSubscribed)
            {
                EventManager.Unsubscribe<CardIntimidatedEvent>(OnCardIntimidated);
                _isSubscribed = false;
            }

            base.Dispose();
        }
    }
}
