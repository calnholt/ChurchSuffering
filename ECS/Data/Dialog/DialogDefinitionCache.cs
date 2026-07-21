using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Dialog
{
    public static class DialogDefinitionCache
    {
        public static Dictionary<string, DialogDefinition> GetAll()
        {
            return new Dictionary<string, DialogDefinition>(DialogCatalog.GetAll());
        }

        public static bool TryGet(string id, out DialogDefinition def)
        {
            return DialogCatalog.TryGet(id, out def);
        }

        public static void Reload() { }
    }
}

