using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation POIs")]
	public class WayStationPoiDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _background;
		private readonly Texture2D _climbPoi;
		private readonly Texture2D _achievementPoi;
		private readonly Texture2D _achievementBadgeCircle;
		private readonly Texture2D _medalPoi;
		private readonly SpriteFont _achievementBadgeFont;

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
		[DebugEditable(DisplayName = "Achievement Badge Size", Step = 2, Min = 16, Max = 80)]
		public int AchievementBadgeSize { get; set; } = 34;
		[DebugEditable(DisplayName = "Achievement Badge X Offset", Step = 1, Min = -80, Max = 80)]
		public int AchievementBadgeOffsetX { get; set; } = -4;
		[DebugEditable(DisplayName = "Achievement Badge Y Offset", Step = 1, Min = -80, Max = 80)]
		public int AchievementBadgeOffsetY { get; set; } = -4;
		[DebugEditable(DisplayName = "Achievement Badge Text Scale", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float AchievementBadgeTextScale { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Medal POI Right Margin", Step = 2, Min = 0, Max = 600)]
		public int MedalPoiRightMargin { get; set; } = 48;
		[DebugEditable(DisplayName = "Medal POI Bottom Margin", Step = 2, Min = 0, Max = 600)]
		public int MedalPoiBottomMargin { get; set; } = 48;
		[DebugEditable(DisplayName = "Medal POI Icon Size", Step = 2, Min = 40, Max = 360)]
		public int MedalPoiIconSize { get; set; } = 128;
		[DebugEditable(DisplayName = "Medal POI Hover Scale", Step = 0.01f, Min = 1f, Max = 2f)]
		public float MedalPoiHoverScale { get; set; } = 1.12f;

		public WayStationPoiDisplaySystem(
			EntityManager entityManager,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_background = imageAssets.GetRequiredTexture("waystation");
			_climbPoi = imageAssets.GetRequiredTexture("waystation/climb-poi");
			_achievementPoi = imageAssets.GetRequiredTexture("waystation/achievement-poi");
			_achievementBadgeCircle = imageAssets.GetAntiAliasedCircle(AchievementBadgeSize / 2);
			_medalPoi = imageAssets.GetRequiredTexture("waystation/medal-poi");
			_achievementBadgeFont = FontSingleton.TitleFont;
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
				HidePoi(WayStationSceneConstants.SaintsMedalsPoiName);
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

			SyncScreenPoi(
				WayStationSceneConstants.SaintsMedalsPoiName,
				"saints-medals",
				"Saints Medals",
				MedalPoiRightMargin,
				MedalPoiBottomMargin,
				MedalPoiIconSize,
				MedalPoiHoverScale,
				visible: HasPurchasedAnyMedals());

			if (climbVisible && WasClicked(WayStationSceneConstants.ClimbPoiName))
			{
				EventManager.Publish(new OpenWayStationClimbSettingsModalEvent());
			}

			if (WasClicked(WayStationSceneConstants.AchievementPoiName))
			{
				EventManager.Publish(new ShowTransition { Scene = SceneId.Achievement, SkipHold = true });
			}

			if (HasPurchasedAnyMedals() && WasClicked(WayStationSceneConstants.SaintsMedalsPoiName))
			{
				EventManager.Publish(new OpenWayStationSaintsMedalsModalEvent());
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
			DrawAchievementBadge();
			DrawPoi(_medalPoi, WayStationSceneConstants.SaintsMedalsPoiName);
		}

		private void DrawAchievementBadge()
		{
			var ui = EntityManager.GetEntity(WayStationSceneConstants.AchievementPoiName)?.GetComponent<UIElement>();
			if (ui == null || ui.IsHidden || !HasAchievementRewardsToClaim()) return;

			int size = System.Math.Max(1, AchievementBadgeSize);
			int centerX = ui.Bounds.Right + AchievementBadgeOffsetX;
			int centerY = ui.Bounds.Bottom + AchievementBadgeOffsetY;
			var bounds = new Rectangle(centerX - size / 2, centerY - size / 2, size, size);
			_spriteBatch.Draw(_achievementBadgeCircle, bounds, new Color(174, 20, 36));

			if (_achievementBadgeFont == null) return;
			const string label = "!";
			Vector2 textSize = _achievementBadgeFont.MeasureString(label) * AchievementBadgeTextScale;
			var textPosition = new Vector2(
				bounds.Center.X - textSize.X / 2f,
				bounds.Center.Y - textSize.Y / 2f);
			_spriteBatch.DrawString(
				_achievementBadgeFont,
				label,
				textPosition,
				Color.White,
				0f,
				Vector2.Zero,
				AchievementBadgeTextScale,
				SpriteEffects.None,
				0f);
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

		private void SyncScreenPoi(
			string entityName,
			string poiId,
			string tooltipTitle,
			int rightMargin,
			int bottomMargin,
			int iconSize,
			float hoverScale,
			bool visible)
		{
			var rect = GetScreenPoiBounds(entityName, rightMargin, bottomMargin, iconSize, hoverScale);
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
			poi.WorldPosition = new Vector2(rect.Center.X, rect.Center.Y);
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
			var source = WayStationMapSourceService.ComputeCenteredCoverSource(
				_background.Width,
				_background.Height,
				Game1.VirtualWidth,
				Game1.VirtualHeight);
			var screen = WayStationMapSourceService.WorldToScreen(
				worldX,
				worldY,
				source,
				Game1.VirtualWidth,
				Game1.VirtualHeight);
			float scale = IsHovered(entityName) ? hoverScale : 1f;
			int size = System.Math.Max(1, (int)System.Math.Round(iconSize * scale));
			return new Rectangle(
				(int)System.Math.Round(screen.X - size / 2f),
				(int)System.Math.Round(screen.Y - size / 2f),
				size,
				size);
		}

		private Rectangle GetScreenPoiBounds(string entityName, int rightMargin, int bottomMargin, int iconSize, float hoverScale)
		{
			float scale = IsHovered(entityName) ? hoverScale : 1f;
			int size = System.Math.Max(1, (int)System.Math.Round(iconSize * scale));
			return new Rectangle(
				Game1.VirtualWidth - System.Math.Max(0, rightMargin) - size,
				Game1.VirtualHeight - System.Math.Max(0, bottomMargin) - size,
				size,
				size);
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

		private static bool HasPurchasedAnyMedals()
		{
			return SaveCache.GetPurchasedWayStationMedalIds().Count > 0;
		}

		private static bool HasAchievementRewardsToClaim()
		{
			return SaveCache.GetCollection().pendingClimbPoints > 0
				|| AchievementManager.GetUnseenCount() > 0;
		}

		private static bool IsWayStationScene(SceneState scene)
		{
			return scene != null && (scene.Current == SceneId.WayStation || scene.Current == SceneId.Snapshot);
		}
	}
}
