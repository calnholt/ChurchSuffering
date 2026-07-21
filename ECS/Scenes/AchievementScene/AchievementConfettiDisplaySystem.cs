using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Systems
{
    [DebugTab("Achievement Confetti")]
    public class AchievementConfettiDisplaySystem : Core.System
    {
        private readonly SpriteBatch _spriteBatch;

        private struct ConfettiParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float RotationSpeed;
            public float Size;
            public Color Color;
            public float SwayPhase;
            public float LifeTime;
            public float MaxLifeTime;
        }

        private readonly List<ConfettiParticle> _particles = new();
        private Texture2D _particleTexture;
        private readonly Random _random = new();

        // Debug Properties
        [DebugEditable(DisplayName = "Particle Count", Step = 5, Min = 10, Max = 500)]
        public int ParticleCount { get; set; } = 120;

        [DebugEditable(DisplayName = "Min Lifetime", Step = 0.1f, Min = 0.5f, Max = 5f)]
        public float MinLifetime { get; set; } = 1f;

        [DebugEditable(DisplayName = "Max Lifetime", Step = 0.1f, Min = 0.5f, Max = 8f)]
        public float MaxLifetime { get; set; } = 2.5f;

        [DebugEditable(DisplayName = "Min Size", Step = 1f, Min = 2f, Max = 30f)]
        public float MinSize { get; set; } = 5f;

        [DebugEditable(DisplayName = "Max Size", Step = 1f, Min = 2f, Max = 30f)]
        public float MaxSize { get; set; } = 10f;

        [DebugEditable(DisplayName = "Gravity", Step = 10f, Min = 0f, Max = 1000f)]
        public float Gravity { get; set; } = 220f;

        [DebugEditable(DisplayName = "Initial Speed Min", Step = 10f, Min = 0f, Max = 1000f)]
        public float InitialSpeedMin { get; set; } = 150f;

        [DebugEditable(DisplayName = "Initial Speed Max", Step = 10f, Min = 0f, Max = 1000f)]
        public float InitialSpeedMax { get; set; } = 400f;

        [DebugEditable(DisplayName = "Drag (Air Resistance)", Step = 0.1f, Min = 0f, Max = 10f)]
        public float Drag { get; set; } = 1.5f;

        [DebugEditable(DisplayName = "Sway Amplitude", Step = 1f, Min = 0f, Max = 100f)]
        public float SwayAmplitude { get; set; } = 15f;

        [DebugEditable(DisplayName = "Sway Frequency", Step = 0.1f, Min = 0f, Max = 20f)]
        public float SwayFrequency { get; set; } = 4.5f;

        [DebugEditable(DisplayName = "Blast Radius Min", Step = 5f, Min = 0f, Max = 200f)]
        public float BlastRadiusMin { get; set; } = 0f;

        [DebugEditable(DisplayName = "Blast Radius Max", Step = 5f, Min = 0f, Max = 200f)]
        public float BlastRadiusMax { get; set; } = 35f;

        [DebugEditable(DisplayName = "Color Variance", Step = 0.05f, Min = 0f, Max = 1f)]
        public float ColorVariance { get; set; } = 0.2f;

        public AchievementConfettiDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
        {
            _spriteBatch = sb;
            _particleTexture = new Texture2D(gd, 1, 1);
            _particleTexture.SetData(new[] { Color.White });

            EventManager.Subscribe<AchievementRevealClickedEvent>(OnRevealClicked);
        }

        private void OnRevealClicked(AchievementRevealClickedEvent evt)
        {
            if (evt.IsSmall) return;

            var entity = FindGridEntity(evt.Row, evt.Column);
            if (entity == null) return;

            var transform = entity.GetComponent<Transform>();
            if (transform == null) return;

            SpawnConfetti(transform.Position);
        }

        private void SpawnConfetti(Vector2 origin)
        {
            for (int i = 0; i < ParticleCount; i++)
            {
                // Random offset within blast radius
                float radius = BlastRadiusMin + (float)_random.NextDouble() * (BlastRadiusMax - BlastRadiusMin);
                float radiusAngle = (float)(_random.NextDouble() * Math.PI * 2);
                Vector2 positionOffset = new Vector2(
                    (float)Math.Cos(radiusAngle) * radius,
                    (float)Math.Sin(radiusAngle) * radius
                );

                float angle = (float)(_random.NextDouble() * Math.PI * 2);
                float speed = InitialSpeedMin + (float)_random.NextDouble() * (InitialSpeedMax - InitialSpeedMin);
                
                // Bias angle slightly upwards for explosion feel
                // Map 0..2PI to make it more likely to go up? 
                // Or just purely random direction is fine for "confetti popper" usually goes up, 
                // but "explosion" goes everywhere. Let's stick to full radial burst for now.
                
                Vector2 velocity = new Vector2(
                    (float)Math.Cos(angle) * speed,
                    (float)Math.Sin(angle) * speed
                );

                // If we want a "popper" feel (upward cone), we could restrict angle:
                // float angle = -MathHelper.PiOver2 + ((float)_random.NextDouble() - 0.5f) * 2f; // Upward +/- 60 deg?
                // But the request says "Explosion effect at the location", implying radial.

                float life = MinLifetime + (float)_random.NextDouble() * (MaxLifetime - MinLifetime);
                float size = MinSize + (float)_random.NextDouble() * (MaxSize - MinSize);

                // Match the scene palette: warm white, crimson, or charcoal.
                Color baseColor;
                int colorRoll = _random.Next(3);
                if (colorRoll == 0) baseColor = AchievementSceneDrawHelpers.WarmWhite;
                else if (colorRoll == 1) baseColor = AchievementSceneDrawHelpers.Red;
                else baseColor = AchievementSceneDrawHelpers.Black4;

                // Apply variance
                float rVar = 1f - ColorVariance + (float)_random.NextDouble() * ColorVariance * 2f;
                float gVar = 1f - ColorVariance + (float)_random.NextDouble() * ColorVariance * 2f;
                float bVar = 1f - ColorVariance + (float)_random.NextDouble() * ColorVariance * 2f;
                
                Color particleColor = new Color(
                    (int)MathHelper.Clamp(baseColor.R * rVar, 0, 255),
                    (int)MathHelper.Clamp(baseColor.G * gVar, 0, 255),
                    (int)MathHelper.Clamp(baseColor.B * bVar, 0, 255)
                );

                _particles.Add(new ConfettiParticle
                {
                    Position = origin + positionOffset,
                    Velocity = velocity,
                    Rotation = (float)(_random.NextDouble() * Math.PI * 2),
                    RotationSpeed = ((float)_random.NextDouble() - 0.5f) * 10f,
                    Size = size,
                    Color = particleColor,
                    SwayPhase = (float)(_random.NextDouble() * Math.PI * 2),
                    LifeTime = life,
                    MaxLifeTime = life
                });
            }
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            // We don't iterate entities in UpdateEntity, we manage our own list
            return new List<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            // Not used
        }

        // We need to override Update to process particles since we don't rely on ECS entities for particles
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            for (int i = _particles.Count - 1; i >= 0; i--)
            {
                var p = _particles[i];
                
                // Update Lifetime
                p.LifeTime -= dt;
                if (p.LifeTime <= 0)
                {
                    _particles.RemoveAt(i);
                    continue;
                }

                // Physics
                p.Velocity.Y += Gravity * dt; // Gravity
                p.Velocity -= p.Velocity * Drag * dt; // Air resistance

                // Sway logic (add horizontal oscillation)
                // We add a velocity component based on sine wave, or modify position directly?
                // Modifying position directly creates a cleaner "floating paper" feel.
                float sway = (float)Math.Sin(gameTime.TotalGameTime.TotalSeconds * SwayFrequency + p.SwayPhase) * SwayAmplitude * dt;
                p.Position.X += sway;

                p.Position += p.Velocity * dt;
                p.Rotation += p.RotationSpeed * dt;

                _particles[i] = p; // Update struct in list
            }
        }

        public void Draw()
        {
            if (_particles.Count == 0 || _particleTexture == null) return;
            var scene = EntityManager.GetEntitiesWithComponent<SceneState>();
            foreach (var entity in scene)
            {
                if (entity.GetComponent<SceneState>()?.Current != SceneId.Achievement) return;
                break;
            }
            
            foreach (var p in _particles)
            {
                // Fade out near end of life
                float alpha = 1f;
                if (p.LifeTime < 0.5f)
                {
                    alpha = p.LifeTime / 0.5f;
                }

                Vector2 origin = new Vector2(0.5f, 0.5f);
                var scale = new Vector2(p.Size * 0.45f, p.Size * 1.35f);

                _spriteBatch.Draw(
                    _particleTexture,
                    p.Position,
                    null,
                    p.Color * alpha,
                    p.Rotation,
                    origin,
                    scale,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        [DebugAction("Spawn Confetti")]
        public void DebugSpawnConfetti()
        {
            // Get a random grid entity and spawn confetti at its position
            int randomRow = _random.Next(AchievementGridDisplaySystem.GRID_ROWS);
            int randomCol = _random.Next(AchievementGridDisplaySystem.GRID_COLUMNS);
            
            var gridEntity = FindGridEntity(randomRow, randomCol);
            if (gridEntity != null)
            {
                var transform = gridEntity.GetComponent<Transform>();
                if (transform != null)
                {
                    SpawnConfetti(transform.Position);
                }
            }
        }

        private Entity FindGridEntity(int row, int col)
        {
            foreach (var entity in EntityManager.GetEntitiesWithComponent<AchievementGridItem>())
            {
                var item = entity.GetComponent<AchievementGridItem>();
                if (item?.Row == row && item.Column == col) return entity;
            }
            return null;
        }
    }
}
