using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Events
{
    /// <summary>
    /// Apply a delta to a passive on an entity. Creates the passive if needed.
    /// If the resulting stacks are <= 0, removes the passive from the dictionary.
    /// </summary>
    public class ApplyPassiveEvent
    {
        public Entity Target { get; set; }
        public AppliedPassiveType Type { get; set; }
        public int Delta { get; set; }
    }

    /// <summary>
    /// Remove all stacks/instances of a passive type from the specified entity.
    /// </summary>
    public class RemovePassive
    {
        public Entity Owner { get; set; }
        public AppliedPassiveType Type { get; set; }
        /// <summary>
        /// Stack count removed. AppliedPassivesManagementSystem sets this from the owner's
        /// current stacks before removal when the passive is still present (overwrites 0 from generic publishers).
        /// </summary>
        public int Amount { get; set; }
    }

    /// <summary>
    /// Remove all stacks/instances of all passive types from the specified entity.
    /// </summary>
    public class RemoveAllPassives
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Emitted when a passive effect actually triggers (e.g., Burn deals damage).
    /// Used by UI to play feedback animations.
    /// </summary>
    public class PassiveTriggered
    {
        public Entity Owner { get; set; }
        public AppliedPassiveType Type { get; set; }
    }
    /// <summary>
    /// Update the stacks of a passive on an entity.
    /// </summary>
    public class UpdatePassive
    {
        public Entity Owner { get; set; }
        public AppliedPassiveType Type { get; set; }
        public int Delta { get; set; }
    }

    /// <summary>
    /// Emitted when a frostbite passive effect is triggered (similar to PassiveTriggered).
    /// Used by display system to play pulse animation.
    /// </summary>
    public class FrostbiteTriggered
    {
        public Entity Target { get; set; }
        public int DamageAmount { get; set; }
        public int TriggerIndex { get; set; }
        public int TriggerCount { get; set; }
    }

    /// <summary>
    /// Emitted when a tribulation effect is triggered (similar to MedalTriggered).
    /// Used by display system to play pulse animation.
    /// </summary>
    public class TribulationTriggered
    {
        public string QuestId { get; set; }
    }
}

