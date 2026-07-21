using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Displays the achievement grid (15x10) with fog-of-war discovery.
    /// Click on completed-unseen cells to reveal adjacent achievements.
    /// </summary>
    [DebugTab("Achievement Grid")]
    public class AchievementGridDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly Texture2D _pixel;
        private readonly Dictionary<string, Entity> _gridEntities = new();
        private bool _gridCreated = false;

        // Time accumulator for pulsing animations
        private float _time = 0f;

        // Grid configuration
        public const int GRID_COLUMNS = 15;
        public const int GRID_ROWS = 10;

        [DebugEditable(DisplayName = "Cell Size", Step = 2, Min = 20, Max = 100)]
        public int CellSize { get; set; } = 54;

        [DebugEditable(DisplayName = "Cell Gap", Step = 1, Min = 0, Max = 20)]
        public int CellGap { get; set; } = 16;

        [DebugEditable(DisplayName = "Grid Offset X", Step = 10, Min = 0, Max = 500)]
        public int GridOffsetX { get; set; } = 120;

        [DebugEditable(DisplayName = "Grid Offset Y", Step = 10, Min = 0, Max = 500)]
        public int GridOffsetY { get; set; } = 174;

        [DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 20)]
        public int CornerRadius { get; set; } = 0;

        [DebugEditable(DisplayName = "Hover Scale", Step = 0.05f, Min = 1f, Max = 2f)]
        public float HoverScale { get; set; } = 1.15f;

        [DebugEditable(DisplayName = "Scale Lerp Speed", Step = 0.5f, Min = 1f, Max = 30f)]
        public float ScaleLerpSpeed { get; set; } = 12f;

        // Exclamation mark configuration
        [DebugEditable(DisplayName = "Exclamation Pulse Speed", Step = 0.5f, Min = 1f, Max = 10f)]
        public float ExclamationPulseSpeed { get; set; } = 3f;

        [DebugEditable(DisplayName = "Exclamation Glow Intensity", Step = 0.1f, Min = 0f, Max = 1f)]
        public float ExclamationGlowIntensity { get; set; } = 0.0f;

        [DebugEditable(DisplayName = "Exclamation Scale", Step = 0.01f, Min = 0.01f, Max = 3f)]
        public float ExclamationScale { get; set; } = 0.28f;

        // Colors
        private readonly Color _completedColor = AchievementSceneDrawHelpers.WarmWhite;
        private readonly Color _visibleColor = new Color(58, 12, 20);
        private readonly Color _hiddenColor = AchievementSceneDrawHelpers.Black1;
        private readonly Color _exclamationColor = AchievementSceneDrawHelpers.Red;
        private readonly Color _exclamationGlowColor = AchievementSceneDrawHelpers.RedBright * 0.55f;

        public AchievementGridDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
        {
            _graphicsDevice = gd;
            _spriteBatch = sb;
            _pixel = new Texture2D(gd, 1, 1);
            _pixel.SetData(new[] { Color.White });

            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
        }

        private void OnLoadScene(LoadSceneEvent evt)
        {
            if (evt.Scene != SceneId.Achievement) return;

            _gridCreated = false;
            _time = 0f;
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
            _time += dt;

            // Create grid entities if needed
            if (!_gridCreated)
            {
                CreateGridEntities();
                _gridCreated = true;
            }

            // Update hover states, scaling, and handle clicks
            UpdateHoverStatesAndClicks(dt);
        }

        private void CreateGridEntities()
        {
            // Clear existing
            foreach (var kv in _gridEntities)
            {
                if (kv.Value != null)
                    EntityManager.DestroyEntity(kv.Value.Id);
            }
            _gridEntities.Clear();

            // Create grid cells
            for (int row = 0; row < GRID_ROWS; row++)
            {
                for (int col = 0; col < GRID_COLUMNS; col++)
                {
                    string key = $"grid_{row}_{col}";
                    var ent = EntityManager.CreateEntity($"AchievementGrid_{row}_{col}");

                    // Find achievement at this position
                    var achievement = AchievementManager.GetAll()
                        .FirstOrDefault(a => a.Row == row && a.Column == col);

                    var rect = GetCellRect(row, col);
                    var pos = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

                    EntityManager.AddComponent(ent, new Transform { Position = pos, ZOrder = 100 });
                    EntityManager.AddComponent(ent, new UIElement
                    {
                        Bounds = rect,
                        IsInteractable = achievement != null && achievement.State != AchievementState.Hidden,
                        TooltipType = TooltipType.None
                    });
                    var parallax = ParallaxLayer.GetUIParallaxLayer();
                    if (achievement != null && achievement.State == AchievementState.Visible)
                    {
                        parallax.MultiplierX = 0.02f;
                        parallax.MultiplierY = 0.02f;
                    }
                    if (achievement != null && (achievement.State == AchievementState.CompleteSeen || achievement.State == AchievementState.CompleteUnseen))
                    {
                        parallax.MultiplierX = 0.015f;
                        parallax.MultiplierY = 0.015f;
                    }
                    EntityManager.AddComponent(ent, parallax);
                    EntityManager.AddComponent(ent, new AchievementGridItem
                    {
                        AchievementId = achievement?.Id ?? string.Empty,
                        Row = row,
                        Column = col,
                        CurrentScale = 1f,
                        TargetScale = 1f,
                        Alpha = 1f,
                        ExplosionOffset = Vector2.Zero,
                        IsAnimatingExplosion = false,
                        RevealPulseProgress = 0f,
                        IsNewlyRevealed = false
                    });
                    EntityManager.AddComponent(ent, new OwnedByScene { Scene = SceneId.Achievement });

                    _gridEntities[key] = ent;
                }
            }
        }

        public Rectangle GetCellRect(int row, int col)
        {
            int x = GridOffsetX + col * (CellSize + CellGap);
            int y = GridOffsetY + row * (CellSize + CellGap);
            return new Rectangle(x, y, CellSize, CellSize);
        }

        private void UpdateHoverStatesAndClicks(float dt)
        {
            // Don't process interactions if clicking is prevented (animation in progress)
            bool canInteract = !StateSingleton.PreventClicking;

            string hoveredId = string.Empty;

            AchievementBase achievement = null;

            foreach (var kv in _gridEntities)
            {
                var ent = kv.Value;
                if (ent == null) continue;

                var ui = ent.GetComponent<UIElement>();
                var gridItem = ent.GetComponent<AchievementGridItem>();
                var transform = ent.GetComponent<Transform>();

                if (ui == null || gridItem == null || transform == null) continue;

                var baseRect = GetCellRect(gridItem.Row, gridItem.Column);
                var baseCenter = new Vector2(baseRect.X + baseRect.Width / 2f, baseRect.Y + baseRect.Height / 2f);
                transform.Position = baseCenter + gridItem.ExplosionOffset;

                // Update bounds based on scale and current position (with explosion offset)
                int scaledSize = (int)(CellSize * gridItem.CurrentScale);
                ui.Bounds = new Rectangle(
                    (int)Math.Round(transform.Position.X - scaledSize / 2f),
                    (int)Math.Round(transform.Position.Y - scaledSize / 2f),
                    scaledSize,
                    scaledSize);

                // Skip hover/click handling if animating (but position/bounds are still updated above)
                if (gridItem.IsAnimatingExplosion || gridItem.IsAnimatingReveal || gridItem.IsAnimatingCompletion)
                {
                    continue;
                }

                // Get achievement state
                achievement = !string.IsNullOrEmpty(gridItem.AchievementId)
                    ? AchievementManager.GetAchievement(gridItem.AchievementId)
                    : null;

                // Update interactability based on state
                // All grid cells are interactable to allow for small explosion feedback
                bool isInteractable = canInteract;
                ui.IsInteractable = isInteractable;

                // Handle click on cells
                if (ui.IsClicked && canInteract)
                {
                    if (achievement != null && achievement.State == AchievementState.CompleteUnseen)
                    {
                        // Publish event to trigger explosion animation
                        EventManager.Publish(new AchievementRevealClickedEvent
                        {
                            AchievementId = achievement.Id,
                            Row = gridItem.Row,
                            Column = gridItem.Column,
                            IsSmall = false
                        });
                    }
                    else
                    {
                        // Publish small explosion for all other cells (Hidden, Visible, CompleteSeen, or Empty)
                        EventManager.Publish(new AchievementRevealClickedEvent
                        {
                            AchievementId = achievement?.Id ?? string.Empty,
                            Row = gridItem.Row,
                            Column = gridItem.Column,
                            IsSmall = true
                        });
                    }
                }

                // Handle hover
                if (ui.IsHovered && isInteractable)
                {
                    gridItem.TargetScale = HoverScale;
                    hoveredId = gridItem.AchievementId;
                }
                else
                {
                    gridItem.TargetScale = 1f;
                }

                // Lerp scale (only when not animating)
                gridItem.CurrentScale = MathHelper.Lerp(gridItem.CurrentScale, gridItem.TargetScale, dt * ScaleLerpSpeed);
            }

            // Publish hover event only if achievement is not hidden
            achievement = AchievementManager.GetAchievement(hoveredId);
            if (achievement != null && achievement.State != AchievementState.Hidden)
            {
                EventManager.Publish(new AchievementGridItemHovered { AchievementId = hoveredId });
            }
            else
            {
                EventManager.Publish(new AchievementGridItemHovered { AchievementId = string.Empty });
            }
        }

        /// <summary>
        /// Get a grid entity by row and column.
        /// </summary>
        public Entity GetGridEntity(int row, int col)
        {
            string key = $"grid_{row}_{col}";
            _gridEntities.TryGetValue(key, out var ent);
            return ent;
        }

        /// <summary>
        /// Get all grid entities.
        /// </summary>
        public IEnumerable<Entity> GetAllGridEntities()
        {
            return _gridEntities.Values.Where(e => e != null);
        }

        public void Draw()
        {
            var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            DrawGridBackdrop();

            // Draw all grid cells
            foreach (var kv in _gridEntities)
            {
                var ent = kv.Value;
                if (ent == null) continue;

                var gridItem = ent.GetComponent<AchievementGridItem>();
                var ui = ent.GetComponent<UIElement>();

                if (gridItem == null || ui == null) continue;

                DrawGridCell(gridItem, ui);
            }
        }

        private void DrawGridBackdrop()
        {
            var panel = AchievementSceneDrawHelpers.GridPanel;
            AchievementSceneDrawHelpers.DrawPanel(_spriteBatch, _pixel, panel);

            int firstCenterX = GridOffsetX + CellSize / 2;
            int firstCenterY = GridOffsetY + CellSize / 2;
            int lastCenterX = firstCenterX + (GRID_COLUMNS - 1) * (CellSize + CellGap);
            int lastCenterY = firstCenterY + (GRID_ROWS - 1) * (CellSize + CellGap);
            var railColor = Color.White * 0.035f;
            for (int row = 0; row < GRID_ROWS; row++)
            {
                int y = firstCenterY + row * (CellSize + CellGap);
                _spriteBatch.Draw(_pixel, new Rectangle(firstCenterX, y, lastCenterX - firstCenterX, 1), railColor);
            }
            for (int col = 0; col < GRID_COLUMNS; col++)
            {
                int x = firstCenterX + col * (CellSize + CellGap);
                _spriteBatch.Draw(_pixel, new Rectangle(x, firstCenterY, 1, lastCenterY - firstCenterY), railColor);
            }
        }

        private void DrawGridCell(AchievementGridItem gridItem, UIElement ui)
        {
            var achievement = !string.IsNullOrEmpty(gridItem.AchievementId)
                ? AchievementManager.GetAchievement(gridItem.AchievementId)
                : null;

            // Determine color based on state
            Color cellColor;
            if (achievement == null)
            {
                cellColor = _hiddenColor;
            }
            else
            {
                switch (achievement.State)
                {
                    case AchievementState.CompleteSeen:
                    case AchievementState.CompleteUnseen:
                        cellColor = _completedColor;
                        break;
                    case AchievementState.Visible:
                        cellColor = _visibleColor;
                        break;
                    case AchievementState.Hidden:
                    default:
                        cellColor = _hiddenColor;
                        break;
                }
            }

            // Apply alpha for reveal animation
            cellColor = cellColor * gridItem.Alpha;

            // Apply reveal pulse effect (scale boost when newly revealed)
            float pulseScale = 1f;
            if (gridItem.IsNewlyRevealed && gridItem.RevealPulseProgress > 0f && gridItem.RevealPulseProgress < 1f)
            {
                // Pulse animation: scale up then back down
                float pulseT = gridItem.RevealPulseProgress;
                pulseScale = 1f + 0.2f * (float)Math.Sin(pulseT * Math.PI);
            }

            int size = (int)(CellSize * gridItem.CurrentScale * pulseScale);
            var center = new Vector2(ui.Bounds.X + ui.Bounds.Width / 2f, ui.Bounds.Y + ui.Bounds.Height / 2f);
            var drawRect = new Rectangle(
                (int)(center.X - size / 2f),
                (int)(center.Y - size / 2f),
                size,
                size);
            _spriteBatch.Draw(_pixel, drawRect, cellColor);

            Color borderColor = achievement?.State switch
            {
                AchievementState.CompleteSeen => Color.White * 0.88f,
                AchievementState.CompleteUnseen => AchievementSceneDrawHelpers.RedBright,
                AchievementState.Visible => AchievementSceneDrawHelpers.Red * 0.82f,
                _ => Color.White * 0.07f,
            };
            AchievementSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, drawRect, borderColor * gridItem.Alpha, 1);
            AchievementSceneDrawHelpers.DrawBorder(
                _spriteBatch,
                _pixel,
                new Rectangle(drawRect.X + 3, drawRect.Y + 3, drawRect.Width - 6, drawRect.Height - 6),
                Color.Black * (0.35f * gridItem.Alpha),
                1);

            if (ui.IsHovered)
            {
                var hoverRect = new Rectangle(drawRect.X - 3, drawRect.Y - 3, drawRect.Width + 6, drawRect.Height + 6);
                AchievementSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, hoverRect, AchievementSceneDrawHelpers.RedBright * 0.9f, 2);
            }

            // Draw pulsing exclamation mark on completed-unseen cells
            if (achievement != null && achievement.State == AchievementState.CompleteUnseen)
            {
                DrawExclamationMark(ui.Bounds, gridItem.CurrentScale);
            }
        }

        private void DrawExclamationMark(Rectangle cellBounds, float scale)
        {
            var font = FontSingleton.TitleFont;
            if (font == null) return;

            // Calculate pulsing effect
            float pulse = (float)Math.Sin(_time * ExclamationPulseSpeed);
            float pulseAlpha = 0.7f + 0.3f * pulse; // Alpha pulses between 0.7 and 1.0
            float pulseGlow = ExclamationGlowIntensity * (0.5f + 0.5f * pulse); // Glow pulses

            string text = "!";
            Vector2 textSize = font.MeasureString(text) * ExclamationScale;

            // Center the exclamation mark in the cell
            Vector2 center = new Vector2(
                cellBounds.X + cellBounds.Width / 2f,
                cellBounds.Y + cellBounds.Height / 2f
            );
            Vector2 textPos = center - textSize / 2f;

            // Draw glow effect (multiple offset draws with lower alpha)
            Color glowColor = _exclamationGlowColor * pulseGlow;
            float glowOffset = 2f;
            for (int i = 0; i < 4; i++)
            {
                float angle = i * MathHelper.PiOver2;
                Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * glowOffset;
                _spriteBatch.DrawString(font, text, textPos + offset, glowColor, 0f, Vector2.Zero, ExclamationScale, SpriteEffects.None, 0f);
            }

            // Draw main exclamation mark
            Color mainColor = _exclamationColor * pulseAlpha;
            _spriteBatch.DrawString(font, text, textPos, mainColor, 0f, Vector2.Zero, ExclamationScale, SpriteEffects.None, 0f);
        }

    }
}
