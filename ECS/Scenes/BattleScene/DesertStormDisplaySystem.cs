using System;
using System.Collections.Generic;
using System.Linq;
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

[DebugTab("Desert Storm")]
public class DesertStormDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private DesertStormOverlay _overlay;
    private bool _failed;
    private bool _isActive;
    private float _timeSeconds;

    public bool CanComposite =>
        ShaderRuntimeOptions.ShadersEnabled &&
        _isActive &&
        !_failed &&
        _overlay?.IsAvailable == true;

    [DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeScale { get; set; } = 5f;

    [DebugEditable(DisplayName = "Base Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
    public float BaseScale { get; set; } = 3.17f;

    [DebugEditable(DisplayName = "Lacunarity", Step = 0.01f, Min = 0.01f, Max = 5f)]
    public float Lacunarity { get; set; } = 2f;

    [DebugEditable(DisplayName = "Persistence", Step = 0.01f, Min = 0f, Max = 1f)]
    public float Persistence { get; set; } = 0.5f;

    [DebugEditable(DisplayName = "Warp Strength", Step = 0.01f, Min = 0f, Max = 10f)]
    public float WarpStrength { get; set; } = 2.97f;

    [DebugEditable(DisplayName = "Density Low", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DensityRemapLow { get; set; } = 0.4f;

    [DebugEditable(DisplayName = "Density High", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DensityRemapHigh { get; set; } = 0.93f;

    [DebugEditable(DisplayName = "Drift Speed", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
    public float DriftSpeed { get; set; } = 0.025f;

    [DebugEditable(DisplayName = "Vertical Drift", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
    public float DriftVertical { get; set; } = 0.006f;

    [DebugEditable(DisplayName = "Warp Drift A", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
    public float WarpDriftA { get; set; } = 0.018f;

    [DebugEditable(DisplayName = "Warp Drift B", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
    public float WarpDriftB { get; set; } = 0.012f;

    [DebugEditable(DisplayName = "Morph Speed", Step = 0.001f, Min = -0.5f, Max = 0.5f)]
    public float MorphSpeed { get; set; } = 0.016f;

    [DebugEditable(DisplayName = "Shadow R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShadowR { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Shadow G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShadowG { get; set; } = 0.47f;

    [DebugEditable(DisplayName = "Shadow B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float ShadowB { get; set; } = 0.37f;

    [DebugEditable(DisplayName = "Mid R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float MidR { get; set; } = 0.70f;

    [DebugEditable(DisplayName = "Mid G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float MidG { get; set; } = 0.62f;

    [DebugEditable(DisplayName = "Mid B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float MidB { get; set; } = 0.51f;

    [DebugEditable(DisplayName = "Highlight R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HighlightR { get; set; } = 0.82f;

    [DebugEditable(DisplayName = "Highlight G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HighlightG { get; set; } = 0.75f;

    [DebugEditable(DisplayName = "Highlight B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float HighlightB { get; set; } = 0.64f;

    [DebugEditable(DisplayName = "Bright R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BrightR { get; set; } = 0.89f;

    [DebugEditable(DisplayName = "Bright G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BrightG { get; set; } = 0.82f;

    [DebugEditable(DisplayName = "Bright B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BrightB { get; set; } = 0.71f;

    [DebugEditable(DisplayName = "Vertical Gradient", Step = 0.01f, Min = -1f, Max = 1f)]
    public float VerticalGradient { get; set; } = 0.08f;

    [DebugEditable(DisplayName = "Dust Base", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DustBase { get; set; } = 0f;

    [DebugEditable(DisplayName = "Dust Density", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DustDensity { get; set; } = 1f;

    [DebugEditable(DisplayName = "Scene Tint R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SceneTintR { get; set; } = 0.90f;

    [DebugEditable(DisplayName = "Scene Tint G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SceneTintG { get; set; } = 0.82f;

    [DebugEditable(DisplayName = "Scene Tint B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SceneTintB { get; set; } = 0.68f;

    [DebugEditable(DisplayName = "Scene Tint Strength", Step = 0.01f, Min = 0f, Max = 1f)]
    public float SceneTintStrength { get; set; } = 0.40f;

    [DebugEditable(DisplayName = "Grain Intensity", Step = 0.01f, Min = 0f, Max = 1f)]
    public float GrainIntensity { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Grain Fineness", Step = 0.01f, Min = 0.01f, Max = 4f)]
    public float GrainFineness { get; set; } = 1f;

    [DebugEditable(DisplayName = "Vignette Amount", Step = 0.01f, Min = 0f, Max = 1f)]
    public float VignetteAmount { get; set; } = 0f;

    public DesertStormDisplaySystem(
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
        _isActive = IsDesertBattleActive();

        if (!wasActive && _isActive)
        {
            _timeSeconds = 0f;
        }

        if (_isActive && ShaderRuntimeOptions.ShadersEnabled)
        {
            EnsureLoaded();
            _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds) *
                MathHelper.Max(0f, TimeScale);
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

    private bool IsDesertBattleActive()
    {
        SceneState scene = EntityManager.GetEntitiesWithComponent<SceneState>()
            .FirstOrDefault()
            ?.GetComponent<SceneState>();
        if (scene?.Current != SceneId.Battle) return false;

        Battlefield battlefield = EntityManager.GetEntitiesWithComponent<Battlefield>()
            .FirstOrDefault()
            ?.GetComponent<Battlefield>();
        return battlefield?.Location == BattleLocation.Desert;
    }

    private void EnsureLoaded()
    {
        if (_failed || _overlay != null) return;

        try
        {
            Effect effect = _content.Load<Effect>("Shaders/DesertStorm");
            _overlay = new DesertStormOverlay(effect);
        }
        catch (Exception ex)
        {
            _failed = true;
            LoggingService.Append(
                "DesertStormDisplaySystem.EnsureLoaded",
                new System.Text.Json.Nodes.JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = ex.Message
                }
            );
        }
    }

    private void ConfigureOverlay()
    {
        float densityLow = MathHelper.Clamp(Math.Min(DensityRemapLow, DensityRemapHigh), 0f, 1f);
        float densityHigh = MathHelper.Clamp(Math.Max(DensityRemapLow, DensityRemapHigh), 0f, 1f);
        if (densityHigh - densityLow < 0.001f)
        {
            densityHigh = Math.Min(1f, densityLow + 0.001f);
            densityLow = Math.Max(0f, densityHigh - 0.001f);
        }

        _overlay.Time = _timeSeconds;
        _overlay.BaseScale = Math.Max(0.001f, BaseScale);
        _overlay.Lacunarity = Math.Max(0.001f, Lacunarity);
        _overlay.Persistence = MathHelper.Clamp(Persistence, 0f, 1f);
        _overlay.WarpStrength = Math.Max(0f, WarpStrength);
        _overlay.DensityRemapLow = densityLow;
        _overlay.DensityRemapHigh = densityHigh;
        _overlay.DriftSpeed = DriftSpeed;
        _overlay.DriftVertical = DriftVertical;
        _overlay.WarpDriftA = WarpDriftA;
        _overlay.WarpDriftB = WarpDriftB;
        _overlay.MorphSpeed = MorphSpeed;
        _overlay.ShadowColor = new Vector3(ShadowR, ShadowG, ShadowB);
        _overlay.MidColor = new Vector3(MidR, MidG, MidB);
        _overlay.HighlightColor = new Vector3(HighlightR, HighlightG, HighlightB);
        _overlay.BrightColor = new Vector3(BrightR, BrightG, BrightB);
        _overlay.VerticalGradient = VerticalGradient;
        _overlay.DustBase = MathHelper.Clamp(DustBase, 0f, 1f);
        _overlay.DustDensity = MathHelper.Clamp(DustDensity, 0f, 1f);
        _overlay.SceneTint = new Vector3(SceneTintR, SceneTintG, SceneTintB);
        _overlay.SceneTintStrength = MathHelper.Clamp(SceneTintStrength, 0f, 1f);
        _overlay.GrainIntensity = Math.Max(0f, GrainIntensity);
        _overlay.GrainFineness = Math.Max(0.001f, GrainFineness);
        _overlay.VignetteAmount = Math.Max(0f, VignetteAmount);
    }
}
