using System;
using System.Globalization;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures;

public sealed class ThornedCardSnapshotFixture : IDisplaySnapshotFixture
{
    private const string DefaultCardId = "strike";

    private Entity _cardEntity;
    private Texture2D _pixel;
    private string _outputFileName = DefaultCardId;
    private float _scale = 1f;
    private float _rotationDegrees;
    private bool _includeFrozen;

    public string Id => "thorned-card";
    public int WarmupFrames => 2;
    public string OutputFileName => _outputFileName;

    public void Setup(DisplaySnapshotContext ctx, string[] args)
    {
        ParseArguments(args, out string cardId);
        if (CardFactory.Create(cardId) == null)
        {
            throw new DisplaySnapshotSetupException($"Unknown card id: '{cardId}'");
        }

        _outputFileName = BuildOutputFileName(cardId);
        DestroyCard(ctx);
        _cardEntity = EntityFactory.CreateCardFromDefinition(
            ctx.World.EntityManager,
            cardId,
            CardData.CardColor.White);
        if (_cardEntity == null)
        {
            throw new DisplaySnapshotSetupException($"Failed to create card entity: '{cardId}'");
        }

        var ui = _cardEntity.GetComponent<UIElement>();
        if (ui != null) ui.IsInteractable = false;

        if (_includeFrozen)
        {
            ctx.World.EntityManager.AddComponent(_cardEntity, new Frozen());
        }
        ctx.World.EntityManager.AddComponent(_cardEntity, new Thorned());

        _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Console.WriteLine(
            $"[DisplaySnapshot] Rendering thorned card: {cardId}, " +
            $"scale={_scale:0.##}, rotation={_rotationDegrees:0.##}, frozen={_includeFrozen}");
    }

    public void Draw(DisplaySnapshotContext ctx)
    {
        DrawBackdrop(ctx);
        var position = new Vector2(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f + 80f);
        if (Math.Abs(_rotationDegrees) <= 0.001f)
        {
            EventManager.Publish(new CardRenderScaledEvent
            {
                Card = _cardEntity,
                Position = position,
                Scale = _scale
            });
            return;
        }

        var transform = _cardEntity.GetComponent<Transform>();
        if (transform != null)
        {
            transform.Rotation = MathHelper.ToRadians(_rotationDegrees);
        }

        EventManager.Publish(new CardRenderScaledRotatedEvent
        {
            Card = _cardEntity,
            Position = position,
            Scale = _scale
        });
    }

    private void DrawBackdrop(DisplaySnapshotContext ctx)
    {
        int width = Game1.VirtualWidth;
        int height = Game1.VirtualHeight;
        ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), new Color(22, 29, 24));

        const int stripeWidth = 80;
        for (int x = -width; x < width * 2; x += stripeWidth)
        {
            ctx.SpriteBatch.Draw(
                _pixel,
                new Rectangle(x, 0, stripeWidth / 2, height),
                new Color(74, 94, 45));
        }

        ctx.SpriteBatch.Draw(
            _pixel,
            new Rectangle(width / 2 - 260, height / 2 - 300, 520, 620),
            new Color(53, 115, 62) * 0.4f);
    }

    private void DestroyCard(DisplaySnapshotContext ctx)
    {
        if (_cardEntity == null) return;
        ctx.World.EntityManager.DestroyEntity(_cardEntity.Id);
        _cardEntity = null;
    }

    private void ParseArguments(string[] args, out string cardId)
    {
        cardId = DefaultCardId;
        _scale = 1f;
        _rotationDegrees = 0f;
        _includeFrozen = false;
        bool cardIdSet = false;

        for (int i = 0; i < (args?.Length ?? 0); i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "no-shaders", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(arg, "--frozen", StringComparison.OrdinalIgnoreCase))
            {
                _includeFrozen = true;
                continue;
            }
            if (string.Equals(arg, "--scale", StringComparison.OrdinalIgnoreCase))
            {
                _scale = ParseFloatOption(args, ref i, "--scale", 0.1f, 3f);
                continue;
            }
            if (string.Equals(arg, "--rotation", StringComparison.OrdinalIgnoreCase))
            {
                _rotationDegrees = ParseFloatOption(args, ref i, "--rotation", -180f, 180f);
                continue;
            }
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new DisplaySnapshotSetupException($"Unknown thorned-card option: '{arg}'");
            }
            if (cardIdSet)
            {
                throw new DisplaySnapshotSetupException($"Unexpected thorned-card argument: '{arg}'");
            }

            cardId = arg;
            cardIdSet = true;
        }
    }

    private string BuildOutputFileName(string cardId)
    {
        if (Math.Abs(_scale - 1f) <= 0.001f &&
            Math.Abs(_rotationDegrees) <= 0.001f &&
            !_includeFrozen)
        {
            return cardId;
        }

        string scale = _scale.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
        string rotation = _rotationDegrees.ToString("0.##", CultureInfo.InvariantCulture)
            .Replace('.', '_')
            .Replace('-', 'n');
        return $"{cardId}-s{scale}-r{rotation}" + (_includeFrozen ? "-frozen" : string.Empty);
    }

    private static float ParseFloatOption(string[] args, ref int index, string option, float min, float max)
    {
        if (index + 1 >= args.Length ||
            !float.TryParse(args[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
            value < min ||
            value > max)
        {
            throw new DisplaySnapshotSetupException(
                $"{option} expects a number from {min.ToString(CultureInfo.InvariantCulture)} " +
                $"to {max.ToString(CultureInfo.InvariantCulture)}");
        }

        index++;
        return value;
    }
}
