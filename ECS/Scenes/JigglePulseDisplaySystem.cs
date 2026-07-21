using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    public class JigglePulseDisplaySystem : Core.System
    {
        private class ActivePulse
        {
            public Vector2 BaseScale;
            public float BaseRotation;
            public JigglePulseConfig Config;
            public float Elapsed;
        }

        private readonly Dictionary<int, ActivePulse> _pulsesByEntityId = new Dictionary<int, ActivePulse>();

        public JigglePulseDisplaySystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<JigglePulseEvent>(OnJigglePulse);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            // Global system; no per-entity iteration needed for UpdateEntity
            return Enumerable.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_pulsesByEntityId.Count > 0)
            {
                var keys = _pulsesByEntityId.Keys.ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    int id = keys[i];
                    if (!_pulsesByEntityId.TryGetValue(id, out var ap)) continue;
                    var e = EntityManager.GetEntity(id);
                    if (e == null || !e.IsActive)
                    {
                        _pulsesByEntityId.Remove(id);
                        continue;
                    }

                    var t = e.GetComponent<Transform>();
                    if (t == null)
                    {
                        _pulsesByEntityId.Remove(id);
                        continue;
                    }

                    ap.Elapsed += dt;
                    float dur = ap.Config.PulseDurationSeconds > 0.01f ? ap.Config.PulseDurationSeconds : 0.01f;
                    float norm = MathHelper.Clamp(ap.Elapsed / dur, 0f, 1f);
                    float env = 1f - norm;
                    env *= env; // quadratic decay
                    float phase = MathHelper.TwoPi * ap.Config.PulseFrequencyHz * ap.Elapsed;
                    float s = (float)System.Math.Sin(phase);
                    float scaleMul = 1f + ap.Config.PulseScaleAmplitude * env * s;
                    float jiggleRad = MathHelper.ToRadians(ap.Config.JiggleDegrees);
                    float rotAdd = jiggleRad * env * (float)System.Math.Sin(phase * 1.2f);

                    t.Scale = new Vector2(ap.BaseScale.X * scaleMul, ap.BaseScale.Y * scaleMul);
                    t.Rotation = ap.BaseRotation + rotAdd;

                    if (ap.Elapsed >= dur)
                    {
                        // Restore and clear
                        t.Scale = ap.BaseScale;
                        t.Rotation = ap.BaseRotation;
                        _pulsesByEntityId.Remove(id);
                    }
                }
            }

            base.Update(gameTime);
        }

        private void OnJigglePulse(JigglePulseEvent evt)
        {
            if (evt?.Target == null) return;
            var t = evt.Target.GetComponent<Transform>();
            if (t == null) return;

            Vector2 baseScale = t.Scale;
            float baseRotation = t.Rotation;

            // If a pulse is already active, preserve its original base values to prevent drift
            if (_pulsesByEntityId.TryGetValue(evt.Target.Id, out var existing))
            {
                baseScale = existing.BaseScale;
                baseRotation = existing.BaseRotation;
            }

            var ap = new ActivePulse
            {
                BaseScale = baseScale,
                BaseRotation = baseRotation,
                Config = evt.Config ?? JigglePulseConfig.Default,
                Elapsed = 0f
            };
            _pulsesByEntityId[evt.Target.Id] = ap; // restart if already present
        }
    }
}






