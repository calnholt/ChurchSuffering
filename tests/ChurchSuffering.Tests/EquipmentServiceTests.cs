using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

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
	public void Activation_only_shop_tooltip_omits_block_and_uses_and_includes_free_action()
	{
		var equipment = EquipmentFactory.Create("helm_of_seeing");
		equipment.FlavorText = "A lens polished under a red moon.";

		string tooltip = EquipmentService.GetTooltipText(
			equipment,
			EquipmentTooltipType.Shop);

		Assert.True(tooltip.IndexOf(equipment.Text) < tooltip.IndexOf(equipment.FlavorText));
		Assert.DoesNotContain("Block:", tooltip);
		Assert.DoesNotContain("Uses:", tooltip);
		Assert.EndsWith("Free Action", tooltip);
	}

	[Fact]
	public void Block_equipment_shop_tooltip_omits_uses()
	{
		var equipment = EquipmentFactory.Create("knightly_grieves");

		string tooltip = EquipmentService.GetTooltipText(
			equipment,
			EquipmentTooltipType.Shop);

		Assert.Contains("Block: 2", tooltip);
		Assert.DoesNotContain("Uses:", tooltip);
	}
}
