using System;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public enum WayStationSnapshotVariant
	{
		Default,
		ModalFirstUnlock,
		ModalHammer,
		ModalFull,
	}

	public static class WayStationSnapshotVariantParser
	{
		public static WayStationSnapshotVariant Parse(string[] args)
		{
			if (args == null || args.Length == 0) return WayStationSnapshotVariant.Default;
			if (args.Length != 1)
				throw new DisplaySnapshotSetupException("Usage: snapshot waystation [modal-first-unlock|modal-hammer|modal-full]");

			return args[0].Trim().ToLowerInvariant() switch
			{
				"default" => WayStationSnapshotVariant.Default,
				"modal-first-unlock" => WayStationSnapshotVariant.ModalFirstUnlock,
				"modal-hammer" => WayStationSnapshotVariant.ModalHammer,
				"modal-full" => WayStationSnapshotVariant.ModalFull,
				_ => throw new DisplaySnapshotSetupException($"Unknown waystation variant: '{args[0]}'"),
			};
		}

		public static string FileSlug(this WayStationSnapshotVariant variant)
		{
			return variant switch
			{
				WayStationSnapshotVariant.ModalFirstUnlock => "modal-first-unlock",
				WayStationSnapshotVariant.ModalHammer => "modal-hammer",
				WayStationSnapshotVariant.ModalFull => "modal-full",
				_ => "default",
			};
		}
	}
}
