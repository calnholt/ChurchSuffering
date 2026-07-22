using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class FullForce : CardBase
    {
        private const int DamageUpgrade = 2;

        public FullForce()
        {
            CardId = CardIds.FullForce.ToKey();
            Name = "Full Force";
            Target = "Enemy";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 9;
            Block = 0;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = entityManager.GetEntity("Player"),
                    Target = entityManager.GetEntity(Target),
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                    Damage += DamageUpgrade;
            };
        }
    }
}
