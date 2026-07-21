using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
    public sealed class CardDisplaySnapshotFixture : IDisplaySnapshotFixture
    {
        public string Id => "card";
        public int WarmupFrames => 2;

        private readonly List<Entity> _cardEntities = new();
        private string _resolvedCardId;
        private Texture2D _pixel;

        public string OutputFileName => _resolvedCardId ?? "card";

        private const int CardWidth = 268;
        private const int CardHeight = 377;
        private const int CardGap = 40;
        private static readonly Color BackgroundColor = new(144, 238, 144);

        public void Setup(DisplaySnapshotContext ctx, string[] args)
        {

            string requestedId = args.Length > 0 ? args[0] : null;
            var allCards = CardFactory.GetAllCards();

            if (!string.IsNullOrEmpty(requestedId))
            {
                if (CardFactory.Create(requestedId) == null)
                {
                    throw new DisplaySnapshotSetupException($"Unknown card id: '{requestedId}'");
                }
                _resolvedCardId = requestedId;
            }
            else
            {
                var nonWeapons = allCards.Values.Where(c => !c.IsWeapon).ToList();
                if (nonWeapons.Count == 0)
                {
                    throw new DisplaySnapshotSetupException("No non-weapon cards available for random selection");
                }
                _resolvedCardId = nonWeapons[Random.Shared.Next(nonWeapons.Count)].CardId;
            }

            Console.WriteLine($"[DisplaySnapshot] Rendering card: {_resolvedCardId}");

            DestroyCards(ctx);
            var colors = new[] { CardData.CardColor.White, CardData.CardColor.Red, CardData.CardColor.Black };
            foreach (var color in colors)
            {
                var entity = EntityFactory.CreateCardFromDefinition(ctx.World.EntityManager, _resolvedCardId, color);
                if (entity == null)
                {
                    throw new DisplaySnapshotSetupException($"Failed to create card entity: '{_resolvedCardId}'");
                }

                var ui = entity.GetComponent<UIElement>();
                if (ui != null) ui.IsInteractable = false;
                _cardEntities.Add(entity);
            }

            _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        public void Draw(DisplaySnapshotContext ctx)
        {
            int vw = Game1.VirtualWidth;
            int vh = Game1.VirtualHeight;
            ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, vw, vh), BackgroundColor);

            int totalWidth = CardWidth * 3 + CardGap * 2;
            int startX = (vw - totalWidth) / 2;
            int startY = (vh - CardHeight) / 2;

            for (int i = 0; i < _cardEntities.Count; i++)
            {
                var card = _cardEntities[i];
                var pos = new Vector2(startX + i * (CardWidth + CardGap), startY);
                EventManager.Publish(new CardRenderScaledEvent
                {
                    Card = card,
                    Position = pos,
                    Scale = 1f
                });
            }
        }

        private void DestroyCards(DisplaySnapshotContext ctx)
        {
            foreach (var entity in _cardEntities)
            {
                ctx.World.EntityManager.DestroyEntity(entity.Id);
            }
            _cardEntities.Clear();
        }
    }
}
