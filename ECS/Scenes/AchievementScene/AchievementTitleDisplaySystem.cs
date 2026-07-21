using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Achievement Title")]
	public class AchievementTitleDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private float _entryProgress = 1f;
		private bool _wasAchievementScene;

		[DebugEditable(DisplayName = "Title X", Step = 2, Min = 0, Max = 800)]
		public int TitleX { get; set; } = 76;

		[DebugEditable(DisplayName = "Title Y", Step = 2, Min = 0, Max = 300)]
		public int TitleY { get; set; } = 34;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.1f, Max = 1f)]
		public float TitleScale { get; set; } = 0.46f;

		[DebugEditable(DisplayName = "Kicker Scale", Step = 0.01f, Min = 0.05f, Max = 0.4f)]
		public float KickerScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Rule Width", Step = 10, Min = 100, Max = 1200)]
		public int RuleWidth { get; set; } = 650;

		[DebugEditable(DisplayName = "Entry Duration", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float EntryDuration { get; set; } = 0.28f;

		public AchievementTitleDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<LoadSceneEvent>(evt =>
			{
				if (evt.Scene == SceneId.Achievement) _entryProgress = 0f;
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<SceneState>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (entity.GetComponent<SceneState>()?.Current != SceneId.Achievement)
			{
				_wasAchievementScene = false;
				return;
			}
			if (!_wasAchievementScene)
			{
				_wasAchievementScene = true;
				_entryProgress = 0f;
			}
			_entryProgress = MathHelper.Clamp(_entryProgress + (float)gameTime.ElapsedGameTime.TotalSeconds / EntryDuration, 0f, 1f);
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene?.Current != SceneId.Achievement) return;

			float eased = 1f - (float)System.Math.Pow(1f - _entryProgress, 3f);
			float x = MathHelper.Lerp(-420f, TitleX, eased);
			var titlePos = new Vector2(x, TitleY);
			AchievementSceneDrawHelpers.DrawTitleText(_spriteBatch, "Achievements", titlePos, TitleScale, Color.White);
			AchievementSceneDrawHelpers.DrawBodyText(
				_spriteBatch,
				"COLLECTION PROGRESS",
				new Vector2(x + 4f, TitleY + 76f),
				KickerScale,
				AchievementSceneDrawHelpers.MutedWhite);

			int ruleX = (int)x + 4;
			int ruleY = TitleY + 104;
			int width = System.Math.Max(1, (int)(RuleWidth * eased));
			_spriteBatch.Draw(_pixel, new Rectangle(ruleX, ruleY, width, 2), AchievementSceneDrawHelpers.Red);
			_spriteBatch.Draw(_pixel, new Rectangle(ruleX, ruleY + 2, width, 1), AchievementSceneDrawHelpers.RedBright * 0.18f);
		}
	}
}
