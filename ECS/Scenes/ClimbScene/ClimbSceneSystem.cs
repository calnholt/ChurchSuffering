using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Climb Scene")]
	public class ClimbSceneSystem : Core.System
	{
		private readonly World _world;
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ContentManager _content;
		private readonly ImageAssetService _imageAssets;
		private bool _firstLoad = true;
		private ClimbBackgroundDisplaySystem _backgroundDisplaySystem;
		private ClimbV2LayoutSystem _layoutSystem;
		private ClimbV2MotionSystem _motionSystem;
		private ShopContainerDisplaySystem _shopContainerDisplaySystem;
		private EncounterContainerDisplaySystem _encounterContainerDisplaySystem;
		private EventContainerDisplaySystem _eventContainerDisplaySystem;
		private ClimbV2TitleDisplaySystem _titleDisplaySystem;
		private DistanceClimbedTimelineDisplaySystem _timelineDisplaySystem;
		private PlayerResourcesDisplaySystem _resourcesDisplaySystem;
		private ClimbOverviewButtonDisplaySystem _overviewDisplaySystem;
		private ShopItemDisplaySystem _shopItemDisplaySystem;
		private EncounterDisplaySystem _encounterDisplaySystem;
		private EventDisplaySystem _eventDisplaySystem;
		private ChoiceStatsRailDisplaySystem _railDisplaySystem;
		private ClimbChoicePreviewDisplaySystem _previewDisplaySystem;
		private ClimbChoiceLayerCompositor _choiceCompositor;
		private bool _effectLoadAttempted;
		private MedalTooltipDisplaySystem _medalTooltipDisplaySystem;
		private ClimbCardUpgradeDisplaySystem _cardUpgradeDisplaySystem;
		private ClimbResourceAcquisitionDisplaySystem _resourceAcquisitionDisplaySystem;
		private ClimbOverviewGamepadInputSystem _overviewGamepadInputSystem;
		private EquipmentTooltipDisplaySystem _equipmentTooltipDisplaySystem;
		private const string EquipmentTooltipEntityName = "Climb_EquipmentTooltip";

		public ClimbSceneSystem(EntityManager entityManager, SystemManager systemManager, World world, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_world = world;
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			_imageAssets = imageAssets;

			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<PrepareSceneEvent>(evt =>
			{
				if (evt.Scene != SceneId.Climb) return;
				AddClimbSystems();
				SetClimbSystemsActive(false);
			});
			EventManager.Subscribe<DeleteCachesEvent>(evt =>
			{
				if (evt.Scene != SceneId.Climb) RemoveClimbSystems();
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!IsClimbScene()) return;
			_equipmentTooltipDisplaySystem?.Update(gameTime);
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt.Scene != SceneId.Climb)
			{
				DeactivateClimbUiEntities(EntityManager);
				EventManager.Publish(new HideLocationNameEvent());
				return;
			}

			SaveCache.EnsureClimbState();
			EventManager.Publish(new ChangeMusicTrack { Track = MusicTrack.Climb });
			EventManager.Publish(new HideLocationNameEvent());
			AddClimbSystems();
			_layoutSystem?.PrepareForLoad(evt);
			SetClimbSystemsActive(true);
			EnsureEquipmentTooltipEntity();
		}

		private void AddClimbSystems()
		{
			if (!_firstLoad) return;
			_firstLoad = false;

			_backgroundDisplaySystem = new ClimbBackgroundDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _content, _imageAssets);
			_world.AddSystem(_backgroundDisplaySystem);
			_layoutSystem = new ClimbV2LayoutSystem(EntityManager);
			_world.AddSystem(_layoutSystem);
			_motionSystem = new ClimbV2MotionSystem(EntityManager);
			_world.AddSystem(_motionSystem);
			_shopContainerDisplaySystem = Add(new ShopContainerDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_encounterContainerDisplaySystem = Add(new EncounterContainerDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_eventContainerDisplaySystem = Add(new EventContainerDisplaySystem(EntityManager));
			_titleDisplaySystem = Add(new ClimbV2TitleDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_timelineDisplaySystem = Add(new DistanceClimbedTimelineDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_resourcesDisplaySystem = Add(new PlayerResourcesDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets));
			_overviewDisplaySystem = Add(new ClimbOverviewButtonDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_shopItemDisplaySystem = Add(new ShopItemDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_encounterDisplaySystem = Add(new EncounterDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_eventDisplaySystem = Add(new EventDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_railDisplaySystem = Add(new ChoiceStatsRailDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets));
			_previewDisplaySystem = Add(new ClimbChoicePreviewDisplaySystem(EntityManager, _spriteBatch, _imageAssets));
			_medalTooltipDisplaySystem = new MedalTooltipDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_medalTooltipDisplaySystem);
			_cardUpgradeDisplaySystem = new ClimbCardUpgradeDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_cardUpgradeDisplaySystem);
			_resourceAcquisitionDisplaySystem = new ClimbResourceAcquisitionDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_resourceAcquisitionDisplaySystem);
			_overviewGamepadInputSystem = new ClimbOverviewGamepadInputSystem(EntityManager);
			_world.AddSystem(_overviewGamepadInputSystem);
			EnsureEquipmentTooltipEntity();
			_equipmentTooltipDisplaySystem = new EquipmentTooltipDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets, EquipmentTooltipEntityName);
		}

		private void RemoveClimbSystems()
		{
			DeactivateClimbUiEntities(EntityManager);
			SetClimbSystemsActive(false);
		}

		private void SetClimbSystemsActive(bool active)
		{
			_backgroundDisplaySystem?.SetActive(active);
			_layoutSystem?.SetActive(active);
			_motionSystem?.SetActive(active);
			_shopContainerDisplaySystem?.SetActive(active);
			_encounterContainerDisplaySystem?.SetActive(active);
			_eventContainerDisplaySystem?.SetActive(active);
			_titleDisplaySystem?.SetActive(active);
			_timelineDisplaySystem?.SetActive(active);
			_resourcesDisplaySystem?.SetActive(active);
			_overviewDisplaySystem?.SetActive(active);
			_shopItemDisplaySystem?.SetActive(active);
			_encounterDisplaySystem?.SetActive(active);
			_eventDisplaySystem?.SetActive(active);
			_railDisplaySystem?.SetActive(active);
			_previewDisplaySystem?.SetActive(active);
			_medalTooltipDisplaySystem?.SetActive(active);
			_cardUpgradeDisplaySystem?.SetActive(active);
			_resourceAcquisitionDisplaySystem?.SetActive(active);
			_overviewGamepadInputSystem?.SetActive(active);
		}

		public void Draw()
		{
			if (_backgroundDisplaySystem != null) FrameProfiler.Measure("ClimbBackgroundDisplaySystem.Draw", _backgroundDisplaySystem.Draw);
			if (_shopContainerDisplaySystem != null) FrameProfiler.Measure("ShopContainerDisplaySystem.Draw", _shopContainerDisplaySystem.Draw);
			if (_encounterContainerDisplaySystem != null) FrameProfiler.Measure("EncounterContainerDisplaySystem.Draw", _encounterContainerDisplaySystem.Draw);
			if (_eventContainerDisplaySystem != null) FrameProfiler.Measure("EventContainerDisplaySystem.Draw", _eventContainerDisplaySystem.Draw);
			if (_titleDisplaySystem != null) FrameProfiler.Measure("ClimbV2TitleDisplaySystem.Draw", _titleDisplaySystem.Draw);
			if (_timelineDisplaySystem != null) FrameProfiler.Measure("DistanceClimbedTimelineDisplaySystem.Draw", _timelineDisplaySystem.Draw);
			if (_resourcesDisplaySystem != null) FrameProfiler.Measure("PlayerResourcesDisplaySystem.Draw", _resourcesDisplaySystem.Draw);
			if (_overviewDisplaySystem != null) FrameProfiler.Measure("ClimbOverviewButtonDisplaySystem.Draw", _overviewDisplaySystem.Draw);
			FrameProfiler.Measure("ClimbV2Choices.Draw", DrawChoices);
			if (_medalTooltipDisplaySystem != null) FrameProfiler.Measure("MedalTooltipDisplaySystem.Draw", _medalTooltipDisplaySystem.Draw);
			if (_cardUpgradeDisplaySystem != null) FrameProfiler.Measure("ClimbCardUpgradeDisplaySystem.Draw", _cardUpgradeDisplaySystem.Draw);
			if (_resourceAcquisitionDisplaySystem != null) FrameProfiler.Measure("ClimbResourceAcquisitionDisplaySystem.Draw", _resourceAcquisitionDisplaySystem.Draw);
			if (_equipmentTooltipDisplaySystem != null) FrameProfiler.Measure("ClimbEquipmentTooltipDisplaySystem.Draw", _equipmentTooltipDisplaySystem.Draw);
		}

		private T Add<T>(T system) where T : Core.System
		{
			_world.AddSystem(system);
			return system;
		}

		private void DrawChoices()
		{
			EnsureEffectsLoaded();
			foreach (var entity in EntityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
				.Where(entity => entity.GetComponent<ClimbShopItemPresentation>() != null
					|| entity.GetComponent<ClimbEncounterPresentation>() != null
					|| entity.GetComponent<ClimbEventPresentation>() != null)
				.OrderBy(entity => entity.GetComponent<Transform>()?.ZOrder ?? 0)
				.ThenBy(entity => entity.Id))
			{
				var motion = entity.GetComponent<ClimbV2ChoiceMotion>();
				var expiry = entity.GetComponent<ClimbChoiceExpiryPreviewPresentation>();
				float opacity = MathHelper.Clamp(expiry?.OpacityMultiplier ?? 1f, 0f, 1f);
				float grayscale = 1f - (1f - MathHelper.Clamp(motion?.Grayscale ?? 0f, 0f, 1f))
					* (1f - MathHelper.Clamp(expiry?.Grayscale ?? 0f, 0f, 1f));
				bool composite = ShaderRuntimeOptions.ShadersEnabled
					&& _choiceCompositor?.IsAvailable == true
					&& (motion != null || expiry != null)
					&& ((motion?.Blur ?? 0f) > 0.05f || grayscale > 0.01f || opacity < 0.999f
						|| (motion?.Sepia ?? 0f) > 0.01f || Math.Abs((motion?.Brightness ?? 1f) - 1f) > 0.01f);
				Action draw = () => DrawChoice(entity, composite ? 1f : opacity);
				if (composite) _choiceCompositor.DrawLayer(
					draw, motion?.Blur ?? 0f, grayscale, motion?.Sepia ?? 0f, motion?.Brightness ?? 1f, opacity);
				else draw();
			}
		}

		private void DrawChoice(Entity entity, float opacityMultiplier)
		{
			if (entity.GetComponent<ClimbShopItemPresentation>() != null) _shopItemDisplaySystem?.DrawEntity(entity, opacityMultiplier);
			else if (entity.GetComponent<ClimbEncounterPresentation>() != null) _encounterDisplaySystem?.DrawEntity(entity, opacityMultiplier);
			else if (entity.GetComponent<ClimbEventPresentation>() != null) _eventDisplaySystem?.DrawEntity(entity, opacityMultiplier);
			_railDisplaySystem?.DrawForSource(entity, opacityMultiplier);
		}

		private void EnsureEffectsLoaded()
		{
			if (_effectLoadAttempted || !ShaderRuntimeOptions.ShadersEnabled) return;
			_effectLoadAttempted = true;
			try
			{
				var blur = _content.Load<Effect>("Shaders/GaussianBlur");
				var filter = _content.Load<Effect>("Shaders/ClimbChoiceFilter");
				_choiceCompositor = new ClimbChoiceLayerCompositor(_graphicsDevice, _spriteBatch, blur, filter);
			}
			catch (Exception ex)
			{
				LoggingService.Append("ClimbSceneSystem.EffectLoadFailed", new JsonObject { ["Message"] = ex.Message });
			}
		}

		public void DrawBackgroundOnly()
		{
			if (_backgroundDisplaySystem != null)
			{
				FrameProfiler.Measure("ClimbBackgroundDisplaySystem.DrawBackgroundOnly", () => _backgroundDisplaySystem.Draw(undimmed: true));
			}
		}

		private void EnsureEquipmentTooltipEntity()
		{
			var entity = EntityManager.GetEntity(EquipmentTooltipEntityName);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(EquipmentTooltipEntityName);
				EntityManager.AddComponent(entity, new EquipmentTooltipState());
				EntityManager.AddComponent(entity, new Transform { ZOrder = 10002 });
				EntityManager.AddComponent(entity, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					IsHidden = true,
					TooltipType = TooltipType.None,
				});
				EntityManager.AddComponent(entity, new OwnedByScene { Scene = SceneId.Climb });
				return;
			}

			if (entity.GetComponent<EquipmentTooltipState>() == null) EntityManager.AddComponent(entity, new EquipmentTooltipState());
			if (entity.GetComponent<Transform>() == null) EntityManager.AddComponent(entity, new Transform { ZOrder = 10002 });
			if (entity.GetComponent<UIElement>() == null)
			{
				EntityManager.AddComponent(entity, new UIElement { Bounds = Rectangle.Empty, IsInteractable = false, IsHidden = true, TooltipType = TooltipType.None });
			}
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		public static void DeactivateClimbUiEntities(EntityManager entityManager)
		{
			if (entityManager == null) return;

			var climbEntities = entityManager.GetAllEntities()
				.Where(entity =>
					entity.GetComponent<ClimbSceneRoot>() != null
					|| entity.GetComponent<ClimbHeaderElement>() != null
					|| entity.GetComponent<ClimbTimelineElement>() != null
					|| entity.GetComponent<ClimbResourceBarElement>() != null
					|| entity.GetComponent<ClimbLoadoutButton>() != null
					|| entity.GetComponent<ClimbColumnPresentation>() != null
					|| entity.GetComponent<ClimbSlotPresentation>() != null
					|| entity.GetComponent<ClimbColumnTransitionInputSuppression>() != null
					|| entity.GetComponent<ClimbV2TitlePresentation>() != null
					|| entity.GetComponent<DistanceClimbedTimelinePresentation>() != null
					|| entity.GetComponent<PlayerResourcesPresentation>() != null
					|| entity.GetComponent<ClimbOverviewButton>() != null
					|| entity.GetComponent<ClimbV2SectionPresentation>() != null
					|| entity.GetComponent<ClimbShopItemPresentation>() != null
					|| entity.GetComponent<ClimbEncounterPresentation>() != null
					|| entity.GetComponent<ClimbEventPresentation>() != null
					|| entity.GetComponent<ClimbChoiceRailPresentation>() != null
					|| entity.GetComponent<ClimbV2InputSuppression>() != null
					|| entity.GetComponent<ClimbShopTooltipSource>() != null
					|| entity.GetComponent<ClimbMedalTooltipSource>() != null
					|| entity.GetComponent<ClimbMedalTooltipAnchor>() != null
					|| string.Equals(entity.Name, EquipmentTooltipEntityName, System.StringComparison.OrdinalIgnoreCase))
				.ToList();

			foreach (var entity in climbEntities)
			{
				var ui = entity.GetComponent<UIElement>();
				if (ui != null)
				{
					if (entity.GetComponent<ClimbColumnTransitionInputSuppression>() != null)
					{
						ui.Restore();
						entityManager.RemoveComponent<ClimbColumnTransitionInputSuppression>(entity);
					}
					if (entity.GetComponent<ClimbV2InputSuppression>() != null)
					{
						ui.Restore();
						entityManager.RemoveComponent<ClimbV2InputSuppression>(entity);
					}
					ui.Bounds = Rectangle.Empty;
					ui.IsInteractable = false;
					ui.IsHidden = true;
					ui.IsHovered = false;
					ui.IsClicked = false;
				}

				var preview = entity.GetComponent<ClimbPreviewState>();
				preview?.Clear();
				var expiry = entity.GetComponent<ClimbChoiceExpiryPreviewPresentation>();
				if (expiry != null)
				{
					expiry.IsActive = false;
					expiry.PulseElapsedSeconds = 0f;
					expiry.Strength = 0f;
					expiry.OpacityMultiplier = 1f;
					expiry.Grayscale = 0f;
				}
			}
		}
	}
}
