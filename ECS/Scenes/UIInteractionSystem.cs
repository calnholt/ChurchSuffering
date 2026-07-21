using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    public class UIInteractionSystem : Core.System
    {
        private Entity _previousHoverFeedbackTarget;

        public UIInteractionSystem(EntityManager entityManager) : base(entityManager)
        {
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<UIElement>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;

            foreach (Entity entity in GetRelevantEntities())
            {
                UIElement ui = entity.GetComponent<UIElement>();
                ui.IsHovered = false;
                ui.IsClicked = false;
            }

            if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager))
            {
                ResetHoverFeedbackTarget();
                return;
            }

            PlayerInputState input = EntityManager
                .GetEntitiesWithComponent<PlayerInputState>()
                .FirstOrDefault()
                ?.GetComponent<PlayerInputState>();
            if (input == null || !input.IsCursorInteractionEnabled)
            {
                ResetHoverFeedbackTarget();
                return;
            }

            Entity target = input.CursorTarget.Entity;
            UIElement targetUi = target?.GetComponent<UIElement>();
            if (targetUi == null || targetUi.IsHidden)
            {
                ResetHoverFeedbackTarget();
                return;
            }

            targetUi.IsHovered = true;
            PlayerInputFrame frame = input.Frame;
            PublishHoverFeedback(target, targetUi, frame.Device);
            bool primary = frame.WasPressed(PlayerButton.Primary);
            bool secondary = frame.WasPressed(PlayerButton.Secondary);
            if (!primary && !secondary) return;
            string contextId = InputContextResolver.ResolveCursorContext(
                EntityManager,
                frame.PointerPosition);
            bool gameplayBlocked = contextId == InputContextIds.Gameplay
                && (StateSingleton.PreventClicking || IsBattleAnimationActive());

            if (!targetUi.IsInteractable
                || targetUi.IsPreventDefaultClick
                || gameplayBlocked
                || (StateSingleton.IsTutorialActive && !target.HasComponent<TutorialInteractionPermitted>()))
            {
                LoggingService.Append("UIInteractionSystem_ClickBlocked", new JsonObject
                {
                    ["entityId"] = target.Id,
                    ["isInteractable"] = targetUi.IsInteractable,
                    ["preventClicking"] = gameplayBlocked,
                    ["isTutorialActive"] = StateSingleton.IsTutorialActive,
                });
                return;
            }

            UIElementEventType eventType = primary
                ? targetUi.EventType
                : targetUi.SecondaryEventType;
            if (primary)
            {
                targetUi.IsClicked = true;
            }

            if (eventType != UIElementEventType.None)
            {
                UIElementEventDelegateService.HandleEvent(eventType, target, EntityManager);
            }
        }

        private void PublishHoverFeedback(
            Entity target,
            UIElement targetUi,
            PlayerInputDevice source)
        {
            Entity feedbackTarget = targetUi.IsInteractable && !targetUi.IsHidden
                ? target
                : null;
            if (ReferenceEquals(feedbackTarget, _previousHoverFeedbackTarget)) return;

            _previousHoverFeedbackTarget = feedbackTarget;
            if (feedbackTarget == null) return;

            EventManager.Publish(new UIElementHoverEnteredEvent
            {
                Entity = feedbackTarget,
                Source = source,
            });
            EventManager.Publish(new PlaySfxEvent
            {
                Track = SfxTrack.Interface,
                Volume = 0.05f,
            });
        }

        private void ResetHoverFeedbackTarget()
        {
            _previousHoverFeedbackTarget = null;
        }

        private bool IsBattleAnimationActive()
        {
            var phase = EntityManager
                .GetEntitiesWithComponent<PhaseState>()
                .FirstOrDefault()
                ?.GetComponent<PhaseState>();
            return phase?.BattleAnimationActive == true;
        }
    }
}
