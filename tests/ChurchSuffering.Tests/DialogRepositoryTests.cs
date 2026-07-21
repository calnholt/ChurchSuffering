using ChurchSuffering.ECS.Data.Dialog;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class DialogCatalogTests
{
	[Fact]
	public void Catalog_contains_desert_dialogue_and_fallen_shepherd_segments()
	{
		Assert.True(DialogCatalog.TryGet("desert_1", out var desert));
		Assert.NotEmpty(desert.ResolveSegment(string.Empty));
		Assert.True(DialogCatalog.TryGet("fallen_shepherd", out var shepherd));
		Assert.Equal("Fallen Shepherd", shepherd.ResolveSegment("intro")[0].actor);
		Assert.Single(shepherd.ResolveSegment("victory"));
		Assert.Empty(shepherd.ResolveSegment("missing"));
	}

	[Fact]
	public void Guided_tutorial_dialogue_uses_supplied_lines_and_remiel_portrait()
	{
		Assert.True(DialogCatalog.TryGet("guided_tutorial", out var tutorial));

		var intro = tutorial.ResolveSegment("intro");
		Assert.Equal(8, intro.Count);
		Assert.Equal("Remiel", intro[0].actor);
		Assert.Equal("There you are, eyes open. Now I need you awake a great deal faster, because we have a problem brewing about thirty feet to your left.", intro[0].message);
		Assert.Equal("Crusader", intro[7].actor);
		Assert.Equal("Then it does not need explaining. It needs to be put down.", intro[7].message);

		var catchBreath = tutorial.ResolveSegment("catch_breath");
		Assert.Equal(3, catchBreath.Count);
		Assert.Equal("Remiel", catchBreath[0].actor);

		var swordRetrieved = tutorial.ResolveSegment("sword_retrieved");
		Assert.Equal(2, swordRetrieved.Count);
		Assert.Equal("Crusader", swordRetrieved[0].actor);

		var lastOfThem = tutorial.ResolveSegment("last_of_them");
		Assert.Equal(2, lastOfThem.Count);
		Assert.Equal("Remiel", lastOfThem[0].actor);

		Assert.Equal("guardian_angel", DialogDisplaySystem.ResolvePortraitAssetName("Remiel"));
		Assert.Equal("crusader_sword", DialogDisplaySystem.ResolvePortraitAssetName("Crusader"));
	}

	[Theory]
	[InlineData("nun_counsel", "Nun", "character/nun")]
	[InlineData("reverent_crusader_counsel", "Reverent Crusader", "character/reverent_crusader")]
	[InlineData("revered_crusader_training", "Revered Crusader", "character/revered_crusader")]
	[InlineData("smith_forging", "Smith", "character/smith")]
	public void Character_dialogue_has_two_lines_and_mapped_portrait(
		string definitionId,
		string actor,
		string portraitAsset)
	{
		Assert.True(DialogCatalog.TryGet(definitionId, out var definition));
		var lines = definition.ResolveSegment("climb_event");

		Assert.Equal(2, lines.Count);
		Assert.Equal(actor, lines[0].actor);
		Assert.Equal("Crusader", lines[1].actor);
		Assert.Equal(portraitAsset, DialogDisplaySystem.ResolvePortraitAssetName(actor));
		Assert.All(lines, line =>
		{
			Assert.All(line.actor, character => Assert.InRange((int)character, 0, 127));
			Assert.All(line.message, character => Assert.InRange((int)character, 0, 127));
		});
	}
}
