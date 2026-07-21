using ChurchSuffering.Diagnostics.Snapshots;
using ChurchSuffering.Diagnostics.Snapshots.Fixtures;
using Xunit;

namespace ChurchSuffering.Tests;

public class PlayerHudSnapshotVariantTests
{
	[Theory]
	[InlineData("default", PlayerHudSnapshotVariantId.Default, "default")]
	[InlineData("unavailable", PlayerHudSnapshotVariantId.Unavailable, "unavailable")]
	[InlineData("incoming-damage", PlayerHudSnapshotVariantId.IncomingDamage, "incoming-damage")]
	[InlineData("low-health", PlayerHudSnapshotVariantId.LowHealth, "low-health")]
	[InlineData("expanded", PlayerHudSnapshotVariantId.Expanded, "expanded")]
	[InlineData("enemy-health", PlayerHudSnapshotVariantId.EnemyHealth, "enemy-health")]
	public void Parses_supported_variants(
		string token,
		PlayerHudSnapshotVariantId expectedId,
		string expectedSlug)
	{
		var variant = PlayerHudSnapshotVariant.Parse(new[] { token });

		Assert.Equal(expectedId, variant.Id);
		Assert.Equal(expectedSlug, variant.FileSlug);
	}

	[Fact]
	public void Defaults_when_variant_is_omitted()
	{
		var variant = PlayerHudSnapshotVariant.Parse(System.Array.Empty<string>());

		Assert.Equal(PlayerHudSnapshotVariantId.Default, variant.Id);
		Assert.Equal("default", variant.FileSlug);
	}

	[Fact]
	public void Rejects_unknown_or_extra_arguments()
	{
		Assert.Throws<DisplaySnapshotSetupException>(
			() => PlayerHudSnapshotVariant.Parse(new[] { "unknown" }));
		Assert.Throws<DisplaySnapshotSetupException>(
			() => PlayerHudSnapshotVariant.Parse(new[] { "default", "extra" }));
	}
}
