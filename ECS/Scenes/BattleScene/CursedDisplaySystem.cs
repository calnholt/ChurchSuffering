using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems;

[DebugTab("Cursed Display")]
public sealed class CursedDisplaySystem : Core.System
{
    private const int RenderPriority = -65;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;

    private Effect _effect;
    private CursedOverlay _overlay;
    private RenderTarget2D _sourceTarget;
    private bool _failed;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Card Radius", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float CardRadius { get; set; } = 0.04f;

    [DebugEditable(DisplayName = "Shape Count", Step = 1f, Min = 0f, Max = 48f)]
    public float ShapeCount { get; set; } = 28f;

    [DebugEditable(DisplayName = "Shape Size Min", Step = 0.001f, Min = 0.001f, Max = 0.25f)]
    public float ShapeSizeMin { get; set; } = 0.018f;

    [DebugEditable(DisplayName = "Shape Size Max", Step = 0.001f, Min = 0.001f, Max = 0.35f)]
    public float ShapeSizeMax { get; set; } = 0.070f;

    [DebugEditable(DisplayName = "Rise Speed Min", Step = 0.001f, Min = 0f, Max = 2f)]
    public float ShapeRiseSpeedMin { get; set; } = 0.045f;

    [DebugEditable(DisplayName = "Rise Speed Max", Step = 0.001f, Min = 0f, Max = 2f)]
    public float ShapeRiseSpeedMax { get; set; } = 0.155f;

    [DebugEditable(DisplayName = "Shape Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShapeOpacity { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Edge Softness", Step = 0.01f, Min = 0.001f, Max = 1f)]
    public float ShapeEdgeSoftness { get; set; } = 0.16f;

    [DebugEditable(DisplayName = "Vertical Fade", Step = 0.01f, Min = 0.001f, Max = 0.5f)]
    public float ShapeVerticalFade { get; set; } = 0.14f;

    [DebugEditable(DisplayName = "Shape Color R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShapeColorR { get; set; } = 0.72f;

    [DebugEditable(DisplayName = "Shape Color G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShapeColorG { get; set; } = 0.16f;

    [DebugEditable(DisplayName = "Shape Color B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShapeColorB { get; set; } = 0.96f;

    [DebugEditable(DisplayName = "Effect Seed", Step = 0.01f, Min = -100f, Max = 100f)]
    public float EffectSeed { get; set; } = 1f;

    [DebugEditable(DisplayName = "Time Speed", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeSpeed { get; set; } = 1f;

    public CursedDisplaySystem(
        EntityManager entityManager,
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        ContentManager content)
        : base(entityManager)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _content = content;

        EventManager.Subscribe<CardRenderEvent>(
            evt => FrameProfiler.Measure("CursedDisplaySystem.OnCardRenderEvent", () => Render(evt.Card, evt.Position, GetScale(evt.Card), GetRotation(evt.Card))),
            RenderPriority);
        EventManager.Subscribe<CardRenderScaledEvent>(
            evt => FrameProfiler.Measure("CursedDisplaySystem.OnCardRenderScaledEvent", () =>
            {
                using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);
                Render(evt.Card, evt.Position, evt.Scale, evt.Rotation);
            }),
            RenderPriority);
        EventManager.Subscribe<CardRenderScaledRotatedEvent>(
            evt => FrameProfiler.Measure("CursedDisplaySystem.OnCardRenderScaledRotatedEvent", () => Render(evt.Card, evt.Position, evt.Scale, GetRotation(evt.Card))),
            RenderPriority);
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
        return EntityManager.GetEntitiesWithComponent<Cursed>();
    }

    public override void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasCursedCards()) EnsureLoaded();
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
    }

    private void Render(Entity card, Vector2 position, float scale, float rotation)
    {
        if (!ShouldRender(card) || !EnsureLoaded() || !EnsureTarget()) return;
        if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
                _graphicsDevice,
                out var currentTargets,
                out var currentTarget)) return;

        var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
        _spriteBatch.End();
        SpriteBatchRenderTargetCompositor.Copy(_graphicsDevice, _spriteBatch, currentTarget, _sourceTarget);

        ConfigureOverlay(card, position, scale, rotation);
        SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, currentTargets);
        _graphicsDevice.Clear(Color.Transparent);
        _overlay.Begin(_spriteBatch);
        _overlay.Draw(_spriteBatch, _sourceTarget);
        _overlay.End(_spriteBatch);
        SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
    }

    private void ConfigureOverlay(Entity card, Vector2 position, float scale, float rotation)
    {
        CardVisualGeometry geometry = CardGeometryService.GetVisualGeometry(
            EntityManager,
            card,
            position,
            Math.Max(0.001f, scale),
            rotation);
        float shapeSizeMin = Math.Max(0.001f, ShapeSizeMin);
        float shapeSizeMax = Math.Max(shapeSizeMin, ShapeSizeMax);
        float riseSpeedMin = Math.Max(0f, ShapeRiseSpeedMin);
        float riseSpeedMax = Math.Max(riseSpeedMin, ShapeRiseSpeedMax);

        _overlay.Time = _timeSeconds;
        _overlay.CardCenter = geometry.Center;
        _overlay.CardSize = new Vector2(Math.Max(1f, geometry.Bounds.Width), Math.Max(1f, geometry.Bounds.Height));
        _overlay.CardRotation = rotation;
        _overlay.CardRadius = Math.Max(0f, CardRadius);
        _overlay.ShapeCount = MathHelper.Clamp(ShapeCount, 0f, 48f);
        _overlay.ShapeSizeMin = shapeSizeMin;
        _overlay.ShapeSizeMax = shapeSizeMax;
        _overlay.ShapeRiseSpeedMin = riseSpeedMin;
        _overlay.ShapeRiseSpeedMax = riseSpeedMax;
        _overlay.ShapeOpacity = MathHelper.Clamp(ShapeOpacity, 0f, 1f);
        _overlay.ShapeEdgeSoftness = Math.Max(0.001f, ShapeEdgeSoftness);
        _overlay.ShapeVerticalFade = Math.Max(0.001f, ShapeVerticalFade);
        _overlay.ShapeColor = new Vector3(
            MathHelper.Clamp(ShapeColorR, 0f, 1f),
            MathHelper.Clamp(ShapeColorG, 0f, 1f),
            MathHelper.Clamp(ShapeColorB, 0f, 1f));
        _overlay.EffectSeed = EffectSeed;
        _overlay.TimeSpeed = Math.Max(0f, TimeSpeed);
    }

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card?.GetComponent<Cursed>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasCursedCards()
    {
        foreach (var _ in EntityManager.GetEntitiesWithComponent<Cursed>()) return true;
        return false;
    }

    private bool EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
        if (_effect == null)
        {
            try
            {
                _effect = _content.Load<Effect>("Shaders/Cursed");
            }
            catch (Exception exception)
            {
                LoggingService.Append("CursedDisplaySystem.EnsureLoaded", new JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = exception.Message
                });
                _failed = true;
                return false;
            }
        }

        _overlay ??= new CursedOverlay(_effect);
        return _overlay.IsAvailable;
    }

    private bool EnsureTarget()
    {
        Rectangle bounds = _graphicsDevice.Viewport.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) return false;
        if (_sourceTarget != null && _sourceTarget.Width == bounds.Width && _sourceTarget.Height == bounds.Height)
        {
            return true;
        }

        _sourceTarget?.Dispose();
        _sourceTarget = new RenderTarget2D(
            _graphicsDevice,
            bounds.Width,
            bounds.Height,
            false,
            SurfaceFormat.Color,
            DepthFormat.None);
        return true;
    }

    private static float GetScale(Entity card) => card?.GetComponent<Transform>()?.Scale.X ?? 1f;
    private static float GetRotation(Entity card) => card?.GetComponent<Transform>()?.Rotation ?? 0f;
}
