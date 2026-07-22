using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class ShieldbearersVigil : CardBase, ICardStatModifierProvider
    {
        private const int BlockBonus = 1;
        private const int DamageUpgrade = 1;

        public ShieldbearersVigil()
        {
            CardId = CardIds.ShieldbearersVigil.ToKey();
            Name = "Shieldbearer's Vigil";
            Target = "Enemy";
            Text = "When this card is in your hand and not pledged, your other cards gain +1 block.";
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
                {
                    Damage += DamageUpgrade;
                }
            };
        }

        public IEnumerable<CardStatModifier> GetStatModifiers(CardStatQuery query)
        {
            if (query?.Kind != CardStatKind.Block) yield break;
            if (CardEntity == null || query.Card == null || query.Card == CardEntity) yield break;
            if (CardEntity.HasComponent<Pledge>()) yield break;

            yield return new CardStatModifier
            {
                Delta = BlockBonus,
                Reason = Name,
                SourceId = CardId,
                SourceType = "Card",
            };
        }
    }
}
