using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("WayStation Incense")]
    public class IncenseDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ContentManager _content;
        private IncenseOverlay _overlay;
        private bool _failed;
        private bool _wasActive;
        private float _timeSeconds;

        [DebugEditable(DisplayName = "Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
        public float Opacity { get; set; } = 0f;

        [DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
        public float TimeScale { get; set; } = 1f;

        [DebugEditable(DisplayName = "Smoke Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
        public float SmokeScale { get; set; } = 3.2f;

        [DebugEditable(DisplayName = "Warp Strength", Step = 0.01f, Min = 0f, Max = 6f)]
        public float WarpStrength { get; set; } = 2.6f;

        [DebugEditable(DisplayName = "Smoke Low", Step = 0.01f, Min = 0f, Max = 1f)]
        public float SmokeLow { get; set; } = 0.30f;

        [DebugEditable(DisplayName = "Smoke High", Step = 0.01f, Min = 0f, Max = 1f)]
        public float SmokeHigh { get; set; } = 0.85f;

        [DebugEditable(DisplayName = "Depth Parallax", Step = 0.01f, Min = 0f, Max = 2f)]
        public float DepthParallax { get; set; } = 0.55f;

        [DebugEditable(DisplayName = "Rise Speed", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
        public float RiseSpeed { get; set; } = 0.055f;

        [DebugEditable(DisplayName = "Churn Speed", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
        public float ChurnSpeed { get; set; } = 0.040f;

        [DebugEditable(DisplayName = "Drift X", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
        public float DriftX { get; set; } = 0.010f;

        [DebugEditable(DisplayName = "Gloom R", Step = 0.01f, Min = 0f, Max = 1f)]
        public float GloomR { get; set; } = 0.030f;

        [DebugEditable(DisplayName = "Gloom G", Step = 0.01f, Min = 0f, Max = 1f)]
        public float GloomG { get; set; } = 0.034f;

        [DebugEditable(DisplayName = "Gloom B", Step = 0.01f, Min = 0f, Max = 1f)]
        public float GloomB { get; set; } = 0.045f;

        [DebugEditable(DisplayName = "Smoke R", Step = 0.01f, Min = 0f, Max = 1f)]
        public float SmokeR { get; set; } = 0.34f;

        [DebugEditable(DisplayName = "Smoke G", Step = 0.01f, Min = 0f, Max = 1f)]
        public float SmokeG { get; set; } = 0.36f;

        [DebugEditable(DisplayName = "Smoke B", Step = 0.01f, Min = 0f, Max = 1f)]
        public float SmokeB { get; set; } = 0.42f;

        [DebugEditable(DisplayName = "Glint R", Step = 0.01f, Min = 0f, Max = 1f)]
        public float GlintR { get; set; } = 1.00f;

        [DebugEditable(DisplayName = "Glint G", Step = 0.01f, Min = 0f, Max = 1f)]
        public float GlintG { get; set; } = 0.82f;

        [DebugEditable(DisplayName = "Glint B", Step = 0.01f, Min = 0f, Max = 1f)]
        public float GlintB { get; set; } = 0.55f;

        [DebugEditable(DisplayName = "Mote Amount", Step = 0.01f, Min = 0f, Max = 1f)]
        public float MoteAmount { get; set; } = 0.1f;

        [DebugEditable(DisplayName = "Mote Scale", Step = 1f, Min = 1f, Max = 400f)]
        public float MoteScale { get; set; } = 190.0f;

        [DebugEditable(DisplayName = "Mote Drift Min", Step = 0.001f, Min = 0f, Max = 0.5f)]
        public float MoteDriftMin { get; set; } = 0.008f;

        [DebugEditable(DisplayName = "Mote Drift Max", Step = 0.001f, Min = 0f, Max = 0.5f)]
        public float MoteDriftMax { get; set; } = 0.045f;

        [DebugEditable(DisplayName = "Mote Flash Min", Step = 0.01f, Min = 0.01f, Max = 10f)]
        public float MoteFlashMin { get; set; } = 0.6f;

        [DebugEditable(DisplayName = "Mote Flash Max", Step = 0.01f, Min = 0.01f, Max = 10f)]
        public float MoteFlashMax { get; set; } = 4.5f;

        [DebugEditable(DisplayName = "Mote Flash Depth", Step = 0.01f, Min = 0f, Max = 1f)]
        public float MoteFlashDepth { get; set; } = 0.9f;

        [DebugEditable(DisplayName = "Vignette Amount", Step = 0.01f, Min = 0f, Max = 2f)]
        public float VignetteAmount { get; set; } = 1.05f;

        [DebugEditable(DisplayName = "Grain Amount", Step = 0.01f, Min = 0f, Max = 0.2f)]
        public float GrainAmount { get; set; } = 0.035f;

        [DebugEditable(DisplayName = "Exposure", Step = 0.01f, Min = 0f, Max = 3f)]
        public float Exposure { get; set; } = 1.15f;

        public IncenseDisplaySystem(
            EntityManager entityManager,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            ContentManager content)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _content = content;
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
            bool isActive = IsWayStationScene();
            if (!isActive)
            {
                _wasActive = false;
                _timeSeconds = 0f;
                return;
            }

            if (!_wasActive)
            {
                _timeSeconds = 0f;
            }

            _wasActive = true;
            if (ShaderRuntimeOptions.ShadersEnabled && EnsureOverlayLoaded())
            {
                _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds)
                    * MathHelper.Max(0f, TimeScale);
            }

            base.Update(gameTime);
        }

        public void Draw()
        {
            if (!IsWayStationScene() ||
                !ShaderRuntimeOptions.ShadersEnabled ||
                _failed ||
                _overlay?.IsAvailable != true)
            {
                return;
            }

            ConfigureOverlay();
            BlendState savedBlend = _graphicsDevice.BlendState;
            SamplerState savedSampler = _graphicsDevice.SamplerStates[0];
            Texture savedTexture = _graphicsDevice.Textures[0];
            DepthStencilState savedDepth = _graphicsDevice.DepthStencilState;
            RasterizerState savedRasterizer = _graphicsDevice.RasterizerState;

            _spriteBatch.End();
            bool overlayBatchStarted = false;
            try
            {
                _overlay.Begin(_spriteBatch);
                overlayBatchStarted = true;
                _overlay.Draw(_spriteBatch);
            }
            finally
            {
                try
                {
                    if (overlayBatchStarted)
                    {
                        _overlay.End(_spriteBatch);
                    }
                }
                finally
                {
                    _graphicsDevice.Textures[0] = savedTexture;
                    _graphicsDevice.SamplerStates[0] = savedSampler;
                    _spriteBatch.Begin(
                        SpriteSortMode.Immediate,
                        savedBlend,
                        savedSampler,
                        savedDepth,
                        savedRasterizer);
                }
            }
        }

        private bool EnsureOverlayLoaded()
        {
            if (_failed) return false;
            if (_overlay != null) return _overlay.IsAvailable;

            try
            {
                Effect effect = _content.Load<Effect>("Shaders/Incense");
                _overlay = new IncenseOverlay(_graphicsDevice, effect);
            }
            catch (Exception exception)
            {
                _failed = true;
                LoggingService.Append(
                    "IncenseDisplaySystem.EnsureOverlayLoaded",
                    new JsonObject
                    {
                        ["error"] = "Failed to load shader",
                        ["exception"] = exception.Message,
                    });
            }

            return _overlay?.IsAvailable == true;
        }

        private void ConfigureOverlay()
        {
            float smokeLow = MathHelper.Clamp(Math.Min(SmokeLow, SmokeHigh), 0f, 1f);
            float smokeHigh = MathHelper.Clamp(Math.Max(SmokeLow, SmokeHigh), 0f, 1f);
            if (smokeHigh - smokeLow < 0.001f)
            {
                smokeHigh = Math.Min(1f, smokeLow + 0.001f);
                smokeLow = Math.Max(0f, smokeHigh - 0.001f);
            }

            float moteDriftMin = Math.Max(0f, Math.Min(MoteDriftMin, MoteDriftMax));
            float moteDriftMax = Math.Max(moteDriftMin + 0.001f, Math.Max(MoteDriftMin, MoteDriftMax));
            float moteFlashMin = Math.Max(0.001f, Math.Min(MoteFlashMin, MoteFlashMax));
            float moteFlashMax = Math.Max(moteFlashMin + 0.001f, Math.Max(MoteFlashMin, MoteFlashMax));

            _overlay.Time = _timeSeconds;
            _overlay.Opacity = MathHelper.Clamp(Opacity, 0f, 1f);
            _overlay.SmokeScale = Math.Max(0.001f, SmokeScale);
            _overlay.WarpStrength = Math.Max(0f, WarpStrength);
            _overlay.SmokeLow = smokeLow;
            _overlay.SmokeHigh = smokeHigh;
            _overlay.DepthParallax = Math.Max(0f, DepthParallax);
            _overlay.RiseSpeed = RiseSpeed;
            _overlay.ChurnSpeed = ChurnSpeed;
            _overlay.DriftX = DriftX;
            _overlay.GloomColor = new Vector3(GloomR, GloomG, GloomB);
            _overlay.SmokeColor = new Vector3(SmokeR, SmokeG, SmokeB);
            _overlay.GlintColor = new Vector3(GlintR, GlintG, GlintB);
            _overlay.MoteAmount = Math.Max(0f, MoteAmount);
            _overlay.MoteScale = Math.Max(1f, MoteScale);
            _overlay.MoteDriftMin = moteDriftMin;
            _overlay.MoteDriftMax = moteDriftMax;
            _overlay.MoteFlashMin = moteFlashMin;
            _overlay.MoteFlashMax = moteFlashMax;
            _overlay.MoteFlashDepth = MathHelper.Clamp(MoteFlashDepth, 0f, 1f);
            _overlay.VignetteAmount = Math.Max(0f, VignetteAmount);
            _overlay.GrainAmount = Math.Max(0f, GrainAmount);
            _overlay.Exposure = Math.Max(0f, Exposure);
        }

        private bool IsWayStationScene()
        {
            SceneState scene = EntityManager.GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>();
            return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
        }
    }
}
