using ChurchSuffering.Diagnostics.Snapshots;
using ChurchSuffering.Diagnostics.Snapshots.Fixtures;
using Xunit;

namespace ChurchSuffering.Tests;

public class EquipmentTooltipSnapshotVariantTests
{
	[Theory]
	[InlineData("active", EquipmentTooltipSnapshotVariantId.Active, "active", "bulwark_plate", false)]
	[InlineData("passive", EquipmentTooltipSnapshotVariantId.Passive, "passive", "knightly_grieves", false)]
	[InlineData("used", EquipmentTooltipSnapshotVariantId.Used, "used", "bulwark_plate", true)]
	public void Parses_supported_variants(
		string token,
		EquipmentTooltipSnapshotVariantId expectedId,
		string expectedSlug,
		string expectedEquipmentId,
		bool expectedIsUsed)
	{
		var variant = EquipmentTooltipSnapshotVariant.Parse([token]);

		Assert.Equal(expectedId, variant.Id);
		Assert.Equal(expectedSlug, variant.FileSlug);
		Assert.Equal(expectedEquipmentId, variant.EquipmentId);
		Assert.Equal(expectedIsUsed, variant.IsUsed);
	}

	[Fact]
	public void Defaults_to_active()
	{
		var variant = EquipmentTooltipSnapshotVariant.Parse([]);

		Assert.Equal(EquipmentTooltipSnapshotVariantId.Active, variant.Id);
		Assert.Equal("active", variant.FileSlug);
	}

	[Fact]
	public void Rejects_unknown_or_extra_arguments()
	{
		Assert.Throws<DisplaySnapshotSetupException>(
			() => EquipmentTooltipSnapshotVariant.Parse(["unknown"]));
		Assert.Throws<DisplaySnapshotSetupException>(
			() => EquipmentTooltipSnapshotVariant.Parse(["active", "extra"]));
	}
}
