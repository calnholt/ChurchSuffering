namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public enum EquipmentTooltipSnapshotVariantId
	{
		Active,
		Passive,
		Used,
	}

	public sealed class EquipmentTooltipSnapshotVariant
	{
		public EquipmentTooltipSnapshotVariantId Id { get; init; }
		public string FileSlug { get; init; }
		public string EquipmentId { get; init; }
		public bool IsUsed { get; init; }

		public static EquipmentTooltipSnapshotVariant Parse(string[] args)
		{
			if (args == null || args.Length == 0)
			{
				return Create(EquipmentTooltipSnapshotVariantId.Active);
			}
			if (args.Length != 1)
			{
				throw new DisplaySnapshotSetupException(
					"equipment-tooltip expects one variant: active, passive, or used");
			}

			return args[0].Trim().ToLowerInvariant() switch
			{
				"active" => Create(EquipmentTooltipSnapshotVariantId.Active),
				"passive" => Create(EquipmentTooltipSnapshotVariantId.Passive),
				"used" => Create(EquipmentTooltipSnapshotVariantId.Used),
				_ => throw new DisplaySnapshotSetupException(
					$"Unknown equipment-tooltip variant: '{args[0]}'"),
			};
		}

		private static EquipmentTooltipSnapshotVariant Create(
			EquipmentTooltipSnapshotVariantId id)
		{
			return id switch
			{
				EquipmentTooltipSnapshotVariantId.Passive => new EquipmentTooltipSnapshotVariant
				{
					Id = id,
					FileSlug = "passive",
					EquipmentId = "knightly_grieves",
				},
				EquipmentTooltipSnapshotVariantId.Used => new EquipmentTooltipSnapshotVariant
				{
					Id = id,
					FileSlug = "used",
					EquipmentId = "bulwark_plate",
					IsUsed = true,
				},
				_ => new EquipmentTooltipSnapshotVariant
				{
					Id = EquipmentTooltipSnapshotVariantId.Active,
					FileSlug = "active",
					EquipmentId = "bulwark_plate",
				},
			};
		}
	}
}
