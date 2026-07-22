using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class IncreaseFaith : CardBase
    {
        private int PowerGained = 1;

        private int AegisGainedUpgrade = 2;
        public IncreaseFaith()
        {
            CardId = CardIds.IncreaseFaith.ToKey();
            Rarity = Rarity.Uncommon;
            Name = "Increase Faith";
            Target = "Player";
            Text = $"Gain {PowerGained} power.";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent 
                { 
                    Target = player, 
                    Type = AppliedPassiveType.Power, 
                    Delta = PowerGained 
                });
            };

            OnPledged = (entityManager, card) =>
            {
                if (IsUpgraded)
                {
                    var player = entityManager.GetEntity("Player");
                    EventManager.Publish(new ApplyPassiveEvent 
                    { 
                        Target = player, 
                        Type = AppliedPassiveType.Aegis, 
                        Delta = AegisGainedUpgrade 
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"When this is pledged, gain {AegisGainedUpgrade} aegis.\n\nGain {PowerGained} power.";
            };
        }
    }
}
