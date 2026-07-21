using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class VanguardsPromise : CardBase
    {
        private int DamageUpgrade = 1;
        public VanguardsPromise()
        {
            CardId = "vanguards_promise";
            Name = "Vanguard's Promise";
            Target = "Enemy";
            Text = "If you have no pledged card, pledge a random card from your discard pile.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 2;
            Block = 2;
            IsFreeAction = true;

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

                if (PledgeService.HasPledgedCardInHand(entityManager)) return;
                EventManager.Publish(new PledgeRandomCardFromDiscardRequested());
            };

            OnUpgrade = (entityManager, card) =>
            {
                Damage += DamageUpgrade;
            };
        }
    }
}
