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

[DebugTab("Poison Card Display")]
internal sealed class PoisonCardOverlayPass : ICardOverlayPass, ICardOverlaySnapshotTimeControl
{
    private readonly EntityManager _entityManager;
    private readonly ContentManager _content;

    private Effect _effect;
    private PoisonCardOverlay _overlay;
    private bool _failed;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Blob Frequency", Step = 0.01f, Min = 0.1f, Max = 20f)]
    public float BlobFrequency { get; set; } = 6f;

    [DebugEditable(DisplayName = "Blob Stretch", Step = 0.01f, Min = 0.01f, Max = 5f)]
    public float BlobStretch { get; set; } = 1.5f;

    [DebugEditable(DisplayName = "Blob Reach Min", Step = 0.01f, Min = 0.01f, Max = 3f)]
    public float BlobReachMin { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Blob Reach Max", Step = 0.01f, Min = 0.01f, Max = 3f)]
    public float BlobReachMax { get; set; } = 0.95f;

    [DebugEditable(DisplayName = "Threshold Low", Step = 0.01f, Min = 0f, Max = 2f)]
    public float ThresholdLow { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Threshold High", Step = 0.01f, Min = 0f, Max = 2f)]
    public float ThresholdHigh { get; set; } = 0.90f;

    [DebugEditable(DisplayName = "Flow Speed", Step = 0.01f, Min = -2f, Max = 2f)]
    public float FlowSpeed { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Refraction", Step = 0.01f, Min = 0f, Max = 1f)]
    public float RefractionAmount { get; set; } = 0.16f;

    [DebugEditable(DisplayName = "Thin Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
    public float AlphaThin { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Thick Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
    public float AlphaThick { get; set; } = 0.90f;

    [DebugEditable(DisplayName = "Absorption Strength", Step = 0.01f, Min = 0f, Max = 1f)]
    public float AbsorptionStrength { get; set; } = 0.90f;

    [DebugEditable(DisplayName = "Absorption R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float AbsorptionColorR { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Absorption G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float AbsorptionColorG { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Absorption B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float AbsorptionColorB { get; set; } = 0.34f;

    [DebugEditable(DisplayName = "Surface R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SlimeSurfaceColorR { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Surface G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SlimeSurfaceColorG { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "Surface B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SlimeSurfaceColorB { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Deep R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SlimeDeepColorR { get; set; } = 0f;

    [DebugEditable(DisplayName = "Deep G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SlimeDeepColorG { get; set; } = 0.26f;

    [DebugEditable(DisplayName = "Deep B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SlimeDeepColorB { get; set; } = 0.08f;

    [DebugEditable(DisplayName = "Light X", Step = 0.01f, Min = -1f, Max = 1f)]
    public float LightDirectionX { get; set; } = -0.40f;

    [DebugEditable(DisplayName = "Light Y", Step = 0.01f, Min = -1f, Max = 1f)]
    public float LightDirectionY { get; set; } = 0.65f;

    [DebugEditable(DisplayName = "Light Z", Step = 0.01f, Min = -1f, Max = 1f)]
    public float LightDirectionZ { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Ambient", Step = 0.01f, Min = 0f, Max = 2f)]
    public float Ambient { get; set; } = 0.38f;

    [DebugEditable(DisplayName = "Diffuse", Step = 0.01f, Min = 0f, Max = 2f)]
    public float Diffuse { get; set; } = 0.75f;

    [DebugEditable(DisplayName = "Specular Power", Step = 1f, Min = 1f, Max = 128f)]
    public float SpecularPower { get; set; } = 46f;

    [DebugEditable(DisplayName = "Specular Intensity", Step = 0.01f, Min = 0f, Max = 2f)]
    public float SpecularIntensity { get; set; } = 0.95f;

    [DebugEditable(DisplayName = "Rim Power", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float RimPower { get; set; } = 2.6f;

    [DebugEditable(DisplayName = "Rim Intensity", Step = 0.01f, Min = 0f, Max = 2f)]
    public float RimIntensity { get; set; } = 0.28f;

    public PoisonCardOverlayPass(EntityManager entityManager, ContentManager content)
    {
        _entityManager = entityManager;
        _content = content;
    }

    public string Name => "Poison";

    public void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasPoisonedCards()) EnsureLoaded();
    }

    public void Render(CardOverlayPassContext context)
    {
        if (context == null || !ShouldRender(context.Card) || !EnsureLoaded()) return;

        ConfigureOverlay(context);
        context.Apply("Poison", (spriteBatch, source) =>
        {
            _overlay.Begin(spriteBatch);
            _overlay.Draw(spriteBatch, source);
            _overlay.End(spriteBatch);
        });
    }

    private void ConfigureOverlay(CardOverlayPassContext context)
    {
        float reachMin = Math.Max(0.01f, BlobReachMin);
        float thresholdLow = Math.Max(0f, ThresholdLow);

        _overlay.Resolution = context.LogicalSize;
        _overlay.Time = _timeSeconds;
        _overlay.BlobFrequency = Math.Max(0.1f, BlobFrequency);
        _overlay.BlobStretch = Math.Max(0.01f, BlobStretch);
        _overlay.BlobReachMin = reachMin;
        _overlay.BlobReachMax = Math.Max(reachMin, BlobReachMax);
        _overlay.ThresholdLow = thresholdLow;
        _overlay.ThresholdHigh = Math.Max(thresholdLow + 0.001f, ThresholdHigh);
        _overlay.FlowSpeed = FlowSpeed;
        _overlay.RefractionAmount = Math.Max(0f, RefractionAmount);
        _overlay.AlphaThin = MathHelper.Clamp(AlphaThin, 0f, 1f);
        _overlay.AlphaThick = MathHelper.Clamp(AlphaThick, 0f, 1f);
        _overlay.AbsorptionStrength = MathHelper.Clamp(AbsorptionStrength, 0f, 1f);
        _overlay.AbsorptionColor = ClampColor(AbsorptionColorR, AbsorptionColorG, AbsorptionColorB);
        _overlay.SlimeSurfaceColor = ClampColor(
            SlimeSurfaceColorR,
            SlimeSurfaceColorG,
            SlimeSurfaceColorB);
        _overlay.SlimeDeepColor = ClampColor(SlimeDeepColorR, SlimeDeepColorG, SlimeDeepColorB);
        _overlay.LightDirection = new Vector3(LightDirectionX, LightDirectionY, LightDirectionZ);
        _overlay.Ambient = Math.Max(0f, Ambient);
        _overlay.Diffuse = Math.Max(0f, Diffuse);
        _overlay.SpecularPower = Math.Max(0.01f, SpecularPower);
        _overlay.SpecularIntensity = Math.Max(0f, SpecularIntensity);
        _overlay.RimPower = Math.Max(0.01f, RimPower);
        _overlay.RimIntensity = Math.Max(0f, RimIntensity);
    }

    public bool AppliesTo(Entity card) => ShouldRender(card);

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card?.GetComponent<Poisoned>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasPoisonedCards()
    {
        foreach (var _ in _entityManager.GetEntitiesWithComponent<Poisoned>()) return true;
        return false;
    }

    private bool EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
        if (_overlay != null) return _overlay.IsAvailable;

        try
        {
            _effect ??= _content.Load<Effect>("Shaders/PoisonCard");
            _overlay = new PoisonCardOverlay(_effect);
            return _overlay.IsAvailable;
        }
        catch (Exception exception)
        {
            LoggingService.Append("PoisonCardOverlayPass.EnsureLoaded", new JsonObject
            {
                ["error"] = exception.Message,
            });
            _failed = true;
            return false;
        }
    }

    public void Reset()
    {
        _effect = null;
        _overlay = null;
        _failed = false;
    }

    public void SetSnapshotTime(float timeSeconds)
    {
        _timeSeconds = Math.Max(0f, timeSeconds);
    }

    private static Vector3 ClampColor(float red, float green, float blue)
    {
        return new Vector3(
            MathHelper.Clamp(red, 0f, 1f),
            MathHelper.Clamp(green, 0f, 1f),
            MathHelper.Clamp(blue, 0f, 1f));
    }
}
