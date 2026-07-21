
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Fury : CardBase
    {
        public Fury()
        {
            CardId = "fury";
            Name = "Fury";
            Target = "Player";
            Text = "Gain 1 aggression, then double your aggression.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Block = 3;
            Type = CardType.Prayer;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                if (!IsUpgraded)
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aggression, Delta = 1 });
                    player.GetComponent<AppliedPassives>().Passives.TryGetValue(AppliedPassiveType.Aggression, out var amount);
                    if (amount > 0)
                    {
                        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aggression, Delta = amount });
                    }
                }
                else
                {
                    var courage = player.GetComponent<Courage>().Amount;
                    EventManager.Publish(new ModifyCourageRequestEvent { Delta = -courage, Type = ModifyCourageType.Spent });
                    EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aggression, Delta = courage * 2 });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = "Lose all courage. Gain X aggression, where X is the number of courage you lost, then double your aggression.";
            };
        }
    }
}
