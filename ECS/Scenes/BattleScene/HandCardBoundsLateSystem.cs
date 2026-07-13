using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Synchronizes hand-card input bounds after position tweening and parallax.
    /// </summary>
    public sealed class HandCardBoundsLateSystem : Core.System
    {
        public HandCardBoundsLateSystem(EntityManager entityManager)
            : base(entityManager)
        {
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive || !IsBattleSceneActive()) return;

            var deck = EntityManager
                .GetEntitiesWithComponent<Deck>()
                .FirstOrDefault()
                ?.GetComponent<Deck>();
            if (deck == null) return;

            CardGeometrySettings settings = CardGeometryService.GetSettings(EntityManager);
            foreach (Entity card in deck.Hand)
            {
                if (!HandStateLoggingService.CountsForHandLayout(card)) continue;

                var transform = card.GetComponent<Transform>();
                var ui = card.GetComponent<UIElement>();
                if (transform == null || ui == null) continue;

                ui.Bounds = CardGeometryService.GetVisualRect(
                    settings,
                    transform.Position,
                    transform.Scale.X);
            }
        }

        private bool IsBattleSceneActive()
        {
            return EntityManager
                .GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>()
                ?.Current == SceneId.Battle;
        }
    }
}
