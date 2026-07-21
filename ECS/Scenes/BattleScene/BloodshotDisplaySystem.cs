using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Scenes.BattleScene
{
    [DebugTab("Bloodshot Display")]
    public class BloodshotDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _gd;
        private readonly SpriteBatch _sb;
        private readonly ContentManager _content;
        
        private Effect _effect;
        private bool _failed;
        private BloodshotOverlay _overlay;
        private float _timeSeconds;
        private bool _isActive;
        private float _fadeIntensity = 1f;

        // Fade timing
        private float _fadeInDuration = 0.2f;
        private float _fadeOutDuration = 0.2f;

        [DebugEditable(DisplayName = "Active")]
        public bool DebugIsActive
        {
            get => _isActive;
            set => _isActive = value;
        }

        [DebugEditable(DisplayName = "Fade Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float FadeIntensity
        {
            get => _fadeIntensity;
            set => _fadeIntensity = MathHelper.Clamp(value, 0f, 1f);
        }

        [DebugEditable(DisplayName = "Fade In Duration", Step = 0.05f, Min = 0.05f, Max = 2f)]
        public float FadeInDuration
        {
            get => _fadeInDuration;
            set => _fadeInDuration = MathHelper.Max(0.05f, value);
        }

        [DebugEditable(DisplayName = "Fade Out Duration", Step = 0.05f, Min = 0.05f, Max = 2f)]
        public float FadeOutDuration
        {
            get => _fadeOutDuration;
            set => _fadeOutDuration = MathHelper.Max(0.05f, value);
        }

        // === Oval Shape ===
        [DebugEditable(DisplayName = "Oval Horizontal Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float OvalHorizontalScale
        {
            get => _overlay?.OvalHorizontalScale ?? 0.8f;
            set { if (_overlay != null) _overlay.OvalHorizontalScale = value; }
        }

        [DebugEditable(DisplayName = "Oval Vertical Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float OvalVerticalScale
        {
            get => _overlay?.OvalVerticalScale ?? 1.25f;
            set { if (_overlay != null) _overlay.OvalVerticalScale = value; }
        }

        // === Blur Effect ===
        [DebugEditable(DisplayName = "Blur Radius", Step = 0.001f, Min = 0f, Max = 0.05f)]
        public float BlurRadius
        {
            get => _overlay?.BlurRadius ?? 0.006f;
            set { if (_overlay != null) _overlay.BlurRadius = value; }
        }

        [DebugEditable(DisplayName = "Blur Start", Step = 0.05f, Min = 0f, Max = 1f)]
        public float BlurStart
        {
            get => _overlay?.BlurStart ?? 0.4f;
            set { if (_overlay != null) _overlay.BlurStart = value; }
        }

        [DebugEditable(DisplayName = "Blur End", Step = 0.05f, Min = 0f, Max = 1.5f)]
        public float BlurEnd
        {
            get => _overlay?.BlurEnd ?? 0.8f;
            set { if (_overlay != null) _overlay.BlurEnd = value; }
        }

        // === Vein Generation ===
        [DebugEditable(DisplayName = "Vein Base Frequency", Step = 1f, Min = 1f, Max = 50f)]
        public float VeinBaseFrequency
        {
            get => _overlay?.VeinBaseFrequency ?? 10f;
            set { if (_overlay != null) _overlay.VeinBaseFrequency = value; }
        }

        [DebugEditable(DisplayName = "Vein Animation Speed", Step = 0.0001f, Min = 0f, Max = 0.5f)]
        public float VeinAnimationSpeed
        {
            get => _overlay?.VeinAnimationSpeed ?? 0.002f;
            set { if (_overlay != null) _overlay.VeinAnimationSpeed = value; }
        }

        [DebugEditable(DisplayName = "Vein Radial Frequency", Step = 1f, Min = 1f, Max = 30f)]
        public float VeinRadialFrequency
        {
            get => _overlay?.VeinRadialFrequency ?? 8f;
            set { if (_overlay != null) _overlay.VeinRadialFrequency = value; }
        }

        [DebugEditable(DisplayName = "Vein Radial Scale", Step = 1f, Min = 1f, Max = 50f)]
        public float VeinRadialScale
        {
            get => _overlay?.VeinRadialScale ?? 10f;
            set { if (_overlay != null) _overlay.VeinRadialScale = value; }
        }

        [DebugEditable(DisplayName = "Vein Time Scale", Step = 0.1f, Min = 0f, Max = 5f)]
        public float VeinTimeScale
        {
            get => _overlay?.VeinTimeScale ?? 0.5f;
            set { if (_overlay != null) _overlay.VeinTimeScale = value; }
        }

        // === Vein Appearance ===
        [DebugEditable(DisplayName = "Vein Edge Start", Step = 0.05f, Min = 0f, Max = 1f)]
        public float VeinEdgeStart
        {
            get => _overlay?.VeinEdgeStart ?? 0.2f;
            set { if (_overlay != null) _overlay.VeinEdgeStart = value; }
        }

        [DebugEditable(DisplayName = "Vein Edge End", Step = 0.05f, Min = 0f, Max = 1.5f)]
        public float VeinEdgeEnd
        {
            get => _overlay?.VeinEdgeEnd ?? 0.9f;
            set { if (_overlay != null) _overlay.VeinEdgeEnd = value; }
        }

        [DebugEditable(DisplayName = "Vein Sharpness Pow", Step = 0.1f, Min = 0.1f, Max = 5f)]
        public float VeinSharpnessPow
        {
            get => _overlay?.VeinSharpnessPow ?? 1f;
            set { if (_overlay != null) _overlay.VeinSharpnessPow = value; }
        }

        [DebugEditable(DisplayName = "Vein Sharpness Mult", Step = 0.1f, Min = 0.1f, Max = 10f)]
        public float VeinSharpnessMult
        {
            get => _overlay?.VeinSharpnessMult ?? 2f;
            set { if (_overlay != null) _overlay.VeinSharpnessMult = value; }
        }

        [DebugEditable(DisplayName = "Vein Threshold Low", Step = 0.05f, Min = 0f, Max = 1f)]
        public float VeinThresholdLow
        {
            get => _overlay?.VeinThresholdLow ?? 0.3f;
            set { if (_overlay != null) _overlay.VeinThresholdLow = value; }
        }

        [DebugEditable(DisplayName = "Vein Threshold High", Step = 0.05f, Min = 0f, Max = 1f)]
        public float VeinThresholdHigh
        {
            get => _overlay?.VeinThresholdHigh ?? 0.7f;
            set { if (_overlay != null) _overlay.VeinThresholdHigh = value; }
        }

        [DebugEditable(DisplayName = "Vein Color Strength", Step = 0.05f, Min = 0f, Max = 1f)]
        public float VeinColorStrength
        {
            get => _overlay?.VeinColorStrength ?? 0.4f;
            set { if (_overlay != null) _overlay.VeinColorStrength = value; }
        }

        // === Redness Effect ===
        [DebugEditable(DisplayName = "Redness Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RednessIntensity
        {
            get => _overlay?.RednessIntensity ?? 0.75f;
            set { if (_overlay != null) _overlay.RednessIntensity = value; }
        }

        [DebugEditable(DisplayName = "Red Tint R", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RedTintR
        {
            get => _overlay?.RedTintR ?? 1f;
            set { if (_overlay != null) _overlay.RedTintR = value; }
        }

        [DebugEditable(DisplayName = "Red Tint G", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RedTintG
        {
            get => _overlay?.RedTintG ?? 0.7f;
            set { if (_overlay != null) _overlay.RedTintG = value; }
        }

        [DebugEditable(DisplayName = "Red Tint B", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RedTintB
        {
            get => _overlay?.RedTintB ?? 0.7f;
            set { if (_overlay != null) _overlay.RedTintB = value; }
        }

        // === Blood Color ===
        [DebugEditable(DisplayName = "Blood Color R", Step = 0.05f, Min = 0f, Max = 1f)]
        public float BloodColorR
        {
            get => _overlay?.BloodColor.X ?? 1f;
            set { if (_overlay != null) _overlay.BloodColor = new Vector3(value, _overlay.BloodColor.Y, _overlay.BloodColor.Z); }
        }

        [DebugEditable(DisplayName = "Blood Color G", Step = 0.05f, Min = 0f, Max = 1f)]
        public float BloodColorG
        {
            get => _overlay?.BloodColor.Y ?? 0f;
            set { if (_overlay != null) _overlay.BloodColor = new Vector3(_overlay.BloodColor.X, value, _overlay.BloodColor.Z); }
        }

        [DebugEditable(DisplayName = "Blood Color B", Step = 0.05f, Min = 0f, Max = 1f)]
        public float BloodColorB
        {
            get => _overlay?.BloodColor.Z ?? 0f;
            set { if (_overlay != null) _overlay.BloodColor = new Vector3(_overlay.BloodColor.X, _overlay.BloodColor.Y, value); }
        }

        // === Clarity/Blur ===
        [DebugEditable(DisplayName = "Clarity Start", Step = 0.05f, Min = 0f, Max = 1.5f)]
        public float ClarityStart
        {
            get => _overlay?.ClarityStart ?? 0.8f;
            set { if (_overlay != null) _overlay.ClarityStart = value; }
        }

        [DebugEditable(DisplayName = "Clarity End", Step = 0.05f, Min = 0f, Max = 1f)]
        public float ClarityEnd
        {
            get => _overlay?.ClarityEnd ?? 0.2f;
            set { if (_overlay != null) _overlay.ClarityEnd = value; }
        }

        [DebugEditable(DisplayName = "Blur Darkness", Step = 0.05f, Min = 0f, Max = 1f)]
        public float BlurDarkness
        {
            get => _overlay?.BlurDarkness ?? 0.7f;
            set { if (_overlay != null) _overlay.BlurDarkness = value; }
        }

        public BloodshotDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content) 
            : base(entityManager)
        {
            _gd = graphicsDevice;
            _sb = spriteBatch;
            _content = content;

            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged);
            EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCachesEvent);
        }

        private void OnDeleteCachesEvent(DeleteCachesEvent evt)
        {
            LoggingService.Append("BloodshotDisplaySystem.OnDeleteCachesEvent", new System.Text.Json.Nodes.JsonObject());
            _isActive = false;
            _fadeIntensity = 0f;
        }

        private void OnPhaseChanged(ChangeBattlePhaseEvent evt)
        {
            LoggingService.Append("BloodshotDisplaySystem.OnPhaseChanged", new System.Text.Json.Nodes.JsonObject
            {
                ["current"] = evt.Current.ToString()
            });
            _isActive = evt.Current == SubPhase.EnemyStart || evt.Current == SubPhase.EnemyAttack || evt.Current == SubPhase.EnemyEnd || evt.Current == SubPhase.Block || evt.Current == SubPhase.PreBlock;
        }

        public void LoadContent()
        {
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (!ShaderRuntimeOptions.ShadersEnabled) return;
            if (_effect == null)
            {
                try
                {
                    _effect = _content.Load<Effect>("Shaders/Bloodshot");
                }
                catch (Exception e)
                {
                    LoggingService.Append("BloodshotDisplaySystem.EnsureLoaded", new System.Text.Json.Nodes.JsonObject { ["error"] = "Failed to load shader", ["exception"] = e.Message });
                    _effect = null;
                    _failed = true;
                }
            }
            if (_effect != null && _overlay == null)
            {
                _overlay = new BloodshotOverlay(_effect);
                // Initialize with default values
                _overlay.OvalHorizontalScale = 0.8f;
                _overlay.OvalVerticalScale = 1.25f;
                _overlay.BlurRadius = 0.006f;
                _overlay.BlurStart = 0.4f;
                _overlay.BlurEnd = 0.8f;
                _overlay.VeinBaseFrequency = 10f;
                _overlay.VeinAnimationSpeed = 0.002f;
                _overlay.VeinRadialFrequency = 8f;
                _overlay.VeinRadialScale = 10f;
                _overlay.VeinTimeScale = 0.5f;
                _overlay.VeinEdgeStart = 0.2f;
                _overlay.VeinEdgeEnd = 0.9f;
                _overlay.VeinSharpnessPow = 1f;
                _overlay.VeinSharpnessMult = 2f;
                _overlay.VeinThresholdLow = 0.3f;
                _overlay.VeinThresholdHigh = 0.7f;
                _overlay.VeinColorStrength = 0.4f;
                _overlay.RednessIntensity = 0.75f;
                _overlay.RedTintR = 1f;
                _overlay.RedTintG = 0.7f;
                _overlay.RedTintB = 0.7f;
                _overlay.BloodColor = new Vector3(1f, 0f, 0f);
                _overlay.ClarityStart = 0.8f;
                _overlay.ClarityEnd = 0.2f;
                _overlay.BlurDarkness = 0.7f;
            }
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Enumerable.Empty<Entity>();
        }

        public override void Update(GameTime gameTime)
        {
            if (!ShaderRuntimeOptions.ShadersEnabled) return;
            if (_failed) return;
            base.Update(gameTime);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _timeSeconds += MathHelper.Max(0f, dt);
            if (_overlay == null) EnsureLoaded();

            // Animate fade intensity
            if (_isActive)
            {
                // Fade in
                float fadeSpeed = 1f / _fadeInDuration;
                _fadeIntensity = MathHelper.Min(1f, _fadeIntensity + fadeSpeed * dt);
            }
            else
            {
                // Fade out
                float fadeSpeed = 1f / _fadeOutDuration;
                _fadeIntensity = MathHelper.Max(0f, _fadeIntensity - fadeSpeed * dt);
            }
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        /// <summary>
        /// Returns true if the effect should be rendered (either active or still fading out).
        /// </summary>
        public new bool IsActive()
        {
            return ShaderRuntimeOptions.ShadersEnabled && _fadeIntensity > 0.001f;
        }

        /// <summary>
        /// Composites the bloodshot effect over the source texture.
        /// </summary>
        /// <param name="sceneSrc">The source texture (usually the scene render target)</param>
        /// <param name="tempOutput">A temporary render target to draw the effect into</param>
        /// <param name="finalTarget">The final destination (null for backbuffer)</param>
        public void Composite(Texture2D sceneSrc, RenderTarget2D tempOutput, RenderTarget2D finalTarget = null)
        {
            if (!ShaderRuntimeOptions.ShadersEnabled)
            {
                if (sceneSrc == null) return;
                _gd.SetRenderTarget(finalTarget);
                _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                _sb.Draw(sceneSrc, _gd.Viewport.Bounds, Color.White);
                _sb.End();
                return;
            }

            // Skip rendering if fade is fully out
            if (_overlay == null || sceneSrc == null || _fadeIntensity <= 0.001f)
            {
                // Fallback: blit original scene directly to finalTarget
                _gd.SetRenderTarget(finalTarget);
                _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
                _sb.Draw(sceneSrc, _gd.Viewport.Bounds, Color.White);
                _sb.End();
                return;
            }

            // Update shader parameters
            _overlay.Time = _timeSeconds;
            _overlay.FadeIntensity = _fadeIntensity;

            // Render bloodshot effect to temp output
            _gd.SetRenderTarget(tempOutput);
            _gd.Clear(Color.Black);

            _overlay.Begin(_sb);
            _overlay.Draw(_sb, sceneSrc);
            _overlay.End(_sb);

            // Present result to finalTarget (backbuffer if null)
            _gd.SetRenderTarget(finalTarget);
            _sb.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
            _sb.Draw(tempOutput, _gd.Viewport.Bounds, Color.White);
            _sb.End();
        }

        [DebugAction("Toggle Bloodshot")]
        public void Debug_ToggleBloodshot()
        {
            _isActive = !_isActive;
            LoggingService.Append("BloodshotDisplaySystem.Debug_ToggleBloodshot", new System.Text.Json.Nodes.JsonObject { ["action"] = "Bloodshot toggled", ["active"] = _isActive });
        }

        [DebugAction("Reset to Defaults")]
        public void Debug_ResetDefaults()
        {
            if (_overlay == null) return;
            
            _overlay.OvalHorizontalScale = 0.8f;
            _overlay.OvalVerticalScale = 1.25f;
            _overlay.BlurRadius = 0.006f;
            _overlay.BlurStart = 0.4f;
            _overlay.BlurEnd = 0.8f;
            _overlay.VeinBaseFrequency = 10f;
            _overlay.VeinAnimationSpeed = 0.002f;
            _overlay.VeinRadialFrequency = 8f;
            _overlay.VeinRadialScale = 10f;
            _overlay.VeinTimeScale = 0.5f;
            _overlay.VeinEdgeStart = 0.2f;
            _overlay.VeinEdgeEnd = 0.9f;
            _overlay.VeinSharpnessPow = 1f;
            _overlay.VeinSharpnessMult = 2f;
            _overlay.VeinThresholdLow = 0.3f;
            _overlay.VeinThresholdHigh = 0.7f;
            _overlay.VeinColorStrength = 0.4f;
            _overlay.RednessIntensity = 0.75f;
            _overlay.RedTintR = 1f;
            _overlay.RedTintG = 0.7f;
            _overlay.RedTintB = 0.7f;
            _overlay.BloodColor = new Vector3(1f, 0f, 0f);
            _overlay.ClarityStart = 0.8f;
            _overlay.ClarityEnd = 0.2f;
            _overlay.BlurDarkness = 0.7f;

            LoggingService.Append("BloodshotDisplaySystem.Debug_ResetDefaults", new System.Text.Json.Nodes.JsonObject { ["action"] = "Reset all parameters to defaults" });
        }
    }
}
