using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class RelentlessStrike : CardBase
    {
        private const string ModificationReason = "RelentlessStrike";
        private int BattleDamageBonus = 4;

        private int BattleDamageBonusUpgrade = 4;
        public RelentlessStrike()
        {
            CardId = "relentless_strike";
            Name = "Relentless Strike";
            Target = "Enemy";
            Text = $"The first time you play this each battle, it goes to the bottom of your deck. It gains +{GetBattleDamageBonus(IsUpgraded)} damage for the rest of the battle.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 9;
            Block = 3;
            Cost = ["White", "Any"];

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                bool isFirstPlay = card.GetComponent<RelentlessStrikeBattleState>() == null;

                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    Delta = -GetDerivedDamage(entityManager, card),
                    AttackCard = card,
                    DamageType = ModifyTypeEnum.Attack
                });

                if (isFirstPlay)
                {
                    entityManager.AddComponent(card, new RelentlessStrikeBattleState { Owner = card });
                    entityManager.AddComponent(card, new MarkedForBottomOfDrawPile { Owner = card });
                    AttackDamageValueService.ApplyDelta(card, BattleDamageBonus, ModificationReason);
                }
            };
            OnUpgrade = (entityManager, card) =>
            {
                Text = $"The first time you play this each battle, it goes to the bottom of your deck. It gains +{GetBattleDamageBonus(IsUpgraded)} damage for the rest of the battle.";
            };
        }

        private int GetBattleDamageBonus(bool isUpgraded)
        {
            return isUpgraded ? BattleDamageBonus + BattleDamageBonusUpgrade : BattleDamageBonus;
        }

        public override void Initialize(EntityManager entityManager, Entity cardEntity)
        {
            base.Initialize(entityManager, cardEntity);
            EventManager.Subscribe<EnemyKilledEvent>(OnBattleEnd);
        }

        private void OnBattleEnd(EnemyKilledEvent evt)
        {
            ClearBattleScopedState();
        }

        private void ClearBattleScopedState()
        {
            if (CardEntity == null) return;
            AttackDamageValueService.RemoveModification(CardEntity, ModificationReason);
            if (CardEntity.GetComponent<RelentlessStrikeBattleState>() != null)
            {
                EntityManager.RemoveComponent<RelentlessStrikeBattleState>(CardEntity);
            }
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<EnemyKilledEvent>(OnBattleEnd);
            base.Dispose();
        }
    }
}
