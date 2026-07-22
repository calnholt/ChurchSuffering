using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class WardingPledge : CardBase, IReplacementEffectProvider
    {
        private const int ScarReplacements = 3;
        private const int BlockUpgrade = 1;
        private int _remaining = ScarReplacements;

        public WardingPledge()
        {
            CardId = CardIds.WardingPledge.ToKey();
            Name = "Warding Pledge";
            Target = "Enemy";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 1;
            Block = 3;
            RefreshText();

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

            OnPledged = (entityManager, card) =>
            {
                _remaining = ScarReplacements;
                RefreshText();
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Block += BlockUpgrade;
                }
            };
        }

        public bool TryReplace(ReplaceableEffectRequest request)
        {
            if (request == null) return false;
            if (request.Kind != ReplaceableEffectKind.ScarGain) return false;
            if (_remaining <= 0) return false;
            if (CardEntity?.GetComponent<Pledge>() == null) return false;

            var player = EntityManager?.GetEntity("Player");
            if (request.OriginalTarget == null || request.OriginalTarget != player) return false;
            if (request.OriginalDelta <= 0) return false;

            request.Actions.Add(new ReplacementEffectAction
            {
                Type = ReplacementEffectActionType.ApplyPassive,
                Source = request.OriginalTarget,
                Target = request.OriginalTarget,
                Delta = request.OriginalDelta,
                PassiveType = AppliedPassiveType.Aegis
            });

            _remaining--;
            RefreshText();
            return true;
        }

        private void RefreshText()
        {
            Text = GetText(_remaining);
            Tooltip = Text;
            var ui = CardEntity?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.Tooltip = string.Empty;
                ui.TooltipKeywordSource = Text ?? string.Empty;
            }
        }

        private static string GetText(int remaining) =>
            $"While this card is pledged, the next three times you gain scars, gain that much aegis instead. ({remaining} remaining)";
    }
}
