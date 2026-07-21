using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Hammer : CardBase
    {
        private int VigorGained = 1;

        public Hammer()
        {
            CardId = "hammer";
            Name = "Hammer";
            Target = "Enemy";
            Text = $"Gain {VigorGained} vigor.";
            Cost = ["Black", "Any"];
            VisualEffectRecipe = HeavyHammerEffect().WithIntensity(0.9f);
            Damage = 3;
            IsWeapon = true;

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
                    Target = player,
                    Type = AppliedPassiveType.Vigor,
                    Delta = VigorGained
                });
            };
        }
    }
}
