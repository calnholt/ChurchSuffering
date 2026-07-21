using System.Collections.Generic;
using System.IO;

namespace ChurchSuffering.ECS.Data.Locations
{
	public static class LocationDefinitionCache
	{
		private static Dictionary<string, LocationDefinition> _cache;
		private static string _folderPath;
		private static readonly object _lock = new object();

		public static Dictionary<string, LocationDefinition> GetAll()
		{
			EnsureLoaded();
			return _cache;
		}

		public static bool TryGet(string id, out LocationDefinition def)
		{
			EnsureLoaded();
			return _cache.TryGetValue(id, out def);
		}

		public static void Reload()
		{
			lock (_lock)
			{
				var folder = ResolveFolderPath();
				_cache = LocationRepository.LoadFromFolder(folder);
			}
		}

		private static void EnsureLoaded()
		{
			if (_cache != null) return;
			lock (_lock)
			{
				if (_cache == null)
				{
					var folder = ResolveFolderPath();
					_cache = LocationRepository.LoadFromFolder(folder);
				}
			}
		}

		private static string ResolveFolderPath()
		{
			if (!string.IsNullOrEmpty(_folderPath) && Directory.Exists(_folderPath)) return _folderPath;
			_folderPath = Path.Combine(System.AppContext.BaseDirectory, "Content", "Data", "Locations");
			return _folderPath;
		}
	}
}


