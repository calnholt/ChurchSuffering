using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Thaw : CardBase
    {
        public Thaw()
        {
            CardId = "thaw";
            Name = "Thaw";
            Target = "Enemy";
            Text = "Lose all frostbite, then gain X temperance where X is the amount of frostbite lost.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");

                int frostbiteLost = 0;
                var passives = player.GetComponent<AppliedPassives>();
                if (passives?.Passives.TryGetValue(AppliedPassiveType.Frostbite, out frostbiteLost) == true && frostbiteLost > 0)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Frostbite,
                        Delta = -frostbiteLost
                    });
                    EventManager.Publish(new ModifyTemperanceEvent { Delta = frostbiteLost });
                    if (IsUpgraded)
                    {
                        EventManager.Publish(new ModifyCourageRequestEvent
                        {
                            Delta = frostbiteLost,
                            Type = ModifyCourageType.Gain
                        });
                    }
                }

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Text = "Lose all frostbite, then gain X temperance and X courage where X is the amount of frostbite lost.";
                }
            };
        }
    }
}
