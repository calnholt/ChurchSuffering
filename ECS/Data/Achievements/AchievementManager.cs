using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Achievements;

namespace ChurchSuffering.ECS.Data.Achievements
{
    /// <summary>
    /// Static manager for all achievements.
    /// Handles initialization, save/load, and completion notifications.
    /// Follows the SaveCache pattern used elsewhere in the codebase.
    /// </summary>
    public static class AchievementManager
    {
        private static readonly Dictionary<string, AchievementBase> _achievements = new();
        private static bool _initialized = false;
        private static readonly object _lock = new();

        #region Initialization

        /// <summary>
        /// Initialize the achievement system.
        /// Creates all achievement instances, loads progress from save, and registers listeners.
        /// Call this once during game startup.
        /// </summary>
        public static void Initialize(EntityManager entityManager)
        {
            if (_initialized) return;

            lock (_lock)
            {
                if (_initialized) return;

                Console.WriteLine("[AchievementManager] Initializing...");

                // Register all achievements
                RegisterAllAchievements();

                // Load progress from save and initialize each achievement
                var progressMap = LoadProgressFromSave();

                foreach (var achievement in _achievements.Values)
                {
                    progressMap.TryGetValue(achievement.Id, out var progress);
                    achievement.Initialize(progress ?? new AchievementProgress { AchievementId = achievement.Id }, entityManager);
                }

                _initialized = true;
                Console.WriteLine($"[AchievementManager] Initialized {_achievements.Count} achievements");
            }
        }

        /// <summary>
        /// Register all achievement instances.
        /// Add new achievements here as they are created.
        /// </summary>
        private static void RegisterAllAchievements()
        {
            // Example achievements demonstrating the pattern
            // These can be removed or replaced with real achievements
            Register(new Slayer());
            Register(new SkeletonSlayer());
            Register(new FirstVictory());
            Register(new JustGettingStarted());
            Register(new CardPlayer());
            Register(new RedCardApprentice());

            Register(new BoldInvestment());
            Register(new LivingOnTheEdge());
            Register(new KunaiStorm());
            Register(new FrozenButUnbroken());
            Register(new Archangel());
            Register(new QuestMaster());
            Register(new Unshackled());
            Register(new Kenosis());
            Register(new HexedHoard());
            Register(new MassRevival());
            Register(new Relentless());
            Register(new FadedSpectrum());
            Register(new MasterArtificer());

            foreach (var achievement in ClimbAchievementCatalog.CreateAll())
            {
                Register(achievement);
            }
        }

        /// <summary>
        /// Register a single achievement instance.
        /// </summary>
        public static void Register(AchievementBase achievement)
        {
            if (achievement == null || string.IsNullOrEmpty(achievement.Id))
            {
                Console.WriteLine("[AchievementManager] Cannot register null achievement or achievement without Id");
                return;
            }

            if (_achievements.ContainsKey(achievement.Id))
            {
                Console.WriteLine($"[AchievementManager] Achievement already registered: {achievement.Id}");
                return;
            }

            _achievements[achievement.Id] = achievement;
        }

        #endregion

        #region Access

        /// <summary>
        /// Get an achievement by ID.
        /// </summary>
        public static AchievementBase GetAchievement(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            _achievements.TryGetValue(id, out var achievement);
            return achievement;
        }

        /// <summary>
        /// Get all registered achievements.
        /// </summary>
        public static IEnumerable<AchievementBase> GetAll()
        {
            return _achievements.Values;
        }

        /// <summary>
        /// Get all completed achievements.
        /// </summary>
        public static IEnumerable<AchievementBase> GetCompleted()
        {
            return _achievements.Values.Where(a => a.IsCompleted);
        }

        /// <summary>
        /// Get all achievements that are complete but unseen.
        /// </summary>
        public static IEnumerable<AchievementBase> GetCompleteUnseen()
        {
            return _achievements.Values.Where(a => a.State == AchievementState.CompleteUnseen);
        }

        /// <summary>
        /// Get the count of unseen completed achievements (for badge display).
        /// </summary>
        public static int GetUnseenCount()
        {
            return _achievements.Values.Count(a => a.State == AchievementState.CompleteUnseen);
        }

        #endregion

        #region Completion

        /// <summary>
        /// Called by AchievementBase when an achievement is completed.
        /// Saves progress and publishes completion event.
        /// NOTE: Does NOT auto-reveal adjacent achievements. Call RevealAdjacentAchievements manually
        /// after the user clicks to reveal (handled by AchievementExplosionSystem).
        /// </summary>
        public static void NotifyCompleted(AchievementBase achievement)
        {
            if (achievement == null) return;

            Console.WriteLine($"[AchievementManager] Achievement completed: {achievement.Id}");

            // Save progress
            SaveProgress();

            // Publish completion event for UI
            EventManager.Publish(new AchievementCompletedEvent
            {
                AchievementId = achievement.Id,
                Name = achievement.Name,
                Description = achievement.Description
            });
        }

        /// <summary>
        /// Reveal achievements adjacent to the specified one (up, down, left, right).
        /// Called by AchievementExplosionSystem after user clicks a completed achievement.
        /// </summary>
        /// <param name="achievementId">The ID of the completed achievement to reveal around.</param>
        /// <returns>List of achievement IDs that were revealed.</returns>
        public static List<string> RevealAdjacentAchievements(string achievementId)
        {
            var revealed = new List<string>();
            var completed = GetAchievement(achievementId);
            if (completed == null) return revealed;

            int row = completed.Row;
            int col = completed.Column;

            foreach (var achievement in _achievements.Values)
            {
                if (achievement.State != AchievementState.Hidden) continue;

                // Check if adjacent (up, down, left, right)
                bool isAdjacent =
                    (achievement.Row == row - 1 && achievement.Column == col) || // Up
                    (achievement.Row == row + 1 && achievement.Column == col) || // Down
                    (achievement.Row == row && achievement.Column == col - 1) || // Left
                    (achievement.Row == row && achievement.Column == col + 1);   // Right

                if (isAdjacent)
                {
                    achievement.Reveal();
                    revealed.Add(achievement.Id);
                    Console.WriteLine($"[AchievementManager] Revealed adjacent: {achievement.Id}");
                }
            }

            return revealed;
        }

        #endregion

        #region Save/Load

        /// <summary>
        /// Load achievement progress from the save file.
        /// </summary>
        private static Dictionary<string, AchievementProgress> LoadProgressFromSave()
        {
            var save = SaveCache.GetAll();
            return save?.achievements ?? new Dictionary<string, AchievementProgress>();
        }

        /// <summary>
        /// Save all achievement progress to the save file.
        /// </summary>
        public static void SaveProgress()
        {
            lock (_lock)
            {
                var save = SaveCache.GetAll();
                if (save == null) return;

                if (save.achievements == null)
                {
                    save.achievements = new Dictionary<string, AchievementProgress>();
                }

                // Update save data with current progress from all achievements
                foreach (var achievement in _achievements.Values)
                {
                    var achievementProgress = GetAchievementProgress(achievement);
                    var progress = new AchievementProgress
                    {
                        AchievementId = achievement.Id,
                        CurrentValue = achievementProgress?.CurrentValue ?? 0,
                        IsCompleted = achievement.IsCompleted,
                        State = achievement.State,
                        trackedCardIds = achievementProgress?.trackedCardIds != null
                            ? new List<string>(achievementProgress.trackedCardIds)
                            : new List<string>(),
                    };

                    save.achievements[achievement.Id] = progress;
                }

                // Persist to disk
                SaveCache.PersistAchievements();
            }
        }

        /// <summary>
        /// Get progress data from an achievement (uses reflection since Progress is protected).
        /// </summary>
        private static AchievementProgress GetAchievementProgress(AchievementBase achievement)
        {
            var progressProperty = typeof(AchievementBase)
                .GetProperty("Progress", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            return progressProperty?.GetValue(achievement) as AchievementProgress;
        }

        #endregion

        #region Utility

        /// <summary>
        /// Reset the manager (useful for testing or new game).
        /// </summary>
        public static void Reset()
        {
            lock (_lock)
            {
                foreach (var achievement in _achievements.Values)
                {
                    achievement.UnregisterListeners();
                }
                _achievements.Clear();
                _initialized = false;
            }
        }

        #endregion
    }
}
