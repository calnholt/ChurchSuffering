using System.Collections.Generic;
using System.Linq;

namespace Crusaders30XX.ECS.Data.Tutorials
{
    public static class TutorialDefinitionCache
    {
        private static readonly Dictionary<string, TutorialDefinition> Definitions =
            BuildDefinitions();

        public static Dictionary<string, TutorialDefinition> GetAll()
        {
            return Definitions;
        }

        public static bool TryGet(string key, out TutorialDefinition def)
        {
            return Definitions.TryGetValue(key, out def);
        }

        public static void Reload() { }

        private static Dictionary<string, TutorialDefinition> BuildDefinitions()
        {
            var definitions = GuidedTutorialDefinitions.GuidedMessages
                .ToDictionary(def => def.key, def => def);

            void Add(string key, string text, string targetType, string targetId, string orientation, string condition = null)
            {
                definitions[key] = new TutorialDefinition
                {
                    key = key,
                    text = text,
                    targetType = targetType,
                    targetId = targetId,
                    bubbleOrientation = orientation,
                    condition = condition,
                };
            }

            Add("equipment", "Equipment can block or activate for an effect. Uses are limited.", "equipment", "Equipment", "right", "has_equipment");
            return definitions;
        }
    }
}
