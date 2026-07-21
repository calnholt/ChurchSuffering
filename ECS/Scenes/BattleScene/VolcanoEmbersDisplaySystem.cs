using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems;

[DebugTab("Volcano Embers")]
public class VolcanoEmbersDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private VolcanoEmbersOverlay _overlay;
    private bool _failed;
    private bool _isActive;
    private float _timeSeconds;

    public bool CanComposite =>
        ShaderRuntimeOptions.ShadersEnabled &&
        _isActive &&
        !_failed &&
        _overlay?.IsAvailable == true;

    [DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeScale { get; set; } = 0.3f;

    [DebugEditable(DisplayName = "Haze Amp", Step = 0.001f, Min = 0f, Max = 0.08f)]
    public float HazeAmp { get; set; } = 0.014f;

    [DebugEditable(DisplayName = "Haze Scale", Step = 0.01f, Min = 0.01f, Max = 12f)]
    public float HazeScale { get; set; } = 3.2f;

    [DebugEditable(DisplayName = "Haze Rise", Step = 0.01f, Min = -2f, Max = 2f)]
    public float HazeRise { get; set; } = 2f;

    [DebugEditable(DisplayName = "Haze Wave Frequency", Step = 0.01f, Min = 0f, Max = 40f)]
    public float HazeWaveFrequency { get; set; } = 11f;

    [DebugEditable(DisplayName = "Haze Wave Speed", Step = 0.01f, Min = -8f, Max = 8f)]
    public float HazeWaveSpeed { get; set; } = 2.49f;

    [DebugEditable(DisplayName = "Haze Noise Mix", Step = 0.01f, Min = 0f, Max = 2f)]
    public float HazeNoiseMix { get; set; } = 0.65f;

    [DebugEditable(DisplayName = "Haze Wave Mix", Step = 0.01f, Min = 0f, Max = 2f)]
    public float HazeWaveMix { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Haze Octaves", Step = 1f, Min = 1f, Max = 4f)]
    public int HazeOctaves { get; set; } = 3;

    [DebugEditable(DisplayName = "Haze Reach", Step = 0.01f, Min = 0.01f, Max = 2f)]
    public float HazeReach { get; set; } = 2f;

    [DebugEditable(DisplayName = "Haze Floor", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HazeFloor { get; set; } = 0.18f;

    [DebugEditable(DisplayName = "Ember Layers", Step = 1f, Min = 1f, Max = 7f)]
    public int EmberLayers { get; set; } = 7;

    [DebugEditable(DisplayName = "Scale Far", Step = 0.01f, Min = 0.01f, Max = 40f)]
    public float ScaleFar { get; set; } = 16f;

    [DebugEditable(DisplayName = "Scale Near", Step = 0.01f, Min = 0.01f, Max = 40f)]
    public float ScaleNear { get; set; } = 5f;

    [DebugEditable(DisplayName = "Size Far", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float SizeFar { get; set; } = 0.030f;

    [DebugEditable(DisplayName = "Size Near", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float SizeNear { get; set; } = 0.090f;

    [DebugEditable(DisplayName = "Size Variation", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SizeVariation { get; set; } = 0.60f;

    [DebugEditable(DisplayName = "Density Far", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DensityFar { get; set; } = 0.70f;

    [DebugEditable(DisplayName = "Density Near", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DensityNear { get; set; } = 0.32f;

    [DebugEditable(DisplayName = "Rise Far", Step = 0.001f, Min = -1f, Max = 1f)]
    public float RiseFar { get; set; } = 0.045f;

    [DebugEditable(DisplayName = "Rise Near", Step = 0.001f, Min = -1f, Max = 1f)]
    public float RiseNear { get; set; } = 0.150f;

    [DebugEditable(DisplayName = "Ember Drift", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
    public float EmberDrift { get; set; } = 0.021f;

    [DebugEditable(DisplayName = "Wander Amp", Step = 0.001f, Min = 0f, Max = 0.5f)]
    public float WanderAmp { get; set; } = 0.094f;

    [DebugEditable(DisplayName = "Wander Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float WanderScale { get; set; } = 1.4f;

    [DebugEditable(DisplayName = "Wander Speed", Step = 0.01f, Min = -2f, Max = 2f)]
    public float WanderSpeed { get; set; } = 0.20f;

    [DebugEditable(DisplayName = "Sway Amp", Step = 0.001f, Min = 0f, Max = 0.5f)]
    public float SwayAmp { get; set; } = 0.072f;

    [DebugEditable(DisplayName = "Sway Rate Min", Step = 0.01f, Min = 0f, Max = 10f)]
    public float SwayRateMin { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Sway Rate Max", Step = 0.01f, Min = 0f, Max = 10f)]
    public float SwayRateMax { get; set; } = 2.4f;

    [DebugEditable(DisplayName = "Twinkle Min Brightness", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TwinkleMinBrightness { get; set; } = 0.25f;

    [DebugEditable(DisplayName = "Twinkle Rate Min", Step = 0.01f, Min = 0f, Max = 20f)]
    public float TwinkleRateMin { get; set; } = 0.8f;

    [DebugEditable(DisplayName = "Twinkle Rate Max", Step = 0.01f, Min = 0f, Max = 20f)]
    public float TwinkleRateMax { get; set; } = 5.0f;

    [DebugEditable(DisplayName = "Ember Core", Step = 0.01f, Min = 0.001f, Max = 1f)]
    public float EmberCore { get; set; } = 0.32f;

    [DebugEditable(DisplayName = "Halo Gain", Step = 0.01f, Min = 0f, Max = 5f)]
    public float HaloGain { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "Core Gain", Step = 0.01f, Min = 0f, Max = 5f)]
    public float CoreGain { get; set; } = 1.30f;

    [DebugEditable(DisplayName = "Ember Bloom", Step = 0.01f, Min = 0f, Max = 2f)]
    public float EmberBloom { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Core R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreR { get; set; } = 1.00f;

    [DebugEditable(DisplayName = "Core G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreG { get; set; } = 0.92f;

    [DebugEditable(DisplayName = "Core B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreB { get; set; } = 0.62f;

    [DebugEditable(DisplayName = "Hot R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HotR { get; set; } = 1.00f;

    [DebugEditable(DisplayName = "Hot G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HotG { get; set; } = 0.48f;

    [DebugEditable(DisplayName = "Hot B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HotB { get; set; } = 0.12f;

    [DebugEditable(DisplayName = "Cool R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoolR { get; set; } = 0.75f;

    [DebugEditable(DisplayName = "Cool G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoolG { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Cool B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoolB { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Gain Far", Step = 0.01f, Min = 0f, Max = 5f)]
    public float GainFar { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Gain Near", Step = 0.01f, Min = 0f, Max = 5f)]
    public float GainNear { get; set; } = 1.20f;

    [DebugEditable(DisplayName = "Ember Gain", Step = 0.01f, Min = 0f, Max = 5f)]
    public float EmberGain { get; set; } = 1f;

    [DebugEditable(DisplayName = "Ember Top Dim", Step = 0.01f, Min = 0f, Max = 1f)]
    public float EmberTopDim { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Ember Fade Low", Step = 0.01f, Min = 0f, Max = 2f)]
    public float EmberFadeLow { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Ember Fade High", Step = 0.01f, Min = 0f, Max = 2f)]
    public float EmberFadeHigh { get; set; } = 1.05f;

    [DebugEditable(DisplayName = "Fallback Top R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundTopR { get; set; } = 0.04f;

    [DebugEditable(DisplayName = "Fallback Top G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundTopG { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Fallback Top B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundTopB { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Fallback Bottom R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundBottomR { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Fallback Bottom G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundBottomG { get; set; } = 0.12f;

    [DebugEditable(DisplayName = "Fallback Bottom B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundBottomB { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Fallback Glow Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float BackgroundGlowScale { get; set; } = 2f;

    public VolcanoEmbersDisplaySystem(
        EntityManager entityManager,
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        ContentManager content
    ) : base(entityManager)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _content = content;
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
        return Array.Empty<Entity>();
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
    }

    public override void Update(GameTime gameTime)
    {
        bool wasActive = _isActive;
        _isActive = IsVolcanoBattleActive();

        if (!wasActive && _isActive)
        {
            _timeSeconds = 0f;
        }

        if (_isActive && ShaderRuntimeOptions.ShadersEnabled)
        {
            EnsureLoaded();
            _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        base.Update(gameTime);
    }

    public void Composite(Texture2D source, RenderTarget2D destination)
    {
        if (!CanComposite || source == null || destination == null) return;

        ConfigureOverlay();
        _graphicsDevice.SetRenderTarget(destination);
        _graphicsDevice.Clear(Color.Black);
        _overlay.Begin(_spriteBatch);
        _overlay.Draw(_spriteBatch, source);
        _overlay.End(_spriteBatch);
    }

    private bool IsVolcanoBattleActive()
    {
        SceneState scene = EntityManager.GetEntitiesWithComponent<SceneState>()
            .FirstOrDefault()
            ?.GetComponent<SceneState>();
        if (scene?.Current != SceneId.Battle) return false;

        Battlefield battlefield = EntityManager.GetEntitiesWithComponent<Battlefield>()
            .FirstOrDefault()
            ?.GetComponent<Battlefield>();
        return battlefield?.Location == BattleLocation.Volcano;
    }

    private void EnsureLoaded()
    {
        if (_failed || _overlay != null) return;

        try
        {
            Effect effect = _content.Load<Effect>("Shaders/VolcanoEmbers");
            _overlay = new VolcanoEmbersOverlay(effect);
        }
        catch (Exception ex)
        {
            _failed = true;
            LoggingService.Append(
                "VolcanoEmbersDisplaySystem.EnsureLoaded",
                new JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = ex.Message
                }
            );
        }
    }

    private void ConfigureOverlay()
    {
        float swayRateMin = Math.Min(SwayRateMin, SwayRateMax);
        float swayRateMax = Math.Max(SwayRateMin, SwayRateMax);
        float twinkleRateMin = Math.Min(TwinkleRateMin, TwinkleRateMax);
        float twinkleRateMax = Math.Max(TwinkleRateMin, TwinkleRateMax);
        float fadeLow = Math.Min(EmberFadeLow, EmberFadeHigh);
        float fadeHigh = Math.Max(EmberFadeLow, EmberFadeHigh);
        if (fadeHigh - fadeLow < 0.001f)
        {
            fadeHigh = fadeLow + 0.001f;
        }

        _overlay.Time = _timeSeconds;
        _overlay.UseSourceTexture = 1f;
        _overlay.TimeScale = Math.Max(0f, TimeScale);

        _overlay.HazeAmp = Math.Max(0f, HazeAmp);
        _overlay.HazeScale = Math.Max(0.001f, HazeScale);
        _overlay.HazeRise = HazeRise;
        _overlay.HazeWaveFrequency = Math.Max(0f, HazeWaveFrequency);
        _overlay.HazeWaveSpeed = HazeWaveSpeed;
        _overlay.HazeNoiseMix = Math.Max(0f, HazeNoiseMix);
        _overlay.HazeWaveMix = Math.Max(0f, HazeWaveMix);
        _overlay.HazeOctaves = MathHelper.Clamp(HazeOctaves, 1, 4);
        _overlay.HazeReach = Math.Max(0.001f, HazeReach);
        _overlay.HazeFloor = MathHelper.Clamp(HazeFloor, 0f, 1f);

        _overlay.EmberLayers = MathHelper.Clamp(EmberLayers, 1, 7);
        _overlay.ScaleFar = Math.Max(0.001f, ScaleFar);
        _overlay.ScaleNear = Math.Max(0.001f, ScaleNear);
        _overlay.SizeFar = Math.Max(0.0001f, SizeFar);
        _overlay.SizeNear = Math.Max(0.0001f, SizeNear);
        _overlay.SizeVariation = MathHelper.Clamp(SizeVariation, 0f, 1f);
        _overlay.DensityFar = MathHelper.Clamp(DensityFar, 0f, 1f);
        _overlay.DensityNear = MathHelper.Clamp(DensityNear, 0f, 1f);
        _overlay.RiseFar = RiseFar;
        _overlay.RiseNear = RiseNear;
        _overlay.EmberDrift = EmberDrift;
        _overlay.WanderAmp = Math.Max(0f, WanderAmp);
        _overlay.WanderScale = Math.Max(0.001f, WanderScale);
        _overlay.WanderSpeed = WanderSpeed;
        _overlay.SwayAmp = Math.Max(0f, SwayAmp);
        _overlay.SwayRateMin = Math.Max(0f, swayRateMin);
        _overlay.SwayRateMax = Math.Max(0f, swayRateMax);
        _overlay.TwinkleMinBrightness = MathHelper.Clamp(TwinkleMinBrightness, 0f, 1f);
        _overlay.TwinkleRateMin = Math.Max(0f, twinkleRateMin);
        _overlay.TwinkleRateMax = Math.Max(0f, twinkleRateMax);
        _overlay.EmberCore = Math.Max(0.001f, EmberCore);
        _overlay.HaloGain = Math.Max(0f, HaloGain);
        _overlay.CoreGain = Math.Max(0f, CoreGain);
        _overlay.EmberBloom = Math.Max(0f, EmberBloom);
        _overlay.CoreColor = new Vector3(CoreR, CoreG, CoreB);
        _overlay.HotColor = new Vector3(HotR, HotG, HotB);
        _overlay.CoolColor = new Vector3(CoolR, CoolG, CoolB);
        _overlay.GainFar = Math.Max(0f, GainFar);
        _overlay.GainNear = Math.Max(0f, GainNear);
        _overlay.EmberGain = Math.Max(0f, EmberGain);
        _overlay.EmberTopDim = MathHelper.Clamp(EmberTopDim, 0f, 1f);
        _overlay.EmberFadeLow = Math.Max(0f, fadeLow);
        _overlay.EmberFadeHigh = Math.Max(0f, fadeHigh);

        _overlay.BackgroundTop = new Vector3(BackgroundTopR, BackgroundTopG, BackgroundTopB);
        _overlay.BackgroundBottom = new Vector3(BackgroundBottomR, BackgroundBottomG, BackgroundBottomB);
        _overlay.BackgroundGlowScale = Math.Max(0.001f, BackgroundGlowScale);
    }
}
