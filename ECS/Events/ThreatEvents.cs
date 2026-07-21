using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Increase or decrease an entity's Threat by Delta.
    /// </summary>
    public class ModifyThreatEvent
    {
        public Entity Target { get; set; }
        public int Delta { get; set; } = 0;
    }

    /// <summary>
    /// Sets an entity's Threat to a specific amount.
    /// </summary>
    public class SetThreatEvent
    {
        public Entity Target { get; set; }
        public int Amount { get; set; } = 0;
    }
}
