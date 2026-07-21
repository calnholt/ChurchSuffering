using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class DeusVult : CardBase
    {
        private int CourageBonus = 1;
        private int DamageMultiplier = 1;
        private bool _weaponUsedThisPhase;

        public DeusVult()
        {
            Name = "Deus Vult";
            CardId = "deus_vult";
            Text = $"You can't play this if you have not used your weapon this turn. Gain {CourageBonus} courage. This gains +X damage, where X is your courage";
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 0;
            Block = 2;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var enemy = entityManager.GetEntity("Enemy");
                EventManager.Publish(new ModifyCourageRequestEvent { Delta = CourageBonus, Type = ModifyCourageType.Gain });
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = player, 
                    Target = enemy, 
                    Delta = -GetDerivedDamage(entityManager, card), 
                    AttackCard = card,
 
                    DamageType = ModifyTypeEnum.Attack 
                });
            };

            GetConditionalDamage = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var courage = player.GetComponent<Courage>().Amount;
                return courage * DamageMultiplier;
            };

            CanPlay = (entityManager, card) => {
                if (IsUpgraded)
                {
                    return true;
                }
                return _weaponUsedThisPhase;
            };

            OnCantPlay = (entityManager, card) =>
            {
                if (IsUpgraded)
                {
                    return;
                }
                if (!_weaponUsedThisPhase)
                    EventManager.Publish(new CantPlayCardMessage { Message = "You must attack with your weapon this turn!" });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Gain {CourageBonus} courage. This gains +X damage, where X is your courage";
            };
        }

        public override void Initialize(EntityManager entityManager, Entity cardEntity)
        {
            base.Initialize(entityManager, cardEntity);
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            if (evt?.Card == null) return;

            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
            if (phase?.Sub != SubPhase.Action) return;

            var cardData = evt.Card.GetComponent<CardData>();
            if (cardData?.Card == null) return;

            var player = EntityManager.GetEntity("Player");
            var weaponId = player?.GetComponent<EquippedWeapon>()?.WeaponId;
            if (string.IsNullOrEmpty(weaponId)) return;

            if (cardData.Card.CardId == weaponId)
                _weaponUsedThisPhase = true;
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.Action || evt.Previous == SubPhase.Action)
                _weaponUsedThisPhase = false;
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            base.Dispose();
        }
    }
}
