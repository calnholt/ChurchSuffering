using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Crusaders30XX.ECS.Data.Medals
{
	public static class SaintBlurbDefinitionCache
	{
		private static Dictionary<string, SaintBlurbDefinition> _cache;
		private static readonly object _lock = new object();

		public static IReadOnlyDictionary<string, SaintBlurbDefinition> GetAll()
		{
			EnsureLoaded();
			return _cache;
		}

		public static bool TryGet(string medalId, out SaintBlurbDefinition definition)
		{
			EnsureLoaded();
			if (string.IsNullOrWhiteSpace(medalId))
			{
				definition = null;
				return false;
			}
			return _cache.TryGetValue(medalId, out definition);
		}

		public static void Reload()
		{
			lock (_lock)
			{
				_cache = LoadFromFile(ResolveFilePath());
			}
		}

		private static void EnsureLoaded()
		{
			if (_cache != null) return;
			lock (_lock)
			{
				if (_cache == null)
				{
					_cache = LoadFromFile(ResolveFilePath());
				}
			}
		}

		private static Dictionary<string, SaintBlurbDefinition> LoadFromFile(string path)
		{
			var result = new Dictionary<string, SaintBlurbDefinition>(StringComparer.OrdinalIgnoreCase);
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return result;

			try
			{
				var json = File.ReadAllText(path);
				var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var file = JsonSerializer.Deserialize<SaintBlurbFile>(json, options);
				foreach (var definition in file?.medals ?? Enumerable.Empty<SaintBlurbDefinition>())
				{
					if (definition == null || string.IsNullOrWhiteSpace(definition.medalId)) continue;
					definition.bioParagraphs ??= new List<string>();
					result[definition.medalId] = definition;
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[SaintBlurbDefinitionCache] Failed to parse {path}: {ex.Message}");
			}
			return result;
		}

		private static string ResolveFilePath()
		{
			return Path.Combine(AppContext.BaseDirectory, "Content", "Data", "Medals", "saint_blurbs.json");
		}
	}
}
