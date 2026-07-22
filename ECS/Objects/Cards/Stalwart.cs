using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Stalwart : CardBase
    {
        private int CourageCost = 1;
        public Stalwart()
        {
            CardId = CardIds.Stalwart.ToKey();
            Name = "Stalwart";
            Text = $"As an additional cost, lose {GetCourageCost(IsUpgraded)} courage.";
            Type = CardType.Block;
            Block = 7;
            
            OnBlock = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = -GetCourageCost(IsUpgraded), Type = ModifyCourageType.Spent });
            };

            CanPlay = (entityManager, card) =>
            {
                var phase = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
                if (phase.Sub == SubPhase.Block)
                {
                    var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                    int courage = player?.GetComponent<Courage>()?.Amount ?? 0;
                    if (courage < GetCourageCost(IsUpgraded)) return false;
                    return true;
                }
                return false;
            };
            OnCantPlay = (entityManager, card) =>
            {
                var phase = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
                if (phase.Sub == SubPhase.Block)
                {
                    var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                    int courage = player?.GetComponent<Courage>()?.Amount ?? 0;
                    if (courage < GetCourageCost(IsUpgraded))
                        EventManager.Publish(new CantPlayCardMessage { Message = $"Requires {GetCourageCost(IsUpgraded)} courage!" });
                }
                else
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = $"Can only pay during block phase!" });
                }
            };
            OnUpgrade = (entityManager, card) =>
            {
                Text = string.Empty;
            };
        }
        private int GetCourageCost(bool isUpgraded)
        {
            return isUpgraded ? 0 : CourageCost;
        }
    }
}

