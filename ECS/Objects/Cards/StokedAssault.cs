using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class StokedAssault : CardBase
    {
        private const int VigorRequired = 2;
        private const int DamageUpgrade = 1;

        public StokedAssault()
        {
            CardId = CardIds.StokedAssault.ToKey();
            Name = "Stoked Assault";
            Target = "Enemy";
            Text = $"You can't play this if you don't have {VigorRequired} vigor.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 4;
            Block = 3;

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

            CanPlay = (entityManager, card) =>
                VigorService.GetPlayerVigorStacks(entityManager) >= VigorRequired;

            OnCantPlay = (entityManager, card) =>
            {
                if (VigorService.GetPlayerVigorStacks(entityManager) < VigorRequired)
                {
                    EventManager.Publish(new CantPlayCardMessage
                    {
                        Message = $"Requires {VigorRequired} vigor!"
                    });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
            };
        }
    }
}
