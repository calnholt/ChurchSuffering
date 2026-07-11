using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Achievement Back Button")]
	public class AchievementBackButtonDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private Entity _buttonEntity;

		[DebugEditable(DisplayName = "Button Width", Step = 5, Min = 100, Max = 400)]
		public int ButtonWidth { get; set; } = 190;

		[DebugEditable(DisplayName = "Button Height", Step = 2, Min = 30, Max = 100)]
		public int ButtonHeight { get; set; } = 52;

		[DebugEditable(DisplayName = "Margin Right", Step = 5, Min = 10, Max = 200)]
		public int MarginRight { get; set; } = 72;

		[DebugEditable(DisplayName = "Margin Top", Step = 5, Min = 10, Max = 200)]
		public int MarginTop { get; set; } = 48;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.01f, Min = 0.05f, Max = 0.4f)]
		public float TextScale { get; set; } = 0.12f;

		public AchievementBackButtonDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<LoadSceneEvent>(evt =>
			{
				if (evt.Scene == SceneId.Achievement) EnsureButtonEntity();
			});
		}

		private Rectangle GetButtonRect() => new(Game1.VirtualWidth - MarginRight - ButtonWidth, MarginTop, ButtonWidth, ButtonHeight);

		private void EnsureButtonEntity()
		{
			if (_buttonEntity != null && EntityManager.GetEntity(_buttonEntity.Name) != null) return;
			_buttonEntity = EntityManager.CreateEntity("AchievementBackButton");
			var rect = GetButtonRect();
			EntityManager.AddComponent(_buttonEntity, new Transform { Position = rect.Center.ToVector2(), ZOrder = 200 });
			EntityManager.AddComponent(_buttonEntity, new UIElement { Bounds = rect, IsInteractable = true, TooltipType = TooltipType.None });
			EntityManager.AddComponent(_buttonEntity, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Below, RequiresHold = true });
			EntityManager.AddComponent(_buttonEntity, new AchievementBackButton());
			EntityManager.AddComponent(_buttonEntity, new OwnedByScene { Scene = SceneId.Achievement });
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<SceneState>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (entity.GetComponent<SceneState>()?.Current != SceneId.Achievement) return;
			EnsureButtonEntity();
			var rect = GetButtonRect();
			var ui = _buttonEntity?.GetComponent<UIElement>();
			var transform = _buttonEntity?.GetComponent<Transform>();
			if (ui == null) return;
			ui.Bounds = rect;
			if (transform != null) transform.Position = rect.Center.ToVector2();
			if (!ui.IsClicked) return;
			EventManager.Publish(new ShowTransition { Scene = SceneId.WayStation, SkipHold = true });
			ui.IsClicked = false;
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene?.Current != SceneId.Achievement) return;
			var ui = _buttonEntity?.GetComponent<UIElement>();
			if (ui == null) return;

			Color fill = ui.IsHovered ? AchievementSceneDrawHelpers.Black4 : AchievementSceneDrawHelpers.Black2;
			Color border = ui.IsHovered ? AchievementSceneDrawHelpers.RedBright : Color.White * 0.48f;
			_spriteBatch.Draw(_pixel, ui.Bounds, fill * 0.92f);
			AchievementSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, ui.Bounds, border, 2);
			const string label = "BACK";
			var size = AchievementSceneDrawHelpers.MeasureBodyText(label, TextScale);
			var pos = new Vector2(ui.Bounds.Center.X - size.X / 2f, ui.Bounds.Center.Y - size.Y / 2f);
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, label, pos, TextScale, AchievementSceneDrawHelpers.WarmWhite);
		}
	}
}
