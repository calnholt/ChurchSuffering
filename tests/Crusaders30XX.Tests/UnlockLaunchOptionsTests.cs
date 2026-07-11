using Crusaders30XX.Diagnostics;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class UnlockLaunchOptionsTests
{
	[Fact]
	public void ConfigureFromArgs_detects_unlock_case_insensitively()
	{
		UnlockLaunchOptions.ConfigureFromArgs(new[] { "UNLOCK" });

		Assert.True(UnlockLaunchOptions.UnlockAllCollectionItems);

		UnlockLaunchOptions.ConfigureFromArgs([]);
	}

	[Fact]
	public void StripLaunchFlag_removes_only_unlock_arguments()
	{
		var args = UnlockLaunchOptions.StripLaunchFlag(
			new[] { "test-fight", "unlock", "hammer", "UNLOCK", "skeleton", "hard" });

		Assert.Equal(new[] { "test-fight", "hammer", "skeleton", "hard" }, args);
	}
}
