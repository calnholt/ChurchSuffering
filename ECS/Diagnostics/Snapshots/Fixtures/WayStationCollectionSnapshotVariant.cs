namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public enum WayStationCollectionSnapshotVariant
	{
		Cards,
		CardsHover,
		Saints,
		SaintsHover,
		Equipment,
		EquipmentHover,
	}

	public static class WayStationCollectionSnapshotVariantParser
	{
		public static WayStationCollectionSnapshotVariant Parse(string[] args)
		{
			if (args?.Length != 1)
				throw Usage();
			return args[0] switch
			{
				"cards" => WayStationCollectionSnapshotVariant.Cards,
				"cards-hover" => WayStationCollectionSnapshotVariant.CardsHover,
				"saints" => WayStationCollectionSnapshotVariant.Saints,
				"saints-hover" => WayStationCollectionSnapshotVariant.SaintsHover,
				"equipment" => WayStationCollectionSnapshotVariant.Equipment,
				"equipment-hover" => WayStationCollectionSnapshotVariant.EquipmentHover,
				_ => throw Usage(),
			};
		}

		public static string FileSlug(this WayStationCollectionSnapshotVariant variant) => variant switch
		{
			WayStationCollectionSnapshotVariant.CardsHover => "cards-hover.png",
			WayStationCollectionSnapshotVariant.Saints => "saints.png",
			WayStationCollectionSnapshotVariant.SaintsHover => "saints-hover.png",
			WayStationCollectionSnapshotVariant.Equipment => "equipment.png",
			WayStationCollectionSnapshotVariant.EquipmentHover => "equipment-hover.png",
			_ => "cards.png",
		};

		private static DisplaySnapshotSetupException Usage() =>
			new("Usage: dotnet run -- snapshot waystation-collection <cards|cards-hover|saints|saints-hover|equipment|equipment-hover>");
	}
}
