namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public enum WayStationSnapshotVariant { Penance12 }

	public static class WayStationSnapshotVariantParser
	{
		public static WayStationSnapshotVariant Parse(string[] args)
		{
			if (args?.Length == 1 && args[0] == "penance-12") return WayStationSnapshotVariant.Penance12;
			throw new DisplaySnapshotSetupException("Usage: dotnet run -- snapshot waystation penance-12");
		}

		public static string FileSlug(this WayStationSnapshotVariant variant) => "penance-12";
	}
}
