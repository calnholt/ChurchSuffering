using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Title Menu")]
	public class TitleMenuDisplaySystem : Core.System
	{
		private readonly World _world;
		private readonly SpriteBatch _spriteBatch;
		private float _t;

		[DebugEditable(DisplayName = "Title Text", Step = 1)]
		public string TitleText { get; set; } = "Church\nSuffering";

		[DebugEditable(DisplayName = "Target Width %", Step = 0.05f, Min = 0.1f, Max = 1f)]
		public float TargetWidthPercent { get; set; } = 0.6f;

		[DebugEditable(DisplayName = "Min Text Scale", Step = 0.05f, Min = 0.05f, Max = 2f)]
		public float MinTextScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Max Text Scale", Step = 0.05f, Min = 0.1f, Max = 5f)]
		public float MaxTextScale { get; set; } = 2.0f;

		[DebugEditable(DisplayName = "Fade In Duration (s)", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float FadeInDurationSeconds { get; set; } = 1.5f;

		[DebugEditable(DisplayName = "Text Offset X", Step = 2, Min = -2000, Max = 2000)]
		public int TextOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Text Offset Y", Step = 2, Min = -2000, Max = 2000)]
		public int TextOffsetY { get; set; } = 0;

		[DebugEditable(DisplayName = "Version Padding", Step = 1, Min = 0, Max = 128)]
		public int VersionPadding { get; set; } = 24;

		[DebugEditable(DisplayName = "Version Text Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float VersionTextScale { get; set; } = 0.12f;

		[DebugEditable(DisplayName = "Version Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
		public float VersionAlpha { get; set; } = 0.75f;

		public TitleMenuDisplaySystem(World world, SpriteBatch spriteBatch)
			: base(world.EntityManager)
		{
			_world = world;
			_spriteBatch = spriteBatch;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.TitleMenu)
			{
				// Reset timer when not active so fade restarts upon returning
				_t = 0f;
				// Disable title menu click area if it exists
				var existing = EntityManager.GetEntity("TitleMenu_ClickArea");
				var uiExisting = existing?.GetComponent<UIElement>();
				if (uiExisting != null) uiExisting.IsInteractable = false;
				return;
			}

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			_t += dt;

			// Ensure a full-screen interactable UI element exists for click handling (immediate, no fade gating)
			int w = Game1.VirtualWidth;
			int h = Game1.VirtualHeight;
			var clickArea = EntityManager.GetEntity("TitleMenu_ClickArea");
			if (clickArea == null)
			{
				clickArea = EntityManager.CreateEntity("TitleMenu_ClickArea");
				EntityManager.AddComponent(clickArea, new Transform { Position = new Vector2(0, 0), ZOrder = 10000 });
				EntityManager.AddComponent(clickArea, new UIElement { Bounds = new Rectangle(0, 0, w, h), IsInteractable = true });
			}
			else
			{
				var t = clickArea.GetComponent<Transform>();
				if (t != null) { t.Position = new Vector2(0, 0); t.ZOrder = 10000; }
				var ui = clickArea.GetComponent<UIElement>();
				if (ui == null)
				{
					EntityManager.AddComponent(clickArea, new UIElement { Bounds = new Rectangle(0, 0, w, h), IsInteractable = true });
				}
				else
				{
					ui.Bounds = new Rectangle(0, 0, w, h);
					ui.IsInteractable = true;
				}
			}

			// Use UIElement click flag instead of raw mouse. Trigger transition immediately when clicked.
			var uiClick = clickArea.GetComponent<UIElement>();
			if (uiClick != null && uiClick.IsClicked && !StateSingleton.IsActive)
			{
				TitleMenuResumeService.OnTitleMenuClicked(_world);
			}

		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.TitleMenu) return;

			int w = Game1.VirtualWidth;
			int h = Game1.VirtualHeight;

			// Compute scale from viewport width
			string[] lines = (TitleText ?? string.Empty).Replace("\r", string.Empty).Split('\n');
			var lineSizes = lines.Select(FontSingleton.TitleFont.MeasureString).ToArray();
			float widestLine = lineSizes.Length > 0 ? lineSizes.Max(size => size.X) : 0f;
			float targetWidth = MathHelper.Clamp(TargetWidthPercent, 0.1f, 1f) * w;
			float scale = widestLine > 0.001f ? targetWidth / widestLine : 1f;
			scale = MathHelper.Clamp(scale, System.Math.Max(0.01f, MinTextScale), System.Math.Max(MinTextScale, MaxTextScale));

			// Fade alpha from 0 -> 1
			float denom = System.Math.Max(0.0001f, FadeInDurationSeconds);
			float t01 = MathHelper.Clamp(_t / denom, 0f, 1f);
			var color = Color.White * t01;

			// Center each line independently within the title block.
			float lineHeight = FontSingleton.TitleFont.LineSpacing * scale;
			float blockHeight = lineHeight * lines.Length;
			float startY = h / 2f - blockHeight / 2f + TextOffsetY;
			for (int i = 0; i < lines.Length; i++)
			{
				float lineWidth = lineSizes[i].X * scale;
				var pos = new Vector2(w / 2f - lineWidth / 2f + TextOffsetX, startY + i * lineHeight);
				_spriteBatch.DrawString(FontSingleton.TitleFont, lines[i], pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			}

			DrawVersionLabel(h);
		}

		private void DrawVersionLabel(int viewportHeight)
		{
			var font = FontSingleton.ChakraPetchFont;
			if (font == null) return;

			string label = GameVersionService.DisplayLabel;
			float versionScale = System.Math.Max(0.01f, VersionTextScale);
			Vector2 size = font.MeasureString(label) * versionScale;
			int padding = System.Math.Max(0, VersionPadding);
			var position = new Vector2(padding, viewportHeight - size.Y - padding);
			var versionColor = Color.White * MathHelper.Clamp(VersionAlpha, 0f, 1f);
			_spriteBatch.DrawString(font, label, position, versionColor, 0f, Vector2.Zero, versionScale, SpriteEffects.None, 0f);
		}
	}
}
