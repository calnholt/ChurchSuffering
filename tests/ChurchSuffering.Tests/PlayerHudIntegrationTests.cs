using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class PlayerHudIntegrationTests : IDisposable
{
	public PlayerHudIntegrationTests() => EventManager.Clear();
	public void Dispose() => EventManager.Clear();

	[Fact]
	public void Player_passives_anchor_below_hud_while_non_player_uses_hp_anchor()
	{
		var entityManager = BuildHud(out var player);
		var system = new AppliedPassivesDisplaySystem(entityManager, null, null);
		var playerTransform = player.GetComponent<Transform>();
		var hud = entityManager.GetEntitiesWithComponent<PlayerHudAnchor>()
			.Single();
		var hudBounds = TransformResolverService.ResolveLocalBounds(
			entityManager,
			hud,
			hud.GetComponent<PlayerHudAnchor>().Bounds);

		Point playerAnchor = system.ResolvePassiveAnchor(player, playerTransform, true);

		Assert.Equal(hudBounds.Center.X, playerAnchor.X);
		Assert.Equal(hudBounds.Bottom + system.OffsetY, playerAnchor.Y);

		var enemy = entityManager.CreateEntity("Enemy");
		var enemyTransform = new Transform { Position = new Vector2(900, 300) };
		entityManager.AddComponent(enemy, enemyTransform);
		entityManager.AddComponent(enemy, new HPBarAnchor
		{
			Rect = new Rectangle(800, 350, 200, 20),
		});

		Point enemyAnchor = system.ResolvePassiveAnchor(enemy, enemyTransform, false);

		Assert.Equal(900, enemyAnchor.X);
		Assert.Equal(385, enemyAnchor.Y);
	}

	[Fact]
	public void Tribulation_anchor_uses_courage_region_right_edge()
	{
		var entityManager = BuildHud(out var player);
		var system = new QuestTribulationDisplaySystem(entityManager, null, null, null);
		var courage = entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.Single(entity => entity.GetComponent<PlayerHudRegion>().Type == PlayerHudRegionType.Courage);
		var courageBounds = TransformResolverService.ResolveLocalBounds(
			entityManager,
			courage,
			courage.GetComponent<PlayerHudRegion>().Bounds);

		Vector2? anchor = system.ResolveCourageAnchor(player);

		Assert.Equal(new Vector2(courageBounds.Right, courageBounds.Center.Y), anchor);
	}

	[Fact]
	public void Moving_root_moves_regions_shadows_hitboxes_and_hud_anchors_by_same_offset()
	{
		var entityManager = BuildHud(out var player);
		entityManager.AddComponent(player, new Courage { Amount = 4 });
		entityManager.AddComponent(player, new ActionPoints { Current = 2 });
		entityManager.AddComponent(player, new Temperance { Amount = 1 });
		entityManager.AddComponent(player, new EquippedTemperanceAbility { AbilityId = "angelic_aura" });
		var root = entityManager.GetEntitiesWithComponent<PlayerHudAnchor>().Single();
		var rootTransform = root.GetComponent<Transform>();
		var offset = new Vector2(23, -17);

		var localRegionBounds = entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.ToDictionary(
				entity => entity.GetComponent<PlayerHudRegion>().Type,
				entity => entity.GetComponent<PlayerHudRegion>().Bounds);
		var resolvedBefore = entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.ToDictionary(
				entity => entity.GetComponent<PlayerHudRegion>().Type,
				entity => TransformResolverService.ResolveLocalBounds(
					entityManager,
					entity,
					entity.GetComponent<PlayerHudRegion>().Bounds));
		var hitboxesBefore = entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.ToDictionary(
				entity => entity.GetComponent<PlayerHudRegion>().Type,
				entity => TransformResolverService.ResolveUIBounds(
					entityManager,
					entity,
					entity.GetComponent<UIElement>()));
		var shadowSystem = new PlayerHudRootDisplaySystem(entityManager, null, null);
		var shadowsBefore = shadowSystem.GetShadowRegionBounds().ToList();
		var courageStateBefore = new PlayerHudCourageDisplaySystem(entityManager, null, null)
			.GetRenderState()
			.Value;
		var actionPointStateBefore = new PlayerHudActionPointDisplaySystem(entityManager, null, null)
			.GetRenderState()
			.Value;
		var temperanceStateBefore = new PlayerHudTemperanceDisplaySystem(entityManager, null, null)
			.GetRenderState()
			.Value;
		var passiveSystem = new AppliedPassivesDisplaySystem(entityManager, null, null);
		var passiveAnchorBefore = passiveSystem.ResolvePassiveAnchor(player, player.GetComponent<Transform>(), true);
		var tribulationSystem = new QuestTribulationDisplaySystem(entityManager, null, null, null);
		var tribulationAnchorBefore = tribulationSystem.ResolveCourageAnchor(player).Value;

		rootTransform.Position += offset;

		foreach (var entity in entityManager.GetEntitiesWithComponent<PlayerHudRegion>())
		{
			var type = entity.GetComponent<PlayerHudRegion>().Type;
			Assert.Equal(localRegionBounds[type], entity.GetComponent<PlayerHudRegion>().Bounds);
			Assert.Equal(Offset(resolvedBefore[type], offset), TransformResolverService.ResolveLocalBounds(
				entityManager,
				entity,
				entity.GetComponent<PlayerHudRegion>().Bounds));
			if (type != PlayerHudRegionType.Root)
			{
				Assert.Equal(Offset(hitboxesBefore[type], offset), TransformResolverService.ResolveUIBounds(
					entityManager,
					entity,
					entity.GetComponent<UIElement>()));
			}
		}

		var shadowsAfter = shadowSystem.GetShadowRegionBounds().ToList();
		Assert.Equal(shadowsBefore.Count, shadowsAfter.Count);
		for (int i = 0; i < shadowsBefore.Count; i++)
		{
			Assert.Equal(Offset(shadowsBefore[i], offset), shadowsAfter[i]);
		}

		Assert.Equal(Offset(courageStateBefore.Bounds, offset), new PlayerHudCourageDisplaySystem(entityManager, null, null).GetRenderState().Value.Bounds);
		Assert.Equal(Offset(actionPointStateBefore.Bounds, offset), new PlayerHudActionPointDisplaySystem(entityManager, null, null).GetRenderState().Value.Bounds);
		Assert.Equal(Offset(temperanceStateBefore.Bounds, offset), new PlayerHudTemperanceDisplaySystem(entityManager, null, null).GetRenderState().Value.Bounds);

		var passiveAnchorAfter = passiveSystem.ResolvePassiveAnchor(player, player.GetComponent<Transform>(), true);
		Assert.Equal(passiveAnchorBefore.X + (int)offset.X, passiveAnchorAfter.X);
		Assert.Equal(passiveAnchorBefore.Y + (int)offset.Y, passiveAnchorAfter.Y);
		Assert.Equal(tribulationAnchorBefore + offset, tribulationSystem.ResolveCourageAnchor(player).Value);
	}

	[Fact]
	public void Battle_exit_and_reentry_never_duplicates_hud_regions()
	{
		var entityManager = BuildHud(out _);
		var layout = new PlayerHudLayoutSystem(entityManager);
		Assert.Equal(6, entityManager.GetEntitiesWithComponent<PlayerHudRegion>().Count());

		SetScene(entityManager, SceneId.Climb);
		layout.Update(new GameTime());
		Assert.Empty(entityManager.GetEntitiesWithComponent<PlayerHudRegion>());

		SetScene(entityManager, SceneId.Battle);
		layout.Update(new GameTime());
		layout.Update(new GameTime());

		Assert.Equal(6, entityManager.GetEntitiesWithComponent<PlayerHudRegion>().Count());
		Assert.Equal(
			6,
			entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.Select(entity => entity.GetComponent<PlayerHudRegion>().Type)
				.Distinct()
				.Count());
	}

	private static EntityManager BuildHud(out Entity player)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Transform { Position = new Vector2(500, 300) });
		entityManager.AddComponent(player, new PortraitInfo
		{
			TextureWidth = 200,
			TextureHeight = 400,
			BaseScale = 0.5f,
		});
		new PlayerHudLayoutSystem(entityManager).Update(new GameTime());
		return entityManager;
	}

	private static void SetScene(EntityManager entityManager, SceneId scene)
	{
		entityManager.GetEntitiesWithComponent<SceneState>()
			.Single()
			.GetComponent<SceneState>()
			.Current = scene;
	}

	private static Rectangle Offset(Rectangle rect, Vector2 offset)
	{
		return new Rectangle(
			rect.X + (int)offset.X,
			rect.Y + (int)offset.Y,
			rect.Width,
			rect.Height);
	}
}
