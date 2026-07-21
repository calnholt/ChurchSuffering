using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Achievements
{
    /// <summary>
    /// Serializable progress data for a single achievement.
    /// Stored in the save file keyed by AchievementId.
    /// </summary>
    public class AchievementProgress
    {
        /// <summary>
        /// Unique identifier matching the achievement's Id.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Current progress value (e.g., kills counted, gold earned).
        /// Interpretation depends on the specific achievement.
        /// </summary>
        public int CurrentValue { get; set; } = 0;

        /// <summary>
        /// Whether this achievement has been completed.
        /// </summary>
        public bool IsCompleted { get; set; } = false;

        /// <summary>
        /// Current visibility/completion state in the state machine.
        /// </summary>
        public AchievementState State { get; set; } = AchievementState.Hidden;

        /// <summary>
        /// Unique identifiers tracked for achievements that count distinct items (e.g. upgraded card ids).
        /// </summary>
        public List<string> trackedCardIds { get; set; } = new List<string>();
    }
}
