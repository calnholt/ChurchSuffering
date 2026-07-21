using System;
using System.Text.Json.Nodes;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

[DebugTab("Cursed Display")]
internal sealed class CursedOverlayPass : ICardOverlayPass, ICardOverlaySnapshotTimeControl
{
    private readonly EntityManager _entityManager;
    private readonly ContentManager _content;

    private Effect _effect;
    private CursedOverlay _overlay;
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

    public CursedOverlayPass(
        EntityManager entityManager,
        ContentManager content)
    {
        _entityManager = entityManager;
        _content = content;
    }

    public string Name => "Cursed";

    public void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasCursedCards()) EnsureLoaded();
    }

    public void Render(CardOverlayPassContext context)
    {
        if (context == null || !ShouldRender(context.Card) || !EnsureLoaded()) return;
        ConfigureOverlay(context);
        context.Apply("Cursed", (spriteBatch, source) =>
        {
            _overlay.Begin(spriteBatch);
            _overlay.Draw(spriteBatch, source);
            _overlay.End(spriteBatch);
        });
    }

    private void ConfigureOverlay(CardOverlayPassContext context)
    {
        CardVisualGeometry geometry = CardGeometryService.GetVisualGeometry(
            _entityManager,
            context.Card,
            context.Position,
            Math.Max(0.001f, context.Scale),
            context.Rotation);
        float shapeSizeMin = Math.Max(0.001f, ShapeSizeMin);
        float shapeSizeMax = Math.Max(shapeSizeMin, ShapeSizeMax);
        float riseSpeedMin = Math.Max(0f, ShapeRiseSpeedMin);
        float riseSpeedMax = Math.Max(riseSpeedMin, ShapeRiseSpeedMax);

        _overlay.Resolution = context.LogicalSize;
        _overlay.Time = _timeSeconds;
        _overlay.CardCenter = context.ToSurface(geometry.Center);
        _overlay.CardSize = new Vector2(Math.Max(1f, geometry.Bounds.Width), Math.Max(1f, geometry.Bounds.Height));
        _overlay.CardRotation = context.Rotation;
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

    public bool AppliesTo(Entity card) => ShouldRender(card);

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card?.GetComponent<Cursed>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasCursedCards()
    {
        foreach (var _ in _entityManager.GetEntitiesWithComponent<Cursed>()) return true;
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
                LoggingService.Append("CursedOverlayPass.EnsureLoaded", new JsonObject
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

    public void Reset()
    {
        _effect = null;
        _overlay = null;
        _failed = false;
    }

    public void SetSnapshotTime(float timeSeconds) => _timeSeconds = Math.Max(0f, timeSeconds);
}
