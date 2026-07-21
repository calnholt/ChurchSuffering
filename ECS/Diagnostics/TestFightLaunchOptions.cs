using System;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Data.RunSetup;

namespace ChurchSuffering.Diagnostics
{
	public sealed class TestFightLaunchOptions
	{
		public const string Command = "test-fight";

		public string WeaponId { get; init; } = string.Empty;
		public string EnemyId { get; init; } = string.Empty;
		public int PenanceLevel { get; init; }

		public static bool TryParse(string[] args, out TestFightLaunchOptions options)
		{
			options = null;
			if (args == null || args.Length == 0 ||
				!string.Equals(args[0], Command, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (args.Length != 4)
			{
				throw new TestFightSetupException(
					"Usage: dotnet run -- test-fight <sword|dagger|hammer> <enemy-id> <penance-level 0-24>");
			}

			string weaponId = args[1].Trim().ToLowerInvariant();
			if (weaponId is not ("sword" or "dagger" or "hammer"))
			{
				throw new TestFightSetupException(
					$"Unknown test-fight weapon '{args[1]}'. Expected sword, dagger, or hammer.");
			}

			string enemyId = args[2].Trim().ToLowerInvariant();
			if (!EnemyFactory.IsRegistered(enemyId))
			{
				throw new TestFightSetupException($"Unknown test-fight enemy '{args[2]}'.");
			}

			if (!int.TryParse(args[3], out int penanceLevel)
				|| penanceLevel < 0
				|| penanceLevel > PenanceRules.MaxLevel)
			{
				throw new TestFightSetupException(
					$"Invalid test-fight Penance '{args[3]}'. Expected an integer from 0 through {PenanceRules.MaxLevel}.");
			}

			options = new TestFightLaunchOptions
			{
				WeaponId = weaponId,
				EnemyId = enemyId,
				PenanceLevel = penanceLevel,
			};
			return true;
		}
	}

	public sealed class TestFightSetupException : Exception
	{
		public TestFightSetupException(string message) : base(message) { }
	}
}
