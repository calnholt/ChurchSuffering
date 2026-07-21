namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Published when a grid item is hovered in the Achievement scene.
    /// Used to update the description panel.
    /// </summary>
    public class AchievementGridItemHovered
    {
        /// <summary>
        /// The achievement ID being hovered, or empty if unhovered.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Published when a completion animation finishes for an achievement.
    /// </summary>
    public class AchievementAnimationComplete
    {
        /// <summary>
        /// The achievement ID that finished animating.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Type of animation that completed.
        /// </summary>
        public AchievementAnimationType AnimationType { get; set; } = AchievementAnimationType.Completion;
    }

    /// <summary>
    /// Type of achievement animation.
    /// </summary>
    public enum AchievementAnimationType
    {
        /// <summary>
        /// Animation for a newly completed achievement.
        /// </summary>
        Completion,

        /// <summary>
        /// Animation for revealing a hidden achievement.
        /// </summary>
        Reveal
    }

    /// <summary>
    /// Published when all completion and reveal animations are done.
    /// </summary>
    public class AchievementAnimationsComplete { }

    /// <summary>
    /// Published when a completed-unseen achievement grid cell is clicked.
    /// Triggers the explosion and reveal animation sequence.
    /// </summary>
    public class AchievementRevealClickedEvent
    {
        /// <summary>
        /// The achievement ID that was clicked.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Grid row of the clicked achievement.
        /// </summary>
        public int Row { get; set; } = 0;

        /// <summary>
        /// Grid column of the clicked achievement.
        /// </summary>
        public int Column { get; set; } = 0;

        /// <summary>
        /// Whether this should play a smaller, weaker explosion animation.
        /// </summary>
        public bool IsSmall { get; set; } = false;
    }
}
