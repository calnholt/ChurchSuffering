using System.IO;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Data.Telemetry;

namespace ChurchSuffering.ECS.Services
{
	public static class CardUsageTelemetryRuntime
	{
		private static readonly object _lock = new();

		public static CardUsageTelemetryStore Store { get; private set; }

		public static void Initialize(bool enabled)
		{
			lock (_lock)
			{
				if (!enabled)
				{
					Store = null;
					return;
				}

				string directory = SaveCache.GetSaveDirectory();
				if (string.IsNullOrWhiteSpace(directory)) return;

				Store = new CardUsageTelemetryStore(
					Path.Combine(directory, "card-usage.json"),
					Path.Combine(directory, "card-usage.csv"));

				var save = SaveCache.GetAll();
				Store.ReconcileRun(save?.isRunActive == true, save?.runMapSeed ?? 0);
			}
		}

		public static void StartNewRun(int runMapSeed)
		{
			lock (_lock)
			{
				Store?.StartNewRun(runMapSeed);
			}
		}

		public static void EndCurrentRun()
		{
			lock (_lock)
			{
				Store?.EndActiveRun();
			}
		}

		public static void ExportCsv()
		{
			lock (_lock)
			{
				Store?.ExportCsv();
			}
		}
	}
}
