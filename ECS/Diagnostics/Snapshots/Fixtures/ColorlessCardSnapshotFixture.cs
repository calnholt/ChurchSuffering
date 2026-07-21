using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures;

public sealed class ColorlessCardSnapshotFixture : IDisplaySnapshotFixture
{
    public string Id => "colorless-card";
    public int WarmupFrames => 2;
    public string OutputFileName => "all-printed-colors";

    private readonly List<Entity> _cards = new();
    private Texture2D _pixel;

    public void Setup(DisplaySnapshotContext ctx, string[] args)
    {
        DestroyCards(ctx);

        var colors = new[]
        {
            CardData.CardColor.White,
            CardData.CardColor.Red,
            CardData.CardColor.Black,
        };

        foreach (var color in colors)
        {
            var card = EntityFactory.CreateCardFromDefinition(
                ctx.World.EntityManager,
                "strike",
                color);
            if (card == null)
            {
                throw new DisplaySnapshotSetupException("Failed to create Colorless snapshot card.");
            }

            card.GetComponent<CardData>().Card.Cost = ["Red", "White", "Black", "Any"];
            ctx.World.EntityManager.AddComponent(card, new Colorless { Owner = card });
            var ui = card.GetComponent<UIElement>();
            if (ui != null) ui.IsInteractable = false;
            _cards.Add(card);
        }

        _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(DisplaySnapshotContext ctx)
    {
        ctx.SpriteBatch.Draw(
            _pixel,
            new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
            new Color(28, 30, 34));

        const int cardWidth = 268;
        const int cardHeight = 377;
        const int gap = 40;
        int totalWidth = cardWidth * _cards.Count + gap * (_cards.Count - 1);
        int startX = (Game1.VirtualWidth - totalWidth) / 2;
        int startY = (Game1.VirtualHeight - cardHeight) / 2;

        for (int i = 0; i < _cards.Count; i++)
        {
            EventManager.Publish(new CardRenderScaledEvent
            {
                Card = _cards[i],
                Position = new Vector2(startX + i * (cardWidth + gap), startY),
                Scale = 1f,
            });
        }
    }

    private void DestroyCards(DisplaySnapshotContext ctx)
    {
        foreach (var card in _cards)
        {
            ctx.World.EntityManager.DestroyEntity(card.Id);
        }
        _cards.Clear();
    }
}
