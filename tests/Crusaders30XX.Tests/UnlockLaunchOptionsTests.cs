using Crusaders30XX.Diagnostics;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class UnlockLaunchOptionsTests
{
	[Fact]
	public void ConfigureFromArgs_detects_unlock_flags_case_insensitively()
	{
		UnlockLaunchOptions.ConfigureFromArgs(new[] { "UNLOCK", "UNLOCK-RUN-SETUP" });

		Assert.True(UnlockLaunchOptions.UnlockAllCollectionItems);
		Assert.True(UnlockLaunchOptions.UnlockAllRunSetupOptions);

		UnlockLaunchOptions.ConfigureFromArgs([]);
	}

	[Fact]
	public void ConfigureFromArgs_keeps_unlock_flags_independent()
	{
		UnlockLaunchOptions.ConfigureFromArgs(new[] { "unlock-run-setup" });

		Assert.False(UnlockLaunchOptions.UnlockAllCollectionItems);
		Assert.True(UnlockLaunchOptions.UnlockAllRunSetupOptions);

		UnlockLaunchOptions.ConfigureFromArgs([]);
	}

	[Fact]
	public void StripLaunchFlags_removes_only_unlock_arguments()
	{
		var args = UnlockLaunchOptions.StripLaunchFlags(
			new[] { "test-fight", "unlock", "hammer", "UNLOCK-RUN-SETUP", "skeleton", "hard" });

		Assert.Equal(new[] { "test-fight", "hammer", "skeleton", "hard" }, args);
	}
}
