using System;
using ChurchSuffering.ECS.Factories;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class NarrativeEventSnapshotVariant
	{
		public string EventTypeId { get; init; } = "icebound_tithe";
		public int VisibleOptionCount { get; init; } = 3;
		public string FileSlug { get; init; } = "icebound-tithe-options-3";

		public static NarrativeEventSnapshotVariant Parse(string[] args)
		{
			string eventId = "icebound_tithe";
			int? options = null;

			for (int i = 0; i < args.Length; i++)
			{
				if (string.Equals(args[i], "--event", StringComparison.OrdinalIgnoreCase))
				{
					if (i + 1 >= args.Length || string.IsNullOrWhiteSpace(args[i + 1]))
					{
						throw new DisplaySnapshotSetupException("Invalid --event value; expected event type id");
					}
					eventId = args[i + 1].Trim();
					i++;
				}
				else if (string.Equals(args[i], "--options", StringComparison.OrdinalIgnoreCase))
				{
					if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out int value) || value is < 1 or > 3)
					{
						throw new DisplaySnapshotSetupException("Invalid --options value; expected 1, 2, or 3");
					}
					options = value;
					i++;
				}
				else
				{
					throw new DisplaySnapshotSetupException($"Unknown argument: '{args[i]}'");
				}
			}

			ValidateEventTypeId(eventId);
			int visibleOptions = options ?? 3;

			return new NarrativeEventSnapshotVariant
			{
				EventTypeId = eventId,
				VisibleOptionCount = visibleOptions,
				FileSlug = BuildSlug(eventId, visibleOptions)
			};
		}

		private static void ValidateEventTypeId(string eventTypeId)
		{
			if (EventFactory.Create(eventTypeId) == null)
			{
				throw new DisplaySnapshotSetupException($"Unknown --event id: '{eventTypeId}'");
			}
		}

		private static string BuildSlug(string eventTypeId, int visibleOptions)
		{
			string eventSlug = eventTypeId.Replace('_', '-');
			return $"{eventSlug}-options-{visibleOptions}";
		}
	}
}
