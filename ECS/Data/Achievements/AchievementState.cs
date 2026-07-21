namespace ChurchSuffering.ECS.Data.Achievements
{
    /// <summary>
    /// State machine for achievement visibility and completion status.
    /// Supports grid-based discovery and deferred reward presentation.
    /// </summary>
    public enum AchievementState
    {
        /// <summary>
        /// Achievement is not visible to the player (fog of war).
        /// </summary>
        Hidden,

        /// <summary>
        /// Achievement is visible but not yet completed.
        /// </summary>
        Visible,

        /// <summary>
        /// Achievement is completed but the player hasn't seen the completion yet.
        /// Used for popup alerts and UI badges.
        /// </summary>
        CompleteUnseen,

        /// <summary>
        /// Achievement is completed and the player has acknowledged it.
        /// </summary>
        CompleteSeen
    }
}
