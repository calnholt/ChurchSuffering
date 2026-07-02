using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Location Scene System")]
	public class LocationSceneSystem : Core.System
	{
    private readonly SystemManager _systemManager;
    private readonly World _world;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private readonly ImageAssetService _imageAssets;
    private bool _firstLoad = true;
	private LocationMapDisplaySystem _locationMapDisplaySystem;
	private PointOfInterestDisplaySystem _pointOfInterestDisplaySystem;
	private TooltipQuestDisplaySystem _tooltipQuestDisplaySystem;
	private ShopPOITooltipDisplaySystem _shopPoiTooltipDisplaySystem;
	private TreasurePOITooltipDisplaySystem _treasurePoiTooltipDisplaySystem;
	private FogDisplaySystem _fogDisplaySystem;
	private LocationPoiRevealCutsceneSystem _poiCutsceneSystem;
	private POIRadiusDebugDisplaySystem _poiRadiusDebugDisplaySystem;
	private HellRiftIndicatorDisplaySystem _hellRiftIndicatorDisplaySystem;
	private LoadoutButtonDisplaySystem _loadoutButtonDisplaySystem;
	private AchievementButtonDisplaySystem _achievementButtonDisplaySystem;
	private MiniMapDisplaySystem _miniMapDisplaySystem;
		private RenderTarget2D _sceneRT;
		private int _rtW;
		private int _rtH;

    public LocationSceneSystem(EntityManager entityManager, SystemManager sm, World world, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content, ImageAssetService imageAssets) : base(entityManager)
    {
      _systemManager = sm;
			_world = world;
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			_imageAssets = imageAssets;
      EventManager.Subscribe<LoadSceneEvent>(_ => {
				if (_.Scene == SceneId.Location)
				{
					SaveCache.ClearPendingBattle();
					EventManager.Publish(new ChangeMusicTrack { Track = MusicTrack.Map });
					EventManager.Publish(new UpdateLocationNameEvent { Title = "Desert" });
					AddLocationSystems();
				}
				else
				{
					// Ensure the location name banner is hidden when switching to non-location scenes
					EventManager.Publish(new HideLocationNameEvent());
				}
      });
			EventManager.Subscribe<DeleteCachesEvent>(_ => {
				_world.RemoveSystem(_locationMapDisplaySystem);
				_world.RemoveSystem(_pointOfInterestDisplaySystem);
				_world.RemoveSystem(_tooltipQuestDisplaySystem);
				_world.RemoveSystem(_shopPoiTooltipDisplaySystem);
				_world.RemoveSystem(_treasurePoiTooltipDisplaySystem);
				_world.RemoveSystem(_fogDisplaySystem);
				_world.RemoveSystem(_poiCutsceneSystem);
				_world.RemoveSystem(_poiRadiusDebugDisplaySystem);
				_world.RemoveSystem(_hellRiftIndicatorDisplaySystem);
				_world.RemoveSystem(_loadoutButtonDisplaySystem);
				_world.RemoveSystem(_achievementButtonDisplaySystem);
				_world.RemoveSystem(_miniMapDisplaySystem);
				_firstLoad = true;
				_rtW = 0;
				_rtH = 0;
				_sceneRT?.Dispose();
				_sceneRT = null;
			});
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
      yield break;
    }

		public void Draw()
		{
			// Ensure render target matches current viewport
			var vp = _graphicsDevice.Viewport;
			if (_sceneRT == null || _rtW != vp.Width || _rtH != vp.Height)
			{
				_rtW = vp.Width;
				_rtH = vp.Height;
				_sceneRT?.Dispose();
				_sceneRT = new RenderTarget2D(
					_graphicsDevice,
					_rtW,
					_rtH,
					false,
					SurfaceFormat.Color,
					DepthFormat.None,
					0,
					RenderTargetUsage.DiscardContents
				);
			}

			// Save current SpriteBatch state from Game1 and end it
			var savedBlend = _graphicsDevice.BlendState;
			var savedSampler = _graphicsDevice.SamplerStates[0];
			var savedDepth = _graphicsDevice.DepthStencilState;
			var savedRasterizer = _graphicsDevice.RasterizerState;
			_spriteBatch.End();

			// Capture the location map into the render target (POIs drawn after fog composite)
			var prevTargets = _graphicsDevice.GetRenderTargets();
			_graphicsDevice.SetRenderTarget(_sceneRT);
			_graphicsDevice.Clear(Color.Transparent);
			_spriteBatch.Begin(
				SpriteSortMode.Immediate,
				BlendState.AlphaBlend,
				SamplerState.AnisotropicClamp,
				DepthStencilState.None,
				RasterizerState.CullNone
			);
			FrameProfiler.Measure("LocationMapDisplaySystem.Draw", _locationMapDisplaySystem.Draw);
			_spriteBatch.End();
			// Restore whatever was bound before (offscreen capture or backbuffer)
			if (prevTargets != null && prevTargets.Length > 0)
				_graphicsDevice.SetRenderTargets(prevTargets);
			else
				_graphicsDevice.SetRenderTarget(null);

			// Re-begin SpriteBatch with the original states so FogDisplaySystem can temporarily end it
			_spriteBatch.Begin(
				SpriteSortMode.Immediate,
				savedBlend,
				savedSampler,
				savedDepth,
				savedRasterizer
			);

			// Composite: blit captured map; optional fog shader when ShadersEnabled
			FrameProfiler.Measure("FogDisplaySystem.Composite", () => _fogDisplaySystem.DrawComposite(_sceneRT));
			FrameProfiler.Measure("PointOfInterestDisplaySystem.DrawShopsOverFog", _pointOfInterestDisplaySystem.DrawShopsOverFog);
			FrameProfiler.Measure("PointOfInterestDisplaySystem.DrawQuestPoisOverFog", _pointOfInterestDisplaySystem.DrawQuestPoisOverFog);
			FrameProfiler.Measure("MiniMapDisplaySystem.Draw", _miniMapDisplaySystem.Draw);
			FrameProfiler.Measure("HellRiftIndicatorDisplaySystem.Draw", _hellRiftIndicatorDisplaySystem.Draw);
			FrameProfiler.Measure("POIRadiusDebugDisplaySystem.Draw", _poiRadiusDebugDisplaySystem.Draw);
			FrameProfiler.Measure("ShopPOITooltipDisplaySystem.Draw", _shopPoiTooltipDisplaySystem.Draw);
			FrameProfiler.Measure("TreasurePOITooltipDisplaySystem.Draw", _treasurePoiTooltipDisplaySystem.Draw);
			FrameProfiler.Measure("TooltipQuestDisplaySystem.Draw", _tooltipQuestDisplaySystem.Draw);
			FrameProfiler.Measure("LoadoutButtonDisplaySystem.Draw", _loadoutButtonDisplaySystem.Draw);
			FrameProfiler.Measure("AchievementButtonDisplaySystem.Draw", _achievementButtonDisplaySystem.Draw);
		}
    private void AddLocationSystems()
		{
			if (!_firstLoad) return;
			_firstLoad = false;
			_locationMapDisplaySystem = new LocationMapDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_locationMapDisplaySystem);
			_pointOfInterestDisplaySystem = new PointOfInterestDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_pointOfInterestDisplaySystem);
			_poiCutsceneSystem = new LocationPoiRevealCutsceneSystem(_world.EntityManager);
			_world.AddSystem(_poiCutsceneSystem);
			_fogDisplaySystem = new FogDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_world.AddSystem(_fogDisplaySystem);
			_poiRadiusDebugDisplaySystem = new POIRadiusDebugDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_poiRadiusDebugDisplaySystem);
			_tooltipQuestDisplaySystem = new TooltipQuestDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_tooltipQuestDisplaySystem);
			_shopPoiTooltipDisplaySystem = new ShopPOITooltipDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_shopPoiTooltipDisplaySystem);
			_treasurePoiTooltipDisplaySystem = new TreasurePOITooltipDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_treasurePoiTooltipDisplaySystem);
			_hellRiftIndicatorDisplaySystem = new HellRiftIndicatorDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_hellRiftIndicatorDisplaySystem);
			_loadoutButtonDisplaySystem = new LoadoutButtonDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_loadoutButtonDisplaySystem);
			_achievementButtonDisplaySystem = new AchievementButtonDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_achievementButtonDisplaySystem);
			_miniMapDisplaySystem = new MiniMapDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_miniMapDisplaySystem);
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
      throw new System.NotImplementedException();
    }
  }
}
