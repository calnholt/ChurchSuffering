using System;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

[DebugTab("Thorned Display")]
internal sealed class ThornedOverlayPass : ICardOverlayPass, ICardOverlaySnapshotTimeControl
{
    private const float MaxThornsPerVine = 16f;

    private readonly EntityManager _entityManager;
    private readonly ContentManager _content;

    private Effect _effect;
    private ThornedOverlay _overlay;
    private bool _failed;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Card Radius", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float CardRadius { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Curse Tint Strength", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CurseTintStrength { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Curse Tint R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CurseTintR { get; set; } = 0.16f;

    [DebugEditable(DisplayName = "Curse Tint G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CurseTintG { get; set; } = 0.27f;

    [DebugEditable(DisplayName = "Curse Tint B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CurseTintB { get; set; } = 0.13f;

    [DebugEditable(DisplayName = "Edge Darken", Step = 0.01f, Min = 0f, Max = 1f)]
    public float EdgeDarken { get; set; } = 0.18f;

    [DebugEditable(DisplayName = "Vine Thickness A", Step = 0.01f, Min = 0.001f, Max = 0.1f)]
    public float VineThicknessA { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Vine Thickness B", Step = 0.01f, Min = 0.001f, Max = 0.1f)]
    public float VineThicknessB { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Outline Extra", Step = 0.01f, Min = 0f, Max = 0.1f)]
    public float OutlineExtra { get; set; }

    [DebugEditable(DisplayName = "Line Softness", Step = 0.001f, Min = 0.0001f, Max = 0.05f)]
    public float LineSoft { get; set; } = 0.0035f;

    [DebugEditable(DisplayName = "Diagonal Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DiagonalOpacity { get; set; } = 1f;

    [DebugEditable(DisplayName = "Diagonal Overshoot", Step = 0.01f, Min = 0f, Max = 0.5f)]
    public float DiagonalOvershoot { get; set; } = 0.14f;

    [DebugEditable(DisplayName = "Squirm Amount A", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float SquirmAmountA { get; set; } = 0.025f;

    [DebugEditable(DisplayName = "Squirm Amount B", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float SquirmAmountB { get; set; } = 0.025f;

    [DebugEditable(DisplayName = "Squirm Freq A", Step = 0.01f, Min = 0.01f, Max = 30f)]
    public float SquirmFrequencyA { get; set; } = 7f;

    [DebugEditable(DisplayName = "Squirm Freq B", Step = 0.01f, Min = 0.01f, Max = 30f)]
    public float SquirmFrequencyB { get; set; } = 8.5f;

    [DebugEditable(DisplayName = "Squirm Speed A", Step = 0.01f, Min = -5f, Max = 5f)]
    public float SquirmSpeedA { get; set; } = 0.18f;

    [DebugEditable(DisplayName = "Squirm Speed B", Step = 0.01f, Min = -5f, Max = 5f)]
    public float SquirmSpeedB { get; set; } = 0.14f;

    [DebugEditable(DisplayName = "Squirm Phase B", Step = 0.01f, Min = -10f, Max = 10f)]
    public float SquirmPhaseB { get; set; } = 2.35f;

    [DebugEditable(DisplayName = "Outline R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float OutlineR { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Outline G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float OutlineG { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Outline B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float OutlineB { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Vine Dark R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineDarkR { get; set; } = 0.04f;

    [DebugEditable(DisplayName = "Vine Dark G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineDarkG { get; set; } = 0.095f;

    [DebugEditable(DisplayName = "Vine Dark B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineDarkB { get; set; } = 0.035f;

    [DebugEditable(DisplayName = "Vine Mid R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineMidR { get; set; } = 0.115f;

    [DebugEditable(DisplayName = "Vine Mid G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineMidG { get; set; } = 0.210f;

    [DebugEditable(DisplayName = "Vine Mid B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineMidB { get; set; } = 0.085f;

    [DebugEditable(DisplayName = "Vine Light R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineLightR { get; set; } = 0.245f;

    [DebugEditable(DisplayName = "Vine Light G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineLightG { get; set; } = 0.355f;

    [DebugEditable(DisplayName = "Vine Light B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineLightB { get; set; } = 0.160f;

    [DebugEditable(DisplayName = "Vine Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineOpacity { get; set; } = 0.96f;

    [DebugEditable(DisplayName = "Vine Shadow", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VineShadow { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Thorns Per Vine", Step = 1f, Min = 0f, Max = MaxThornsPerVine)]
    public float ThornsPerVine { get; set; } = 10f;

    [DebugEditable(DisplayName = "Thorn Length", Step = 0.01f, Min = 0f, Max = 0.2f)]
    public float ThornLength { get; set; } = 0.050f;

    [DebugEditable(DisplayName = "Thorn Base", Step = 0.01f, Min = 0f, Max = 0.08f)]
    public float ThornBase { get; set; } = 0.012f;

    [DebugEditable(DisplayName = "Thorn Fill R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ThornFillR { get; set; } = 0.940f;

    [DebugEditable(DisplayName = "Thorn Fill G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ThornFillG { get; set; } = 0.930f;

    [DebugEditable(DisplayName = "Thorn Fill B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ThornFillB { get; set; } = 0.865f;

    [DebugEditable(DisplayName = "Thorn Light R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ThornLightR { get; set; } = 1f;

    [DebugEditable(DisplayName = "Thorn Light G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ThornLightG { get; set; } = 0.985f;

    [DebugEditable(DisplayName = "Thorn Light B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ThornLightB { get; set; } = 0.920f;

    [DebugEditable(DisplayName = "Edge Creep", Step = 0.01f, Min = 0.001f, Max = 0.3f)]
    public float EdgeCreep { get; set; } = 0.075f;

    [DebugEditable(DisplayName = "Edge Root Density", Step = 0.01f, Min = 0f, Max = 1f)]
    public float EdgeRootDensity { get; set; } = 0.48f;

    [DebugEditable(DisplayName = "Edge Root Scale", Step = 0.01f, Min = 0.01f, Max = 100f)]
    public float EdgeRootScale { get; set; } = 35f;

    [DebugEditable(DisplayName = "Time Speed", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeSpeed { get; set; } = 1f;

    public ThornedOverlayPass(
        EntityManager entityManager,
        ContentManager content)
    {
        _entityManager = entityManager;
        _content = content;
    }

    public string Name => "Thorned";

    public void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasThornedCards()) EnsureLoaded();
    }

    public void Render(CardOverlayPassContext context)
    {
        if (context == null || !ShouldRender(context.Card) || !EnsureLoaded()) return;
        ConfigureOverlay(context);
        context.Apply("Thorned", (spriteBatch, source) =>
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

        _overlay.Resolution = context.LogicalSize;
        _overlay.Time = _timeSeconds;
        _overlay.CardCenter = context.ToSurface(geometry.Center);
        _overlay.CardSize = new Vector2(Math.Max(1f, geometry.Bounds.Width), Math.Max(1f, geometry.Bounds.Height));
        _overlay.CardRotation = context.Rotation;
        _overlay.CardRadius = Math.Max(0f, CardRadius);
        _overlay.CurseTintStrength = MathHelper.Clamp(CurseTintStrength, 0f, 1f);
        _overlay.CurseTint = new Vector3(CurseTintR, CurseTintG, CurseTintB);
        _overlay.EdgeDarken = Math.Max(0f, EdgeDarken);
        _overlay.VineThicknessA = Math.Max(0.001f, VineThicknessA);
        _overlay.VineThicknessB = Math.Max(0.001f, VineThicknessB);
        _overlay.OutlineExtra = Math.Max(0f, OutlineExtra);
        _overlay.LineSoft = Math.Max(0.0001f, LineSoft);
        _overlay.DiagonalOpacity = MathHelper.Clamp(DiagonalOpacity, 0f, 1f);
        _overlay.DiagonalOvershoot = Math.Max(0f, DiagonalOvershoot);
        _overlay.SquirmAmountA = Math.Max(0f, SquirmAmountA);
        _overlay.SquirmAmountB = Math.Max(0f, SquirmAmountB);
        _overlay.SquirmFrequencyA = Math.Max(0.0001f, SquirmFrequencyA);
        _overlay.SquirmFrequencyB = Math.Max(0.0001f, SquirmFrequencyB);
        _overlay.SquirmSpeedA = SquirmSpeedA;
        _overlay.SquirmSpeedB = SquirmSpeedB;
        _overlay.SquirmPhaseB = SquirmPhaseB;
        _overlay.OutlineColor = new Vector3(OutlineR, OutlineG, OutlineB);
        _overlay.VineDark = new Vector3(VineDarkR, VineDarkG, VineDarkB);
        _overlay.VineMid = new Vector3(VineMidR, VineMidG, VineMidB);
        _overlay.VineLight = new Vector3(VineLightR, VineLightG, VineLightB);
        _overlay.VineOpacity = MathHelper.Clamp(VineOpacity, 0f, 1f);
        _overlay.VineShadow = Math.Max(0f, VineShadow);
        _overlay.ThornsPerVine = MathHelper.Clamp((float)Math.Round(ThornsPerVine), 0f, MaxThornsPerVine);
        _overlay.ThornLength = Math.Max(0f, ThornLength);
        _overlay.ThornBase = Math.Max(0f, ThornBase);
        _overlay.ThornWhite = new Vector3(ThornFillR, ThornFillG, ThornFillB);
        _overlay.ThornLight = new Vector3(ThornLightR, ThornLightG, ThornLightB);
        _overlay.EdgeCreep = Math.Max(0.001f, EdgeCreep);
        _overlay.EdgeRootDensity = MathHelper.Clamp(EdgeRootDensity, 0f, 1f);
        _overlay.EdgeRootScale = Math.Max(0.001f, EdgeRootScale);
        _overlay.TimeSpeed = Math.Max(0f, TimeSpeed);
    }

    public bool AppliesTo(Entity card) => ShouldRender(card);

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card?.GetComponent<Thorned>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasThornedCards()
    {
        foreach (var _ in _entityManager.GetEntitiesWithComponent<Thorned>()) return true;
        return false;
    }

    private bool EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
        if (_effect == null)
        {
            try
            {
                _effect = _content.Load<Effect>("Shaders/Thorned");
            }
            catch (Exception exception)
            {
                LoggingService.Append("ThornedOverlayPass.EnsureLoaded", new JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = exception.Message
                });
                _failed = true;
                return false;
            }
        }

        _overlay ??= new ThornedOverlay(_effect);
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
