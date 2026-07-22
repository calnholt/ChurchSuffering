using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Curse : CardBase
    {
        public static readonly string CardIdValue = CardIds.Curse.ToKey();

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
