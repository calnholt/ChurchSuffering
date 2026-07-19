using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class RecklessBarrage : CardBase
    {
        private const int NonPledgedRequired = 2;
        private const int DamageUpgrade = 2;

        public RecklessBarrage()
        {
            CardId = "reckless_barrage";
            Name = "Reckless Barrage";
            Target = "Enemy";
            Text = $"You need {NonPledgedRequired}+ other non-pledged cards in your hand to play this. As an additional cost, discard a random card from your hand.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                var hand = deckEntity?.GetComponent<Deck>()?.Hand;
                if (hand != null)
                {
                    var candidates = hand.Where(c => c != card).ToList();
                    if (candidates.Count > 0)
                    {
                        var discardTarget = candidates[Random.Shared.Next(candidates.Count)];
                        EventManager.Publish(new CardMoveRequested
                        {
                            Card = discardTarget,
                            Deck = deckEntity,
                            Destination = CardZoneType.DiscardPile,
                            Reason = "RecklessBarrage"
                        });
                    }
                }

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity(Target),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            CanPlay = (entityManager, card) =>
            {
                var hand = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>()?.Hand;
                if (hand == null) return false;
                return hand.Count(c => c != card && c.GetComponent<Pledge>() == null) >= NonPledgedRequired;
            };

            OnCantPlay = (entityManager, card) =>
            {
                EventManager.Publish(new CantPlayCardMessage
                {
                    Message = $"Requires {NonPledgedRequired}+ other non-pledged cards in hand!"
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
            };
        }
    }
}
