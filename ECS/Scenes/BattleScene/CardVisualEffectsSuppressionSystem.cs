using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Input;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    public sealed class CardVisualEffectsSuppressionSystem : Core.System
    {
        private const float LeftTriggerThreshold = 0.15f;

        private Entity _suppressedCard;

        public CardVisualEffectsSuppressionSystem(EntityManager entityManager)
            : base(entityManager)
        {
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Enumerable.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            Entity target = ResolveSuppressionTarget();
            if (target == null)
            {
                ClearAllSuppression();
                return;
            }

            ClearSuppressionExcept(target);

            if (target == _suppressedCard)
            {
                if (!target.HasComponent<SuppressCardVisualEffects>())
                {
                    EntityManager.AddComponent(target, new SuppressCardVisualEffects());
                }

                return;
            }

            ClearSuppression(_suppressedCard);
            _suppressedCard = target;

            if (!_suppressedCard.HasComponent<SuppressCardVisualEffects>())
            {
                EntityManager.AddComponent(_suppressedCard, new SuppressCardVisualEffects());
            }
        }

        private Entity ResolveSuppressionTarget()
        {
            if (!IsBattleScene())
            {
                ClearAllSuppression();
                return null;
            }

            PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
            bool modifierHeld = frame.LeftTrigger >= LeftTriggerThreshold
                || frame.IsDown(PlayerButton.Shift);
            if (!modifierHeld)
            {
                return null;
            }

            return EntityManager
                .GetEntitiesWithComponent<CardData>()
                .Where(card => card.GetComponent<UIElement>()?.IsHovered == true)
                .OrderByDescending(card => card.GetComponent<Transform>()?.ZOrder ?? 0)
                .FirstOrDefault();
        }

        private bool IsBattleScene()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>()
                ?.Current == SceneId.Battle;
        }

        private void ClearSuppression(Entity card)
        {
            if (card?.HasComponent<SuppressCardVisualEffects>() == true)
            {
                EntityManager.RemoveComponent<SuppressCardVisualEffects>(card);
            }
        }

        private void ClearAllSuppression()
        {
            foreach (var card in EntityManager.GetEntitiesWithComponent<SuppressCardVisualEffects>().ToList())
            {
                EntityManager.RemoveComponent<SuppressCardVisualEffects>(card);
            }

            _suppressedCard = null;
        }

        private void ClearSuppressionExcept(Entity target)
        {
            foreach (var card in EntityManager.GetEntitiesWithComponent<SuppressCardVisualEffects>().ToList())
            {
                if (card != target)
                {
                    EntityManager.RemoveComponent<SuppressCardVisualEffects>(card);
                }
            }
        }
    }
}
