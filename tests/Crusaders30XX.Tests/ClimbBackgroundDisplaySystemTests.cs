using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class ClimbBackgroundDisplaySystemTests
{
	[Fact]
	public void Three_distinct_active_encounter_locations_use_three_shader_layers()
	{
		var plan = ClimbBackgroundDisplaySystem.BuildLayerPlan(new[]
		{
			Slot(BattleLocation.Tundra),
			Slot(BattleLocation.Jungle),
			Slot(BattleLocation.Volcano),
		});

		Assert.True(plan.UseShader);
		Assert.Equal(3, plan.LocationCount);
		Assert.Equal(BattleLocation.Tundra, plan.TopLocation);
		Assert.Equal(BattleLocation.Jungle, plan.MiddleLocation);
		Assert.Equal(BattleLocation.Volcano, plan.BottomLocation);
		Assert.Equal(0.5f, plan.LayerSplit);
	}

	[Fact]
	public void Two_distinct_active_encounter_locations_reuse_second_location_for_bottom_layer()
	{
		var plan = ClimbBackgroundDisplaySystem.BuildLayerPlan(new[]
		{
			Slot(BattleLocation.Gothic),
			Slot(BattleLocation.Gothic),
			Slot(BattleLocation.Desert),
		});

		Assert.True(plan.UseShader);
		Assert.Equal(2, plan.LocationCount);
		Assert.Equal(BattleLocation.Gothic, plan.TopLocation);
		Assert.Equal(BattleLocation.Desert, plan.MiddleLocation);
		Assert.Equal(BattleLocation.Desert, plan.BottomLocation);
		Assert.Equal(1f, plan.LayerSplit);
	}

	[Fact]
	public void One_distinct_active_encounter_location_disables_shader()
	{
		var plan = ClimbBackgroundDisplaySystem.BuildLayerPlan(new[]
		{
			Slot(BattleLocation.Jungle),
			Slot(BattleLocation.Jungle),
			Slot(BattleLocation.Jungle),
		});

		Assert.False(plan.UseShader);
		Assert.Equal(1, plan.LocationCount);
		Assert.Equal(BattleLocation.Jungle, plan.TopLocation);
	}

	[Fact]
	public void Completed_empty_and_null_slots_are_ignored()
	{
		var plan = ClimbBackgroundDisplaySystem.BuildLayerPlan(new List<ClimbEncounterSlotSave>
		{
			null,
			Slot(BattleLocation.Tundra, completed: true),
			Slot(BattleLocation.Jungle, enemyId: ""),
			Slot(BattleLocation.Volcano),
			Slot(BattleLocation.Gothic),
		});

		Assert.True(plan.UseShader);
		Assert.Equal(2, plan.LocationCount);
		Assert.Equal(BattleLocation.Volcano, plan.TopLocation);
		Assert.Equal(BattleLocation.Gothic, plan.MiddleLocation);
		Assert.Equal(BattleLocation.Gothic, plan.BottomLocation);
		Assert.Equal(1f, plan.LayerSplit);
	}

	private static ClimbEncounterSlotSave Slot(
		BattleLocation location,
		bool completed = false,
		string enemyId = "skeleton")
	{
		return new ClimbEncounterSlotSave
		{
			enemyId = enemyId,
			battleLocation = location,
			isCompleted = completed,
		};
	}
}
