using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class PlayerHudLayoutSystemTests : IDisposable
{
	public PlayerHudLayoutSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Layout_centers_reserved_stack_below_stable_portrait_bounds()
	{
		var entityManager = BuildWorld(SceneId.Battle, out var player);
		var system = new PlayerHudLayoutSystem(entityManager);

		system.Update(new GameTime());

		var anchor = GetAnchor(entityManager);
		var rootEntity = GetRegionEntity(entityManager, PlayerHudRegionType.Root);
		var worldBounds = TransformResolverService.ResolveLocalBounds(entityManager, rootEntity, anchor.Bounds);
		Assert.Equal(new Rectangle(450, 200, 100, 200), anchor.StablePortraitBounds);
		Assert.Equal(500, worldBounds.Center.X);
		Assert.Equal(anchor.StablePortraitBounds.Bottom + system.PortraitGap, worldBounds.Y);
		Assert.Equal(system.ChipHeight * 2 - system.RowOverlap, anchor.Bounds.Height);
		Assert.Equal(new Vector2(worldBounds.X, worldBounds.Y), rootEntity.GetComponent<Transform>().Position);

		var animation = player.GetComponent<ActorPresentationState>();
		animation.DrawOffset = new Vector2(700, -300);
		animation.ScaleMultiplier = new Vector2(4f, 2f);
		player.GetComponent<PortraitInfo>().CurrentScale = 5f;
		system.Update(new GameTime());

		Assert.Equal(new Rectangle(450, 200, 100, 200), GetAnchor(entityManager).StablePortraitBounds);
		Assert.Equal(anchor.Bounds, GetAnchor(entityManager).Bounds);
		Assert.Equal(worldBounds, TransformResolverService.ResolveLocalBounds(entityManager, rootEntity, GetAnchor(entityManager).Bounds));
	}

	[Fact]
	public void Layout_applies_mockup_rows_overlap_hp_extension_and_pledge_reservation()
	{
		var entityManager = BuildWorld(SceneId.Battle, out _);
		var system = new PlayerHudLayoutSystem(entityManager);

		system.Update(new GameTime());

		var root = GetRegionEntity(entityManager, PlayerHudRegionType.Root);
		var health = GetRegionEntity(entityManager, PlayerHudRegionType.Health);
		var courage = GetRegionEntity(entityManager, PlayerHudRegionType.Courage);
		var temperance = GetRegionEntity(entityManager, PlayerHudRegionType.Temperance);
		var actionPoint = GetRegionEntity(entityManager, PlayerHudRegionType.ActionPoint);
		var pledge = GetRegionEntity(entityManager, PlayerHudRegionType.Pledge);
		var rootBounds = GetWorldBounds(entityManager, PlayerHudRegionType.Root);
		var healthBounds = GetWorldBounds(entityManager, PlayerHudRegionType.Health);
		var courageBounds = GetWorldBounds(entityManager, PlayerHudRegionType.Courage);
		var temperanceBounds = GetWorldBounds(entityManager, PlayerHudRegionType.Temperance);
		var actionPointBounds = GetWorldBounds(entityManager, PlayerHudRegionType.ActionPoint);
		var pledgeBounds = GetWorldBounds(entityManager, PlayerHudRegionType.Pledge);

		Assert.All(new[] { health, courage, temperance, actionPoint, pledge },
			entity => Assert.Equal(new Rectangle(0, 0, entity.GetComponent<PlayerHudRegion>().Bounds.Width, 36), entity.GetComponent<PlayerHudRegion>().Bounds));
		Assert.Equal(rootBounds.Y, healthBounds.Y);
		Assert.Equal(rootBounds.Y + system.ChipHeight - system.RowOverlap, courageBounds.Y);
		Assert.Equal(14, courageBounds.Right - temperanceBounds.X);
		Assert.Equal(14, temperanceBounds.Right - actionPointBounds.X);
		Assert.Equal(14, actionPointBounds.Right - pledgeBounds.X);
		Assert.Equal(14, healthBounds.X - rootBounds.X);
		Assert.Equal(14, healthBounds.Right - pledgeBounds.Right);
		Assert.Equal(new Vector2(14, 0), health.GetComponent<Transform>().Position);
		Assert.Equal(new Vector2(0, system.ChipHeight - system.RowOverlap), courage.GetComponent<Transform>().Position);
		var anchor = GetAnchor(entityManager);
		Assert.Equal(36, anchor.PledgeIconSize);
		Assert.Equal(new Color(196, 30, 58), anchor.HudRed);
		Assert.Equal(new Color(10, 10, 10), anchor.HudBlack);
		Assert.Equal(Color.White, anchor.HudWhite);
		Assert.Equal(0.1f, anchor.LabelFontScale);
		Assert.Equal(0.2f, anchor.ValueFontScale);
		Assert.Equal(2, anchor.LabelLetterSpacing);
		Assert.Equal(8, anchor.ContentGap);
		Assert.Equal(26, anchor.HealthTrackHeight);
		Assert.Equal(2, anchor.HealthTrackBorderThickness);
		Assert.Equal(21, anchor.TemperanceChunkWidth);
		Assert.Equal(26, anchor.TemperanceChunkHeight);
		Assert.Equal(-8, anchor.TemperanceChunkGap);
		Assert.Equal(10, anchor.PledgeContentGap);
		Assert.Equal(0, anchor.CourageInsetShadowHeight);
		Assert.Equal(64, anchor.CourageInsetShadowAlpha);
		Assert.Equal(10, anchor.ActionPointGlowRadius);
		Assert.Equal(115, anchor.ActionPointGlowAlpha);
		Assert.Equal(6, anchor.ShadowOffsetY);
		Assert.Equal(20, anchor.ShadowBlurRadius);
		Assert.Equal(140, anchor.ShadowAlpha);

		var reservedRootBounds = root.GetComponent<PlayerHudRegion>().Bounds;
		var reservedPledgeBounds = pledge.GetComponent<PlayerHudRegion>().Bounds;
		pledge.GetComponent<PlayerHudRegion>().IsVisible = false;
		system.Update(new GameTime());

		Assert.Equal(reservedRootBounds, GetRegion(entityManager, PlayerHudRegionType.Root).Bounds);
		Assert.Equal(reservedPledgeBounds, GetRegion(entityManager, PlayerHudRegionType.Pledge).Bounds);
	}

	[Fact]
	public void Layout_repairs_transform_hierarchy_and_root_ui_parallax()
	{
		var entityManager = BuildWorld(SceneId.Battle, out _);
		var system = new PlayerHudLayoutSystem(entityManager);

		system.Update(new GameTime());

		var root = GetRegionEntity(entityManager, PlayerHudRegionType.Root);
		var rootParallax = root.GetComponent<ParallaxLayer>();
		var expectedParallax = ParallaxLayer.GetUIParallaxLayer();
		Assert.NotNull(rootParallax);
		Assert.Equal(expectedParallax.MultiplierX, rootParallax.MultiplierX);
		Assert.Equal(expectedParallax.MultiplierY, rootParallax.MultiplierY);
		Assert.Equal(expectedParallax.MaxOffset, rootParallax.MaxOffset);
		Assert.Equal(expectedParallax.SmoothTime, rootParallax.SmoothTime);

		var childRegions = entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.Where(entity => entity.GetComponent<PlayerHudRegion>().Type != PlayerHudRegionType.Root)
			.ToList();
		Assert.All(childRegions, entity =>
		{
			Assert.Same(root, entity.GetComponent<ParentTransform>().Parent);
			Assert.False(entity.HasComponent<ParallaxLayer>());
			Assert.Equal(Rectangle.Empty.Location, entity.GetComponent<PlayerHudRegion>().Bounds.Location);
			Assert.Equal(Rectangle.Empty.Location, entity.GetComponent<UIElement>().Bounds.Location);
		});
	}

	[Fact]
	public void Layout_is_exclusive_owner_of_region_transforms_and_ui_bounds()
	{
		var entityManager = BuildWorld(SceneId.Battle, out _);
		var system = new PlayerHudLayoutSystem(entityManager);
		system.Update(new GameTime());
		var courageEntity = GetRegionEntity(entityManager, PlayerHudRegionType.Courage);
		var transform = courageEntity.GetComponent<Transform>();
		var ui = courageEntity.GetComponent<UIElement>();
		var expectedPosition = transform.Position;

		transform.Position = new Vector2(-1000, -1000);
		transform.Scale = new Vector2(8f, 9f);
		transform.Rotation = 2f;
		ui.Bounds = new Rectangle(-1000, -1000, 1, 1);
		system.Update(new GameTime());

		var bounds = courageEntity.GetComponent<PlayerHudRegion>().Bounds;
		Assert.Equal(expectedPosition, transform.Position);
		Assert.Equal(Vector2.One, transform.Scale);
		Assert.Equal(0f, transform.Rotation);
		Assert.Equal(bounds, ui.Bounds);
		Assert.True(ui.IsInteractable);
		Assert.False(ui.ShowHoverHighlight);
		Assert.Equal(TooltipPosition.Above, ui.TooltipPosition);
	}

	[Fact]
	public void Lifecycle_creates_once_deduplicates_and_destroys_outside_hud_scenes()
	{
		var entityManager = BuildWorld(SceneId.Battle, out _);
		var system = new PlayerHudLayoutSystem(entityManager);

		system.Update(new GameTime());
		system.Update(new GameTime());
		Assert.Equal(6, entityManager.GetEntitiesWithComponent<PlayerHudRegion>().Count());

		var duplicate = entityManager.CreateEntity("DuplicateCourage");
		entityManager.AddComponent(duplicate, new PlayerHudRegion { Type = PlayerHudRegionType.Courage });
		entityManager.AddComponent(duplicate, new Transform());
		entityManager.AddComponent(duplicate, new UIElement());
		system.Update(new GameTime());
		Assert.Equal(6, entityManager.GetEntitiesWithComponent<PlayerHudRegion>().Count());
		Assert.Single(
			entityManager.GetEntitiesWithComponent<PlayerHudRegion>(),
			entity => entity.GetComponent<PlayerHudRegion>().Type == PlayerHudRegionType.Courage);

		var courageUi = GetRegionEntity(entityManager, PlayerHudRegionType.Courage).GetComponent<UIElement>();
		courageUi.IsHovered = true;
		SetScene(entityManager, SceneId.Location);
		system.Update(new GameTime());
		Assert.Empty(entityManager.GetEntitiesWithComponent<PlayerHudRegion>());

		SetScene(entityManager, SceneId.Snapshot);
		system.Update(new GameTime());
		Assert.Equal(6, entityManager.GetEntitiesWithComponent<PlayerHudRegion>().Count());
		Assert.All(entityManager.GetEntitiesWithComponent<PlayerHudRegion>(), entity =>
			Assert.Equal(SceneId.Snapshot, entity.GetComponent<OwnedByScene>().Scene));
	}

	[Fact]
	public void Load_scene_event_removes_hoverable_regions_immediately()
	{
		var entityManager = BuildWorld(SceneId.Battle, out _);
		var system = new PlayerHudLayoutSystem(entityManager);
		system.Update(new GameTime());
		GetRegionEntity(entityManager, PlayerHudRegionType.ActionPoint)
			.GetComponent<UIElement>().IsHovered = true;

		EventManager.Publish(new LoadSceneEvent
		{
			PreviousScene = SceneId.Battle,
			Scene = SceneId.Location,
		});

		Assert.Empty(entityManager.GetEntitiesWithComponent<PlayerHudRegion>());
	}

	[Fact]
	public void Missing_stable_portrait_hides_and_clears_all_region_bounds()
	{
		var entityManager = BuildWorld(SceneId.Battle, out var player);
		player.GetComponent<PortraitInfo>().TextureHeight = 0;
		var system = new PlayerHudLayoutSystem(entityManager);

		system.Update(new GameTime());

		Assert.Equal(6, entityManager.GetEntitiesWithComponent<PlayerHudRegion>().Count());
		Assert.All(entityManager.GetEntitiesWithComponent<PlayerHudRegion>(), entity =>
		{
			Assert.Equal(Rectangle.Empty, entity.GetComponent<PlayerHudRegion>().Bounds);
			Assert.Equal(Rectangle.Empty, entity.GetComponent<UIElement>().Bounds);
			Assert.True(entity.GetComponent<UIElement>().IsHidden);
			Assert.False(entity.GetComponent<UIElement>().IsInteractable);
			Assert.False(entity.GetComponent<UIElement>().IsHovered);
		});
	}

	[Fact]
	public void Feedback_updates_component_scale_without_writing_layout_transform()
	{
		var entityManager = BuildWorld(SceneId.Battle, out _);
		var layout = new PlayerHudLayoutSystem(entityManager);
		layout.Update(new GameTime());
		var feedbackSystem = new PlayerHudFeedbackSystem(entityManager);
		var courage = GetRegionEntity(entityManager, PlayerHudRegionType.Courage);
		var originalPosition = courage.GetComponent<Transform>().Position;
		var feedback = courage.GetComponent<PlayerHudFeedbackState>();

		EventManager.Publish(new ModifyCourageEvent { Delta = 1 });
		feedbackSystem.Update(new GameTime(TimeSpan.FromSeconds(0.15), TimeSpan.FromSeconds(0.15)));

		Assert.True(feedback.IsPulsing);
		Assert.True(feedback.Scale > 1f);
		Assert.Equal(originalPosition, courage.GetComponent<Transform>().Position);

		feedbackSystem.Update(new GameTime(TimeSpan.FromSeconds(0.30), TimeSpan.FromSeconds(0.15)));
		Assert.False(feedback.IsPulsing);
		Assert.Equal(1f, feedback.Scale);
		Assert.Equal(originalPosition, courage.GetComponent<Transform>().Position);
	}

	[Fact]
	public void Hover_highlight_defaults_on_and_can_be_disabled_for_tooltip_only_regions()
	{
		var ordinary = new UIElement
		{
			IsInteractable = true,
			IsHovered = true,
		};
		Assert.True(ordinary.ShowHoverHighlight);
		Assert.True(UIElementHighlightSystem.ShouldShowHoverHighlight(ordinary));

		ordinary.ShowHoverHighlight = false;
		Assert.False(UIElementHighlightSystem.ShouldShowHoverHighlight(ordinary));
	}

	private static EntityManager BuildWorld(SceneId scene, out Entity player)
	{
		var entityManager = new EntityManager();
		var sceneEntity = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(sceneEntity, new SceneState { Current = scene });

		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Transform
		{
			Position = new Vector2(500, 300),
			Scale = new Vector2(0.5f, 0.5f),
		});
		entityManager.AddComponent(player, new PortraitInfo
		{
			TextureWidth = 200,
			TextureHeight = 400,
			BaseScale = 0.5f,
			CurrentScale = 0.5f,
		});
		entityManager.AddComponent(player, new ActorPresentationState());
		return entityManager;
	}

	private static void SetScene(EntityManager entityManager, SceneId scene)
	{
		entityManager.GetEntitiesWithComponent<SceneState>()
			.Single()
			.GetComponent<SceneState>()
			.Current = scene;
	}

	private static PlayerHudAnchor GetAnchor(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<PlayerHudAnchor>()
			.Single()
			.GetComponent<PlayerHudAnchor>();
	}

	private static Entity GetRegionEntity(EntityManager entityManager, PlayerHudRegionType type)
	{
		return entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.Single(entity => entity.GetComponent<PlayerHudRegion>().Type == type);
	}

	private static PlayerHudRegion GetRegion(EntityManager entityManager, PlayerHudRegionType type)
	{
		return GetRegionEntity(entityManager, type).GetComponent<PlayerHudRegion>();
	}

	private static Rectangle GetWorldBounds(EntityManager entityManager, PlayerHudRegionType type)
	{
		var entity = GetRegionEntity(entityManager, type);
		return TransformResolverService.ResolveLocalBounds(
			entityManager,
			entity,
			entity.GetComponent<PlayerHudRegion>().Bounds);
	}
}
