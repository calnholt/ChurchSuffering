using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class PlayerHudPledgeDisplaySystemTests : IDisposable
{
	public PlayerHudPledgeDisplaySystemTests()
	{
		EventManager.Clear();
		StateSingleton.IsPledgeEnabled = true;
	}

	public void Dispose()
	{
		EventManager.Clear();
		StateSingleton.IsPledgeEnabled = true;
	}

	[Fact]
	public void Available_state_renders_without_changing_reserved_bounds()
	{
		var entityManager = BuildHud(eligibleCard: true);
		var regionEntity = GetRegionEntity(entityManager);
		var region = regionEntity.GetComponent<PlayerHudRegion>();
		var ui = regionEntity.GetComponent<UIElement>();
		Rectangle reservedBounds = region.Bounds;
		var system = new PlayerHudPledgeDisplaySystem(entityManager, null, null, null);

		system.Update(new GameTime());
		var state = AssertNullable(system.GetRenderState());

		Assert.True(region.IsVisible);
		Assert.Equal(reservedBounds, region.Bounds);
		Assert.Equal(28, state.IconBounds.Width);
		Assert.Equal(28, state.IconBounds.Height);
		Assert.False(ui.IsInteractable);
		Assert.False(ui.IsHovered);
		Assert.Equal(TooltipType.None, ui.TooltipType);
	}

	[Fact]
	public void Unavailable_state_hides_visuals_but_retains_reserved_geometry()
	{
		var entityManager = BuildHud(eligibleCard: false);
		var regionEntity = GetRegionEntity(entityManager);
		var region = regionEntity.GetComponent<PlayerHudRegion>();
		var ui = regionEntity.GetComponent<UIElement>();
		Rectangle reservedBounds = region.Bounds;
		var system = new PlayerHudPledgeDisplaySystem(entityManager, null, null, null);

		system.Update(new GameTime());

		Assert.False(region.IsVisible);
		Assert.Null(system.GetRenderState());
		Assert.Equal(reservedBounds, region.Bounds);
		Assert.False(ui.IsInteractable);
		Assert.False(ui.IsHovered);
		Assert.False(ui.ShowHoverHighlight);
	}

	[Fact]
	public void Aspect_fit_preserves_source_ratio_inside_icon_box()
	{
		Rectangle result = PlayerHudPledgeDisplaySystem.AspectFit(
			new Rectangle(0, 0, 100, 50),
			new Rectangle(10, 20, 36, 36));

		Assert.Equal(new Rectangle(10, 29, 36, 18), result);
	}

	private static EntityManager BuildHud(bool eligibleCard)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });
		var phase = entityManager.CreateEntity("Phase");
		entityManager.AddComponent(phase, new PhaseState
		{
			Main = MainPhase.PlayerTurn,
			Sub = SubPhase.Action,
		});
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Transform { Position = new Vector2(500, 300) });
		entityManager.AddComponent(player, new PortraitInfo
		{
			TextureWidth = 200,
			TextureHeight = 400,
			BaseScale = 0.5f,
		});

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		if (eligibleCard)
		{
			deck.Hand.Add(EntityFactory.CreateCardFromDefinition(
				entityManager,
				"strike",
				CardData.CardColor.White));
		}

		new PlayerHudLayoutSystem(entityManager).Update(new GameTime());
		return entityManager;
	}

	private static Entity GetRegionEntity(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.Single(entity => entity.GetComponent<PlayerHudRegion>().Type == PlayerHudRegionType.Pledge);
	}

	private static PlayerHudPledgeRenderState AssertNullable(PlayerHudPledgeRenderState? state)
	{
		Assert.True(state.HasValue);
		return state.Value;
	}
}
