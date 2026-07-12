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

[DebugTab("Scorched Display")]
public sealed class ScorchedDisplaySystem : Core.System
{
    private const int RenderPriority = -60;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;

    private Effect _effect;
    private ScorchedOverlay _overlay;
    private bool _failed;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Card Radius", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float CardRadius { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Fire Reach", Step = 0.01f, Min = 0.01f, Max = 0.4f)]
    public float FireReach { get; set; } = 0.13f;

    [DebugEditable(DisplayName = "Fire Inner", Step = 0.01f, Min = 0f, Max = 0.1f)]
    public float FireInner { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Flame Shape", Step = 0.01f, Min = 0.01f, Max = 2f)]
    public float FlameShape { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Flame Sharp", Step = 0.01f, Min = 0.01f, Max = 20f)]
    public float FlameSharp { get; set; } = 7f;

    [DebugEditable(DisplayName = "Flame Threshold", Step = 0.01f, Min = 0f, Max = 0.95f)]
    public float FlameThreshold { get; set; }

    [DebugEditable(DisplayName = "Heat Fade", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HeatFade { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Fire Scale", Step = 0.01f, Min = 0.01f, Max = 30f)]
    public float FireScale { get; set; } = 7.5f;

    [DebugEditable(DisplayName = "Fire Rise", Step = 0.01f, Min = -10f, Max = 10f)]
    public float FireRise { get; set; } = 1.7f;

    [DebugEditable(DisplayName = "Fire Evolve", Step = 0.01f, Min = -10f, Max = 10f)]
    public float FireEvolve { get; set; } = 1.1f;

    [DebugEditable(DisplayName = "Fire Turbulence", Step = 0.01f, Min = 0f, Max = 2f)]
    public float FireTurbulence { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Fire Lean Out", Step = 0.01f, Min = -3f, Max = 3f)]
    public float FireLeanOut { get; set; } = 1.2f;

    [DebugEditable(DisplayName = "Fire Fuel", Step = 0.01f, Min = 0f, Max = 3f)]
    public float FireFuel { get; set; } = 1f;

    [DebugEditable(DisplayName = "Top Bias", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TopBias { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Fire Brightness", Step = 0.01f, Min = 0f, Max = 4f)]
    public float FireBrightness { get; set; } = 1.35f;

    [DebugEditable(DisplayName = "Fire Tint R", Step = 0.01f, Min = 0f, Max = 3f)]
    public float FireTintR { get; set; } = 1f;

    [DebugEditable(DisplayName = "Fire Tint G", Step = 0.01f, Min = 0f, Max = 3f)]
    public float FireTintG { get; set; } = 1f;

    [DebugEditable(DisplayName = "Fire Tint B", Step = 0.01f, Min = 0f, Max = 3f)]
    public float FireTintB { get; set; } = 1f;

    [DebugEditable(DisplayName = "Ember Strength", Step = 0.01f, Min = 0f, Max = 4f)]
    public float EmberStrength { get; set; } = 1.3f;

    [DebugEditable(DisplayName = "Ember Reach", Step = 0.01f, Min = 0.01f, Max = 0.4f)]
    public float EmberReach { get; set; } = 0.11f;

    [DebugEditable(DisplayName = "Ember Grid", Step = 1f, Min = 1f, Max = 100f)]
    public float EmberGrid { get; set; } = 22f;

    [DebugEditable(DisplayName = "Ember Size", Step = 0.01f, Min = 0f, Max = 0.5f)]
    public float EmberSize { get; set; } = 0.09f;

    [DebugEditable(DisplayName = "Ember Color R", Step = 0.01f, Min = 0f, Max = 3f)]
    public float EmberColorR { get; set; } = 1f;

    [DebugEditable(DisplayName = "Ember Color G", Step = 0.01f, Min = 0f, Max = 3f)]
    public float EmberColorG { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Ember Color B", Step = 0.01f, Min = 0f, Max = 3f)]
    public float EmberColorB { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Card Scorch", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CardScorch { get; set; }

    [DebugEditable(DisplayName = "Card Glow", Step = 0.01f, Min = 0f, Max = 2f)]
    public float CardGlow { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Time Speed", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeSpeed { get; set; } = 0.6f;

    public ScorchedDisplaySystem(
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
            evt => FrameProfiler.Measure("ScorchedDisplaySystem.OnCardRenderEvent", () => Render(evt.Card, evt.Position, GetScale(evt.Card), GetRotation(evt.Card))),
            RenderPriority);
        EventManager.Subscribe<CardRenderScaledEvent>(
            evt => FrameProfiler.Measure("ScorchedDisplaySystem.OnCardRenderScaledEvent", () =>
            {
                using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);
                Render(evt.Card, evt.Position, evt.Scale, evt.Rotation);
            }),
            RenderPriority);
        EventManager.Subscribe<CardRenderScaledRotatedEvent>(
            evt => FrameProfiler.Measure("ScorchedDisplaySystem.OnCardRenderScaledRotatedEvent", () => Render(evt.Card, evt.Position, evt.Scale, GetRotation(evt.Card))),
            RenderPriority);
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
        return EntityManager.GetEntitiesWithComponent<Scorched>();
    }

    public override void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasScorchedCards()) EnsureLoaded();
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
    }

    private void Render(Entity card, Vector2 position, float scale, float rotation)
    {
        if (!ShouldRender(card) || !EnsureLoaded()) return;
        if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
                _graphicsDevice,
                out var currentTargets,
                out var currentTarget)) return;

        using var sourceLease = FullScreenRenderTargetPool.Acquire(
            _graphicsDevice,
            currentTarget.Width,
            currentTarget.Height);
        RenderTarget2D sourceTarget = sourceLease.Target;

        var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
        _spriteBatch.End();
        SpriteBatchRenderTargetCompositor.Copy(_graphicsDevice, _spriteBatch, currentTarget, sourceTarget);

        ConfigureOverlay(card, position, scale, rotation);
        SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, currentTargets);
        _graphicsDevice.Clear(Color.Transparent);
        _overlay.Begin(_spriteBatch);
        _overlay.Draw(_spriteBatch, sourceTarget);
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

        _overlay.Time = _timeSeconds;
        _overlay.CardCenter = geometry.Center;
        _overlay.CardSize = new Vector2(Math.Max(1f, geometry.Bounds.Width), Math.Max(1f, geometry.Bounds.Height));
        _overlay.CardRotation = rotation;
        _overlay.CardRadius = Math.Max(0f, CardRadius);
        _overlay.FireReach = Math.Max(0.001f, FireReach);
        _overlay.FireInner = Math.Max(0f, FireInner);
        _overlay.FlameShape = Math.Max(0.001f, FlameShape);
        _overlay.FlameSharp = Math.Max(0.001f, FlameSharp);
        _overlay.FlameThreshold = MathHelper.Clamp(FlameThreshold, 0f, 0.95f);
        _overlay.HeatFade = MathHelper.Clamp(HeatFade, 0f, 1f);
        _overlay.FireScale = Math.Max(0.001f, FireScale);
        _overlay.FireRise = FireRise;
        _overlay.FireEvolve = FireEvolve;
        _overlay.FireTurbulence = Math.Max(0f, FireTurbulence);
        _overlay.FireLeanOut = FireLeanOut;
        _overlay.FireFuel = Math.Max(0f, FireFuel);
        _overlay.TopBias = MathHelper.Clamp(TopBias, 0f, 1f);
        _overlay.FireBrightness = Math.Max(0f, FireBrightness);
        _overlay.FireTint = new Vector3(FireTintR, FireTintG, FireTintB);
        _overlay.EmberStrength = Math.Max(0f, EmberStrength);
        _overlay.EmberReach = Math.Max(0.001f, EmberReach);
        _overlay.EmberGrid = Math.Max(0.001f, EmberGrid);
        _overlay.EmberSize = Math.Max(0f, EmberSize);
        _overlay.EmberColor = new Vector3(EmberColorR, EmberColorG, EmberColorB);
        _overlay.CardScorch = MathHelper.Clamp(CardScorch, 0f, 1f);
        _overlay.CardGlow = Math.Max(0f, CardGlow);
        _overlay.TimeSpeed = Math.Max(0f, TimeSpeed);
    }

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card?.GetComponent<Scorched>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasScorchedCards()
    {
        foreach (var _ in EntityManager.GetEntitiesWithComponent<Scorched>()) return true;
        return false;
    }

    private bool EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
        if (_effect == null)
        {
            try
            {
                _effect = _content.Load<Effect>("Shaders/Scorched");
            }
            catch (Exception exception)
            {
                LoggingService.Append("ScorchedDisplaySystem.EnsureLoaded", new JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = exception.Message
                });
                _failed = true;
                return false;
            }
        }

        _overlay ??= new ScorchedOverlay(_effect);
        return _overlay.IsAvailable;
    }

    private static float GetScale(Entity card) => card?.GetComponent<Transform>()?.Scale.X ?? 1f;
    private static float GetRotation(Entity card) => card?.GetComponent<Transform>()?.Rotation ?? 0f;
}
