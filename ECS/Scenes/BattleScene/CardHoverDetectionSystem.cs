using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// System that publishes CardInHoveredEvent each frame indicating which card in hand is currently hovered.
    /// </summary>
    public class CardHoverDetectionSystem : Core.System
    {
        public CardHoverDetectionSystem(EntityManager entityManager) : base(entityManager)
        {
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            // This system doesn't iterate entities, it works at the system level
            return Enumerable.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            // Not used - this system overrides Update() instead
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Get deck entity
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null)
            {
                EventManager.Publish(new CardInHandHoveredEvent { Card = null });
                return;
            }

            // Get deck component and hand list
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null || deck.Hand == null)
            {
                EventManager.Publish(new CardInHandHoveredEvent { Card = null });
                return;
            }

            // Find first card in hand where UIElement.IsHovered == true
            Entity hoveredCard = null;
            foreach (var card in deck.Hand)
            {
                var uiElement = card.GetComponent<UIElement>();
                if (uiElement != null && uiElement.IsHovered)
                {
                    hoveredCard = card;
                    break;
                }
            }

            // Publish event with the found card (or null if none hovered)
            EventManager.Publish(new CardInHandHoveredEvent { Card = hoveredCard });
        }
    }
}






