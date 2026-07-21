namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Published when an achievement is completed.
    /// UI systems can subscribe to show popups, badges, etc.
    /// </summary>
    public class AchievementCompletedEvent
    {
        /// <summary>
        /// The unique ID of the completed achievement.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Display name of the completed achievement.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Description of the completed achievement.
        /// </summary>
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Published when an achievement's progress is updated (but not yet complete).
    /// UI systems can subscribe to show progress indicators.
    /// </summary>
    public class AchievementProgressUpdatedEvent
    {
        /// <summary>
        /// The unique ID of the achievement.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Current progress value.
        /// </summary>
        public int CurrentValue { get; set; } = 0;
    }

    /// <summary>Published once a completed achievement has been acknowledged in the Achievement scene.</summary>
    public class AchievementSeenEvent
    {
        public string AchievementId { get; set; } = string.Empty;
        public int Points { get; set; }
    }
}
