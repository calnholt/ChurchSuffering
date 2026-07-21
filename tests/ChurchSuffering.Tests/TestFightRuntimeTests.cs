using ChurchSuffering.Diagnostics;
using Xunit;

namespace ChurchSuffering.Tests;

public class TestFightRuntimeTests
{
	[Fact]
	public void Battle_seeds_change_and_hp_delta_tracks_results_with_floor()
	{
		int nextSeed = 40;
		TestFightRuntime.Configure(new TestFightLaunchOptions
		{
			WeaponId = "hammer",
			EnemyId = "skeleton",
			PenanceLevel = 24,
		});
		TestFightRuntime.SetDeckSeedProviderForTests(() => ++nextSeed);

		try
		{
			Assert.Equal(41, TestFightRuntime.BeginBattle());
			Assert.Equal(42, TestFightRuntime.BeginBattle());
			Assert.Equal(10, TestFightRuntime.ApplyHpDelta(10));

			TestFightRuntime.RecordVictory();
			Assert.Equal(1, TestFightRuntime.HpDelta);
			Assert.Equal(11, TestFightRuntime.CurrentMaxHp);

			for (int i = 0; i < 20; i++)
			{
				TestFightRuntime.RecordDefeat();
			}

			Assert.Equal(-9, TestFightRuntime.HpDelta);
			Assert.Equal(1, TestFightRuntime.CurrentMaxHp);
		}
		finally
		{
			TestFightRuntime.Reset();
		}
	}
}
