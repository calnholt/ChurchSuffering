using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Tutorials
{
    public class TutorialDefinition
    {
        public string key { get; set; }                    // Unique identifier
        public string text { get; set; }                   // Display text
        public string targetType { get; set; }             // "entity_name", "component_hand", "ui_region", "component_any"
        public string targetId { get; set; }               // Entity name, component type, or region id
        public List<string> targetIds { get; set; }        // For multiple targets (optional)
        public string bubbleOrientation { get; set; }      // "top", "bottom", "left", "right"
        public string condition { get; set; }              // Optional condition key (e.g., "has_cost_card")
    }

    public class TutorialGroup
    {
        public string id { get; set; }                     // Group id (from filename)
        public List<TutorialDefinition> tutorials { get; set; }
    }
}

