using System;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    [DebugTab("Parallax Layer System")]
    public class ParallaxLayerSystem : Core.System
    {
        private readonly GraphicsDevice _graphics;
        private Vector2 _cursorPos;

        private struct ParallaxState
        {
            public Vector2 Anchor;
            public Vector2 LastWrittenPos;
            public bool Initialized;
        }

        private readonly Dictionary<int, ParallaxState> _states = new();

        public ParallaxLayerSystem(EntityManager em, GraphicsDevice graphics)
            : base(em)
        {
            _graphics = graphics;
            EventManager.Subscribe<CursorStateEvent>(OnCursor);
            EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
        }

        private void OnCursor(CursorStateEvent evt)
        {
            _cursorPos = evt.Position;
        }

        private void OnDeleteCaches(DeleteCachesEvent evt)
        {
            _states.Clear();
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<ParallaxLayer>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var layer = entity.GetComponent<ParallaxLayer>();
            var t = entity.GetComponent<Transform>();
            if (layer == null || t == null) return;

            int id = entity.Id;
            if (!_states.TryGetValue(id, out var state))
            {
                state = new ParallaxState();
            }

            // First frame: anchor on current position
            if (!state.Initialized)
            {
                state.Anchor = t.Position;
                state.Initialized = true;
            }
            // Detect external write: something else moved the entity since our last write
            else if (t.Position != state.LastWrittenPos)
            {
                state.Anchor = t.Position;
            }

            // Compute parallax offset from cursor
            int w = Game1.VirtualWidth;
            int h = Game1.VirtualHeight;
            var center = new Vector2(w / 2f, h / 2f);
            Vector2 delta = center - _cursorPos;
            Vector2 raw = new Vector2(delta.X * layer.MultiplierX, delta.Y * layer.MultiplierY);
            float max = Math.Max(0f, layer.MaxOffset);
            Vector2 offset = ClampMagnitude(raw, max);

            // Smooth toward target
            Vector2 target = state.Anchor + offset;
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float smooth = layer.SmoothTime;
            float a = (smooth <= 0f) ? 1f : (1f - (float)Math.Exp(-dt / smooth));
            Vector2 newPos = Vector2.Lerp(t.Position, target, MathHelper.Clamp(a, 0f, 1f));

            t.Position = newPos;
            state.LastWrittenPos = newPos;
            _states[id] = state;
        }

        private static Vector2 ClampMagnitude(Vector2 v, float maxLen)
        {
            float len = v.Length();
            if (len <= maxLen || len == 0f) return v;
            return v * (maxLen / len);
        }
    }
}
