using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class EvenTemper : CardBase
    {
        public int Temperance = 1;
        public int BlockUpgrade = 1;

        public EvenTemper()
        {
            CardId = CardIds.EvenTemper.ToKey();
            Rarity = Rarity.Common;
            Name = "Even Temper";
            Text = $"Gain {Temperance} temperance.";
            Block = 3;
            Type = CardType.Block;
            VisualEffectRecipe = DefensiveGuardEffect();

            OnBlock = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyTemperanceEvent { Delta = Temperance });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                    Block += BlockUpgrade;
            };
        }
    }
}
