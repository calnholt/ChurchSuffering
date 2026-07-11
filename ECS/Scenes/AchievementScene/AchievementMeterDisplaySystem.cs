using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays the achievement points meter at the bottom of the screen.
    /// </summary>
    [DebugTab("Achievement Meter")]
    public class AchievementMeterDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font = FontSingleton.ContentFont;

        private Texture2D _barBackgroundTex;
        private Texture2D _barFillTex;
		private int _cachedBarW, _cachedBarH, _cachedBarR;
		private Texture2D _claimButtonTexture;
		private Entity _claimButtonEntity;

        private float _displayedProgress = 0f; // For smooth animation
        private float _targetProgress = 0f;

        // Meter configuration
        [DebugEditable(DisplayName = "Bar X", Step = 10, Min = 50, Max = 500)]
        public int BarX { get; set; } = 150;

        [DebugEditable(DisplayName = "Bar Y", Step = 10, Min = 600, Max = 1000)]
        public int BarY { get; set; } = 950;

        [DebugEditable(DisplayName = "Bar Width", Step = 20, Min = 400, Max = 1600)]
        public int BarWidth { get; set; } = 1600;

        [DebugEditable(DisplayName = "Bar Height", Step = 2, Min = 16, Max = 60)]
        public int BarHeight { get; set; } = 50;

        [DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 20)]
        public int CornerRadius { get; set; } = 8;

		[DebugEditable(DisplayName = "Claim Button Width", Step = 10, Min = 180, Max = 700)]
		public int ClaimButtonWidth { get; set; } = 360;

		[DebugEditable(DisplayName = "Claim Button Height", Step = 2, Min = 24, Max = 100)]
		public int ClaimButtonHeight { get; set; } = 44;

        [DebugEditable(DisplayName = "Label Scale", Step = 0.01f, Min = 0.1f, Max = 0.4f)]
        public float LabelScale { get; set; } = 0.18f;

        [DebugEditable(DisplayName = "Animation Speed", Step = 0.5f, Min = 1f, Max = 10f)]
        public float AnimationSpeed { get; set; } = 4f;

        // Colors
        private readonly Color _backgroundColor = new Color(30, 30, 30);
        private readonly Color _fillColor = new Color(180, 50, 50); // Brick red
        private readonly Color _textColor = Color.Black;

        public AchievementMeterDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
        {
            _graphicsDevice = gd;
            _spriteBatch = sb;

            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
        }

        private void OnLoadScene(LoadSceneEvent evt)
        {
            if (evt.Scene == SceneId.Achievement)
            {
                // Reset animation to start from current progress
                _displayedProgress = CalculateProgress();
                _targetProgress = _displayedProgress;
				EnsureClaimButton();
            }
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var scene = entity.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Update target progress
            _targetProgress = CalculateProgress();

            // Animate towards target
            _displayedProgress = MathHelper.Lerp(_displayedProgress, _targetProgress, dt * AnimationSpeed);
			UpdateClaimButton();
        }

        private float CalculateProgress()
        {
            var state = CollectionProgressionRules.GetLevelState(GetTotalPoints());
            return (float)state.PointsInLevel / state.PointsRequired;
        }

        private int GetTotalPoints()
        {
            return SaveCache.GetCollection().totalPoints;
        }

        private int GetCurrentLevel()
        {
            return CollectionProgressionRules.GetLevelState(GetTotalPoints()).Level;
        }

        public void Draw()
        {
            var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            EnsureTextures();

            // Draw background bar
            _spriteBatch.Draw(_barBackgroundTex, new Rectangle(BarX, BarY, BarWidth, BarHeight), _backgroundColor);

            // Draw fill bar
            int fillWidth = Math.Max(CornerRadius * 2, (int)(BarWidth * _displayedProgress));
            if (fillWidth > CornerRadius * 2 && _displayedProgress > 0.01f)
            {
                var fillTex = GetOrCreateBar(fillWidth, BarHeight, CornerRadius);
                _spriteBatch.Draw(fillTex, new Rectangle(BarX, BarY, fillWidth, BarHeight), _fillColor);
            }

            // Draw border/outline
            DrawBorder();

            // Draw labels
            DrawLabels();
			DrawClaimButton();
        }

        private void EnsureTextures()
        {
            if (_barBackgroundTex == null || _cachedBarW != BarWidth || _cachedBarH != BarHeight || _cachedBarR != CornerRadius)
            {
                _barBackgroundTex?.Dispose();
                _barFillTex?.Dispose();
                _barBackgroundTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, BarWidth, BarHeight, CornerRadius);
                _barFillTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, BarWidth, BarHeight, CornerRadius);
                _cachedBarW = BarWidth;
                _cachedBarH = BarHeight;
                _cachedBarR = CornerRadius;
            }
        }

        private Texture2D GetOrCreateBar(int width, int height, int radius)
        {
            // For dynamic fill widths, create on demand
            return RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
        }

        private void DrawBorder()
        {
            // Draw subtle border lines at top and bottom of bar
            var borderColor = new Color(80, 80, 80);
            
            // Use the background texture with a tint for the border effect
            // This creates a slight outline effect
        }

        private void DrawLabels()
        {
            if (_font == null) return;

            int totalPoints = GetTotalPoints();
            int currentLevel = GetCurrentLevel();
            var levelState = CollectionProgressionRules.GetLevelState(totalPoints);

            // Draw level label on the left
            string levelText = $"Level {currentLevel}";
            var levelSize = _font.MeasureString(levelText) * LabelScale;
            float levelX = BarX;
            float levelY = BarY - levelSize.Y - 4;
            _spriteBatch.DrawString(_font, levelText, new Vector2(levelX, levelY), _textColor, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);

            // Draw points label on the right
            string pointsText = $"{levelState.PointsInLevel} / {levelState.PointsRequired}";
            var pointsSize = _font.MeasureString(pointsText) * LabelScale;
            float pointsX = BarX + BarWidth - pointsSize.X;
            float pointsY = BarY - pointsSize.Y - 4;
            _spriteBatch.DrawString(_font, pointsText, new Vector2(pointsX, pointsY), _textColor, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);

            // Draw total points below the bar
            string totalText = $"Total: {totalPoints} points";
            var totalSize = _font.MeasureString(totalText) * (LabelScale * 0.85f);
            float totalX = BarX + (BarWidth - totalSize.X) / 2f;
            float totalY = BarY + BarHeight + 6;
            _spriteBatch.DrawString(_font, totalText, new Vector2(totalX, totalY), _textColor, 0f, Vector2.Zero, LabelScale * 0.85f, SpriteEffects.None, 0f);
        }

		private void EnsureClaimButton()
		{
			if (_claimButtonEntity != null && EntityManager.GetEntity(_claimButtonEntity.Name) != null) return;
			_claimButtonEntity = EntityManager.CreateEntity("AchievementClaimClimbPointsButton");
			EntityManager.AddComponent(_claimButtonEntity, new Transform { ZOrder = 205 });
			EntityManager.AddComponent(_claimButtonEntity, new UIElement { TooltipType = TooltipType.None });
			EntityManager.AddComponent(_claimButtonEntity, new OwnedByScene { Scene = SceneId.Achievement });
		}

		private Rectangle GetClaimButtonRect()
		{
			return new Rectangle(
				BarX + BarWidth - ClaimButtonWidth,
				BarY - ClaimButtonHeight - 40,
				ClaimButtonWidth,
				ClaimButtonHeight);
		}

		private void UpdateClaimButton()
		{
			EnsureClaimButton();
			var ui = _claimButtonEntity?.GetComponent<UIElement>();
			if (ui == null) return;
			ui.Bounds = GetClaimButtonRect();
			int pending = SaveCache.GetCollection().pendingClimbPoints;
			ui.IsHidden = false;
			ui.IsInteractable = pending > 0;
			if (ui.IsClicked && pending > 0)
			{
				EventManager.Publish(new ClaimPendingClimbPointsEvent());
				ui.IsClicked = false;
			}
		}

		private void DrawClaimButton()
		{
			var ui = _claimButtonEntity?.GetComponent<UIElement>();
			if (ui == null || ui.IsHidden) return;
			int pending = SaveCache.GetCollection().pendingClimbPoints;
			bool enabled = pending > 0;
			_claimButtonTexture ??= RoundedRectTextureFactory.CreateRoundedRect(
				_graphicsDevice,
				ClaimButtonWidth,
				ClaimButtonHeight,
				Math.Min(CornerRadius, ClaimButtonHeight / 2));
			var fill = enabled ? new Color(132, 38, 38) : new Color(55, 55, 55);
			_spriteBatch.Draw(_claimButtonTexture, ui.Bounds, fill);
			string label = enabled ? $"CLAIM +{pending} CLIMB POINTS" : "NO CLIMB POINTS";
			var size = _font.MeasureString(label) * LabelScale;
			var position = new Vector2(ui.Bounds.Center.X - size.X / 2f, ui.Bounds.Center.Y - size.Y / 2f);
			_spriteBatch.DrawString(_font, label, position, enabled ? Color.White : new Color(150, 150, 150), 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);
		}
    }
}
