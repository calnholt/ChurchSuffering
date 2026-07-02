using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class BattleScars : CardBase
    {
        private const int ScarThreshold = 2;
        private const int VigorGained = 2;
        private const int VigorGainedUpgrade = 3;

        public BattleScars()
        {
            CardId = "battle_scars";
            Name = "Battle Scars";
            Target = "Enemy";
            Text = $"If you have {ScarThreshold} or more scars, gain {GetVigorGained(IsUpgraded)} vigor.";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 7;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                var passives = player?.GetComponent<AppliedPassives>();
                int scarStacks = 0;
                passives?.Passives.TryGetValue(AppliedPassiveType.Scar, out scarStacks);

                if (scarStacks >= ScarThreshold)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Vigor,
                        Delta = GetVigorGained(IsUpgraded)
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
                if (card != null)
                {
                    IsFreeAction = true;
                    Text = $"If you have {ScarThreshold} or more scars, gain {GetVigorGained(IsUpgraded)} vigor.";
                }
            };
        }

        private static int GetVigorGained(bool isUpgraded) => isUpgraded ? VigorGainedUpgrade : VigorGained;
    }
}
