using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class EquipmentArtServiceTests
{
	[Theory]
	[InlineData("scarlet_coif")]
	[InlineData("scarlet_treads")]
	[InlineData("scarlet_vest")]
	[InlineData("scarlet_wraps")]
	[InlineData("ivory_coif")]
	[InlineData("ivory_treads")]
	[InlineData("ivory_vest")]
	[InlineData("ivory_wraps")]
	[InlineData("knightly_chest")]
	[InlineData("knightly_grieves")]
	[InlineData("knightly_gauntlets")]
	[InlineData("knightly_helm")]
	public void Supplied_equipment_uses_its_own_content_asset(string equipmentId)
	{
		Assert.Equal("Equipment/" + equipmentId, EquipmentArtService.GetAssetName(equipmentId));
	}
}
