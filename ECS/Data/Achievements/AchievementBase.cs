using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Data.Achievements
{
    /// <summary>
    /// Abstract base class for all achievements.
    /// Each achievement defines its own metadata, subscribes to relevant events,
    /// and tracks its own progress independently.
    /// </summary>
    public abstract class AchievementBase
    {
        #region Metadata (defined by subclass)

        /// <summary>
        /// Unique identifier for this achievement.
        /// </summary>
        public string Id { get; protected set; } = string.Empty;

        /// <summary>
        /// Display name shown to the player.
        /// </summary>
        public string Name { get; protected set; } = string.Empty;

        /// <summary>
        /// Description of how to earn this achievement.
        /// </summary>
        public string Description { get; protected set; } = string.Empty;

        /// <summary>
        /// Grid row position (for grid-based discovery UI).
        /// </summary>
        public int Row { get; protected set; } = 0;

        /// <summary>
        /// Grid column position (for grid-based discovery UI).
        /// </summary>
        public int Column { get; protected set; } = 0;

        /// <summary>
        /// Whether this achievement starts visible (not hidden in fog).
        /// </summary>
        public bool StartsVisible { get; protected set; } = false;

        /// <summary>
        /// Points awarded when this achievement is completed.
        /// Used for the achievement meter progress bar.
        /// </summary>
        public int Points { get; protected set; } = 10;

        /// <summary>
        /// Target value for counter-based achievements (e.g., "Kill 10 enemies").
        /// Used for progress display. If 0, no progress is shown.
        /// </summary>
        public int TargetValue { get; protected set; } = 0;

        protected EntityManager EntityManager;

        #endregion

        #region Runtime State

        /// <summary>
        /// Current state in the achievement state machine.
        /// </summary>
        public AchievementState State
        {
            get => Progress?.State ?? AchievementState.Hidden;
            protected set
            {
                if (Progress != null)
                {
                    Progress.State = value;
                }
            }
        }

        /// <summary>
        /// Per-achievement progress data (persisted to save file).
        /// </summary>
        protected AchievementProgress Progress { get; private set; }

        /// <summary>
        /// Whether this achievement is already completed.
        /// </summary>
        public bool IsCompleted => Progress?.IsCompleted ?? false;

        #endregion

        #region Lifecycle

        /// <summary>
        /// Initialize the achievement with its progress data.
        /// Called by AchievementManager after loading progress from save.
        /// </summary>
        public void Initialize(AchievementProgress progress, EntityManager entityManager)
        {
            Progress = progress ?? new AchievementProgress { AchievementId = Id };
            this.EntityManager = entityManager;
            // Ensure the progress has the correct ID
            if (string.IsNullOrEmpty(Progress.AchievementId))
            {
                Progress.AchievementId = Id;
            }

            // Set initial visibility if this achievement starts visible and hasn't been touched
            if (StartsVisible && Progress.State == AchievementState.Hidden && !Progress.IsCompleted)
            {
                Progress.State = AchievementState.Visible;
            }

            // Only register listeners if not already completed
            if (!IsCompleted)
            {
                RegisterListeners();
            }
        }

        /// <summary>
        /// Subscribe to the events this achievement needs to track.
        /// Override in subclass to subscribe to specific events.
        /// </summary>
        public abstract void RegisterListeners();

        /// <summary>
        /// Unsubscribe from all events.
        /// Override in subclass to unsubscribe from specific events.
        /// </summary>
        public abstract void UnregisterListeners();

        /// <summary>
        /// Called when progress is updated. Subclass should check if completion criteria are met.
        /// </summary>
        protected abstract void EvaluateCompletion();

        #endregion

        #region Progress Helpers

        /// <summary>
        /// Increment the progress counter by the specified amount.
        /// </summary>
        protected void IncrementProgress(int amount = 1)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (Progress == null || IsCompleted) return;

            Progress.CurrentValue += amount;
            EvaluateCompletion();
        }

        /// <summary>
        /// Set the progress counter to a specific value.
        /// </summary>
        protected void SetProgress(int value)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (Progress == null || IsCompleted) return;

            Progress.CurrentValue = value;
            EvaluateCompletion();
        }

        /// <summary>
        /// Get the current progress value.
        /// </summary>
        protected int GetProgress()
        {
            return Progress?.CurrentValue ?? 0;
        }

        /// <summary>
        /// Track a unique card id for achievements that count distinct upgrades.
        /// Persists progress immediately so meta survives across runs.
        /// </summary>
        protected void TrackUniqueCardId(string cardId)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (Progress == null || IsCompleted) return;
            if (string.IsNullOrWhiteSpace(cardId)) return;

            Progress.trackedCardIds ??= new List<string>();
            if (Progress.trackedCardIds.Contains(cardId)) return;

            Progress.trackedCardIds.Add(cardId);
            Progress.CurrentValue = Progress.trackedCardIds.Count;
            AchievementManager.SaveProgress();
            EvaluateCompletion();
        }

        #endregion

        #region Completion

        /// <summary>
        /// Mark this achievement as complete.
        /// Transitions to CompleteUnseen state and notifies the manager.
        /// </summary>
        protected void Complete()
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (Progress == null) return;

            // Already completed
            if (Progress.IsCompleted ||
                State == AchievementState.CompleteUnseen ||
                State == AchievementState.CompleteSeen)
            {
                return;
            }

            Console.WriteLine($"[Achievement] Completed: {Id} - {Name}");

            Progress.IsCompleted = true;
            Progress.State = AchievementState.CompleteUnseen;

            // Stop listening for events
            UnregisterListeners();

            // Notify the manager
            AchievementManager.NotifyCompleted(this);
        }

        /// <summary>
        /// Mark the achievement as seen by the player (after viewing completion).
        /// </summary>
        public void MarkAsSeen()
        {
            if (State == AchievementState.CompleteUnseen)
            {
                State = AchievementState.CompleteSeen;
                AchievementManager.SaveProgress();
            }
        }

        /// <summary>
        /// Reveal this achievement (transition from Hidden to Visible).
        /// Used by grid discovery when adjacent achievements are completed.
        /// </summary>
        public void Reveal()
        {
            if (State == AchievementState.Hidden)
            {
                State = AchievementState.Visible;
                AchievementManager.SaveProgress();
            }
        }

        #endregion
    }
}
