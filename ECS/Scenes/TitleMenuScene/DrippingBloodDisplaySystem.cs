using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems;

[DebugTab("Dripping Blood")]
public sealed class DrippingBloodDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private readonly Texture2D _whitePixel;

    private DrippingBloodOverlay _overlay;
    private bool _failed;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Drip Count", Step = 1, Min = 1, Max = 25)]
    public int DripCount { get; set; } = 20;

    [DebugEditable(DisplayName = "Layer Count", Step = 1, Min = 1, Max = 3)]
    public int LayerCount { get; set; } = 1;

    [DebugEditable(DisplayName = "Speed Min", Step = 0.01f, Min = 0.01f, Max = 1f)]
    public float SpeedMin { get; set; } = 0.06f;

    [DebugEditable(DisplayName = "Speed Max", Step = 0.01f, Min = 0.01f, Max = 1f)]
    public float SpeedMax { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Rest Min", Step = 0.1f, Min = 0f, Max = 20f)]
    public float RestMin { get; set; } = 1.5f;

    [DebugEditable(DisplayName = "Rest Max", Step = 0.1f, Min = 0f, Max = 20f)]
    public float RestMax { get; set; } = 5f;

    [DebugEditable(DisplayName = "Fade Power", Step = 0.1f, Min = 0.1f, Max = 6f)]
    public float FadePower { get; set; } = 1.8f;

    [DebugEditable(DisplayName = "Offscreen Fade", Step = 0.05f, Min = 0.05f, Max = 2f)]
    public float OffscreenFade { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Width Min", Step = 0.001f, Min = 0.001f, Max = 0.1f)]
    public float WidthMin { get; set; } = 0.003f;

    [DebugEditable(DisplayName = "Width Max", Step = 0.001f, Min = 0.001f, Max = 0.1f)]
    public float WidthMax { get; set; } = 0.016f;

    [DebugEditable(DisplayName = "Taper At Top", Step = 0.05f, Min = 0f, Max = 1f)]
    public float TaperAtTop { get; set; } = 0.65f;

    [DebugEditable(DisplayName = "Tip Roundness", Step = 0.05f, Min = 0f, Max = 2f)]
    public float TipRoundness { get; set; } = 1f;

    [DebugEditable(DisplayName = "Wobble Amount", Step = 0.0005f, Min = 0f, Max = 0.03f)]
    public float WobbleAmount { get; set; } = 0.0025f;

    [DebugEditable(DisplayName = "Wobble Frequency", Step = 1f, Min = 1f, Max = 50f)]
    public float WobbleFrequency { get; set; } = 14f;

    [DebugEditable(DisplayName = "Thickness Variation", Step = 0.05f, Min = 0f, Max = 1f)]
    public float ThicknessVariation { get; set; } = 0.35f;

    [DebugEditable(DisplayName = "Background R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundR { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Background G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundG { get; set; } = 0.003f;

    [DebugEditable(DisplayName = "Background B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundB { get; set; } = 0.003f;

    [DebugEditable(DisplayName = "Drip R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DripR { get; set; } = 0.70f;

    [DebugEditable(DisplayName = "Drip G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DripG { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Drip B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float DripB { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Vignette Strength", Step = 0.05f, Min = 0f, Max = 1f)]
    public float VignetteStrength { get; set; }

    public DrippingBloodDisplaySystem(
        EntityManager entityManager,
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        ContentManager content)
        : base(entityManager)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _content = content;
        _whitePixel = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whitePixel.SetData(new[] { Color.White });
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
        return EntityManager.GetEntitiesWithComponent<SceneState>();
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
        var scene = entity.GetComponent<SceneState>();
        if (scene == null || scene.Current != SceneId.TitleMenu)
        {
            _timeSeconds = 0f;
            return;
        }

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
    }

    public void Draw()
    {
        var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
            .FirstOrDefault()
            ?.GetComponent<SceneState>();
        if (scene == null || scene.Current != SceneId.TitleMenu) return;

        if (!ShaderRuntimeOptions.ShadersEnabled || !EnsureOverlayLoaded())
        {
            DrawFallback();
            return;
        }

        _overlay.Time = _timeSeconds;
        _overlay.DripCount = Math.Clamp(DripCount, 1, 25);
        _overlay.LayerCount = Math.Clamp(LayerCount, 1, 3);
        _overlay.SpeedMin = Math.Min(SpeedMin, SpeedMax);
        _overlay.SpeedMax = Math.Max(SpeedMin, SpeedMax);
        _overlay.RestMin = Math.Min(RestMin, RestMax);
        _overlay.RestMax = Math.Max(RestMin, RestMax);
        _overlay.FadePower = FadePower;
        _overlay.OffscreenFade = OffscreenFade;
        _overlay.WidthMin = Math.Min(WidthMin, WidthMax);
        _overlay.WidthMax = Math.Max(WidthMin, WidthMax);
        _overlay.TaperAtTop = TaperAtTop;
        _overlay.TipRoundness = TipRoundness;
        _overlay.WobbleAmount = WobbleAmount;
        _overlay.WobbleFrequency = WobbleFrequency;
        _overlay.ThicknessVariation = ThicknessVariation;
        _overlay.BackgroundColor = new Vector3(BackgroundR, BackgroundG, BackgroundB);
        _overlay.DripColor = new Vector3(DripR, DripG, DripB);
        _overlay.VignetteStrength = VignetteStrength;

        BlendState savedBlend = _graphicsDevice.BlendState;
        SamplerState savedSampler = _graphicsDevice.SamplerStates[0];
        DepthStencilState savedDepth = _graphicsDevice.DepthStencilState;
        RasterizerState savedRasterizer = _graphicsDevice.RasterizerState;

        _spriteBatch.End();
        _overlay.Begin(_spriteBatch);
        _overlay.Draw(_spriteBatch);
        _overlay.End(_spriteBatch);
        _spriteBatch.Begin(
            SpriteSortMode.Immediate,
            savedBlend,
            savedSampler,
            savedDepth,
            savedRasterizer,
            null,
            Game1.Display.SpriteBatchTransform);
    }

    private bool EnsureOverlayLoaded()
    {
        if (_failed) return false;
        if (_overlay != null) return _overlay.IsAvailable;

        try
        {
            var effect = _content.Load<Effect>("Shaders/DrippingBlood");
            _overlay = new DrippingBloodOverlay(_graphicsDevice, effect);
        }
        catch (Exception exception)
        {
            LoggingService.Append(
                "DrippingBloodDisplaySystem.EnsureOverlayLoaded",
                new System.Text.Json.Nodes.JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = exception.Message
                });
            _failed = true;
        }

        return _overlay?.IsAvailable == true;
    }

    private void DrawFallback()
    {
        _spriteBatch.Draw(
            _whitePixel,
            new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
            Color.Black);
    }
}
