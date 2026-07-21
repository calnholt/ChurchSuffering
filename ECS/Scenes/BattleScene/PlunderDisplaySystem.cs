using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Displays the plundered card to the right of the enemy.
    /// The deprecated HPDisplaySystem temporarily renders the damage gauge via the HP component.
    /// Technical debt: move the gauge rendering into this system and remove HPDisplaySystem.
    /// </summary>
    [DebugTab("Plunder Display")]
    public class PlunderDisplaySystem : Core.System
    {
        #region Debug-Editable Fields

        [DebugEditable(DisplayName = "Card X Offset", Step = 5f, Min = -300f, Max = 300f)]
        public float CardXOffset { get; set; } = 225f;

        [DebugEditable(DisplayName = "Card Y Offset", Step = 5f, Min = -200f, Max = 200f)]
        public float CardYOffset { get; set; } = -20f;

        [DebugEditable(DisplayName = "Card Scale", Step = 0.05f, Min = 0.2f, Max = 1.5f)]
        public float CardScale { get; set; } = 0.6f;

        [DebugEditable(DisplayName = "HP Bar X Offset", Step = 2f, Min = -300f, Max = 300f)]
        public int HPBarXOffset { get; set; } = 0;

        [DebugEditable(DisplayName = "HP Bar Y Offset", Step = 2f, Min = -300f, Max = 300f)]
        public int HPBarYOffset { get; set; } = 192;

        [DebugEditable(DisplayName = "HP Bar Width", Step = 2f, Min = 10f, Max = 400f)]
        public int HPBarWidth { get; set; } = 120;

        [DebugEditable(DisplayName = "HP Bar Height", Step = 1f, Min = 4f, Max = 60f)]
        public int HPBarHeight { get; set; } = 26;

        #endregion

        public PlunderDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Plundered>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public void Draw()
        {
            // Find the plundered card
            var plunderedCard = EntityManager.GetEntitiesWithComponent<Plundered>().FirstOrDefault();
            if (plunderedCard == null) return;

            // Don't draw if card is currently animating (PlunderSnatchDisplaySystem handles rendering)
            if (plunderedCard.HasComponent<PlunderSnatchFlight>() ||
                plunderedCard.HasComponent<PlunderRescueFlight>())
            {
                return;
            }

            var plundered = plunderedCard.GetComponent<Plundered>();
            if (plundered == null) return;

            // Get enemy position
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (enemy == null) return;

            var enemyTransform = enemy.GetComponent<Transform>();
            if (enemyTransform == null) return;

            // Calculate card position (to the right of enemy)
            var cardPosition = new Vector2(
                enemyTransform.Position.X + CardXOffset,
                enemyTransform.Position.Y + CardYOffset
            );

            // Update the card's Transform for the legacy plunder HP gauge.
            var cardTransform = plunderedCard.GetComponent<Transform>();
            if (cardTransform != null)
            {
                cardTransform.Position = cardPosition;
            }

            // Set per-entity HP bar positioning override
            var hpOverride = plunderedCard.GetComponent<HPBarOverride>();
            if (hpOverride == null)
            {
                hpOverride = new HPBarOverride { Owner = plunderedCard };
                EntityManager.AddComponent(plunderedCard, hpOverride);
            }
            hpOverride.OffsetX = HPBarXOffset;
            hpOverride.OffsetY = HPBarYOffset;
            hpOverride.BarWidth = HPBarWidth;
            hpOverride.BarHeight = HPBarHeight;

            // Render the plundered card
            EventManager.Publish(new CardRenderScaledEvent
            {
                Card = plunderedCard,
                Position = cardPosition,
                Scale = CardScale
            });
        }
    }
}
