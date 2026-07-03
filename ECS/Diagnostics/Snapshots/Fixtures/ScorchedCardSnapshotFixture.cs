using System;
using System.Globalization;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures;

public sealed class ScorchedCardSnapshotFixture : IDisplaySnapshotFixture
{
    private const string DefaultCardId = "strike";

    private Entity _cardEntity;
    private Texture2D _pixel;
    private string _outputFileName = DefaultCardId;
    private float _scale = 1f;
    private float _rotationDegrees;

    public string Id => "scorched-card";
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

        ctx.World.EntityManager.AddComponent(_cardEntity, new Scorched());

        _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Console.WriteLine(
            $"[DisplaySnapshot] Rendering scorched card: {cardId}, " +
            $"scale={_scale:0.##}, rotation={_rotationDegrees:0.##}");
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
        ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), new Color(24, 28, 38));

        const int stripeWidth = 80;
        for (int x = -width; x < width * 2; x += stripeWidth)
        {
            ctx.SpriteBatch.Draw(
                _pixel,
                new Rectangle(x, 0, stripeWidth / 2, height),
                new Color(106, 56, 34));
        }

        ctx.SpriteBatch.Draw(
            _pixel,
            new Rectangle(width / 2 - 260, height / 2 - 300, 520, 620),
            new Color(164, 77, 45) * 0.4f);
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
        bool cardIdSet = false;

        for (int i = 0; i < (args?.Length ?? 0); i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "no-shaders", StringComparison.OrdinalIgnoreCase)) continue;
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
                throw new DisplaySnapshotSetupException($"Unknown scorched-card option: '{arg}'");
            }
            if (cardIdSet)
            {
                throw new DisplaySnapshotSetupException($"Unexpected scorched-card argument: '{arg}'");
            }

            cardId = arg;
            cardIdSet = true;
        }
    }

    private string BuildOutputFileName(string cardId)
    {
        if (Math.Abs(_scale - 1f) <= 0.001f &&
            Math.Abs(_rotationDegrees) <= 0.001f)
        {
            return cardId;
        }

        string scale = _scale.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
        string rotation = _rotationDegrees.ToString("0.##", CultureInfo.InvariantCulture)
            .Replace('.', '_')
            .Replace('-', 'n');
        return $"{cardId}-s{scale}-r{rotation}";
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
