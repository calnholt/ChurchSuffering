using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class StokeTheFurnace : CardBase
    {
        private int CourageCost = 2;
        private int VigorGained = 1;
        private int MaxRepeats = 3;

        public StokeTheFurnace()
        {
            CardId = CardIds.StokeTheFurnace.ToKey();
            Rarity = Rarity.Common;
            Name = "Stoke the Furnace";
            Target = "Player";
            Text = $"Lose {CourageCost} courage, gain {VigorGained} vigor. Repeat up to {MaxRepeats} times if possible.";
            VisualEffectRecipe = PlayerAttackEffect();
            Type = CardType.Attack;
            Damage = 2;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                for (int i = 0; i < MaxRepeats; i++)
                {
                    var courageCmp = player?.GetComponent<Courage>();
                    int courage = courageCmp?.Amount ?? 0;
                    if (courage < CourageCost) break;

                    EventManager.Publish(new ModifyCourageRequestEvent { Delta = -CourageCost, Type = ModifyCourageType.Spent });
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Vigor,
                        Delta = VigorGained
                    });
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
                IsFreeAction = true;
            };
        }
    }
}
