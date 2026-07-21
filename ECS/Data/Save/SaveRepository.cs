using System.IO;
using System.Text.Json;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Data.Loadouts;

namespace ChurchSuffering.ECS.Data.Save
{
	public static class SaveRepository
	{
		public static SaveFile Load(string fileAbsPath)
		{
			var result = new SaveFile();
			if (string.IsNullOrEmpty(fileAbsPath) || !File.Exists(fileAbsPath)) return result;
			try
			{
				var json = File.ReadAllText(fileAbsPath);
				var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
				var data = JsonSerializer.Deserialize<SaveFile>(json, opts) ?? new SaveFile();
				// Ensure non-null collections
				if (data.items == null) data.items = new System.Collections.Generic.List<SaveItem>();
				if (data.loadouts == null) data.loadouts = new System.Collections.Generic.List<LoadoutDefinition>();
				if (data.seenTutorials == null) data.seenTutorials = new System.Collections.Generic.List<string>();
				if (data.achievements == null) data.achievements = new System.Collections.Generic.Dictionary<string, AchievementProgress>();
				result = data;
			}
			catch (System.Exception ex)
			{
				System.Console.WriteLine($"[SaveRepository] Failed to parse {fileAbsPath}: {ex.Message}");
			}
			return result;
		}

		public static bool Save(string fileAbsPath, SaveFile data)
		{
			if (string.IsNullOrEmpty(fileAbsPath) || data == null) return false;
			string temporaryPath = $"{fileAbsPath}.tmp";
			try
			{
				var dir = Path.GetDirectoryName(fileAbsPath);
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
				var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
				File.WriteAllText(temporaryPath, json);
				File.Move(temporaryPath, fileAbsPath, overwrite: true);
				return true;
			}
			catch (System.Exception ex)
			{
				System.Console.WriteLine($"[SaveRepository] Failed to write {fileAbsPath}: {ex.Message}");
				try
				{
					if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
				}
				catch { }
				return false;
			}
		}
	}
}
