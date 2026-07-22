using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;



namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Burn : CardBase
    {
        private int BurnAmount = 1;
        public Burn()
        {
            CardId = CardIds.Burn.ToKey();
            Name = "Burn";
            Target = "Enemy";
            Text = $"If the enemy has burn, the enemy gains {BurnAmount + 1} burn, otherwise the enemy gains {BurnAmount} burn.";
            Block = 2;
            Type = CardType.Prayer;
            VisualEffectRecipe = PlayerAttackEffect();
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var enemy = entityManager.GetEntity("Enemy");
                int burnDelta;

                if (IsUpgraded)
                {
                    bool isScorched = card.GetComponent<Scorched>() != null;
                    burnDelta = isScorched ? BurnAmount + 2 : BurnAmount + 1;
                }
                else
                {
                    var passives = enemy.GetComponent<AppliedPassives>();
                    bool enemyHasBurn = passives?.Passives.TryGetValue(AppliedPassiveType.Burn, out int burnStacks) == true
                        && burnStacks > 0;
                    burnDelta = enemyHasBurn ? BurnAmount + 1 : BurnAmount;
                }

                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = enemy,
                    Type = AppliedPassiveType.Burn,
                    Delta = burnDelta
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Apply {BurnAmount + 1} burn to the enemy. If this is scorched, apply {BurnAmount + 2} burn instead.";
            };
        }
    }
}
