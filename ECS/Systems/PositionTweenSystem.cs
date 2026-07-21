using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    public class PositionTweenSystem : Core.System
    {
        public PositionTweenSystem(EntityManager em) : base(em) { }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<PositionTween>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var tween = entity.GetComponent<PositionTween>();
            var t = entity.GetComponent<Transform>();
            if (tween == null || t == null) return;

            if (!tween.Initialized)
            {
                tween.Current = t.Position;
                tween.Initialized = true;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float alpha = 1f - (float)System.Math.Exp(-tween.Speed * dt);
            tween.Current = Vector2.Lerp(tween.Current, tween.Target, MathHelper.Clamp(alpha, 0f, 1f));
            t.Position = tween.Current;
        }
    }
}
