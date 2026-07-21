using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Components
{
    /// <summary>
    /// Marker for achievement grid cell entities.
    /// </summary>
    public class AchievementGridItem : IComponent
    {
        public Entity Owner { get; set; }

        /// <summary>
        /// The achievement ID this grid item represents.
        /// </summary>
        public string AchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Grid row position.
        /// </summary>
        public int Row { get; set; } = 0;

        /// <summary>
        /// Grid column position.
        /// </summary>
        public int Column { get; set; } = 0;

        /// <summary>
        /// Current scale for hover/animation effects.
        /// </summary>
        public float CurrentScale { get; set; } = 1f;

        /// <summary>
        /// Target scale for smooth scaling transitions.
        /// </summary>
        public float TargetScale { get; set; } = 1f;

        /// <summary>
        /// Current alpha for fade animations.
        /// </summary>
        public float Alpha { get; set; } = 1f;

        /// <summary>
        /// Whether this item is currently animating a completion.
        /// </summary>
        public bool IsAnimatingCompletion { get; set; } = false;

        /// <summary>
        /// Whether this item is currently animating a reveal.
        /// </summary>
        public bool IsAnimatingReveal { get; set; } = false;

        /// <summary>
        /// Current offset from base position during explosion animation.
        /// </summary>
        public Vector2 ExplosionOffset { get; set; } = Vector2.Zero;

        /// <summary>
        /// Whether this item is participating in an explosion animation.
        /// </summary>
        public bool IsAnimatingExplosion { get; set; } = false;

        /// <summary>
        /// Progress (0-1) for reveal pulse animation on newly revealed cells.
        /// </summary>
        public float RevealPulseProgress { get; set; } = 0f;

        /// <summary>
        /// Whether this cell was just revealed and should show pulse animation.
        /// </summary>
        public bool IsNewlyRevealed { get; set; } = false;
    }

    /// <summary>
    /// Marker for the back button in the Achievement scene.
    /// </summary>
    public class AchievementBackButton : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// Marker for the Achievement access button in the Location scene.
    /// </summary>
    public class AchievementButton : IComponent
    {
        public Entity Owner { get; set; }
    }

    /// <summary>
    /// State component for the Achievement scene.
    /// </summary>
    public class AchievementSceneState : IComponent
    {
        public Entity Owner { get; set; }

        /// <summary>
        /// Currently hovered achievement ID (empty if none).
        /// </summary>
        public string HoveredAchievementId { get; set; } = string.Empty;

        /// <summary>
        /// Whether we're currently playing completion animations.
        /// </summary>
        public bool IsPlayingCompletionAnimations { get; set; } = false;

        /// <summary>
        /// Whether we're currently playing reveal animations.
        /// </summary>
        public bool IsPlayingRevealAnimations { get; set; } = false;

        /// <summary>
        /// Time elapsed in current animation sequence.
        /// </summary>
        public float AnimationTime { get; set; } = 0f;

        /// <summary>
        /// Index of current animation in the sequence.
        /// </summary>
        public int AnimationIndex { get; set; } = 0;
    }
}
