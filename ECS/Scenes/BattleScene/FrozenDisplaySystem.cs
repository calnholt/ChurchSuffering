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

[DebugTab("Frozen Display")]
public sealed class FrozenDisplaySystem : Core.System
{
    private const int RenderPriority = -50;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;

    private Effect _effect;
    private FrozenOverlay _overlay;
    private RenderTarget2D _sourceTarget;
    private bool _failed;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Card Radius", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float CardRadius { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Ice Tint Strength", Step = 0.01f, Min = 0f, Max = 1f)]
    public float IceTintStrength { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Ice Tint R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float IceTintR { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Ice Tint G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float IceTintG { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Ice Tint B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float IceTintB { get; set; } = 1f;

    [DebugEditable(DisplayName = "Ice Brighten", Step = 0.01f, Min = 0f, Max = 1f)]
    public float IceBrighten { get; set; } = 0f;

    [DebugEditable(DisplayName = "Refraction Amount", Step = 0.0001f, Min = 0f, Max = 0.05f)]
    public float RefractAmount { get; set; } = 0.0001f;

    [DebugEditable(DisplayName = "Refraction Scale", Step = 0.01f, Min = 0.01f, Max = 100f)]
    public float RefractScale { get; set; } = 20f;

    [DebugEditable(DisplayName = "Refraction Speed", Step = 0.01f, Min = -5f, Max = 5f)]
    public float RefractSpeed { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Frost Edge", Step = 0.01f, Min = 0.001f, Max = 0.5f)]
    public float FrostEdge { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Frost Density", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FrostDensity { get; set; } = 0f;

    [DebugEditable(DisplayName = "Frost Scale", Step = 0.01f, Min = 0.01f, Max = 20f)]
    public float FrostScale { get; set; } = 1.75f;

    [DebugEditable(DisplayName = "Frost Color R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FrostColorR { get; set; } = 0f;

    [DebugEditable(DisplayName = "Frost Color G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FrostColorG { get; set; } = 0f;

    [DebugEditable(DisplayName = "Frost Color B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FrostColorB { get; set; } = 1f;

    [DebugEditable(DisplayName = "Sparkle Amount", Step = 0.01f, Min = 0f, Max = 2f)]
    public float SparkleAmount { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Sparkle Scale", Step = 1f, Min = 1f, Max = 2000f)]
    public float SparkleScale { get; set; } = 990f;

    [DebugEditable(DisplayName = "Sparkle Size", Step = 0.01f, Min = 0.001f, Max = 1f)]
    public float SparkleSize { get; set; } = 0.12f;

    [DebugEditable(DisplayName = "Sparkle Speed", Step = 0.01f, Min = 0f, Max = 10f)]
    public float SparkleSpeed { get; set; } = 1.5f;

    [DebugEditable(DisplayName = "Crack Amount", Step = 0.01f, Min = 0f, Max = 2f)]
    public float CrackAmount { get; set; } = 1f;

    [DebugEditable(DisplayName = "Crack Scale", Step = 0.01f, Min = 0.01f, Max = 50f)]
    public float CrackScale { get; set; } = 10f;

    [DebugEditable(DisplayName = "Crack Seed X", Step = 0.01f, Min = -100f, Max = 100f)]
    public float CrackSeedX { get; set; } = 7.06f;

    [DebugEditable(DisplayName = "Crack Seed Y", Step = 0.01f, Min = -100f, Max = 100f)]
    public float CrackSeedY { get; set; } = 7.05f;

    [DebugEditable(DisplayName = "Crack Sharpness", Step = 0.01f, Min = 0.01f, Max = 100f)]
    public float CrackSharpness { get; set; } = 43f;

    [DebugEditable(DisplayName = "Crack Depth", Step = 0.01f, Min = 0f, Max = 10f)]
    public float CrackDepth { get; set; } = 2.2f;

    [DebugEditable(DisplayName = "Crack Light", Step = 0.01f, Min = 0f, Max = 2f)]
    public float CrackLight { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Crack Shade", Step = 0.01f, Min = 0f, Max = 2f)]
    public float CrackShade { get; set; } = 0.28f;

    [DebugEditable(DisplayName = "Crack Occlusion", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CrackOcclusion { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Crack Tint R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CrackTintR { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Crack Tint G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CrackTintG { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Crack Tint B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CrackTintB { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Crack Light X", Step = 0.01f, Min = -1f, Max = 1f)]
    public float CrackLightX { get; set; } = -0.6f;

    [DebugEditable(DisplayName = "Crack Light Y", Step = 0.01f, Min = -1f, Max = 1f)]
    public float CrackLightY { get; set; } = 0.7f;

    [DebugEditable(DisplayName = "Crack Light Z", Step = 0.01f, Min = -1f, Max = 1f)]
    public float CrackLightZ { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Facet Tilt", Step = 0.01f, Min = 0f, Max = 2f)]
    public float FacetTilt { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Facet Refraction", Step = 0.001f, Min = 0f, Max = 0.1f)]
    public float FacetRefract { get; set; } = 0.004f;

    [DebugEditable(DisplayName = "Facet Reflection", Step = 0.01f, Min = 0f, Max = 2f)]
    public float FacetReflect { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Facet Warble", Step = 0.01f, Min = 0f, Max = 3f)]
    public float FacetWarble { get; set; } = 1f;

    [DebugEditable(DisplayName = "Breath Strength", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BreathStrength { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "Breath Offset", Step = 0.01f, Min = -1f, Max = 1f)]
    public float BreathOffset { get; set; } = -0.10f;

    [DebugEditable(DisplayName = "Breath Height", Step = 0.01f, Min = 0.01f, Max = 2f)]
    public float BreathHeight { get; set; } = 0.422f;

    [DebugEditable(DisplayName = "Breath Width", Step = 0.01f, Min = 0.01f, Max = 3f)]
    public float BreathWidth { get; set; } = 1f;

    [DebugEditable(DisplayName = "Breath Spread", Step = 0.01f, Min = 0f, Max = 3f)]
    public float BreathSpread { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Breath Edge Softness", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BreathEdgeSoftness { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Breath Rise", Step = 0.01f, Min = -1f, Max = 1f)]
    public float BreathRise { get; set; } = 0.03f;

    [DebugEditable(DisplayName = "Breath Scale", Step = 0.01f, Min = 0.01f, Max = 20f)]
    public float BreathScale { get; set; } = 5.5f;

    [DebugEditable(DisplayName = "Breath Swirl", Step = 0.01f, Min = 0f, Max = 5f)]
    public float BreathSwirl { get; set; } = 1.8f;

    [DebugEditable(DisplayName = "Breath Swirl Speed", Step = 0.01f, Min = 0f, Max = 10f)]
    public float BreathSwirlSpeed { get; set; } = 1.6f;

    [DebugEditable(DisplayName = "Breath Puff", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BreathPuff { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Breath Color R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BreathColorR { get; set; } = 0.90f;

    [DebugEditable(DisplayName = "Breath Color G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BreathColorG { get; set; } = 0.95f;

    [DebugEditable(DisplayName = "Breath Color B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BreathColorB { get; set; } = 1f;

    public FrozenDisplaySystem(
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
            evt => FrameProfiler.Measure("FrozenDisplaySystem.OnCardRenderEvent", () => Render(evt.Card, evt.Position, GetScale(evt.Card), GetRotation(evt.Card))),
            RenderPriority);
        EventManager.Subscribe<CardRenderScaledEvent>(
            evt => FrameProfiler.Measure("FrozenDisplaySystem.OnCardRenderScaledEvent", () =>
            {
                using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);
                Render(evt.Card, evt.Position, evt.Scale, evt.Rotation);
            }),
            RenderPriority);
        EventManager.Subscribe<CardRenderScaledRotatedEvent>(
            evt => FrameProfiler.Measure("FrozenDisplaySystem.OnCardRenderScaledRotatedEvent", () => Render(evt.Card, evt.Position, evt.Scale, GetRotation(evt.Card))),
            RenderPriority);
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
        return EntityManager.GetEntitiesWithComponent<Frozen>();
    }

    public override void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasFrozenCards()) EnsureLoaded();
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

        ConfigureOverlay(position, scale, rotation);
        SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, currentTargets);
        _graphicsDevice.Clear(Color.Transparent);
        _overlay.Begin(_spriteBatch);
        _overlay.Draw(_spriteBatch, _sourceTarget);
        _overlay.End(_spriteBatch);
        SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
    }

    private void ConfigureOverlay(Vector2 position, float scale, float rotation)
    {
        CardGeometrySettings settings = CardGeometryService.GetSettings(EntityManager);
        float safeScale = Math.Max(0.001f, scale);
        float width = (settings?.CardWidth ?? CardGeometrySettings.DefaultWidth) * safeScale;
        float height = (settings?.CardHeight ?? CardGeometrySettings.DefaultHeight) * safeScale;

        _overlay.Time = _timeSeconds;
        _overlay.CardCenter = CardGeometryService.GetVisualCenter(settings, position, safeScale);
        _overlay.CardSize = new Vector2(width, height);
        _overlay.CardRotation = rotation;
        _overlay.CardRadius = Math.Max(0f, CardRadius);
        _overlay.IceTintStrength = MathHelper.Clamp(IceTintStrength, 0f, 1f);
        _overlay.IceTint = new Vector3(IceTintR, IceTintG, IceTintB);
        _overlay.IceBrighten = Math.Max(0f, IceBrighten);
        _overlay.RefractAmount = Math.Max(0f, RefractAmount);
        _overlay.RefractScale = Math.Max(0.001f, RefractScale);
        _overlay.RefractSpeed = RefractSpeed;
        _overlay.FrostEdge = Math.Max(0.001f, FrostEdge);
        _overlay.FrostDensity = MathHelper.Clamp(FrostDensity, 0f, 1f);
        _overlay.FrostScale = Math.Max(0.001f, FrostScale);
        _overlay.FrostColor = new Vector3(FrostColorR, FrostColorG, FrostColorB);
        _overlay.SparkleAmount = Math.Max(0f, SparkleAmount);
        _overlay.SparkleScale = Math.Max(0.001f, SparkleScale);
        _overlay.SparkleSize = Math.Max(0.001f, SparkleSize);
        _overlay.SparkleSpeed = Math.Max(0f, SparkleSpeed);
        _overlay.CrackAmount = Math.Max(0f, CrackAmount);
        _overlay.CrackScale = Math.Max(0.001f, CrackScale);
        _overlay.CrackSeed = new Vector2(CrackSeedX, CrackSeedY);
        _overlay.CrackSharpness = Math.Max(0.001f, CrackSharpness);
        _overlay.CrackDepth = Math.Max(0f, CrackDepth);
        _overlay.CrackLight = Math.Max(0f, CrackLight);
        _overlay.CrackShade = Math.Max(0f, CrackShade);
        _overlay.CrackOcclusion = MathHelper.Clamp(CrackOcclusion, 0f, 1f);
        _overlay.CrackDeepTint = new Vector3(CrackTintR, CrackTintG, CrackTintB);
        _overlay.CrackLightDirection = new Vector3(CrackLightX, CrackLightY, CrackLightZ);
        _overlay.FacetTilt = Math.Max(0f, FacetTilt);
        _overlay.FacetRefract = Math.Max(0f, FacetRefract);
        _overlay.FacetReflect = Math.Max(0f, FacetReflect);
        _overlay.FacetWarble = Math.Max(0f, FacetWarble);
        _overlay.BreathStrength = MathHelper.Clamp(BreathStrength, 0f, 1f);
        _overlay.BreathOffset = BreathOffset;
        _overlay.BreathHeight = Math.Max(0.001f, BreathHeight);
        _overlay.BreathWidth = Math.Max(0.001f, BreathWidth);
        _overlay.BreathSpread = Math.Max(0f, BreathSpread);
        _overlay.BreathEdgeSoftness = MathHelper.Clamp(BreathEdgeSoftness, 0f, 1f);
        _overlay.BreathRise = BreathRise;
        _overlay.BreathScale = Math.Max(0.001f, BreathScale);
        _overlay.BreathSwirl = Math.Max(0f, BreathSwirl);
        _overlay.BreathSwirlSpeed = Math.Max(0f, BreathSwirlSpeed);
        _overlay.BreathPuff = MathHelper.Clamp(BreathPuff, 0f, 1f);
        _overlay.BreathColor = new Vector3(BreathColorR, BreathColorG, BreathColorB);
    }

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card?.GetComponent<Frozen>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasFrozenCards()
    {
        foreach (var _ in EntityManager.GetEntitiesWithComponent<Frozen>()) return true;
        return false;
    }

    private bool EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
        if (_effect == null)
        {
            try
            {
                _effect = _content.Load<Effect>("Shaders/Frozen");
            }
            catch (Exception exception)
            {
                LoggingService.Append("FrozenDisplaySystem.EnsureLoaded", new JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = exception.Message
                });
                _failed = true;
                return false;
            }
        }

        _overlay ??= new FrozenOverlay(_effect);
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
