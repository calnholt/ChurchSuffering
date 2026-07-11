using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Renders a turbulent noise background shader for the Achievement scene.
    /// All parameters are exposed as debug-editable fields for runtime tweaking.
    /// </summary>
    [DebugTab("Achievement Background")]
    public class AchievementBackgroundDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ContentManager _content;
        private readonly Texture2D _pixel;
        private AchievementBackgroundOverlay _overlay;
        private float _timeSeconds;

        // Noise parameters
        [DebugEditable(DisplayName = "Noise Scale", Step = 0.5f, Min = 0.5f, Max = 20f)]
        public float NoiseScale { get; set; } = 3.8f;

        [DebugEditable(DisplayName = "Time Speed", Step = 0.05f, Min = 0f, Max = 2f)]
        public float TimeSpeed { get; set; } = 0.025f;

        // Turbulence parameters
        [DebugEditable(DisplayName = "Turb Initial Inc", Step = 0.05f, Min = 0f, Max = 2f)]
        public float TurbInitialInc { get; set; } = 1.1f;

        [DebugEditable(DisplayName = "Turb Initial Div", Step = 0.05f, Min = 0.5f, Max = 5f)]
        public float TurbInitialDiv { get; set; } = 1.4f;

        [DebugEditable(DisplayName = "Turb Octave Multiplier", Step = 0.1f, Min = 1f, Max = 5f)]
        public float TurbOctaveMultiplier { get; set; } = 2.83f;

        [DebugEditable(DisplayName = "Turb Inc Decay", Step = 0.05f, Min = 0f, Max = 1f)]
        public float TurbIncDecay { get; set; } = 0.5f;

        // UV manipulation
        [DebugEditable(DisplayName = "UV Distort Factor", Step = 0.05f, Min = 0f, Max = 1f)]
        public float UVDistortFactor { get; set; } = 0.0f;

        [DebugEditable(DisplayName = "Rotation Speed", Step = 0.05f, Min = 0f, Max = 2f)]
        public float RotationSpeed { get; set; } = 0.05f;

        [DebugEditable(DisplayName = "Ray Depth", Step = 0.5f, Min = 1f, Max = 20f)]
        public float RayDepth { get; set; } = 1.0f;

        // Color parameters
        [DebugEditable(DisplayName = "Brightness", Step = 0.1f, Min = 0f, Max = 5f)]
        public float ColorBrightness { get; set; } = 0.36f;

        [DebugEditable(DisplayName = "Tint R", Step = 0.05f, Min = 0f, Max = 2f)]
        public float TintR { get; set; } = 1.15f;

        [DebugEditable(DisplayName = "Tint G", Step = 0.05f, Min = 0f, Max = 2f)]
        public float TintG { get; set; } = 0.82f;

        [DebugEditable(DisplayName = "Tint B", Step = 0.05f, Min = 0f, Max = 2f)]
        public float TintB { get; set; } = 0.86f;

        [DebugEditable(DisplayName = "Channel Weight R", Step = 0.1f, Min = 0f, Max = 3f)]
        public float ChannelWeightR { get; set; } = 1.25f;

        [DebugEditable(DisplayName = "Channel Weight G", Step = 0.1f, Min = 0f, Max = 3f)]
        public float ChannelWeightG { get; set; } = 0.72f;

        [DebugEditable(DisplayName = "Channel Weight B", Step = 0.1f, Min = 0f, Max = 3f)]
        public float ChannelWeightB { get; set; } = 0.78f;

        // Vignette parameters
        [DebugEditable(DisplayName = "Vignette Strength", Step = 0.05f, Min = 0f, Max = 1f)]
        public float VignetteStrength { get; set; } = 0.72f;

        [DebugEditable(DisplayName = "Vignette Radius", Step = 0.05f, Min = 0.1f, Max = 1.5f)]
        public float VignetteRadius { get; set; } = 0.75f;

        [DebugEditable(DisplayName = "UI Darken Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float UiDarkenAlpha { get; set; } = 0.38f;

        public AchievementBackgroundDisplaySystem(EntityManager em, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content)
            : base(em)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _content = content;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        public void Draw()
        {
            var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            DrawStaticBackdrop();
            if (!ShaderRuntimeOptions.ShadersEnabled) return;

            EnsureOverlayLoaded();
            if (_overlay == null || !_overlay.IsAvailable) return;

            // Configure overlay with debug-editable values
            _overlay.TimeSeconds = _timeSeconds;
            _overlay.NoiseScale = NoiseScale;
            _overlay.TimeSpeed = TimeSpeed;
            _overlay.TurbInitialInc = TurbInitialInc;
            _overlay.TurbInitialDiv = TurbInitialDiv;
            _overlay.TurbOctaveMultiplier = TurbOctaveMultiplier;
            _overlay.TurbIncDecay = TurbIncDecay;
            _overlay.UVDistortFactor = UVDistortFactor;
            _overlay.RotationSpeed = RotationSpeed;
            _overlay.RayDepth = RayDepth;
            _overlay.ColorBrightness = ColorBrightness;
            _overlay.TintColor = new Vector3(TintR, TintG, TintB);
            _overlay.ChannelWeightR = ChannelWeightR;
            _overlay.ChannelWeightG = ChannelWeightG;
            _overlay.ChannelWeightB = ChannelWeightB;
            _overlay.VignetteStrength = VignetteStrength;
            _overlay.VignetteRadius = VignetteRadius;

            // Save current SpriteBatch device states and temporarily end the batch
            var savedBlend = _graphicsDevice.BlendState;
            var savedSampler = _graphicsDevice.SamplerStates[0];
            var savedDepth = _graphicsDevice.DepthStencilState;
            var savedRasterizer = _graphicsDevice.RasterizerState;
            _spriteBatch.End();

            // Draw overlay with its own begin/end using the effect
            _overlay.Begin(_spriteBatch);
            _overlay.Draw(_spriteBatch);
            _overlay.End(_spriteBatch);

            // Restore the previous SpriteBatch with saved states for subsequent draws
            _spriteBatch.Begin(
                SpriteSortMode.Immediate,
                savedBlend,
                savedSampler,
                savedDepth,
                savedRasterizer
            );

            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * UiDarkenAlpha);
        }

        private void DrawStaticBackdrop()
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), AchievementSceneDrawHelpers.Black0);
            const int strips = 24;
            for (int i = 0; i < strips; i++)
            {
                int x0 = Game1.VirtualWidth * i / strips;
                int x1 = Game1.VirtualWidth * (i + 1) / strips;
                float t = i / (float)(strips - 1);
                float center = 1f - MathHelper.Clamp(System.Math.Abs(t - 0.38f) * 2.2f, 0f, 1f);
                var tint = Color.Lerp(AchievementSceneDrawHelpers.Black0, new Color(34, 10, 14), center) * 0.45f;
                _spriteBatch.Draw(_pixel, new Rectangle(x0, 0, System.Math.Max(1, x1 - x0), Game1.VirtualHeight), tint);
            }
        }

        private void EnsureOverlayLoaded()
        {
            if (!ShaderRuntimeOptions.ShadersEnabled) return;
            if (_overlay != null) return;
            Effect fx = null;
            try
            {
                fx = _content.Load<Effect>("Shaders/AchievementBackground");
            }
            catch { }
            _overlay = new AchievementBackgroundOverlay(_graphicsDevice, fx);
        }
    }
}
