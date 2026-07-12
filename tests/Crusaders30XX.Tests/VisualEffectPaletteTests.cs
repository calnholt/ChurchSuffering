using Crusaders30XX.ECS.Data.VisualEffects;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class VisualEffectPaletteTests
{
	[Fact]
	public void Recipe_defaults_to_physical_palette()
	{
		Assert.Equal(VisualEffectPalette.Physical, new VisualEffectRecipe().Palette);
	}

	[Fact]
	public void Clone_and_module_changes_preserve_palette()
	{
		var recipe = new VisualEffectRecipe()
			.WithPalette(VisualEffectPalette.Ice)
			.WithModules(VisualEffectModule.FrostBurst);

		Assert.Equal(VisualEffectPalette.Ice, recipe.Clone().Palette);
		Assert.Equal(VisualEffectPalette.Ice, recipe.WithModules(VisualEffectModule.Shards).Palette);
	}

	[Fact]
	public void Beat_conversion_preserves_palette()
	{
		var beat = new VisualEffectBeat { Palette = VisualEffectPalette.Shadow }
			.WithModules(VisualEffectModule.ShadowTendrils);

		Assert.Equal(VisualEffectPalette.Shadow, beat.ToLegacyRecipe().Palette);
	}

	[Fact]
	public void Palettes_have_distinct_primary_and_readable_highlight_colors()
	{
		var fire = VisualEffectPaletteResolver.Resolve(VisualEffectPalette.Fire);
		var ice = VisualEffectPaletteResolver.Resolve(VisualEffectPalette.Ice);

		Assert.NotEqual(fire.Primary, ice.Primary);
		Assert.NotEqual(fire.Primary, fire.Highlight);
		Assert.NotEqual(ice.Primary, ice.Highlight);
	}
}
