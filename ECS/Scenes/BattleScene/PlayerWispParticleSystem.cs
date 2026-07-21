using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Draws glowing red wisps around the player portrait area.
    /// Anchors to the same transform as PlayerDisplaySystem.
    /// </summary>
    [DebugTab("PlayerWisps")] 
    public class PlayerWispParticleSystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;

        private float _elapsedSeconds;

        // Wispy particle settings (runtime adjustable)
        private float _spawnRatePerSecond = 10f;
        [DebugEditable(DisplayName = "Spawn Rate (per sec)", Step = 0.5f, Min = 0f, Max = 200f)]
        public float SpawnRatePerSecond { get => _spawnRatePerSecond; set => _spawnRatePerSecond = MathF.Max(0f, value); }

        // Courage mapping: how much Courage corresponds to maximum particle density
        private int _courageAtMaxWisps = 10;
        [DebugEditable(DisplayName = "Courage At Max Wisps", Step = 1f, Min = 1f, Max = 100f)]
        public int CourageAtMaxWisps { get => _courageAtMaxWisps; set => _courageAtMaxWisps = Math.Max(1, value); }

        private int _maxCount = 150;
        [DebugEditable(DisplayName = "Max Particles", Step = 10f, Min = 0f, Max = 2000f)]
        public int MaxCount { get => _maxCount; set => _maxCount = Math.Max(0, value); }

        private float _minLifetime = 3f;
        private float _maxLifetime = 4f;
        [DebugEditable(DisplayName = "Min Lifetime (s)", Step = 0.1f, Min = 0.05f, Max = 30f)]
        public float MinLifetime { get => _minLifetime; set => _minLifetime = MathF.Max(0.05f, MathF.Min(value, _maxLifetime)); }
        [DebugEditable(DisplayName = "Max Lifetime (s)", Step = 0.1f, Min = 0.05f, Max = 30f)]
        public float MaxLifetime { get => _maxLifetime; set => _maxLifetime = MathF.Max(value, _minLifetime); }

        private float _minSpeed = 40f;   // px/sec upward
        private float _maxSpeed = 56f;
        [DebugEditable(DisplayName = "Min Up Speed", Step = 1f, Min = 0f, Max = 2000f)]
        public float MinSpeed { get => _minSpeed; set => _minSpeed = MathF.Max(0f, MathF.Min(value, _maxSpeed)); }
        [DebugEditable(DisplayName = "Max Up Speed", Step = 1f, Min = 0f, Max = 2000f)]
        public float MaxSpeed { get => _maxSpeed; set => _maxSpeed = MathF.Max(value, _minSpeed); }

        private float _minSwayAmplitude = 5f; // px
        private float _maxSwayAmplitude = 15f;
        [DebugEditable(DisplayName = "Min Sway Amp (px)", Step = 0.5f, Min = 0f, Max = 500f)]
        public float MinSwayAmplitude { get => _minSwayAmplitude; set => _minSwayAmplitude = MathF.Max(0f, MathF.Min(value, _maxSwayAmplitude)); }
        [DebugEditable(DisplayName = "Max Sway Amp (px)", Step = 0.5f, Min = 0f, Max = 500f)]
        public float MaxSwayAmplitude { get => _maxSwayAmplitude; set => _maxSwayAmplitude = MathF.Max(value, _minSwayAmplitude); }

        private float _minSwayHz = 0.7f;
        private float _maxSwayHz = 1f;
        [DebugEditable(DisplayName = "Min Sway Hz", Step = 0.05f, Min = 0f, Max = 10f)]
        public float MinSwayHz { get => _minSwayHz; set => _minSwayHz = MathF.Max(0f, MathF.Min(value, _maxSwayHz)); }
        [DebugEditable(DisplayName = "Max Sway Hz", Step = 0.05f, Min = 0f, Max = 10f)]
        public float MaxSwayHz { get => _maxSwayHz; set => _maxSwayHz = MathF.Max(value, _minSwayHz); }

        // Adjustable visual radius range in pixels (core circle), before glow multiplier
        private float _wispMinRadiusPx = 5f;
        private float _wispMaxRadiusPx = 10f;
        [DebugEditable(DisplayName = "Min Radius (px)", Step = 0.5f, Min = 0.1f, Max = 200f)]
        public float WispMinRadiusPx { get => _wispMinRadiusPx; set => _wispMinRadiusPx = MathF.Max(0.1f, MathF.Min(value, _wispMaxRadiusPx)); }
        [DebugEditable(DisplayName = "Max Radius (px)", Step = 0.5f, Min = 0.1f, Max = 400f)]
        public float WispMaxRadiusPx { get => _wispMaxRadiusPx; set => _wispMaxRadiusPx = MathF.Max(value, _wispMinRadiusPx); }

        // Transparency controls (multipliers applied after lifetime fade)
        private float _wispCoreAlphaMultiplier = 1f;
        private float _wispGlowAlphaMultiplier = 1f;
        [DebugEditable(DisplayName = "Core Alpha Mult", Step = 0.05f, Min = 0f, Max = 2f)]
        public float WispCoreAlphaMultiplier { get => _wispCoreAlphaMultiplier; set => _wispCoreAlphaMultiplier = MathHelper.Clamp(value, 0f, 2f); }
        [DebugEditable(DisplayName = "Glow Alpha Mult", Step = 0.05f, Min = 0f, Max = 2f)]
        public float WispGlowAlphaMultiplier { get => _wispGlowAlphaMultiplier; set => _wispGlowAlphaMultiplier = MathHelper.Clamp(value, 0f, 2f); }

        private readonly List<WispParticle> _wisps = new();
        private readonly Random _random = new Random();
        private float _spawnAccumulator;
        private Texture2D _wispTexture; // soft radial sprite

        private struct WispParticle
        {
            public Vector2 StartPosition;
            public float Age;
            public float Lifetime;
            public float UpwardSpeed;
            public float SwayAmplitude;
            public float SwayAngularVelocity; // radians/sec
            public float SwayPhase;            // radians
            public float SizeScale;
        }

        public PlayerWispParticleSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            // Purely presentational
            return Array.Empty<Entity>();
        }

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _elapsedSeconds += dt;

            // Determine intensity from player's Courage
            float intensity = GetCourageIntensity();
            if (intensity <= 0f)
            {
                _wisps.Clear();
                return;
            }

            EnsureWispTexture();
            if (!TryGetAnchor(out var anchorTransform, out var portraitInfo)) return;

            var portraitPosition = anchorTransform.Position;
            var portraitScale = anchorTransform.Scale.X; // uniform scale
            float texW = portraitInfo?.TextureWidth ?? 0;
            float texH = portraitInfo?.TextureHeight ?? 0;

            // Spawn new wisps based on rate scaled by Courage, accumulating fractional spawns
            float effectiveSpawnRate = _spawnRatePerSecond * intensity;
            _spawnAccumulator += effectiveSpawnRate * dt;
            int toSpawn = (int)_spawnAccumulator;
            if (toSpawn > 0)
            {
                _spawnAccumulator -= toSpawn;
                int effectiveMax = (int)MathF.Round(_maxCount * intensity);
                for (int i = 0; i < toSpawn && _wisps.Count < effectiveMax; i++)
                {
                    SpawnWisp(portraitPosition, portraitScale, texW, texH);
                }
            }

            // Update existing wisps
            for (int i = _wisps.Count - 1; i >= 0; i--)
            {
                var p = _wisps[i];
                p.Age += dt;
                if (p.Age >= p.Lifetime)
                {
                    _wisps.RemoveAt(i);
                    continue;
                }
                // advance sway phase
                p.SwayPhase += p.SwayAngularVelocity * dt;
                _wisps[i] = p;
            }

            base.Update(gameTime);
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public void Draw()
        {
            if (_wispTexture == null) return;
            if (!TryGetAnchor(out var anchorTransform, out var portraitInfo)) return;
            var portraitPosition = anchorTransform.Position;
            var portraitScale = anchorTransform.Scale.X;
            float texW = portraitInfo?.TextureWidth ?? 0;
            float texH = portraitInfo?.TextureHeight ?? 0;
            DrawWisps(portraitPosition, portraitScale, texW, texH);
        }

        private void SpawnWisp(Vector2 portraitPosition, float portraitScale, float texW, float texH)
        {
            // Spawn region: around lower half of portrait, small random ring around center
            float halfW = 0.45f * texW * portraitScale;
            float halfH = 0.4f * texH * portraitScale; // bias lower area
            float rx = (float)(_random.NextDouble() * 2 - 1);
            float ry = (float)(_random.NextDouble() * 2 - 1);
            var spawnOffset = new Vector2(rx * halfW, ry * halfH + 0.2f * texH * portraitScale);

            // Convert desired pixel radius to sprite scale based on radial texture radius
            float texRadius = _wispTexture != null ? _wispTexture.Width / 2f : 24f;
            float desiredRadiusPx = MathHelper.Lerp(_wispMinRadiusPx, _wispMaxRadiusPx, (float)_random.NextDouble())
                                     * (0.7f + 0.3f * portraitScale);
            float sizeScale = desiredRadiusPx / texRadius;

            var p = new WispParticle
            {
                StartPosition = portraitPosition + spawnOffset,
                Age = 0f,
                Lifetime = MathHelper.Lerp(_minLifetime, _maxLifetime, (float)_random.NextDouble()),
                UpwardSpeed = MathHelper.Lerp(_minSpeed, _maxSpeed, (float)_random.NextDouble()),
                SwayAmplitude = MathHelper.Lerp(_minSwayAmplitude, _maxSwayAmplitude, (float)_random.NextDouble()) * (0.6f + 0.4f * portraitScale),
                SwayAngularVelocity = MathHelper.TwoPi * MathHelper.Lerp(_minSwayHz, _maxSwayHz, (float)_random.NextDouble()),
                SwayPhase = MathHelper.TwoPi * (float)_random.NextDouble(),
                SizeScale = sizeScale
            };

            _wisps.Add(p);
        }

        private void DrawWisps(Vector2 portraitPosition, float portraitScale, float texW, float texH)
        {
            if (_wispTexture == null || _wisps.Count == 0) return;

            for (int i = 0; i < _wisps.Count; i++)
            {
                var p = _wisps[i];
                float t = MathHelper.Clamp(p.Age / p.Lifetime, 0f, 1f);

                // position over time: upward drift + horizontal sway
                float up = -p.UpwardSpeed * p.Age; // negative Y is up
                float swayX = p.SwayAmplitude * MathF.Sin(p.SwayPhase);
                var pos = p.StartPosition + new Vector2(swayX, up);

                // ease-in fade and ease-out alpha
                float alphaIn = MathF.Min(1f, t * 3f);
                float alphaOut = 1f - t;
                float alpha = MathF.Pow(alphaIn * alphaOut, 1.2f);

                // color: glowing red
                byte r = 255;
                byte g = 50;
                byte b = 60;

                // scale grows slightly as it rises
                float s = p.SizeScale * (1f + 0.15f * t);
                var colorCore = new Color(r, g, b) * (_wispCoreAlphaMultiplier * alpha);
                var colorGlow = new Color(r, g, b) * (_wispGlowAlphaMultiplier * alpha);

                var originPx = new Vector2(_wispTexture.Width / 2f, _wispTexture.Height / 2f);

                // soft outer glow
                _spriteBatch.Draw(_wispTexture, pos, null, colorGlow, 0f, originPx, s * 1.8f, SpriteEffects.None, 0f);
                // bright core
                _spriteBatch.Draw(_wispTexture, pos, null, colorCore, 0f, originPx, s, SpriteEffects.None, 0f);
            }
        }

        private void EnsureWispTexture()
        {
            if (_wispTexture != null) return;

            // Create a small radial gradient texture (soft circle)
            int size = 48;
            _wispTexture = new Texture2D(_graphicsDevice, size, size, false, SurfaceFormat.Color);
            var data = new Color[size * size];
            float radius = size / 2f;
            var center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float t = MathHelper.Clamp(d / radius, 0f, 1f);
                    // Inverted smoothstep for soft center, feathered edges
                    float a = 1f - t * t * (3f - 2f * t);
                    data[y * size + x] = Color.FromNonPremultiplied(255, 255, 255, (int)(a * 255));
                }
            }
            _wispTexture.SetData(data);
        }

        private float GetCourageIntensity()
        {
            try
            {
                var player = EntityManager.GetEntitiesWithComponent<Components.Player>()
                    .FirstOrDefault(e => e.HasComponent<Components.Courage>());
                if (player == null) return 0f;
                var courage = player.GetComponent<Components.Courage>();
                int amount = Math.Max(0, courage?.Amount ?? 0);
                if (amount <= 0) return 0f;
                return MathHelper.Clamp(amount / (float)_courageAtMaxWisps, 0f, 1f);
            }
            catch
            {
                return 0f;
            }
        }

        private bool TryGetAnchor(out Components.Transform transform, out Components.PortraitInfo info)
        {
            transform = null;
            info = null;
            var anchor = EntityManager.GetEntitiesWithComponent<Components.Player>().FirstOrDefault();
            if (anchor == null) return false;
            transform = anchor.GetComponent<Components.Transform>();
            info = anchor.GetComponent<Components.PortraitInfo>();
            return transform != null;
        }
    }
}
