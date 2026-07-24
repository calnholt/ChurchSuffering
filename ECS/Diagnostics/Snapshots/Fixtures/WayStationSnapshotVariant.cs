namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public enum WayStationSnapshotVariant { Incense, Penance12 }

	public static class WayStationSnapshotVariantParser
	{
		public static WayStationSnapshotVariant Parse(string[] args)
		{
			if (args?.Length == 1 && args[0] == "incense") return WayStationSnapshotVariant.Incense;
			if (args?.Length == 1 && args[0] == "penance-12") return WayStationSnapshotVariant.Penance12;
			throw new DisplaySnapshotSetupException("Usage: dotnet run -- snapshot waystation <incense|penance-12>");
		}

		public static string FileSlug(this WayStationSnapshotVariant variant) => variant switch
		{
			WayStationSnapshotVariant.Incense => "incense",
			WayStationSnapshotVariant.Penance12 => "penance-12",
			_ => throw new DisplaySnapshotSetupException($"Unsupported Waystation snapshot variant: {variant}"),
		};
	}
}
