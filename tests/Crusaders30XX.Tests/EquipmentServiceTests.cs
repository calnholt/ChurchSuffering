using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class EquipmentServiceTests
{
	public static TheoryData<string> ColorSetEquipmentIds => new()
	{
		"ivory_coif",
		"ivory_vest",
		"ivory_wraps",
		"ivory_treads",
		"scarlet_coif",
		"scarlet_vest",
		"scarlet_wraps",
		"scarlet_treads",
		"pale_greathelm",
		"pale_cuirass",
		"pale_vambraces",
		"pale_greaves",
		"crimson_greathelm",
		"crimson_cuirass",
		"crimson_vambraces",
		"crimson_greaves",
	};

	[Theory]
	[MemberData(nameof(ColorSetEquipmentIds))]
	public void Color_set_equipment_has_flavor_text_in_shop_tooltip(string equipmentId)
	{
		var equipment = EquipmentFactory.Create(equipmentId);

		Assert.False(string.IsNullOrWhiteSpace(equipment.FlavorText));

		string tooltip = EquipmentService.GetTooltipText(
			equipment,
			EquipmentTooltipType.Shop);

		Assert.Contains(equipment.FlavorText, tooltip);
	}

	[Fact]
	public void Simple_tooltip_orders_rules_before_flavor_and_includes_free_action()
	{
		var equipment = EquipmentFactory.Create("helm_of_seeing");
		equipment.FlavorText = "A lens polished under a red moon.";

		string tooltip = EquipmentService.GetTooltipText(
			equipment,
			EquipmentTooltipType.Shop);

		Assert.True(tooltip.IndexOf(equipment.Text) < tooltip.IndexOf(equipment.FlavorText));
		Assert.Contains("Block: 2 | Uses: 1", tooltip);
		Assert.EndsWith("Free Action", tooltip);
	}
}
