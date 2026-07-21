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

public class PlayerHudResourceDisplaySystemTests : IDisposable
{
	public PlayerHudResourceDisplaySystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Theory]
	[InlineData(7, "7")]
	[InlineData(12, "12")]
	public void Courage_render_state_uses_actual_single_and_multi_digit_values(int amount, string expected)
	{
		var entityManager = BuildHud(out var player);
		player.GetComponent<Courage>().Amount = amount;
		var system = new PlayerHudCourageDisplaySystem(entityManager, null, null);

		var state = AssertNullable(system.GetRenderState());

		Assert.Equal("COUR", state.Label);
		Assert.Equal(expected, state.Value);
		Assert.Equal(new Color(196, 30, 58), state.BackgroundColor);
		Assert.Equal(Color.White, state.LabelColor);
		Assert.Equal(Color.White, state.ValueColor);
		Assert.Equal(0, state.EffectSize);
		Assert.Equal((byte)64, state.EffectColor.A);
	}

	[Theory]
	[InlineData(0, "0")]
	[InlineData(15, "15")]
	public void Action_point_render_state_uses_actual_zero_and_multi_digit_values(int amount, string expected)
	{
		var entityManager = BuildHud(out var player);
		player.GetComponent<ActionPoints>().Current = amount;
		var system = new PlayerHudActionPointDisplaySystem(entityManager, null, null);

		var state = AssertNullable(system.GetRenderState());

		Assert.Equal("AP", state.Label);
		Assert.Equal(expected, state.Value);
		Assert.Equal(Color.White, state.BackgroundColor);
		Assert.Equal(new Color(10, 10, 10), state.LabelColor);
		Assert.Equal(new Color(10, 10, 10), state.ValueColor);
		Assert.Equal(10, state.EffectSize);
		Assert.Equal(Color.FromNonPremultiplied(196, 30, 58, 115), state.EffectColor);
	}

	[Theory]
	[InlineData(MainPhase.StartBattle, SubPhase.StartBattle)]
	[InlineData(MainPhase.EnemyTurn, SubPhase.Block)]
	[InlineData(MainPhase.EnemyTurn, SubPhase.EnemyAttack)]
	[InlineData(MainPhase.PlayerTurn, SubPhase.PlayerStart)]
	[InlineData(MainPhase.PlayerTurn, SubPhase.Action)]
	[InlineData(MainPhase.PlayerTurn, SubPhase.PlayerEnd)]
	public void Action_points_remain_visible_in_every_battle_phase(MainPhase main, SubPhase sub)
	{
		var entityManager = BuildHud(out _);
		var phase = entityManager.CreateEntity("Phase");
		entityManager.AddComponent(phase, new PhaseState { Main = main, Sub = sub });
		var system = new PlayerHudActionPointDisplaySystem(entityManager, null, null);

		var state = system.GetRenderState();

		Assert.True(state.HasValue);
		Assert.Equal("0", state.Value.Value);
	}

	[Fact]
	public void Resource_tooltips_use_layout_bounds_stay_above_and_disable_highlight()
	{
		var entityManager = BuildHud(out var player);
		player.GetComponent<Courage>().Amount = 3;
		player.GetComponent<ActionPoints>().Current = 1;
		var courage = GetRegionEntity(entityManager, PlayerHudRegionType.Courage);
		var actionPoint = GetRegionEntity(entityManager, PlayerHudRegionType.ActionPoint);
		Rectangle courageBounds = courage.GetComponent<UIElement>().Bounds;
		Rectangle actionPointBounds = actionPoint.GetComponent<UIElement>().Bounds;
		var courageSystem = new PlayerHudCourageDisplaySystem(entityManager, null, null);
		var actionPointSystem = new PlayerHudActionPointDisplaySystem(entityManager, null, null);

		courageSystem.Update(new GameTime());
		actionPointSystem.Update(new GameTime());

		var courageUi = courage.GetComponent<UIElement>();
		Assert.Equal(courageBounds, courageUi.Bounds);
		Assert.Equal("3 Courage\n\nBlocking with red cards increases your courage by 1.", courageUi.Tooltip);
		Assert.Equal(TooltipType.Text, courageUi.TooltipType);
		Assert.Equal(TooltipPosition.Above, courageUi.TooltipPosition);
		Assert.True(courageUi.IsInteractable);
		Assert.False(courageUi.ShowHoverHighlight);

		var actionPointUi = actionPoint.GetComponent<UIElement>();
		Assert.Equal(actionPointBounds, actionPointUi.Bounds);
		Assert.Equal("1 Action Point\n\nSpend Action Points to play cards during the Action phase.", actionPointUi.Tooltip);
		Assert.Equal(TooltipType.Text, actionPointUi.TooltipType);
		Assert.Equal(TooltipPosition.Above, actionPointUi.TooltipPosition);
		Assert.True(actionPointUi.IsInteractable);
		Assert.False(actionPointUi.ShowHoverHighlight);

		player.GetComponent<ActionPoints>().Current = 9;
		actionPointSystem.Update(new GameTime());
		Assert.StartsWith("9 Action Points", actionPointUi.Tooltip);
	}

	[Fact]
	public void Pulse_scale_changes_render_inputs_without_writing_layout_owned_state()
	{
		var entityManager = BuildHud(out _);
		var courage = GetRegionEntity(entityManager, PlayerHudRegionType.Courage);
		var actionPoint = GetRegionEntity(entityManager, PlayerHudRegionType.ActionPoint);
		var courageRegion = courage.GetComponent<PlayerHudRegion>();
		var actionPointRegion = actionPoint.GetComponent<PlayerHudRegion>();
		var courageTransform = courage.GetComponent<Transform>();
		var actionPointTransform = actionPoint.GetComponent<Transform>();
		Rectangle courageBounds = courageRegion.Bounds;
		Rectangle actionPointBounds = actionPointRegion.Bounds;
		Rectangle courageWorldBounds = TransformResolverService.ResolveLocalBounds(entityManager, courage, courageBounds);
		Rectangle actionPointWorldBounds = TransformResolverService.ResolveLocalBounds(entityManager, actionPoint, actionPointBounds);
		Vector2 couragePosition = courageTransform.Position;
		Vector2 actionPointPosition = actionPointTransform.Position;
		courage.GetComponent<PlayerHudFeedbackState>().Scale = 1.25f;
		actionPoint.GetComponent<PlayerHudFeedbackState>().Scale = 1.5f;
		var courageSystem = new PlayerHudCourageDisplaySystem(entityManager, null, null);
		var actionPointSystem = new PlayerHudActionPointDisplaySystem(entityManager, null, null);

		var courageState = AssertNullable(courageSystem.GetRenderState());
		var actionPointState = AssertNullable(actionPointSystem.GetRenderState());

		Assert.Equal(1.25f, courageState.PulseScale);
		Assert.Equal((int)Math.Round(courageBounds.Width * 1.25f), courageState.Bounds.Width);
		Assert.Equal((int)Math.Round(courageBounds.Height * 1.25f), courageState.Bounds.Height);
		Assert.Equal(courageWorldBounds.Center, courageState.Bounds.Center);
		Assert.Equal(0, courageState.EffectSize);

		Assert.Equal(1.5f, actionPointState.PulseScale);
		Assert.Equal((int)Math.Round(actionPointBounds.Width * 1.5f), actionPointState.Bounds.Width);
		Assert.Equal((int)Math.Round(actionPointBounds.Height * 1.5f), actionPointState.Bounds.Height);
		Assert.Equal(actionPointWorldBounds.Center, actionPointState.Bounds.Center);
		Assert.Equal(15, actionPointState.EffectSize);

		Assert.Equal(courageBounds, courageRegion.Bounds);
		Assert.Equal(actionPointBounds, actionPointRegion.Bounds);
		Assert.Equal(couragePosition, courageTransform.Position);
		Assert.Equal(actionPointPosition, actionPointTransform.Position);
	}

	private static EntityManager BuildHud(out Entity player)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });

		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Courage());
		entityManager.AddComponent(player, new ActionPoints());
		entityManager.AddComponent(player, new Transform { Position = new Vector2(500, 300) });
		entityManager.AddComponent(player, new PortraitInfo
		{
			TextureWidth = 200,
			TextureHeight = 400,
			BaseScale = 0.5f,
			CurrentScale = 0.5f,
		});
		entityManager.AddComponent(player, new ActorPresentationState());

		var layout = new PlayerHudLayoutSystem(entityManager);
		layout.Update(new GameTime());
		return entityManager;
	}

	private static Entity GetRegionEntity(EntityManager entityManager, PlayerHudRegionType type)
	{
		return entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.Single(entity => entity.GetComponent<PlayerHudRegion>().Type == type);
	}

	private static PlayerHudResourceRenderState AssertNullable(PlayerHudResourceRenderState? state)
	{
		Assert.True(state.HasValue);
		return state.Value;
	}
}
