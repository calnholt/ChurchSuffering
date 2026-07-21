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

public class PlayerHudTemperanceDisplaySystemTests : IDisposable
{
	public PlayerHudTemperanceDisplaySystemTests() => EventManager.Clear();
	public void Dispose() => EventManager.Clear();

	[Theory]
	[InlineData(0, 1, 0)]
	[InlineData(1, 1, 1)]
	[InlineData(0, 3, 0)]
	[InlineData(2, 3, 2)]
	[InlineData(3, 3, 3)]
	[InlineData(9, 3, 3)]
	[InlineData(4, 5, 4)]
	[InlineData(5, 5, 5)]
	[InlineData(8, 5, 5)]
	public void Chunk_state_clamps_amount_for_supported_thresholds(
		int amount,
		int threshold,
		int expectedFilled)
	{
		var state = PlayerHudTemperanceRendering.BuildChunkState(amount, threshold);

		Assert.Equal(threshold, state.Threshold);
		Assert.Equal(expectedFilled, state.FilledChunks);
	}

	[Fact]
	public void Render_state_uses_equipped_ability_threshold_and_feedback_scale()
	{
		var entityManager = BuildHud(out var player);
		player.GetComponent<Temperance>().Amount = 2;
		player.GetComponent<EquippedTemperanceAbility>().AbilityId = "angelic_aura";
		var regionEntity = GetRegionEntity(entityManager);
		Rectangle bounds = regionEntity.GetComponent<PlayerHudRegion>().Bounds;
		Rectangle worldBounds = TransformResolverService.ResolveLocalBounds(entityManager, regionEntity, bounds);
		Vector2 position = regionEntity.GetComponent<Transform>().Position;
		regionEntity.GetComponent<PlayerHudFeedbackState>().Scale = 1.25f;
		var system = new PlayerHudTemperanceDisplaySystem(entityManager, null, null);

		var state = AssertNullable(system.GetRenderState());

		Assert.Equal(2, state.Threshold);
		Assert.Equal(2, state.FilledChunks);
		Assert.Equal(1.25f, state.PulseScale);
		Assert.Equal(worldBounds.Center, state.Bounds.Center);
		Assert.Equal(bounds, regionEntity.GetComponent<PlayerHudRegion>().Bounds);
		Assert.Equal(position, regionEntity.GetComponent<Transform>().Position);
	}

	[Fact]
	public void Tooltip_stays_above_and_preserves_layout_bounds()
	{
		var entityManager = BuildHud(out var player);
		player.GetComponent<Temperance>().Amount = 1;
		var regionEntity = GetRegionEntity(entityManager);
		var ui = regionEntity.GetComponent<UIElement>();
		Rectangle bounds = ui.Bounds;
		var system = new PlayerHudTemperanceDisplaySystem(entityManager, null, null);

		system.Update(new GameTime());

		Assert.Equal(bounds, ui.Bounds);
		Assert.Equal(
			"Temperance\n\nAngelic Aura - Gain 3 aegis.\n\nBlocking with white cards gains temperance.",
			ui.Tooltip);
		Assert.Equal(TooltipType.Text, ui.TooltipType);
		Assert.Equal(TooltipPosition.Above, ui.TooltipPosition);
		Assert.False(ui.ShowHoverHighlight);
	}

	[Fact]
	public void Tooltip_falls_back_when_no_ability_is_equipped()
	{
		var entityManager = BuildHud(out var player);
		player.GetComponent<EquippedTemperanceAbility>().AbilityId = string.Empty;
		var regionEntity = GetRegionEntity(entityManager);
		var ui = regionEntity.GetComponent<UIElement>();
		var system = new PlayerHudTemperanceDisplaySystem(entityManager, null, null);

		system.Update(new GameTime());

		Assert.Equal(
			"Temperance\n\n\n\nBlocking with white cards gains temperance.",
			ui.Tooltip);
	}

	private static EntityManager BuildHud(out Entity player)
	{
		var entityManager = new EntityManager();
		var scene = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Temperance());
		entityManager.AddComponent(player, new EquippedTemperanceAbility { AbilityId = "angelic_aura" });
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

	private static Entity GetRegionEntity(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<PlayerHudRegion>()
			.Single(entity => entity.GetComponent<PlayerHudRegion>().Type == PlayerHudRegionType.Temperance);
	}

	private static PlayerHudTemperanceRenderState AssertNullable(PlayerHudTemperanceRenderState? state)
	{
		Assert.True(state.HasValue);
		return state.Value;
	}
}
