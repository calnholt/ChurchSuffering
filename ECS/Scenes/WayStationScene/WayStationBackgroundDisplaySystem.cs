using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation Background")]
	public class WayStationBackgroundDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _background;

		public WayStationBackgroundDisplaySystem(
			EntityManager entityManager,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_background = imageAssets.GetRequiredTexture("waystation");
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			if (!IsWayStationScene(scene)) return;

			_spriteBatch.Draw(
				_background,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				GetMapSource(),
				Color.White);
		}

		private Rectangle GetMapSource()
		{
			var view = EntityManager.GetEntity(WayStationSceneConstants.MapViewName)
				?.GetComponent<WayStationMapView>();
			if (view != null && view.Source.Width > 0 && view.Source.Height > 0)
			{
				return view.Source;
			}

			return ComputeCenteredCoverSource(Game1.VirtualWidth, Game1.VirtualHeight);
		}

		private Rectangle ComputeCenteredCoverSource(int targetWidth, int targetHeight)
		{
			float targetAspect = targetWidth / (float)System.Math.Max(1, targetHeight);
			float textureAspect = _background.Width / (float)_background.Height;
			int coverWidth = _background.Width;
			int coverHeight = _background.Height;
			if (textureAspect > targetAspect)
			{
				coverWidth = (int)System.Math.Round(_background.Height * targetAspect);
			}
			else
			{
				coverHeight = (int)System.Math.Round(_background.Width / targetAspect);
			}

			int x = (_background.Width - coverWidth) / 2;
			int y = (_background.Height - coverHeight) / 2;
			return new Rectangle(x, y, coverWidth, coverHeight);
		}

		private static bool IsWayStationScene(SceneState scene)
		{
			return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
		}
	}
}
