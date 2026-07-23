using System;
using System.IO;

namespace ChurchSuffering.ECS.Services
{
	/// <summary>
	/// Read-only helper that loads the release number from the shipped VERSION file.
	/// </summary>
	public static class GameVersionService
	{
		private static readonly Lazy<string> _displayLabel = new(LoadDisplayLabel);

		public static string DisplayLabel => _displayLabel.Value;

		private static string LoadDisplayLabel()
		{
			try
			{
				var path = Path.Combine(AppContext.BaseDirectory, "VERSION");
				if (!File.Exists(path)) return "v?";

				var version = File.ReadAllText(path).Trim();
				return string.IsNullOrEmpty(version) ? "v?" : "v" + version;
			}
			catch
			{
				return "v?";
			}
		}
	}
}
