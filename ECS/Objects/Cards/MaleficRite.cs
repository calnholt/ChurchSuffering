using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class MaleficRite : CardBase
    {
        private const int BaseAggressionGained = 4;
        private const int BaseCurseMultiplier = 2;
        private const int UpgradedCurseMultiplier = 3;
        private const int BlockAmount = 2;
        private const int BlockUpgrade = 3;
        private bool _isSubscribed;

        public MaleficRite()
        {
            CardId = "malefic_rite";
            Name = "Malefic Rite";
            Target = "Player";
            RefreshText();
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = BlockAmount;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                var aggressionGained = GetAggressionGained(GetCursesRemovedThisClimb(player));

                if (player != null)
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = player,
                        Type = AppliedPassiveType.Aggression,
                        Delta = aggressionGained
                    });
                }

                EventManager.Publish(new ApplyCardApplicationEvent
                {
                    Amount = 1,
                    Type = CardApplicationType.Cursed,
                    Target = CardApplicationTarget.Deck,
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                if (card != null)
                {
                    Block = BlockUpgrade;
                    RefreshText();
                }
            };
        }

        public override void Initialize(EntityManager entityManager, Entity cardEntity)
        {
            base.Initialize(entityManager, cardEntity);
            RefreshText();
            if (_isSubscribed) return;
            EventManager.Subscribe<TrackingEvent>(OnTrackingEvent, priority: -10);
            _isSubscribed = true;
        }

        private void OnTrackingEvent(TrackingEvent evt)
        {
            if (evt?.Type != TrackingTypeEnum.CursesRemoved.ToString()) return;
            RefreshText();
        }

        private void RefreshText()
        {
            Text = GetText(IsUpgraded, GetCursesRemovedThisClimb());
            Tooltip = Text;
            var ui = CardEntity?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.Tooltip = Tooltip ?? string.Empty;
            }
        }

        private int GetCursesRemovedThisClimb() =>
            GetCursesRemovedThisClimb(EntityManager?.GetEntity("Player"));

        private static int GetCursesRemovedThisClimb(Entity player)
        {
            var battleState = player?.GetComponent<BattleStateInfo>();
            if (battleState?.RunTracking != null &&
                battleState.RunTracking.TryGetValue(TrackingTypeEnum.CursesRemoved.ToString(), out int removed))
            {
                return removed;
            }

            return 0;
        }

        private int GetAggressionGained(int cursesRemoved) =>
            BaseAggressionGained + cursesRemoved * GetCurseMultiplier(IsUpgraded);

        private static int GetCurseMultiplier(bool isUpgraded) =>
            isUpgraded ? UpgradedCurseMultiplier : BaseCurseMultiplier;

        private static string GetMultiplierText(bool isUpgraded) =>
            isUpgraded ? "thrice" : "twice";

        private static string GetCurseCountText(int cursesRemoved) =>
            $"{cursesRemoved} {(cursesRemoved == 1 ? "curse" : "curses")}";

        private static string GetText(bool isUpgraded, int cursesRemoved) =>
            $"Gain {BaseAggressionGained} + X aggression, where X is {GetMultiplierText(isUpgraded)} the number of curses you have removed this climb.\n\nA random card in your deck becomes cursed. (You have removed {GetCurseCountText(cursesRemoved)})";

        public override void Dispose()
        {
            if (_isSubscribed)
            {
                EventManager.Unsubscribe<TrackingEvent>(OnTrackingEvent);
                _isSubscribed = false;
            }

            base.Dispose();
        }
    }
}
