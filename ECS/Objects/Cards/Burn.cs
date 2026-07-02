using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;



namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Burn : CardBase
    {
        private int BurnAmount = 1;
        private int CourageThreshold = 3;
        private int ActionPointBonus = 1;
        public Burn()
        {
            CardId = "burn";
            Name = "Burn";
            Target = "Enemy";
            Text = $"Apply {BurnAmount} burn to the enemy. If you have {CourageThreshold}+ courage, gain {ActionPointBonus} action point.";
            Block = 2;
            Type = CardType.Prayer;
            VisualEffectRecipe = PlayerAttackEffect();

            OnPlay = (entityManager, card) =>
            {
                var courage = entityManager.GetEntity("Player").GetComponent<Courage>().Amount;
                EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Burn, Delta = BurnAmount });
                if (courage >= CourageThreshold)
                {
                    EventManager.Publish(new ModifyActionPointsEvent { Delta = ActionPointBonus });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                IsFreeAction = true;
                Text = $"Apply {BurnAmount} burn to the enemy.";
            };
        }
    }
}