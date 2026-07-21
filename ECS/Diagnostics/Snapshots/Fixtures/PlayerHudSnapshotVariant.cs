using System;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public enum PlayerHudSnapshotVariantId
	{
		Default,
		Unavailable,
		IncomingDamage,
		LowHealth,
		Expanded,
		EnemyHealth,
	}

	public sealed class PlayerHudSnapshotVariant
	{
		public PlayerHudSnapshotVariantId Id { get; init; }
		public string FileSlug { get; init; }

		public static PlayerHudSnapshotVariant Parse(string[] args)
		{
			if (args == null || args.Length == 0)
			{
				return Create(PlayerHudSnapshotVariantId.Default);
			}
			if (args.Length != 1)
			{
				throw new DisplaySnapshotSetupException(
					"player-hud accepts exactly one variant");
			}

			return args[0].Trim().ToLowerInvariant() switch
			{
				"default" => Create(PlayerHudSnapshotVariantId.Default),
				"unavailable" => Create(PlayerHudSnapshotVariantId.Unavailable),
				"incoming-damage" => Create(PlayerHudSnapshotVariantId.IncomingDamage),
				"low-health" => Create(PlayerHudSnapshotVariantId.LowHealth),
				"expanded" => Create(PlayerHudSnapshotVariantId.Expanded),
				"enemy-health" => Create(PlayerHudSnapshotVariantId.EnemyHealth),
				_ => throw new DisplaySnapshotSetupException(
					$"Unknown player-hud variant: '{args[0]}'"),
			};
		}

		private static PlayerHudSnapshotVariant Create(PlayerHudSnapshotVariantId id)
		{
			string slug = id switch
			{
				PlayerHudSnapshotVariantId.IncomingDamage => "incoming-damage",
				PlayerHudSnapshotVariantId.LowHealth => "low-health",
				PlayerHudSnapshotVariantId.EnemyHealth => "enemy-health",
				_ => id.ToString().ToLowerInvariant(),
			};
			return new PlayerHudSnapshotVariant { Id = id, FileSlug = slug };
		}
	}
}
