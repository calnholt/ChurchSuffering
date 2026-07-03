using System;
using System.Collections.Generic;
using System.Linq;
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

[DebugTab("Jungle Background")]
public class JungleBackgroundDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private JungleBackgroundOverlay _overlay;
    private bool _failed;
    private bool _isActive;
    private float _timeSeconds;

    public bool CanComposite =>
        ShaderRuntimeOptions.ShadersEnabled &&
        _isActive &&
        !_failed &&
        _overlay?.IsAvailable == true;

    [DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeScale { get; set; } = 1f;

    [DebugEditable(DisplayName = "Leaf Count", Step = 1f, Min = 1f, Max = 150f)]
    public int LeafCount { get; set; } = 150;

    [DebugEditable(DisplayName = "Field Overfill", Step = 0.01f, Min = 0.1f, Max = 3f)]
    public float FieldOverfill { get; set; } = 2f;

    [DebugEditable(DisplayName = "Fall Base", Step = 0.01f, Min = -1f, Max = 1f)]
    public float FallBase { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Fall Parallax", Step = 0.01f, Min = -2f, Max = 4f)]
    public float FallParallax { get; set; } = 0.9f;

    [DebugEditable(DisplayName = "Wind Drift", Step = 0.01f, Min = -1f, Max = 1f)]
    public float WindDrift { get; set; } = -0.05f;

    [DebugEditable(DisplayName = "Wind Parallax", Step = 0.01f, Min = -2f, Max = 4f)]
    public float WindParallax { get; set; } = 1f;

    [DebugEditable(DisplayName = "Wind Gust", Step = 0.01f, Min = 0f, Max = 1f)]
    public float WindGust { get; set; } = 0.01f;

    [DebugEditable(DisplayName = "Wind Gust Rate", Step = 0.01f, Min = 0.001f, Max = 3f)]
    public float WindGustRate { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Spin X", Step = 0.01f, Min = -2f, Max = 2f)]
    public float SpinRateX { get; set; } = 0.20f;

    [DebugEditable(DisplayName = "Spin Y", Step = 0.01f, Min = -2f, Max = 2f)]
    public float SpinRateY { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Spin Desync", Step = 0.01f, Min = 0f, Max = 3f)]
    public float SpinDesync { get; set; } = 1f;

    [DebugEditable(DisplayName = "Sway Amp", Step = 0.01f, Min = 0f, Max = 3f)]
    public float SwayAmp { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Sway Tilt", Step = 0.01f, Min = 0f, Max = 3f)]
    public float SwayTilt { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Sway Rate Min", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float SwayRateMin { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Sway Rate Max", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float SwayRateMax { get; set; } = 1.6f;

    [DebugEditable(DisplayName = "Leaf Radius Min", Step = 0.01f, Min = 0.01f, Max = 3f)]
    public float LeafRadiusMin { get; set; } = 0.3f;

    [DebugEditable(DisplayName = "Leaf Radius Var", Step = 0.01f, Min = 0f, Max = 3f)]
    public float LeafRadiusVariation { get; set; } = 0.8f;

    [DebugEditable(DisplayName = "Dark Leaf R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafColorDarkR { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Dark Leaf G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafColorDarkG { get; set; } = 0.22f;

    [DebugEditable(DisplayName = "Dark Leaf B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafColorDarkB { get; set; } = 0.03f;

    [DebugEditable(DisplayName = "Light Leaf R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafColorLightR { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Light Leaf G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafColorLightG { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Light Leaf B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafColorLightB { get; set; } = 0.18f;

    [DebugEditable(DisplayName = "Hue Jitter", Step = 0.01f, Min = 0f, Max = 1f)]
    public float LeafHueJitter { get; set; } = 0.12f;

    [DebugEditable(DisplayName = "Leaf Brightness", Step = 0.01f, Min = 0f, Max = 3f)]
    public float LeafBrightness { get; set; } = 1f;

    [DebugEditable(DisplayName = "Far Fade", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FarFade { get; set; } = 0.40f;

    [DebugEditable(DisplayName = "Camera Distance", Step = 0.01f, Min = 0.1f, Max = 40f)]
    public float CameraDistance { get; set; } = 15f;

    [DebugEditable(DisplayName = "Camera FOV", Step = 0.01f, Min = 0.1f, Max = 5f)]
    public float CameraFov { get; set; } = 1.5f;

    [DebugEditable(DisplayName = "Leaf Z Back", Step = 0.01f, Min = 0.1f, Max = 40f)]
    public float LeafZBack { get; set; } = 8f;

    [DebugEditable(DisplayName = "Background Brightness", Step = 0.01f, Min = 0f, Max = 3f)]
    public float BackgroundBrightness { get; set; } = 1f;

    [DebugEditable(DisplayName = "Sky Low R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundSkyLowR { get; set; } = 0.20f;

    [DebugEditable(DisplayName = "Sky Low G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundSkyLowG { get; set; } = 0.26f;

    [DebugEditable(DisplayName = "Sky Low B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundSkyLowB { get; set; } = 0.30f;

    [DebugEditable(DisplayName = "Sky High R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundSkyHighR { get; set; } = 0.45f;

    [DebugEditable(DisplayName = "Sky High G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundSkyHighG { get; set; } = 0.52f;

    [DebugEditable(DisplayName = "Sky High B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundSkyHighB { get; set; } = 0.55f;

    public JungleBackgroundDisplaySystem(
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
        _isActive = IsJungleBattleActive();

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

    private bool IsJungleBattleActive()
    {
        SceneState scene = EntityManager.GetEntitiesWithComponent<SceneState>()
            .FirstOrDefault()
            ?.GetComponent<SceneState>();
        if (scene?.Current != SceneId.Battle) return false;

        Battlefield battlefield = EntityManager.GetEntitiesWithComponent<Battlefield>()
            .FirstOrDefault()
            ?.GetComponent<Battlefield>();
        return battlefield?.Location == BattleLocation.Jungle;
    }

    private void EnsureLoaded()
    {
        if (_failed || _overlay != null) return;

        try
        {
            Effect effect = _content.Load<Effect>("Shaders/JungleBackground");
            _overlay = new JungleBackgroundOverlay(effect);
        }
        catch (Exception ex)
        {
            _failed = true;
            LoggingService.Append(
                "JungleBackgroundDisplaySystem.EnsureLoaded",
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
        float swayLow = Math.Max(0.0001f, Math.Min(SwayRateMin, SwayRateMax));
        float swayHigh = Math.Max(swayLow + 0.0001f, Math.Max(SwayRateMin, SwayRateMax));

        _overlay.Time = _timeSeconds;
        _overlay.LeafCount = MathHelper.Clamp(LeafCount, 1, 150);
        _overlay.FieldOverfill = Math.Max(0.001f, FieldOverfill);
        _overlay.TimeScale = Math.Max(0f, TimeScale);
        _overlay.FallBase = FallBase;
        _overlay.FallParallax = FallParallax;
        _overlay.WindDrift = WindDrift;
        _overlay.WindParallax = WindParallax;
        _overlay.WindGust = Math.Max(0f, WindGust);
        _overlay.WindGustRate = Math.Max(0.0001f, WindGustRate);
        _overlay.SpinRate = new Vector2(SpinRateX, SpinRateY);
        _overlay.SpinDesync = Math.Max(0f, SpinDesync);
        _overlay.SwayAmp = Math.Max(0f, SwayAmp);
        _overlay.SwayTilt = Math.Max(0f, SwayTilt);
        _overlay.SwayRateMin = swayLow;
        _overlay.SwayRateMax = swayHigh;
        _overlay.LeafRadiusMin = Math.Max(0.001f, LeafRadiusMin);
        _overlay.LeafRadiusVariation = Math.Max(0f, LeafRadiusVariation);
        _overlay.LeafColorDark = new Vector3(LeafColorDarkR, LeafColorDarkG, LeafColorDarkB);
        _overlay.LeafColorLight = new Vector3(LeafColorLightR, LeafColorLightG, LeafColorLightB);
        _overlay.LeafHueJitter = Math.Max(0f, LeafHueJitter);
        _overlay.LeafBrightness = Math.Max(0f, LeafBrightness);
        _overlay.FarFade = MathHelper.Clamp(FarFade, 0f, 1f);
        _overlay.CameraDistance = Math.Max(0.001f, CameraDistance);
        _overlay.CameraFov = Math.Max(0.001f, CameraFov);
        _overlay.LeafZBack = Math.Max(0.001f, LeafZBack);
        _overlay.BackgroundBrightness = Math.Max(0f, BackgroundBrightness);
        _overlay.BackgroundSkyLow = new Vector3(BackgroundSkyLowR, BackgroundSkyLowG, BackgroundSkyLowB);
        _overlay.BackgroundSkyHigh = new Vector3(BackgroundSkyHighR, BackgroundSkyHighG, BackgroundSkyHighB);
    }
}
