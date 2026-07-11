using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
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
		private ClimbHeaderLayoutSystem _headerLayoutSystem;
		private ClimbHeaderDisplaySystem _headerDisplaySystem;
		private ClimbColumnLayoutSystem _columnLayoutSystem;
		private ClimbColumnDisplaySystem _columnDisplaySystem;
		private ClimbCardUpgradeDisplaySystem _cardUpgradeDisplaySystem;
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
			EventManager.Subscribe<DeleteCachesEvent>(_ => RemoveClimbSystems());
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
		}

		private void AddClimbSystems()
		{
			if (!_firstLoad) return;
			_firstLoad = false;

			_backgroundDisplaySystem = new ClimbBackgroundDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _content, _imageAssets);
			_world.AddSystem(_backgroundDisplaySystem);
			_headerLayoutSystem = new ClimbHeaderLayoutSystem(EntityManager);
			_world.AddSystem(_headerLayoutSystem);
			_headerDisplaySystem = new ClimbHeaderDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_headerDisplaySystem);
			_columnDisplaySystem = new ClimbColumnDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_columnDisplaySystem);
			_columnLayoutSystem = new ClimbColumnLayoutSystem(EntityManager);
			_world.AddSystem(_columnLayoutSystem);
			_cardUpgradeDisplaySystem = new ClimbCardUpgradeDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_cardUpgradeDisplaySystem);
			EnsureEquipmentTooltipEntity();
			_equipmentTooltipDisplaySystem = new EquipmentTooltipDisplaySystem(EntityManager, _graphicsDevice, _spriteBatch, _imageAssets, EquipmentTooltipEntityName);
		}

		private void RemoveClimbSystems()
		{
			DeactivateClimbUiEntities(EntityManager);
			_world.RemoveSystem(_backgroundDisplaySystem);
			_world.RemoveSystem(_headerLayoutSystem);
			_world.RemoveSystem(_headerDisplaySystem);
			_world.RemoveSystem(_columnLayoutSystem);
			_world.RemoveSystem(_columnDisplaySystem);
			_world.RemoveSystem(_cardUpgradeDisplaySystem);
			_cardUpgradeDisplaySystem = null;
			var tooltip = EntityManager.GetEntity(EquipmentTooltipEntityName);
			if (tooltip != null) EntityManager.DestroyEntity(tooltip.Id);
			_equipmentTooltipDisplaySystem = null;
			_firstLoad = true;
		}

		public void Draw()
		{
			if (_backgroundDisplaySystem != null) FrameProfiler.Measure("ClimbBackgroundDisplaySystem.Draw", _backgroundDisplaySystem.Draw);
			if (_headerDisplaySystem != null) FrameProfiler.Measure("ClimbHeaderDisplaySystem.Draw", _headerDisplaySystem.Draw);
			if (_columnDisplaySystem != null) FrameProfiler.Measure("ClimbColumnDisplaySystem.Draw", _columnDisplaySystem.Draw);
			if (_cardUpgradeDisplaySystem != null) FrameProfiler.Measure("ClimbCardUpgradeDisplaySystem.Draw", _cardUpgradeDisplaySystem.Draw);
			if (_equipmentTooltipDisplaySystem != null) FrameProfiler.Measure("ClimbEquipmentTooltipDisplaySystem.Draw", _equipmentTooltipDisplaySystem.Draw);
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
					|| entity.GetComponent<ClimbShopTooltipSource>() != null
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
					ui.Bounds = Rectangle.Empty;
					ui.IsInteractable = false;
					ui.IsHidden = true;
					ui.IsHovered = false;
					ui.IsClicked = false;
				}

				var preview = entity.GetComponent<ClimbPreviewState>();
				preview?.Clear();
			}
		}
	}
}
