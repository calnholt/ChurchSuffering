using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Ravage : CardBase
    {
        private int MillAmount = 1;

        private int MillAmountUpgrade = 3;
        private int DamageAmountUpgrade = 3;
        public Ravage()
        {
            CardId = "ravage";
            Name = "Ravage";
            Target = "Enemy";
            Text = $"As an additional cost, mill {GetMillAmount(IsUpgraded)} cards.";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 8;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                for (int j = 0; j < GetMillAmount(IsUpgraded); j++)
                {
                    EventManager.Publish(new MillCardEvent { });
                }
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = player, 
                    Target = enemy, 
                    Delta = -GetDerivedDamage(entityManager, card), 
                    AttackCard = card,
 
                    DamageType = ModifyTypeEnum.Attack 
                });
            };

            CanPlay = (entityManager, card) =>
            {
                var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                var deck = deckEntity?.GetComponent<Deck>();
                return deck != null && deck.DrawPile.Count >= GetMillAmount(IsUpgraded);
            };
            OnCantPlay = (entityManager, card) =>
            {
                var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                var deck = deckEntity?.GetComponent<Deck>();
                if (deck == null || deck.DrawPile.Count < GetMillAmount(IsUpgraded))
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {MillAmount} cards in deck!" });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageAmountUpgrade;
                Text = $"As an additional cost, mill {GetMillAmount(IsUpgraded)} cards.";
            };
        }

        private int GetMillAmount(bool isUpgraded)
        {
            return isUpgraded ? MillAmount + MillAmountUpgrade : MillAmount;
        }
    }
}
