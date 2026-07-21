using System;
using System.Globalization;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures;

public sealed class BoosterPackOpeningSnapshotVariant
{
	public float TimeSeconds { get; init; } = 5.14f;
	public int Seed { get; init; } = 1337;
	public string FileSlug { get; init; } = "time-5.14-seed-1337";

	public static BoosterPackOpeningSnapshotVariant Parse(string[] args)
	{
		float time = 5.14f;
		int seed = 1337;
		for (int index = 0; index < args.Length; index++)
		{
			if (string.Equals(args[index], "--time", StringComparison.OrdinalIgnoreCase))
			{
				if (index + 1 >= args.Length
					|| !float.TryParse(
						args[index + 1],
						NumberStyles.Float,
						CultureInfo.InvariantCulture,
						out time)
					|| !float.IsFinite(time)
					|| time is < 0f or > 30f)
				{
					throw new DisplaySnapshotSetupException(
						"Invalid --time value; expected a finite number from 0.0 through 30.0");
				}
				index++;
			}
			else if (string.Equals(args[index], "--seed", StringComparison.OrdinalIgnoreCase))
			{
				if (index + 1 >= args.Length
					|| !int.TryParse(
						args[index + 1],
						NumberStyles.Integer,
						CultureInfo.InvariantCulture,
						out seed))
				{
					throw new DisplaySnapshotSetupException(
						"Invalid --seed value; expected a signed 32-bit integer");
				}
				index++;
			}
			else
			{
				throw new DisplaySnapshotSetupException($"Unknown argument: '{args[index]}'");
			}
		}

		string timeSlug = time.ToString("0.00", CultureInfo.InvariantCulture);
		return new BoosterPackOpeningSnapshotVariant
		{
			TimeSeconds = time,
			Seed = seed,
			FileSlug = $"time-{timeSlug}-seed-{seed}",
		};
	}
}
