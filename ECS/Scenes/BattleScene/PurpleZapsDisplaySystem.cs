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

[DebugTab("Purple Zaps")]
public class PurpleZapsDisplaySystem : Core.System
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;
    private PurpleZapsOverlay _overlay;
    private bool _failed;
    private bool _isActive;
    private float _timeSeconds;

    public bool CanComposite =>
        ShaderRuntimeOptions.ShadersEnabled &&
        _isActive &&
        !_failed &&
        _overlay?.IsAvailable == true;

    [DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
    public float TimeScale { get; set; } = 0.09f;

    [DebugEditable(DisplayName = "Zoom", Step = 0.01f, Min = 0.01f, Max = 0.5f)]
    public float Zoom { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Zap Warp", Step = 0.01f, Min = 0.01f, Max = 5f)]
    public float ZapWarp { get; set; } = 1.50f;

    [DebugEditable(DisplayName = "Zap Swirl", Step = 0.01f, Min = -20f, Max = 20f)]
    public float ZapSwirl { get; set; } = 9.00f;

    [DebugEditable(DisplayName = "Zap Growth", Step = 0.001f, Min = -0.05f, Max = 0.10f)]
    public float ZapGrowth { get; set; } = 0.021f;

    [DebugEditable(DisplayName = "Zap Speed", Step = 0.01f, Min = -5f, Max = 5f)]
    public float ZapSpeed { get; set; } = 1.00f;

    [DebugEditable(DisplayName = "Zap Floor", Step = 0.01f, Min = 0f, Max = 2f)]
    public float ZapFloor { get; set; } = 0.55f;

    [DebugEditable(DisplayName = "Zap Gain", Step = 0.01f, Min = 0f, Max = 5f)]
    public float ZapGain { get; set; } = 1.60f;

    [DebugEditable(DisplayName = "Glow R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float GlowR { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Glow G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float GlowG { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Glow B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float GlowB { get; set; } = 0.29f;

    [DebugEditable(DisplayName = "Core R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreR { get; set; } = 0.85f;

    [DebugEditable(DisplayName = "Core G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreG { get; set; } = 0.60f;

    [DebugEditable(DisplayName = "Core B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float CoreB { get; set; } = 1.00f;

    [DebugEditable(DisplayName = "Core Low", Step = 0.01f, Min = 0f, Max = 5f)]
    public float ZapCoreLow { get; set; } = 0.80f;

    [DebugEditable(DisplayName = "Core High", Step = 0.01f, Min = 0f, Max = 5f)]
    public float ZapCoreHigh { get; set; } = 2.00f;

    [DebugEditable(DisplayName = "Background Dim", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundDim { get; set; } = 0f;

    [DebugEditable(DisplayName = "Fallback Top R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundTopR { get; set; } = 0.05f;

    [DebugEditable(DisplayName = "Fallback Top G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundTopG { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Fallback Top B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundTopB { get; set; } = 0.12f;

    [DebugEditable(DisplayName = "Fallback Bottom R", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundBottomR { get; set; } = 0.00f;

    [DebugEditable(DisplayName = "Fallback Bottom G", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundBottomG { get; set; } = 0.00f;

    [DebugEditable(DisplayName = "Fallback Bottom B", Step = 0.01f, Min = 0f, Max = 1f)]
    public float BackgroundBottomB { get; set; } = 0.00f;

    public PurpleZapsDisplaySystem(
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
        _isActive = IsGothicBattleActive();

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

    private bool IsGothicBattleActive()
    {
        SceneState scene = EntityManager.GetEntitiesWithComponent<SceneState>()
            .FirstOrDefault()
            ?.GetComponent<SceneState>();
        if (scene?.Current != SceneId.Battle) return false;

        Battlefield battlefield = EntityManager.GetEntitiesWithComponent<Battlefield>()
            .FirstOrDefault()
            ?.GetComponent<Battlefield>();
        return battlefield?.Location == BattleLocation.Gothic;
    }

    private void EnsureLoaded()
    {
        if (_failed || _overlay != null) return;

        try
        {
            Effect effect = _content.Load<Effect>("Shaders/PurpleZaps");
            _overlay = new PurpleZapsOverlay(effect);
        }
        catch (Exception ex)
        {
            _failed = true;
            LoggingService.Append(
                "PurpleZapsDisplaySystem.EnsureLoaded",
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
        float coreLow = Math.Min(ZapCoreLow, ZapCoreHigh);
        float coreHigh = Math.Max(ZapCoreLow, ZapCoreHigh);
        if (coreHigh - coreLow < 0.001f)
        {
            coreHigh = coreLow + 0.001f;
        }

        _overlay.Time = _timeSeconds;
        _overlay.UseSourceTexture = 1f;
        _overlay.Zoom = Math.Max(0.001f, Zoom);
        _overlay.ZapWarp = Math.Max(0.001f, ZapWarp);
        _overlay.ZapSwirl = ZapSwirl;
        _overlay.ZapGrowth = ZapGrowth;
        _overlay.ZapSpeed = ZapSpeed;
        _overlay.ZapFloor = Math.Max(0f, ZapFloor);
        _overlay.ZapGain = Math.Max(0f, ZapGain);
        _overlay.ZapGlowColor = new Vector3(GlowR, GlowG, GlowB);
        _overlay.ZapCoreColor = new Vector3(CoreR, CoreG, CoreB);
        _overlay.ZapCoreLow = Math.Max(0f, coreLow);
        _overlay.ZapCoreHigh = Math.Max(0f, coreHigh);
        _overlay.BackgroundDim = MathHelper.Clamp(BackgroundDim, 0f, 1f);
        _overlay.BackgroundFallbackTop = new Vector3(BackgroundTopR, BackgroundTopG, BackgroundTopB);
        _overlay.BackgroundFallbackBottom = new Vector3(BackgroundBottomR, BackgroundBottomG, BackgroundBottomB);
    }
}
