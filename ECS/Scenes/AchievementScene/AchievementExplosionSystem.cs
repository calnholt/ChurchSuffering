using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Handles the explosion and reveal animations when clicking a completed achievement.
    /// Phase 1: All grid cells push outward from the clicked cell (closer = more displacement)
    /// Phase 2: Newly revealed cells pulse together and transition color
    /// </summary>
    [DebugTab("Achievement Explosion")]
    public class AchievementExplosionSystem : Core.System
    {
        // Animation state
        private enum AnimationPhase { Idle, Explosion, Reveal }
        private AnimationPhase _phase = AnimationPhase.Idle;
        private float _animationTime = 0f;

        // Source of the explosion
        private int _sourceRow = 0;
        private int _sourceCol = 0;
        private string _sourceAchievementId = string.Empty;
        private bool _isSmall = false;

        // List of achievement IDs that were revealed
        private List<string> _revealedIds = new();

        // Random noise for explosion effect
        private Random _explosionRandom;
        private Dictionary<(int row, int col), (float angleNoise, float magnitudeNoise)> _cellNoise = new();

        // Debug mode - click any cell to test explosion visuals without game logic
        private bool _isDebugMode = false;

        // Configuration
        [DebugEditable(DisplayName = "Debug Mode (Click Any Cell)", Step = 1f, Min = 0f, Max = 1f)]
        public float DebugModeToggle
        {
            get => _isDebugMode ? 1f : 0f;
            set => _isDebugMode = value >= 0.5f;
        }

        [DebugEditable(DisplayName = "Explosion Duration", Step = 0.05f, Min = 0.1f, Max = 1f)]
        public float ExplosionDuration { get; set; } = 0.45f;

        [DebugEditable(DisplayName = "Explosion Max Offset", Step = 5f, Min = 10f, Max = 1000f)]
        public float ExplosionMaxOffset { get; set; } = 90f;

        [DebugEditable(DisplayName = "Explosion Falloff", Step = 0.1f, Min = 0.5f, Max = 5f)]
        public float ExplosionFalloff { get; set; } = 0.5f;

        [DebugEditable(DisplayName = "Reveal Pulse Duration", Step = 0.1f, Min = 0.2f, Max = 2f)]
        public float RevealPulseDuration { get; set; } = 0.6f;

        [DebugEditable(DisplayName = "Reveal Pulse Scale", Step = 0.05f, Min = 1f, Max = 1.5f)]
        public float RevealPulseScale { get; set; } = 1f;

        [DebugEditable(DisplayName = "Source Pulse Duration", Step = 0.1f, Min = 0.2f, Max = 1f)]
        public float SourcePulseDuration { get; set; } = 0.5f;

        [DebugEditable(DisplayName = "Source Pulse Scale", Step = 0.1f, Min = 1f, Max = 2f)]
        public float SourcePulseScale { get; set; } = 2f;

        [DebugEditable(DisplayName = "Direction Noise (Degrees)", Step = 5f, Min = 0f, Max = 90f)]
        public float DirectionNoiseDegrees { get; set; } = 0f;

        [DebugEditable(DisplayName = "Magnitude Noise (%)", Step = 0.05f, Min = 0f, Max = 0.5f)]
        public float MagnitudeNoisePercent { get; set; } = 0f;

        [DebugEditable(DisplayName = "Small Explosion Multiplier", Step = 0.05f, Min = 0.1f, Max = 1f)]
        public float SmallExplosionMultiplier { get; set; } = 0.1f;

        public AchievementExplosionSystem(EntityManager em) : base(em)
        {
            EventManager.Subscribe<AchievementRevealClickedEvent>(OnRevealClicked);
            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
        }

        private void OnLoadScene(LoadSceneEvent evt)
        {
            if (evt.Scene != SceneId.Achievement) return;

            // Reset state when entering the scene
            _phase = AnimationPhase.Idle;
            _animationTime = 0f;
            _isSmall = false;
            _revealedIds.Clear();
            StateSingleton.PreventClicking = false;
        }

        private void OnRevealClicked(AchievementRevealClickedEvent evt)
        {
            if (_phase != AnimationPhase.Idle) return;

            Console.WriteLine($"[AchievementExplosionSystem] Reveal clicked: {evt.AchievementId} at ({evt.Row}, {evt.Column}), IsSmall: {evt.IsSmall}");

            _sourceAchievementId = evt.AchievementId;
            _sourceRow = evt.Row;
            _sourceCol = evt.Column;
            _isSmall = evt.IsSmall;

            // Start explosion animation
            _phase = AnimationPhase.Explosion;
            _animationTime = 0f;

            // Block UI interactions
            StateSingleton.PreventClicking = true;

            // Generate random noise for this explosion
            GenerateExplosionNoise();

            // Mark all grid items as participating in explosion
            foreach (var ent in GetGridEntities())
            {
                var gridItem = ent.GetComponent<AchievementGridItem>();
                if (gridItem != null)
                {
                    gridItem.IsAnimatingExplosion = true;
                }
            }

            // Animate the source cell completion
            var sourceEnt = GetGridEntity(_sourceRow, _sourceCol);
            if (sourceEnt != null)
            {
                var gridItem = sourceEnt.GetComponent<AchievementGridItem>();
                if (gridItem != null)
                {
                    gridItem.IsAnimatingCompletion = true;
                }
            }
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var scene = entity.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Debug mode: check for clicks on any grid cell
            if (_isDebugMode && _phase == AnimationPhase.Idle)
            {
                CheckDebugClicks();
            }

            if (_phase == AnimationPhase.Idle) return;

            _animationTime += dt;

            switch (_phase)
            {
                case AnimationPhase.Explosion:
                    UpdateExplosionPhase(dt);
                    break;
                case AnimationPhase.Reveal:
                    UpdateRevealPhase(dt);
                    break;
            }
        }

        private void CheckDebugClicks()
        {
            foreach (var ent in GetGridEntities())
            {
                var ui = ent.GetComponent<UIElement>();
                var gridItem = ent.GetComponent<AchievementGridItem>();

                if (ui == null || gridItem == null) continue;

                // In debug mode, make all cells interactable
                ui.IsInteractable = true;

                if (ui.IsClicked)
                {
                    Console.WriteLine($"[AchievementExplosionSystem] DEBUG: Clicked cell at ({gridItem.Row}, {gridItem.Column})");
                    StartDebugExplosion(gridItem.Row, gridItem.Column);
                    break;
                }
            }
        }

        private void StartDebugExplosion(int row, int col)
        {
            _sourceRow = row;
            _sourceCol = col;
            _sourceAchievementId = string.Empty; // No achievement ID in debug mode
            _isSmall = false; // Reset small flag for debug explosions

            // Start explosion animation
            _phase = AnimationPhase.Explosion;
            _animationTime = 0f;

            // Block UI interactions
            StateSingleton.PreventClicking = true;

            // Generate random noise for this explosion
            GenerateExplosionNoise();

            // Mark all grid items as participating in explosion
            foreach (var ent in GetGridEntities())
            {
                var gridItem = ent.GetComponent<AchievementGridItem>();
                if (gridItem != null)
                {
                    gridItem.IsAnimatingExplosion = true;
                }
            }

            // Animate the source cell completion
            var sourceEnt = GetGridEntity(_sourceRow, _sourceCol);
            if (sourceEnt != null)
            {
                var gridItem = sourceEnt.GetComponent<AchievementGridItem>();
                if (gridItem != null)
                {
                    gridItem.IsAnimatingCompletion = true;
                }
            }
        }

        private void UpdateExplosionPhase(float dt)
        {
            float progress = Math.Min(1f, _animationTime / ExplosionDuration);

            // Easing: ease out back for push, ease in for return
            float easedProgress = EaseOutBack(progress);
            float returnProgress = progress > 0.5f ? (progress - 0.5f) * 2f : 0f;
            float returnEased = EaseInQuad(returnProgress);

            // Calculate combined progress (out then back)
            float effectStrength = progress < 0.5f
                ? easedProgress
                : easedProgress * (1f - returnEased);

            // Update source cell scale animation
            var sourceEnt = GetGridEntity(_sourceRow, _sourceCol);
            if (sourceEnt != null)
            {
                var gridItem = sourceEnt.GetComponent<AchievementGridItem>();
                if (gridItem != null)
                {
                    // Pulse animation for the source cell
                    float pulseProgress = Math.Min(1f, _animationTime / SourcePulseDuration);
                    float pulseScale = SourcePulseScale;
                    if (_isSmall) pulseScale = 1f + (SourcePulseScale - 1f) * SmallExplosionMultiplier;

                    gridItem.CurrentScale = 1f + (pulseScale - 1f) * (float)Math.Sin(pulseProgress * Math.PI);
                }
            }

            // Update explosion offsets for all cells
            Vector2 sourceCenter = GetCellCenter(_sourceRow, _sourceCol);

            foreach (var ent in GetGridEntities())
            {
                var gridItem = ent.GetComponent<AchievementGridItem>();
                if (gridItem == null) continue;

                // Skip the source cell for offset (it stays in place but pulses)
                if (gridItem.Row == _sourceRow && gridItem.Column == _sourceCol)
                {
                    gridItem.ExplosionOffset = Vector2.Zero;
                    continue;
                }

                Vector2 cellCenter = GetCellCenter(gridItem.Row, gridItem.Column);
                Vector2 direction = cellCenter - sourceCenter;

                float distance = direction.Length();
                if (distance > 0)
                {
                    direction.Normalize();

                    // Apply noise to direction and magnitude
                    float angleNoise = 0f;
                    float magnitudeNoise = 1f;
                    if (_cellNoise.TryGetValue((gridItem.Row, gridItem.Column), out var noise))
                    {
                        angleNoise = noise.angleNoise;
                        magnitudeNoise = noise.magnitudeNoise;
                    }

                    // Rotate direction by noise angle
                    Vector2 noisyDirection = RotateVector(direction, angleNoise);

                    // Falloff: closer cells move more
                    float distanceFactor = 1f / (float)Math.Pow(distance / 50f + 1f, ExplosionFalloff);
                    float offset = ExplosionMaxOffset * distanceFactor * effectStrength * magnitudeNoise;

                    if (_isSmall) offset *= SmallExplosionMultiplier;

                    gridItem.ExplosionOffset = noisyDirection * offset;
                }
                else
                {
                    gridItem.ExplosionOffset = Vector2.Zero;
                }
            }

            // Reveal adjacent achievements at explosion midpoint (skip in debug mode or if small)
            if (!_isDebugMode && !_isSmall && _animationTime >= ExplosionDuration * 0.5f && _revealedIds.Count == 0)
            {
                _revealedIds = AchievementManager.RevealAdjacentAchievements(_sourceAchievementId);

                // Mark revealed cells for pulse animation
                foreach (var revealedId in _revealedIds)
                {
                    var achievement = AchievementManager.GetAchievement(revealedId);
                    if (achievement != null)
                    {
                        var revealedEnt = GetGridEntity(achievement.Row, achievement.Column);
                        if (revealedEnt != null)
                        {
                            var gridItem = revealedEnt.GetComponent<AchievementGridItem>();
                            if (gridItem != null)
                            {
                                gridItem.IsNewlyRevealed = true;
                                gridItem.RevealPulseProgress = 0f;
                                gridItem.IsAnimatingReveal = true;
                            }
                        }
                    }
                }

                Console.WriteLine($"[AchievementExplosionSystem] Revealed {_revealedIds.Count} adjacent achievements");
            }

            // Transition to reveal phase
            if (_animationTime >= ExplosionDuration)
            {
                // Clear explosion offsets
                foreach (var ent in GetGridEntities())
                {
                    var gridItem = ent.GetComponent<AchievementGridItem>();
                    if (gridItem != null)
                    {
                        gridItem.ExplosionOffset = Vector2.Zero;
                        gridItem.IsAnimatingExplosion = false;
                    }
                }

                // Reset source cell
                if (sourceEnt != null)
                {
                    var gridItem = sourceEnt.GetComponent<AchievementGridItem>();
                    if (gridItem != null)
                    {
                        gridItem.IsAnimatingCompletion = false;
                        gridItem.CurrentScale = 1f;
                    }
                }

                // Mark the source achievement as seen (skip in debug mode)
                if (!_isDebugMode)
                {
                    var achievement = AchievementManager.GetAchievement(_sourceAchievementId);
                    if (achievement != null)
                    {
                        achievement.MarkAsSeen();
                    }
                }

                // Move to reveal phase if there are reveals (skip in debug mode or if small)
                if (!_isDebugMode && !_isSmall && _revealedIds.Count > 0)
                {
                    _phase = AnimationPhase.Reveal;
                    _animationTime = 0f;
                }
                else
                {
                    CompleteAnimation();
                }
            }
        }

        private void UpdateRevealPhase(float dt)
        {
            float progress = Math.Min(1f, _animationTime / RevealPulseDuration);

            // Update all revealed cells with synchronized pulse
            foreach (var revealedId in _revealedIds)
            {
                var achievement = AchievementManager.GetAchievement(revealedId);
                if (achievement == null) continue;

                var ent = GetGridEntity(achievement.Row, achievement.Column);
                if (ent == null) continue;

                var gridItem = ent.GetComponent<AchievementGridItem>();
                if (gridItem == null) continue;

                gridItem.RevealPulseProgress = progress;

                // Scale effect for pulse
                float pulseScale = 1f + (RevealPulseScale - 1f) * (float)Math.Sin(progress * Math.PI);
                gridItem.CurrentScale = pulseScale;
            }

            // Complete reveal phase
            if (_animationTime >= RevealPulseDuration)
            {
                // Reset revealed cells
                foreach (var revealedId in _revealedIds)
                {
                    var achievement = AchievementManager.GetAchievement(revealedId);
                    if (achievement == null) continue;

                    var ent = GetGridEntity(achievement.Row, achievement.Column);
                    if (ent == null) continue;

                    var gridItem = ent.GetComponent<AchievementGridItem>();
                    if (gridItem == null) continue;

                    gridItem.RevealPulseProgress = 1f;
                    gridItem.IsNewlyRevealed = false;
                    gridItem.IsAnimatingReveal = false;
                    gridItem.CurrentScale = 1f;
                }

                CompleteAnimation();
            }
        }

        private void CompleteAnimation()
        {
            _phase = AnimationPhase.Idle;
            _animationTime = 0f;
            _revealedIds.Clear();
            _sourceAchievementId = string.Empty;
            _isSmall = false;

            // Re-enable UI interactions
            StateSingleton.PreventClicking = false;

            // Publish completion event
            EventManager.Publish(new AchievementAnimationsComplete());

            Console.WriteLine("[AchievementExplosionSystem] Animation complete");
        }

        private Vector2 GetCellCenter(int row, int col)
        {
            var entity = GetGridEntity(row, col);
            var transform = entity?.GetComponent<Transform>();
            var item = entity?.GetComponent<AchievementGridItem>();
            return transform == null ? Vector2.Zero : transform.Position - (item?.ExplosionOffset ?? Vector2.Zero);
        }

        private void GenerateExplosionNoise()
        {
            _explosionRandom = new Random();
            _cellNoise.Clear();

            foreach (var ent in GetGridEntities())
            {
                var gridItem = ent.GetComponent<AchievementGridItem>();
                if (gridItem == null) continue;

                // Generate random angle noise (in radians)
                float angleNoise = ((float)_explosionRandom.NextDouble() * 2f - 1f) * DirectionNoiseDegrees * MathHelper.Pi / 180f;

                // Generate random magnitude noise (as a multiplier)
                float magnitudeNoise = 1f + ((float)_explosionRandom.NextDouble() * 2f - 1f) * MagnitudeNoisePercent;

                _cellNoise[(gridItem.Row, gridItem.Column)] = (angleNoise, magnitudeNoise);
            }
        }

        private IEnumerable<Entity> GetGridEntities()
        {
            return EntityManager.GetEntitiesWithComponent<AchievementGridItem>();
        }

        private Entity GetGridEntity(int row, int col)
        {
            foreach (var entity in GetGridEntities())
            {
                var item = entity.GetComponent<AchievementGridItem>();
                if (item?.Row == row && item.Column == col) return entity;
            }
            return null;
        }

        private Vector2 RotateVector(Vector2 v, float radians)
        {
            float cos = (float)Math.Cos(radians);
            float sin = (float)Math.Sin(radians);
            return new Vector2(v.X * cos - v.Y * sin, v.X * sin + v.Y * cos);
        }

        // Easing functions
        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * (float)Math.Pow(t - 1f, 3) + c1 * (float)Math.Pow(t - 1f, 2);
        }

        private float EaseInQuad(float t)
        {
            return t * t;
        }

        private float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }
    }
}
