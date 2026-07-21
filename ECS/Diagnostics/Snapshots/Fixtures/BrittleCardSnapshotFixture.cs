using System;
using System.Globalization;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures;

public sealed class BrittleCardSnapshotFixture : IDisplaySnapshotFixture
{
    public string Id => "brittle-card";
    public int WarmupFrames => 2;
    public string OutputFileName => _outputFileName ?? DefaultCardId;

    private const string DefaultCardId = "strike";
    private Entity _cardEntity;
    private Texture2D _pixel;
    private string _resolvedCardId = DefaultCardId;
    private string _outputFileName = DefaultCardId;
    private float _scale = 1f;
    private float _rotationDegrees;
    private bool _includePledge;

    public void Setup(DisplaySnapshotContext ctx, string[] args)
    {

        ParseArguments(args, out string requestedId);
        if (CardFactory.Create(requestedId) == null)
        {
            throw new DisplaySnapshotSetupException($"Unknown card id: '{requestedId}'");
        }

        _resolvedCardId = requestedId;
        _outputFileName = BuildOutputFileName(requestedId);
        DestroyCard(ctx);

        _cardEntity = EntityFactory.CreateCardFromDefinition(ctx.World.EntityManager, _resolvedCardId, CardData.CardColor.White);
        if (_cardEntity == null)
        {
            throw new DisplaySnapshotSetupException($"Failed to create card entity: '{_resolvedCardId}'");
        }

        var ui = _cardEntity.GetComponent<UIElement>();
        if (ui != null) ui.IsInteractable = false;

        ctx.World.EntityManager.AddComponent(_cardEntity, new Brittle());
        if (_includePledge)
        {
            ctx.World.EntityManager.AddComponent(_cardEntity, new Pledge
            {
                Owner = _cardEntity,
                CanPlay = true
            });
        }

        _pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Console.WriteLine(
            $"[DisplaySnapshot] Rendering brittle card: {_resolvedCardId}, " +
            $"scale={_scale:0.##}, rotation={_rotationDegrees:0.##}, pledge={_includePledge}");
    }

    public void Draw(DisplaySnapshotContext ctx)
    {
        DrawBackdrop(ctx);

        var position = new Vector2(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f + 40f);
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
        int vw = Game1.VirtualWidth;
        int vh = Game1.VirtualHeight;
        ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, vw, vh), new Color(28, 34, 48));

        const int stripeWidth = 80;
        for (int x = -vw; x < vw * 2; x += stripeWidth)
        {
            var stripe = new Rectangle(x, 0, stripeWidth / 2, vh);
            ctx.SpriteBatch.Draw(_pixel, stripe, new Color(58, 112, 146));
        }

        var cardBackdrop = new Rectangle(vw / 2 - 230, vh / 2 - 260, 460, 560);
        ctx.SpriteBatch.Draw(_pixel, cardBackdrop, new Color(176, 64, 88) * 0.35f);
    }

    private void DestroyCard(DisplaySnapshotContext ctx)
    {
        if (_cardEntity == null) return;

        ctx.World.EntityManager.DestroyEntity(_cardEntity.Id);
        _cardEntity = null;
    }

    private void ParseArguments(string[] args, out string requestedId)
    {
        requestedId = DefaultCardId;
        _scale = 1f;
        _rotationDegrees = 0f;
        _includePledge = false;

        bool cardIdSet = false;
        for (int i = 0; i < (args?.Length ?? 0); i++)
        {
            string arg = args[i];
            if (string.Equals(arg, "no-shaders", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (string.Equals(arg, "--pledge", StringComparison.OrdinalIgnoreCase))
            {
                _includePledge = true;
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
                throw new DisplaySnapshotSetupException($"Unknown brittle-card option: '{arg}'");
            }
            if (cardIdSet)
            {
                throw new DisplaySnapshotSetupException($"Unexpected brittle-card argument: '{arg}'");
            }

            requestedId = arg;
            cardIdSet = true;
        }
    }

    private string BuildOutputFileName(string requestedId)
    {
        if (Math.Abs(_scale - 1f) > 0.001f || Math.Abs(_rotationDegrees) > 0.001f || _includePledge)
        {
            string scaleSlug = _scale.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_');
            string rotationSlug = _rotationDegrees.ToString("0.##", CultureInfo.InvariantCulture).Replace('.', '_').Replace('-', 'n');
            return $"{requestedId}-s{scaleSlug}-r{rotationSlug}" + (_includePledge ? "-pledge" : string.Empty);
        }

        return requestedId;
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
