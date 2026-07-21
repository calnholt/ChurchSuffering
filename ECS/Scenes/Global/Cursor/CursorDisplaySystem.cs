using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    [DebugTab("Cursor Display")]
    public class CursorDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly Texture2D _cursorCross;
        private PlayerInputState _state;
        private Entity _previousTarget;
        private float _scale = 1f;
        private float _pulseTimer;

        [DebugEditable(DisplayName = "Cursor Radius (px)", Step = 1f, Min = 2f, Max = 256f)]
        public int CursorRadius { get; set; } = 26;

        [DebugEditable(DisplayName = "Cursor Opacity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float CursorOpacity { get; set; } = 0.45f;

        [DebugEditable(DisplayName = "Cross Opacity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float CrossOpacity { get; set; } = 0.9f;

        [DebugEditable(DisplayName = "Cross Scale Multiplier", Step = 0.05f, Min = 0.25f, Max = 3f)]
        public float CrossScale { get; set; } = 1f;

        [DebugEditable(DisplayName = "Cross Anim Speed", Step = 1f, Min = 1f, Max = 60f)]
        public float CrossAnimSpeed { get; set; } = 16f;

        public CursorDisplaySystem(
            EntityManager entityManager,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _cursorCross = imageAssets.GetRequiredTexture("cursor_cross");
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<PlayerInputState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            _state = entity.GetComponent<PlayerInputState>();
            Entity target = _state?.CursorTarget.Entity;
            if (target != _previousTarget)
            {
                _pulseTimer = 0.06f;
                _previousTarget = target;
            }

            float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float targetScale = target == null ? 1f : (_pulseTimer > 0f ? 0.84f : 0.9f);
            _scale += (targetScale - _scale) * MathHelper.Clamp(elapsed * CrossAnimSpeed, 0f, 1f);
            _pulseTimer = Math.Max(0f, _pulseTimer - elapsed);
        }

        public void Draw()
        {
            if (_state == null) return;
            Vector2 position = _state.Frame.PointerPosition;
            int radius = Math.Max(1, CursorRadius);
            Texture2D circleTexture = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
            byte discAlpha = (byte)Math.Round(MathHelper.Clamp(CursorOpacity, 0f, 1f) * 255f);
            Color discColor = Color.FromNonPremultiplied(255, 255, 255, discAlpha);
            _spriteBatch.Draw(
                circleTexture,
                position,
                null,
                discColor,
                0f,
                new Vector2(radius, radius),
                1f,
                SpriteEffects.None,
                0f);

            Vector2 origin = new(_cursorCross.Width / 2f, _cursorCross.Height / 2f);
            float fit = radius * 2f / Math.Max(_cursorCross.Width, _cursorCross.Height) * 0.75f;
            byte crossAlpha = (byte)Math.Round(MathHelper.Clamp(CrossOpacity, 0f, 1f) * 255f);
            Color crossColor = Color.FromNonPremultiplied(255, 255, 255, crossAlpha);
            _spriteBatch.Draw(
                _cursorCross,
                position,
                null,
                crossColor,
                0f,
                origin,
                fit * CrossScale * _scale,
                SpriteEffects.None,
                0f);
        }
    }
}
