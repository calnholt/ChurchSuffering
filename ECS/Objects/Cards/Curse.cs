using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Curse : CardBase
    {
        public const string CardIdValue = "curse";

        public Curse()
        {
            CardId = CardIdValue;
            Name = "Curse";
            Rarity = Rarity.Common;
            Type = CardType.Prayer;
            Block = 3;
            IsFreeAction = true;
            CanAddToLoadout = false;
            Text = "Remove the curse from this card.";

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new RemoveCardApplication
                {
                    Card = card,
                    Type = CardApplicationType.Cursed,
                });
            };
        }
    }
}
