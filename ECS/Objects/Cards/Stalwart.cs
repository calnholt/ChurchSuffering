using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Stalwart : CardBase
    {
        private int CourageCost = 1;
        public Stalwart()
        {
            CardId = "stalwart";
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

