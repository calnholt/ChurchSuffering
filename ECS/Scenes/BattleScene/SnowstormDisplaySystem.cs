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

[DebugTab("Snowstorm")]
public class SnowstormDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private SnowstormOverlay _overlay;
    private bool _failed;
    private bool _isActive;
    private float _timeSeconds;

    public bool CanComposite =>
        ShaderRuntimeOptions.ShadersEnabled &&
        _isActive &&
        !_failed &&
        _overlay?.IsAvailable == true;

    [DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeScale { get; set; } = 0.2f;

    [DebugEditable(DisplayName = "Snow Layers", Step = 1f, Min = 1f, Max = 6f)]
    public int SnowLayers { get; set; } = 6;

    [DebugEditable(DisplayName = "Scale Far", Step = 0.01f, Min = 0.01f, Max = 60f)]
    public float ScaleFar { get; set; } = 24f;

    [DebugEditable(DisplayName = "Scale Near", Step = 0.01f, Min = 0.01f, Max = 60f)]
    public float ScaleNear { get; set; } = 5f;

    [DebugEditable(DisplayName = "Size Far", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float SizeFar { get; set; } = 0.040f;

    [DebugEditable(DisplayName = "Size Near", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float SizeNear { get; set; } = 0.13f;

    [DebugEditable(DisplayName = "Flake Jitter", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeJitter { get; set; } = 0.59f;

    [DebugEditable(DisplayName = "Size Variation", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeSizeVariation { get; set; } = 1f;

    [DebugEditable(DisplayName = "Density Far", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DensityFar { get; set; } = 0.95f;

    [DebugEditable(DisplayName = "Density Near", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DensityNear { get; set; } = 0.59f;

    [DebugEditable(DisplayName = "Fall Far", Step = 0.001f, Min = -1f, Max = 1f)]
    public float FallFar { get; set; } = 0.08f;

    [DebugEditable(DisplayName = "Fall Near", Step = 0.001f, Min = -1f, Max = 1f)]
    public float FallNear { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Flow Strength", Step = 0.01f, Min = 0f, Max = 2f)]
    public float FlowStrength { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Flow Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float FlowScale { get; set; } = 1.3f;

    [DebugEditable(DisplayName = "Flow Scroll X", Step = 0.01f, Min = -2f, Max = 2f)]
    public float FlowScrollX { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Flow Scroll Y", Step = 0.01f, Min = -2f, Max = 2f)]
    public float FlowScrollY { get; set; } = 0.16f;

    [DebugEditable(DisplayName = "Flow Depth Min", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlowDepthMin { get; set; } = 0.40f;

    [DebugEditable(DisplayName = "Wind Drift", Step = 0.01f, Min = -2f, Max = 2f)]
    public float WindDrift { get; set; } = 0.14f;

    [DebugEditable(DisplayName = "Wind Gust", Step = 0.01f, Min = 0f, Max = 2f)]
    public float WindGust { get; set; } = 0.40f;

    [DebugEditable(DisplayName = "Wind Gust Rate", Step = 0.01f, Min = 0f, Max = 3f)]
    public float WindGustRate { get; set; } = 0.21f;

    [DebugEditable(DisplayName = "Wind Parallax", Step = 0.01f, Min = 0f, Max = 3f)]
    public float WindParallax { get; set; } = 1f;

    [DebugEditable(DisplayName = "Sheet R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetR { get; set; } = 0.86f;

    [DebugEditable(DisplayName = "Sheet G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetG { get; set; } = 0.90f;

    [DebugEditable(DisplayName = "Sheet B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetB { get; set; } = 0.97f;

    [DebugEditable(DisplayName = "Sheet Base", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetBase { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Sheet Gust", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetGust { get; set; } = 0.26f;

    [DebugEditable(DisplayName = "Sheet Scale", Step = 0.01f, Min = 0.01f, Max = 20f)]
    public float SheetScale { get; set; } = 1.7f;

    [DebugEditable(DisplayName = "Sheet Stretch", Step = 0.01f, Min = 0.01f, Max = 40f)]
    public float SheetStretch { get; set; } = 7f;

    [DebugEditable(DisplayName = "Sheet Speed", Step = 0.01f, Min = -10f, Max = 10f)]
    public float SheetSpeed { get; set; } = 1.9f;

    [DebugEditable(DisplayName = "Sheet Low", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetLow { get; set; } = 0.42f;

    [DebugEditable(DisplayName = "Sheet High", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SheetHigh { get; set; } = 0.86f;

    [DebugEditable(DisplayName = "Sway Amp", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SwayAmp { get; set; } = 0.16f;

    [DebugEditable(DisplayName = "Sway Rate Min", Step = 0.01f, Min = 0f, Max = 10f)]
    public float SwayRateMin { get; set; } = 0.8f;

    [DebugEditable(DisplayName = "Sway Rate Max", Step = 0.01f, Min = 0f, Max = 10f)]
    public float SwayRateMax { get; set; } = 2.6f;

    [DebugEditable(DisplayName = "Twinkle Min Brightness", Step = 0.01f, Min = 0f, Max = 1f)]
    public float TwinkleMinBrightness { get; set; } = 0.40f;

    [DebugEditable(DisplayName = "Twinkle Rate Min", Step = 0.01f, Min = 0f, Max = 20f)]
    public float TwinkleRateMin { get; set; } = 0.6f;

    [DebugEditable(DisplayName = "Twinkle Rate Max", Step = 0.01f, Min = 0f, Max = 20f)]
    public float TwinkleRateMax { get; set; } = 4f;

    [DebugEditable(DisplayName = "Twinkle Depth Bias", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TwinkleDepthBias { get; set; } = 1.4f;

    [DebugEditable(DisplayName = "Crystal Min", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CrystalMin { get; set; } = 0.60f;

    [DebugEditable(DisplayName = "Crystal Arm", Step = 0.01f, Min = 0.01f, Max = 2f)]
    public float CrystalArm { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Crystal Thick", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float CrystalThick { get; set; } = 0.060f;

    [DebugEditable(DisplayName = "Crystal Spin Min", Step = 0.01f, Min = -5f, Max = 5f)]
    public float CrystalSpinMin { get; set; } = -0.5f;

    [DebugEditable(DisplayName = "Crystal Spin Max", Step = 0.01f, Min = -5f, Max = 5f)]
    public float CrystalSpinMax { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Crystal Variety", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CrystalVariety { get; set; } = 0.40f;

    [DebugEditable(DisplayName = "DoF Focus", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DofFocus { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "DoF Spread", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DofSpread { get; set; } = 0.10f;

    [DebugEditable(DisplayName = "Edge Sharp", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float EdgeSharp { get; set; } = 0.010f;

    [DebugEditable(DisplayName = "Edge Soft", Step = 0.001f, Min = 0.001f, Max = 0.5f)]
    public float EdgeSoft { get; set; } = 0.110f;

    [DebugEditable(DisplayName = "Flake Gain", Step = 0.01f, Min = 0f, Max = 5f)]
    public float FlakeGain { get; set; } = 1.25f;

    [DebugEditable(DisplayName = "Sparkle Glow", Step = 0.01f, Min = 0f, Max = 2f)]
    public float SparkleGlow { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Flake Far R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeFarR { get; set; } = 0.60f;

    [DebugEditable(DisplayName = "Flake Far G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeFarG { get; set; } = 0.69f;

    [DebugEditable(DisplayName = "Flake Far B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeFarB { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "Flake Near R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeNearR { get; set; } = 0.93f;

    [DebugEditable(DisplayName = "Flake Near G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeNearG { get; set; } = 0.97f;

    [DebugEditable(DisplayName = "Flake Near B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FlakeNearB { get; set; } = 1.00f;

    [DebugEditable(DisplayName = "Far Fade", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FarFade { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Sky Top R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyTopR { get; set; } = 0.03f;

    [DebugEditable(DisplayName = "Sky Top G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyTopG { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Sky Top B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyTopB { get; set; } = 0.13f;

    [DebugEditable(DisplayName = "Sky Bottom R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyBottomR { get; set; } = 0.12f;

    [DebugEditable(DisplayName = "Sky Bottom G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyBottomG { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Sky Bottom B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyBottomB { get; set; } = 0.22f;

    [DebugEditable(DisplayName = "Sky Gradient", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SkyGradient { get; set; } = 1f;

    [DebugEditable(DisplayName = "Haze R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HazeR { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Haze G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HazeG { get; set; } = 0.60f;

    [DebugEditable(DisplayName = "Haze B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HazeB { get; set; } = 0.68f;

    [DebugEditable(DisplayName = "Haze Base", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HazeBase { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Haze Gust", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HazeGust { get; set; } = 0.14f;

    [DebugEditable(DisplayName = "Haze Scale", Step = 0.01f, Min = 0.01f, Max = 20f)]
    public float HazeScale { get; set; } = 2.2f;

    [DebugEditable(DisplayName = "Haze Drift", Step = 0.01f, Min = -2f, Max = 2f)]
    public float HazeDrift { get; set; } = 0.06f;

    [DebugEditable(DisplayName = "Dither", Step = 0.001f, Min = 0f, Max = 0.1f)]
    public float Dither { get; set; } = 0f;

    public SnowstormDisplaySystem(
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
        _isActive = IsTundraBattleActive();

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

    private bool IsTundraBattleActive()
    {
        SceneState scene = EntityManager.GetEntitiesWithComponent<SceneState>()
            .FirstOrDefault()
            ?.GetComponent<SceneState>();
        if (scene?.Current != SceneId.Battle) return false;

        Battlefield battlefield = EntityManager.GetEntitiesWithComponent<Battlefield>()
            .FirstOrDefault()
            ?.GetComponent<Battlefield>();
        return battlefield?.Location == BattleLocation.Tundra;
    }

    private void EnsureLoaded()
    {
        if (_failed || _overlay != null) return;

        try
        {
            Effect effect = _content.Load<Effect>("Shaders/Snowstorm");
            _overlay = new SnowstormOverlay(effect);
        }
        catch (Exception ex)
        {
            _failed = true;
            LoggingService.Append(
                "SnowstormDisplaySystem.EnsureLoaded",
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
        float sheetLow = Math.Min(SheetLow, SheetHigh);
        float sheetHigh = Math.Max(SheetLow, SheetHigh);
        if (sheetHigh - sheetLow < 0.001f)
        {
            sheetHigh = sheetLow + 0.001f;
        }

        float swayRateMin = Math.Min(SwayRateMin, SwayRateMax);
        float swayRateMax = Math.Max(SwayRateMin, SwayRateMax);
        float twinkleRateMin = Math.Min(TwinkleRateMin, TwinkleRateMax);
        float twinkleRateMax = Math.Max(TwinkleRateMin, TwinkleRateMax);
        float crystalSpinMin = Math.Min(CrystalSpinMin, CrystalSpinMax);
        float crystalSpinMax = Math.Max(CrystalSpinMin, CrystalSpinMax);

        _overlay.Time = _timeSeconds;
        _overlay.UseSourceTexture = 1f;
        _overlay.TimeScale = Math.Max(0f, TimeScale);

        _overlay.SnowLayers = MathHelper.Clamp(SnowLayers, 1, 6);
        _overlay.ScaleFar = Math.Max(0.001f, ScaleFar);
        _overlay.ScaleNear = Math.Max(0.001f, ScaleNear);
        _overlay.SizeFar = Math.Max(0.0001f, SizeFar);
        _overlay.SizeNear = Math.Max(0.0001f, SizeNear);
        _overlay.FlakeJitter = MathHelper.Clamp(FlakeJitter, 0f, 1f);
        _overlay.FlakeSizeVariation = MathHelper.Clamp(FlakeSizeVariation, 0f, 1f);
        _overlay.DensityFar = MathHelper.Clamp(DensityFar, 0f, 1f);
        _overlay.DensityNear = MathHelper.Clamp(DensityNear, 0f, 1f);
        _overlay.FallFar = FallFar;
        _overlay.FallNear = FallNear;

        _overlay.FlowStrength = Math.Max(0f, FlowStrength);
        _overlay.FlowScale = Math.Max(0.001f, FlowScale);
        _overlay.FlowScrollX = FlowScrollX;
        _overlay.FlowScrollY = FlowScrollY;
        _overlay.FlowDepthMin = MathHelper.Clamp(FlowDepthMin, 0f, 1f);
        _overlay.WindDrift = WindDrift;
        _overlay.WindGust = Math.Max(0f, WindGust);
        _overlay.WindGustRate = Math.Max(0f, WindGustRate);
        _overlay.WindParallax = Math.Max(0f, WindParallax);

        _overlay.SheetColor = ClampVector(SheetR, SheetG, SheetB);
        _overlay.SheetBase = Math.Max(0f, SheetBase);
        _overlay.SheetGust = Math.Max(0f, SheetGust);
        _overlay.SheetScale = Math.Max(0.001f, SheetScale);
        _overlay.SheetStretch = Math.Max(0.001f, SheetStretch);
        _overlay.SheetSpeed = SheetSpeed;
        _overlay.SheetLow = MathHelper.Clamp(sheetLow, 0f, 1f);
        _overlay.SheetHigh = MathHelper.Clamp(sheetHigh, 0f, 1f);

        _overlay.SwayAmp = Math.Max(0f, SwayAmp);
        _overlay.SwayRateMin = Math.Max(0f, swayRateMin);
        _overlay.SwayRateMax = Math.Max(0f, swayRateMax);
        _overlay.TwinkleMinBrightness = MathHelper.Clamp(TwinkleMinBrightness, 0f, 1f);
        _overlay.TwinkleRateMin = Math.Max(0f, twinkleRateMin);
        _overlay.TwinkleRateMax = Math.Max(0f, twinkleRateMax);
        _overlay.TwinkleDepthBias = Math.Max(0f, TwinkleDepthBias);

        _overlay.CrystalMin = MathHelper.Clamp(CrystalMin, 0f, 1f);
        _overlay.CrystalArm = Math.Max(0.001f, CrystalArm);
        _overlay.CrystalThick = Math.Max(0.0001f, CrystalThick);
        _overlay.CrystalSpinMin = crystalSpinMin;
        _overlay.CrystalSpinMax = crystalSpinMax;
        _overlay.CrystalVariety = MathHelper.Clamp(CrystalVariety, 0f, 1f);

        _overlay.DofFocus = MathHelper.Clamp(DofFocus, 0f, 1f);
        _overlay.DofSpread = Math.Max(0f, DofSpread);
        _overlay.EdgeSharp = Math.Max(0.0001f, EdgeSharp);
        _overlay.EdgeSoft = Math.Max(0.0001f, EdgeSoft);

        _overlay.FlakeGain = Math.Max(0f, FlakeGain);
        _overlay.SparkleGlow = Math.Max(0f, SparkleGlow);
        _overlay.FlakeColorFar = ClampVector(FlakeFarR, FlakeFarG, FlakeFarB);
        _overlay.FlakeColorNear = ClampVector(FlakeNearR, FlakeNearG, FlakeNearB);
        _overlay.FarFade = MathHelper.Clamp(FarFade, 0f, 1f);

        _overlay.SkyTop = ClampVector(SkyTopR, SkyTopG, SkyTopB);
        _overlay.SkyBottom = ClampVector(SkyBottomR, SkyBottomG, SkyBottomB);
        _overlay.SkyGradient = MathHelper.Clamp(SkyGradient, 0f, 1f);

        _overlay.HazeColor = ClampVector(HazeR, HazeG, HazeB);
        _overlay.HazeBase = Math.Max(0f, HazeBase);
        _overlay.HazeGust = Math.Max(0f, HazeGust);
        _overlay.HazeScale = Math.Max(0.001f, HazeScale);
        _overlay.HazeDrift = HazeDrift;
        _overlay.Dither = Math.Max(0f, Dither);
    }

    private static Vector3 ClampVector(float r, float g, float b)
    {
        return new Vector3(
            MathHelper.Clamp(r, 0f, 1f),
            MathHelper.Clamp(g, 0f, 1f),
            MathHelper.Clamp(b, 0f, 1f)
        );
    }
}
