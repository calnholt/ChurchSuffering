using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation POIs")]
	public class WayStationPoiDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _climbPoi;
		private readonly Texture2D _achievementPoi;

		[DebugEditable(DisplayName = "Climb POI World X", Step = 10, Min = 0, Max = 4096)]
		public float ClimbPoiWorldX { get; set; } = 1350f;
		[DebugEditable(DisplayName = "Climb POI World Y", Step = 10, Min = 0, Max = 4096)]
		public float ClimbPoiWorldY { get; set; } = 240f;
		[DebugEditable(DisplayName = "Climb POI Icon Size", Step = 2, Min = 40, Max = 360)]
		public int ClimbPoiIconSize { get; set; } = 100;
		[DebugEditable(DisplayName = "Climb POI Hover Scale", Step = 0.01f, Min = 1f, Max = 2f)]
		public float ClimbPoiHoverScale { get; set; } = 1.16f;

		[DebugEditable(DisplayName = "Achievement POI World X", Step = 10, Min = 0, Max = 4096)]
		public float AchievementPoiWorldX { get; set; } = 130f;
		[DebugEditable(DisplayName = "Achievement POI World Y", Step = 10, Min = 0, Max = 4096)]
		public float AchievementPoiWorldY { get; set; } = 650f;
		[DebugEditable(DisplayName = "Achievement POI Icon Size", Step = 2, Min = 40, Max = 360)]
		public int AchievementPoiIconSize { get; set; } = 100;
		[DebugEditable(DisplayName = "Achievement POI Hover Scale", Step = 0.01f, Min = 1f, Max = 2f)]
		public float AchievementPoiHoverScale { get; set; } = 1.16f;

		public WayStationPoiDisplaySystem(
			EntityManager entityManager,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_climbPoi = imageAssets.GetRequiredTexture("waystation/climb-poi");
			_achievementPoi = imageAssets.GetRequiredTexture("waystation/achievement-poi");
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (!IsWayStationScene(scene))
			{
				HidePoi(WayStationSceneConstants.ClimbPoiName);
				HidePoi(WayStationSceneConstants.AchievementPoiName);
				return;
			}

			bool climbVisible = !IsClimbModalOpen();
			SyncPoi(
				WayStationSceneConstants.ClimbPoiName,
				"climb",
				"Start Climb",
				ClimbPoiWorldX,
				ClimbPoiWorldY,
				ClimbPoiIconSize,
				ClimbPoiHoverScale,
				climbVisible);

			SyncPoi(
				WayStationSceneConstants.AchievementPoiName,
				"achievement",
				"Achievements",
				AchievementPoiWorldX,
				AchievementPoiWorldY,
				AchievementPoiIconSize,
				AchievementPoiHoverScale,
				visible: true);

			if (climbVisible && WasClicked(WayStationSceneConstants.ClimbPoiName))
			{
				EventManager.Publish(new OpenWayStationClimbSettingsModalEvent());
			}

			if (WasClicked(WayStationSceneConstants.AchievementPoiName))
			{
				EventManager.Publish(new ShowTransition { Scene = SceneId.Achievement, SkipHold = true });
			}
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			if (!IsWayStationScene(scene)) return;

			DrawPoi(_climbPoi, WayStationSceneConstants.ClimbPoiName);
			DrawPoi(_achievementPoi, WayStationSceneConstants.AchievementPoiName);
		}

		private void SyncPoi(
			string entityName,
			string poiId,
			string tooltipTitle,
			float worldX,
			float worldY,
			int iconSize,
			float hoverScale,
			bool visible)
		{
			var rect = GetPoiBounds(entityName, worldX, worldY, iconSize, hoverScale);
			var entity = EntityManager.GetEntity(entityName);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(entityName);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement { TooltipType = TooltipType.None });
				EntityManager.AddComponent(entity, new PointOfInterest { Id = poiId, Type = PointOfInterestType.Quest });
				EntityManager.AddComponent(entity, new POITitleTooltipSource { Title = tooltipTitle });
			}

			var transform = entity.GetComponent<Transform>();
			transform.Position = new Vector2(rect.X, rect.Y);
			transform.ZOrder = 1200;

			var ui = entity.GetComponent<UIElement>();
			ui.Bounds = rect;
			ui.IsInteractable = visible;
			ui.IsHidden = !visible;
			ui.LayerType = UILayerType.Default;
			ui.TooltipType = TooltipType.None;

			var poi = entity.GetComponent<PointOfInterest>();
			poi.WorldPosition = new Vector2(worldX, worldY);
			poi.DisplayRadius = rect.Width / 2f;
		}

		private void HidePoi(string entityName)
		{
			var ui = EntityManager.GetEntity(entityName)?.GetComponent<UIElement>();
			if (ui == null) return;
			ui.IsInteractable = false;
			ui.IsHidden = true;
		}

		private void DrawPoi(Texture2D texture, string entityName)
		{
			var ui = EntityManager.GetEntity(entityName)?.GetComponent<UIElement>();
			if (ui == null || ui.IsHidden) return;

			Color tint = ui.IsHovered ? Color.White : Color.White * 0.88f;
			_spriteBatch.Draw(texture, ui.Bounds, tint);
		}

		private Rectangle GetPoiBounds(string entityName, float worldX, float worldY, int iconSize, float hoverScale)
		{
			var source = GetMapSource();
			int targetWidth = System.Math.Max(1, source.TargetWidth);
			int targetHeight = System.Math.Max(1, source.TargetHeight);
			float screenX = (worldX - source.Source.X) / System.Math.Max(1f, source.Source.Width) * targetWidth;
			float screenY = (worldY - source.Source.Y) / System.Math.Max(1f, source.Source.Height) * targetHeight;
			float scale = IsHovered(entityName) ? hoverScale : 1f;
			int size = System.Math.Max(1, (int)System.Math.Round(iconSize * scale));
			return new Rectangle(
				(int)System.Math.Round(screenX - size / 2f),
				(int)System.Math.Round(screenY - size / 2f),
				size,
				size);
		}

		private WayStationMapView GetMapSource()
		{
			var view = EntityManager.GetEntity(WayStationSceneConstants.MapViewName)
				?.GetComponent<WayStationMapView>();
			if (view != null && view.Source.Width > 0 && view.Source.Height > 0)
			{
				return view;
			}

			return new WayStationMapView
			{
				Source = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				TargetWidth = Game1.VirtualWidth,
				TargetHeight = Game1.VirtualHeight,
				Zoom = 1f
			};
		}

		private bool IsClimbModalOpen()
		{
			var animation = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)
				?.GetComponent<ModalAnimation>();
			return animation != null && (animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden);
		}

		private bool WasClicked(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsClicked == true;
		}

		private bool IsHovered(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsHovered == true;
		}

		private static bool IsWayStationScene(SceneState scene)
		{
			return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
		}
	}
}
