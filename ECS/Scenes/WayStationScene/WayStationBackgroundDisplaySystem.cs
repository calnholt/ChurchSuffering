using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
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
				WayStationMapSourceService.ComputeCenteredCoverSource(
					_background.Width,
					_background.Height,
					Game1.VirtualWidth,
					Game1.VirtualHeight),
				Color.White);
		}

		private static bool IsWayStationScene(SceneState scene)
		{
			return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
		}
	}
}
