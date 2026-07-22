using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class SuddenThrust : CardBase
    {
        private int CourageGain = 1;
        private int DamageUpgrade = 1;

        public SuddenThrust()
        {
            CardId = CardIds.SuddenThrust.ToKey();
            Name = "Sudden Thrust";
            Target = "Enemy";
            Text = $"Gain {CourageGain} courage.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 2;
            Block = 3;

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
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageGain, Type = ModifyCourageType.Gain });
            };
            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
            };
        }
    }
}
