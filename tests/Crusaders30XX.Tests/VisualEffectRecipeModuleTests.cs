using System;
using System.Linq;
using Crusaders30XX.ECS.Data.VisualEffects;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class VisualEffectRecipeModuleTests
{
	[Fact]
	public void Empty_recipe_has_no_modules()
	{
		var recipe = new VisualEffectRecipe();

		foreach (var module in Enum.GetValues<VisualEffectModule>())
		{
			Assert.False(recipe.HasModule(module));
		}
	}

	[Fact]
	public void With_modules_builds_membership_mask_and_normalizes_duplicates()
	{
		var recipe = new VisualEffectRecipe().WithModules(
			VisualEffectModule.HitFlash,
			VisualEffectModule.Shake,
			VisualEffectModule.HitFlash);

		Assert.True(recipe.HasModule(VisualEffectModule.HitFlash));
		Assert.True(recipe.HasModule(VisualEffectModule.Shake));
		Assert.False(recipe.HasModule(VisualEffectModule.WhiteWash));
		Assert.Equal(2, recipe.Modules.Count);
	}

	[Fact]
	public void Clone_and_copy_operations_preserve_membership_mask()
	{
		var recipe = new VisualEffectRecipe()
			.WithModules(VisualEffectModule.RockBlast, VisualEffectModule.SmokeBlobs)
			.WithIntensity(1.4f);

		var clone = recipe.Clone();
		var filtered = clone.WithModules(
			clone.Modules.Where(module => module != VisualEffectModule.SmokeBlobs).ToArray());

		Assert.True(clone.HasModule(VisualEffectModule.RockBlast));
		Assert.True(clone.HasModule(VisualEffectModule.SmokeBlobs));
		Assert.True(filtered.HasModule(VisualEffectModule.RockBlast));
		Assert.False(filtered.HasModule(VisualEffectModule.SmokeBlobs));
	}

	[Fact]
	public void Every_visual_effect_module_fits_in_the_membership_mask()
	{
		var values = Enum.GetValues<VisualEffectModule>();

		Assert.All(values, module => Assert.InRange((int)module, 0, 63));
		Assert.Equal(values.Length, values.Select(module => (int)module).Distinct().Count());
	}
}
