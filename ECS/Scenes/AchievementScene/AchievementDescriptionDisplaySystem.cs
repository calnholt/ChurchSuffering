using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Achievement Description")]
	public class AchievementDescriptionDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private string _currentHoveredId = string.Empty;
		private float _slideProgress;

		[DebugEditable(DisplayName = "Panel Width", Step = 10, Min = 300, Max = 800)]
		public int PanelWidth { get; set; } = 576;

		[DebugEditable(DisplayName = "Panel Height", Step = 10, Min = 260, Max = 700)]
		public int PanelHeight { get; set; } = 430;

		[DebugEditable(DisplayName = "Padding", Step = 2, Min = 8, Max = 60)]
		public int Padding { get; set; } = 32;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.1f, Max = 1f)]
		public float TitleScale { get; set; } = 0.31f;

		[DebugEditable(DisplayName = "Body Scale", Step = 0.01f, Min = 0.05f, Max = 0.4f)]
		public float BodyScale { get; set; } = 0.115f;

		[DebugEditable(DisplayName = "Meta Scale", Step = 0.01f, Min = 0.05f, Max = 0.4f)]
		public float MetaScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Slide Speed", Step = 0.5f, Min = 1f, Max = 30f)]
		public float SlideSpeed { get; set; } = 11f;

		[DebugEditable(DisplayName = "Slide Offset", Step = 10, Min = 20, Max = 400)]
		public int SlideOffset { get; set; } = 180;

		public AchievementDescriptionDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<AchievementGridItemHovered>(OnHovered);
			EventManager.Subscribe<LoadSceneEvent>(evt =>
			{
				if (evt.Scene != SceneId.Achievement) return;
				_currentHoveredId = string.Empty;
				_slideProgress = 0f;
			});
		}

		private void OnHovered(AchievementGridItemHovered evt)
		{
			if (_currentHoveredId == evt.AchievementId) return;
			_currentHoveredId = evt.AchievementId;
			if (!string.IsNullOrEmpty(_currentHoveredId)) _slideProgress = 0f;
		}

		internal void ShowImmediatelyForSnapshot(string achievementId)
		{
			_currentHoveredId = achievementId ?? string.Empty;
			_slideProgress = string.IsNullOrEmpty(_currentHoveredId) ? 0f : 1f;
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<SceneState>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (entity.GetComponent<SceneState>()?.Current != SceneId.Achievement) return;
			float target = string.IsNullOrEmpty(_currentHoveredId) ? 0f : 1f;
			float amount = MathHelper.Clamp((float)gameTime.ElapsedGameTime.TotalSeconds * SlideSpeed, 0f, 1f);
			_slideProgress = MathHelper.Lerp(_slideProgress, target, amount);
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene?.Current != SceneId.Achievement || _slideProgress < 0.01f) return;
			var achievement = AchievementManager.GetAchievement(_currentHoveredId);
			if (achievement == null) return;

			var baseRect = AchievementSceneDrawHelpers.DetailPanel;
			var panel = new Rectangle(
				baseRect.X + (int)((1f - _slideProgress) * SlideOffset),
				baseRect.Y,
				PanelWidth,
				PanelHeight);
			float alpha = MathHelper.Clamp(_slideProgress, 0f, 1f);
			AchievementSceneDrawHelpers.DrawPanel(_spriteBatch, _pixel, panel, alpha);
			DrawContent(achievement, panel, alpha);
		}

		private void DrawContent(AchievementBase achievement, Rectangle panel, float alpha)
		{
			int x = panel.X + Padding;
			int y = panel.Y + Padding;
			int width = panel.Width - Padding * 2;

			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, "ACHIEVEMENT RECORD", new Vector2(x, y), MetaScale, AchievementSceneDrawHelpers.RedBright * alpha);
			y += 34;
			AchievementSceneDrawHelpers.DrawTitleText(_spriteBatch, achievement.Name, new Vector2(x, y), TitleScale, Color.White * alpha);
			y += 72;
			_spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 1), Color.White * (0.12f * alpha));
			y += 22;

			string description = WrapText(achievement.Description, width, BodyScale);
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, description, new Vector2(x, y), BodyScale, AchievementSceneDrawHelpers.WarmWhite * alpha);
			int lineCount = Math.Max(1, description.Count(c => c == '\n') + 1);
			y += lineCount * 28 + 24;

			if (achievement.TargetValue > 0)
			{
				int current = GetAchievementProgress(achievement);
				float progress = MathHelper.Clamp(current / (float)achievement.TargetValue, 0f, 1f);
				AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, "PROGRESS", new Vector2(x, y), MetaScale, AchievementSceneDrawHelpers.MutedWhite * alpha);
				string value = $"{current} / {achievement.TargetValue}";
				var valueSize = AchievementSceneDrawHelpers.MeasureBodyText(value, MetaScale);
				AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, value, new Vector2(panel.Right - Padding - valueSize.X, y), MetaScale, AchievementSceneDrawHelpers.WarmWhite * alpha);
				y += 30;
				var track = new Rectangle(x, y, width, 12);
				_spriteBatch.Draw(_pixel, track, AchievementSceneDrawHelpers.Black3 * alpha);
				AchievementSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, track, Color.White * (0.18f * alpha));
				int fill = (int)(track.Width * progress);
				if (fill > 0) _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y, fill, track.Height), AchievementSceneDrawHelpers.Red * alpha);
			}

			string status = achievement.IsCompleted ? "COMPLETED" : "IN PROGRESS";
			Color statusColor = achievement.IsCompleted ? AchievementSceneDrawHelpers.RedBright : AchievementSceneDrawHelpers.MutedWhite;
			int footerY = panel.Bottom - Padding - 22;
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, status, new Vector2(x, footerY), MetaScale, statusColor * alpha);
			string points = $"+{achievement.Points} ACHIEVEMENT POINTS";
			var pointsSize = AchievementSceneDrawHelpers.MeasureBodyText(points, MetaScale);
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, points, new Vector2(panel.Right - Padding - pointsSize.X, footerY), MetaScale, AchievementSceneDrawHelpers.WarmWhite * alpha);
		}

		private static int GetAchievementProgress(AchievementBase achievement)
		{
			var property = typeof(AchievementBase).GetProperty("Progress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
			return (property?.GetValue(achievement) as AchievementProgress)?.CurrentValue ?? 0;
		}

		private static string WrapText(string text, int maxWidth, float scale)
		{
			var font = FontSingleton.ChakraPetchFont;
			if (font == null || string.IsNullOrWhiteSpace(text)) return text ?? string.Empty;
			var lines = new List<string>();
			string current = string.Empty;
			foreach (string word in AchievementSceneDrawHelpers.ToAscii(text).Split(' ', StringSplitOptions.RemoveEmptyEntries))
			{
				string candidate = string.IsNullOrEmpty(current) ? word : current + " " + word;
				if (font.MeasureString(candidate).X * scale > maxWidth && !string.IsNullOrEmpty(current))
				{
					lines.Add(current);
					current = word;
				}
				else current = candidate;
			}
			if (!string.IsNullOrEmpty(current)) lines.Add(current);
			return string.Join("\n", lines);
		}
	}
}
