using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.Save;

namespace ChurchSuffering.ECS.Data.Loadouts
{
	public static class LoadoutDefinitionCache
	{
		public static bool TryGet(string id, out LoadoutDefinition def)
		{
			def = SaveCache.GetLoadout(id);
			return def != null;
		}

		public static Dictionary<string, LoadoutDefinition> GetAll()
		{
			var list = SaveCache.GetAllLoadouts();
			var dict = new Dictionary<string, LoadoutDefinition>();
			if (list != null)
			{
				foreach (var l in list)
				{
					if (l != null && !string.IsNullOrEmpty(l.id))
					{
						dict[l.id] = l;
					}
				}
			}
			return dict;
		}

		public static void Reload()
		{
			SaveCache.Reload();
		}
	}
}
