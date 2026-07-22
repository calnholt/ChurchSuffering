using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class MarkOfAnathema : CardBase
    {
        private const int AnathemaAmount = 4;
        private const int DamageUpgrade = 3;
        private const int BlockUpgrade = 1;

        public MarkOfAnathema()
        {
            CardId = CardIds.MarkOfAnathema.ToKey();
            Name = "Mark of Anathema";
            Target = "Enemy";
            Text = $"The enemy gains {AnathemaAmount} anathema.";
            Cost = ["Any", "Any", "Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 7;
            Block = 2;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = enemy,
                    Type = AppliedPassiveType.Anathema,
                    Delta = AnathemaAmount
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
                Block += BlockUpgrade;
            };
        }
    }
}
