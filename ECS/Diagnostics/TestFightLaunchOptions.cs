using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

namespace Crusaders30XX.Diagnostics
{
	public sealed class TestFightLaunchOptions
	{
		public const string Command = "test-fight";

		public string WeaponId { get; init; } = string.Empty;
		public string EnemyId { get; init; } = string.Empty;
		public ClimbDifficulty Difficulty { get; init; } = ClimbDifficulty.Easy;

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
					"Usage: dotnet run -- test-fight <sword|dagger|hammer> <enemy-id> <easy|normal|hard>");
			}

			string weaponId = args[1].Trim().ToLowerInvariant();
			if (weaponId is not ("sword" or "dagger" or "hammer"))
			{
				throw new TestFightSetupException(
					$"Unknown test-fight weapon '{args[1]}'. Expected sword, dagger, or hammer.");
			}

			string enemyId = args[2].Trim().ToLowerInvariant();
			string normalizedEnemy = enemyId.Replace("-", string.Empty, StringComparison.Ordinal);
			if (!Enum.TryParse(normalizedEnemy, ignoreCase: true, out EnemyId parsedEnemy))
			{
				throw new TestFightSetupException($"Unknown test-fight enemy '{args[2]}'.");
			}
			_ = GeneratedEnemyCatalog.GetDefinition(parsedEnemy);

			if (!TryParseDifficulty(args[3], out var difficulty))
			{
				throw new TestFightSetupException(
					$"Unknown test-fight difficulty '{args[3]}'. Expected easy, normal, or hard.");
			}

			options = new TestFightLaunchOptions
			{
				WeaponId = weaponId,
				EnemyId = enemyId,
				Difficulty = difficulty,
			};
			return true;
		}

		private static bool TryParseDifficulty(string value, out ClimbDifficulty difficulty)
		{
			switch (value?.Trim().ToLowerInvariant())
			{
				case "easy":
					difficulty = ClimbDifficulty.Easy;
					return true;
				case "normal":
					difficulty = ClimbDifficulty.Normal;
					return true;
				case "hard":
					difficulty = ClimbDifficulty.Hard;
					return true;
				default:
					difficulty = ClimbDifficulty.Easy;
					return false;
			}
		}
	}

	public sealed class TestFightSetupException : Exception
	{
		public TestFightSetupException(string message) : base(message) { }
	}
}
