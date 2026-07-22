using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Sacrifice : CardBase
    {
        private int ScarAmount = 1;
        private int TemperanceAmount = 1;
        private int TemperanceUpgradeAmount = 1;
        private int ResurrectAmount = 2;

        public Sacrifice()
        {
            CardId = CardIds.Sacrifice.ToKey();
            Name = "Sacrifice";
            Target = "Player";
            Text = $"Gain {ScarAmount} scar, {TemperanceAmount} temperance, and resurrect {ResurrectAmount}.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Scar,
                    Delta = ScarAmount
                });
                EventManager.Publish(new ModifyTemperanceEvent { Delta = TemperanceAmount });
                EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card == null) return;
                TemperanceAmount += TemperanceUpgradeAmount;
                Text = $"Gain {ScarAmount} scar, {TemperanceAmount} temperance, and resurrect {ResurrectAmount}.";
            };
        }
    }
}
