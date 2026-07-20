using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures;

public sealed class PoisonCardSnapshotFixture : IDisplaySnapshotFixture
{
    private const string DefaultCardId = "strike";

    private Entity _cardEntity;
    private Texture2D _pixel;
    private CardDisplaySystem _cardDisplay;
    private string _cardId = DefaultCardId;

    public string Id => "poison-card";
    public int WarmupFrames => 2;
    public string OutputFileName => _cardId;

    public void Setup(DisplaySnapshotContext ctx, string[] args)
    {
        _cardId = ParseCardId(args);
        if (CardFactory.Create(_cardId) == null)
        {
            throw new DisplaySnapshotSetupException($"Unknown card id: '{_cardId}'");
        }

        DestroyCard(ctx);
        _cardDisplay = ctx.World.GetSystem<CardDisplaySystem>() ??
            throw new DisplaySnapshotSetupException("CardDisplaySystem is not registered");
        _cardDisplay.SetPoisonOverlaySnapshotTime(1.25f);

        _cardEntity = EntityFactory.CreateCardFromDefinition(
            ctx.World.EntityManager,
            _cardId,
            CardData.CardColor.White);
        if (_cardEntity == null)
        {
            throw new DisplaySnapshotSetupException($"Failed to create card entity: '{_cardId}'");
        }

        UIElement ui = _cardEntity.GetComponent<UIElement>();
        if (ui != null) ui.IsInteractable = false;

        ctx.World.EntityManager.AddComponent(_cardEntity, new Poisoned { Owner = _cardEntity });

        _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Console.WriteLine($"[DisplaySnapshot] Rendering poisoned card: {_cardId}");
    }

    public void Draw(DisplaySnapshotContext ctx)
    {
        DrawBackdrop(ctx);
        _cardDisplay.SetPoisonOverlaySnapshotTime(1.25f);
        EventManager.Publish(new CardRenderScaledEvent
        {
            Card = _cardEntity,
            Position = new Vector2(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f + 80f),
            Scale = 1f,
        });
    }

    private void DrawBackdrop(DisplaySnapshotContext ctx)
    {
        int width = Game1.VirtualWidth;
        int height = Game1.VirtualHeight;
        ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), new Color(22, 18, 34));

        const int stripeWidth = 80;
        for (int x = -width; x < width * 2; x += stripeWidth)
        {
            ctx.SpriteBatch.Draw(
                _pixel,
                new Rectangle(x, 0, stripeWidth / 2, height),
                new Color(66, 40, 96));
        }

        ctx.SpriteBatch.Draw(
            _pixel,
            new Rectangle(width / 2 - 260, height / 2 - 300, 520, 620),
            new Color(82, 150, 54) * 0.40f);
    }

    private void DestroyCard(DisplaySnapshotContext ctx)
    {
        if (_cardEntity == null) return;
        ctx.World.EntityManager.DestroyEntity(_cardEntity.Id);
        _cardEntity = null;
    }

    private static string ParseCardId(string[] args)
    {
        string cardId = DefaultCardId;
        bool cardIdSet = false;

        foreach (string arg in args ?? Array.Empty<string>())
        {
            if (string.Equals(arg, "no-shaders", StringComparison.OrdinalIgnoreCase)) continue;
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new DisplaySnapshotSetupException($"Unknown poison-card option: '{arg}'");
            }
            if (cardIdSet)
            {
                throw new DisplaySnapshotSetupException($"Unexpected poison-card argument: '{arg}'");
            }

            cardId = arg;
            cardIdSet = true;
        }

        return cardId;
    }
}
