using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures;

public sealed class CardRenderPipelineSnapshotFixture : IDisplaySnapshotFixture
{
    private const string DefaultCardId = "strike";
    private const float SnapshotTimeSeconds = 1.25f;
    private const float SheenElapsedSeconds = 0.42f;

    private Entity _cardEntity;
    private Texture2D _pixel;
    private CardDisplaySystem _cardDisplay;
    private Variant _variant;

    public string Id => "card-render-pipeline";
    public int WarmupFrames => 2;
    public string OutputFileName => ToSlug(_variant);

    public void Setup(DisplaySnapshotContext ctx, string[] args)
    {
        _variant = ParseVariant(args);
        DestroyCard(ctx);

        _cardDisplay = ctx.World.GetSystem<CardDisplaySystem>() ??
            throw new DisplaySnapshotSetupException("CardDisplaySystem is not registered");
        _cardDisplay.SetAllOverlaySnapshotTimes(SnapshotTimeSeconds);

        _cardEntity = EntityFactory.CreateCardFromDefinition(
            ctx.World.EntityManager,
            DefaultCardId,
            CardData.CardColor.White);
        if (_cardEntity == null)
        {
            throw new DisplaySnapshotSetupException($"Failed to create card entity: '{DefaultCardId}'");
        }

        if (_cardEntity.GetComponent<UIElement>() is UIElement ui)
        {
            ui.IsInteractable = false;
        }

        if (_variant is Variant.AllStatuses or Variant.AllStatusesSheen)
        {
            ctx.World.EntityManager.AddComponent(_cardEntity, new Brittle());
            ctx.World.EntityManager.AddComponent(_cardEntity, new Frozen());
            ctx.World.EntityManager.AddComponent(_cardEntity, new Thorned());
            ctx.World.EntityManager.AddComponent(_cardEntity, new Scorched());
            ctx.World.EntityManager.AddComponent(_cardEntity, new Cursed());
            ctx.World.EntityManager.AddComponent(_cardEntity, new Poisoned { Owner = _cardEntity });
        }

        if (_variant is Variant.SheenOnly or Variant.AllStatusesSheen)
        {
            ctx.World.EntityManager.AddComponent(_cardEntity, new CardSheen
            {
                Owner = _cardEntity,
                IsActive = true,
                HasActivationTime = true,
                ActivationTimeSeconds = SnapshotTimeSeconds - SheenElapsedSeconds,
            });
        }

        _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        Console.WriteLine($"[DisplaySnapshot] Rendering card pipeline variant: {ToSlug(_variant)}");
    }

    public void Draw(DisplaySnapshotContext ctx)
    {
        DrawBackdrop(ctx);
        _cardDisplay.SetAllOverlaySnapshotTimes(SnapshotTimeSeconds);
        EventManager.Publish(new CardRenderScaledEvent
        {
            Card = _cardEntity,
            Position = new Vector2(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f + 80f),
            Scale = 0.9f,
            Rotation = MathHelper.ToRadians(12f),
        });
    }

    private void DrawBackdrop(DisplaySnapshotContext ctx)
    {
        int width = Game1.VirtualWidth;
        int height = Game1.VirtualHeight;
        ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), new Color(20, 24, 34));

        const int stripeWidth = 80;
        for (int x = -width; x < width * 2; x += stripeWidth)
        {
            ctx.SpriteBatch.Draw(
                _pixel,
                new Rectangle(x, 0, stripeWidth / 2, height),
                new Color(86, 51, 96));
        }

        ctx.SpriteBatch.Draw(
            _pixel,
            new Rectangle(width / 2 - 300, height / 2 - 330, 600, 680),
            new Color(74, 120, 92) * 0.45f);
    }

    private void DestroyCard(DisplaySnapshotContext ctx)
    {
        if (_cardEntity == null) return;
        ctx.World.EntityManager.DestroyEntity(_cardEntity.Id);
        _cardEntity = null;
    }

    private static Variant ParseVariant(string[] args)
    {
        string variant = null;
        foreach (string arg in args ?? Array.Empty<string>())
        {
            if (string.Equals(arg, "no-shaders", StringComparison.OrdinalIgnoreCase)) continue;
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new DisplaySnapshotSetupException($"Unknown card-render-pipeline option: '{arg}'");
            }
            if (variant != null)
            {
                throw new DisplaySnapshotSetupException($"Unexpected card-render-pipeline argument: '{arg}'");
            }
            variant = arg;
        }

        return variant?.ToLowerInvariant() switch
        {
            null or "all-statuses" => Variant.AllStatuses,
            "sheen-only" => Variant.SheenOnly,
            "all-statuses-sheen" => Variant.AllStatusesSheen,
            _ => throw new DisplaySnapshotSetupException(
                "card-render-pipeline variant must be all-statuses, sheen-only, or all-statuses-sheen"),
        };
    }

    private static string ToSlug(Variant variant) => variant switch
    {
        Variant.AllStatuses => "all-statuses",
        Variant.SheenOnly => "sheen-only",
        Variant.AllStatusesSheen => "all-statuses-sheen",
        _ => throw new ArgumentOutOfRangeException(nameof(variant)),
    };

    private enum Variant
    {
        AllStatuses,
        SheenOnly,
        AllStatusesSheen,
    }
}
