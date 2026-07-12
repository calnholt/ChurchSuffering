using System;
using System.Collections.Generic;
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

[DebugTab("Brittle Display")]
public class BrittleDisplaySystem : Core.System
{
    private const int PreRenderPriority = 100;
    private const int PostRenderPriority = -100;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;

    private Effect _effect;
    private BrittleOverlay _overlay;
    private FullScreenRenderTargetPool.Lease _beforeCardLease;
    private FullScreenRenderTargetPool.Lease _afterCardLease;
    private bool _failed;
    private bool _hasCapture;
    private Entity _capturedCard;
    private Vector2 _capturedCardCenter;
    private float _capturedCardScale = 1f;
    private float _capturedCardRotation;
    private float _timeSeconds;

    [DebugEditable(DisplayName = "Chunk Size Px", Step = 1f, Min = 4f, Max = 80f)]
    public float ChunkSizePx { get; set; } = 22f;

    [DebugEditable(DisplayName = "Mask Threshold", Step = 0.005f, Min = 0.001f, Max = 0.2f)]
    public float MaskThreshold { get; set; } = 0.02f;

    [DebugEditable(DisplayName = "Fall Fraction", Step = 0.01f, Min = 0f, Max = 1f)]
    public float FallFraction { get; set; } = 0.15f;

    [DebugEditable(DisplayName = "Max Fall", Step = 0.5f, Min = 0f, Max = 12f)]
    public float MaxFall { get; set; } = 12f;

    [DebugEditable(DisplayName = "Max Drift", Step = 0.1f, Min = 0f, Max = 2f)]
    public float MaxDrift { get; set; } = 1.2f;

    [DebugEditable(DisplayName = "Edge Glow Amount", Step = 0.05f, Min = 0f, Max = 2f)]
    public float EdgeGlowAmount { get; set; } = 0.6f;

    [DebugEditable(DisplayName = "Hole Darken", Step = 0.05f, Min = 0f, Max = 1.5f)]
    public float HoleDarken { get; set; } = 1f;

    public BrittleDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content)
        : base(entityManager)
    {
        _graphicsDevice = graphicsDevice;
        _spriteBatch = spriteBatch;
        _content = content;

        EventManager.Subscribe<CardBaseRenderStartedEvent>(OnCardBaseRenderStarted, PreRenderPriority);
        EventManager.Subscribe<CardBaseRenderCompletedEvent>(OnCardBaseRenderCompleted, PostRenderPriority);
        EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCachesEvent);
    }

    protected override IEnumerable<Entity> GetRelevantEntities()
    {
        return EntityManager.GetEntitiesWithComponent<Brittle>();
    }

    public override void Update(GameTime gameTime)
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return;

        _timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
        if (_overlay == null && HasAnyBrittleCards())
        {
            EnsureLoaded();
        }
    }

    protected override void UpdateEntity(Entity entity, GameTime gameTime)
    {
    }

    private void OnDeleteCachesEvent(DeleteCachesEvent evt)
    {
        _hasCapture = false;
        _capturedCard = null;
        ReleaseCaptureTargets();
    }

    private void OnCardBaseRenderStarted(CardBaseRenderStartedEvent evt)
    {
        BeginBrittleRender(
            evt.Card,
            evt.Position,
            evt.Scale,
            evt.Rotation);
    }

    private void OnCardBaseRenderCompleted(CardBaseRenderCompletedEvent evt)
    {
        EndBrittleRender(evt.Card);
    }

    private void BeginBrittleRender(Entity card, Vector2 position, float scale, float rotation)
    {
        _hasCapture = false;
        _capturedCard = null;
        ReleaseCaptureTargets();

        if (!ShouldRender(card)) return;
        if (!EnsureLoaded()) return;

        if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
                _graphicsDevice,
                out var currentTargets,
                out var currentTarget)) return;

        _beforeCardLease = FullScreenRenderTargetPool.Acquire(
            _graphicsDevice,
            currentTarget.Width,
            currentTarget.Height);
        _afterCardLease = FullScreenRenderTargetPool.Acquire(
            _graphicsDevice,
            currentTarget.Width,
            currentTarget.Height);

        var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
        _spriteBatch.End();

        SpriteBatchRenderTargetCompositor.Copy(_graphicsDevice, _spriteBatch, currentTarget, _beforeCardLease.Target);
        SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, currentTargets);
        SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);

        _hasCapture = true;
        _capturedCard = card;
        _capturedCardScale = Math.Max(0.001f, scale);
        _capturedCardRotation = rotation;
        _capturedCardCenter = CardGeometryService.GetVisualGeometry(
            EntityManager,
            card,
            position,
            _capturedCardScale,
            rotation).Center;
    }

    private void EndBrittleRender(Entity card)
    {
        if (!_hasCapture || _capturedCard != card)
        {
            return;
        }

        _hasCapture = false;
        _capturedCard = null;

        if (!ShouldRender(card) || _overlay == null || _beforeCardLease?.Target == null || _afterCardLease?.Target == null)
        {
            ReleaseCaptureTargets();
            return;
        }

        if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
                _graphicsDevice,
                out var currentTargets,
                out var currentTarget))
        {
            ReleaseCaptureTargets();
            return;
        }

        var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
        _spriteBatch.End();

        SpriteBatchRenderTargetCompositor.Copy(_graphicsDevice, _spriteBatch, currentTarget, _afterCardLease.Target);

        _overlay.Time = _timeSeconds;
        _overlay.BackgroundTexture = _beforeCardLease.Target;
        _overlay.CardCenter = _capturedCardCenter;
        _overlay.CardScale = _capturedCardScale;
        _overlay.CardRotation = _capturedCardRotation;
        _overlay.ChunkSizePx = ChunkSizePx;
        _overlay.MaskThreshold = MaskThreshold;
        _overlay.FallFraction = FallFraction;
        _overlay.MaxFall = MaxFall;
        _overlay.MaxDrift = MaxDrift;
        _overlay.EdgeGlowAmount = EdgeGlowAmount;
        _overlay.HoleDarken = HoleDarken;

        SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, currentTargets);
        _graphicsDevice.Clear(Color.Transparent);

        _overlay.Begin(_spriteBatch);
        _overlay.Draw(_spriteBatch, _afterCardLease.Target);
        _overlay.End(_spriteBatch);

        SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
        ReleaseCaptureTargets();
    }

    private bool ShouldRender(Entity card)
    {
        return ShaderRuntimeOptions.ShadersEnabled &&
            !_failed &&
            card != null &&
            card.GetComponent<Brittle>() != null &&
            card.GetComponent<SuppressCardVisualEffects>() == null;
    }

    private bool HasAnyBrittleCards()
    {
        foreach (var _ in EntityManager.GetEntitiesWithComponent<Brittle>())
        {
            return true;
        }

        return false;
    }

    private bool EnsureLoaded()
    {
        if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
        if (_effect == null)
        {
            try
            {
                _effect = _content.Load<Effect>("Shaders/Brittle");
            }
            catch (Exception e)
            {
                LoggingService.Append("BrittleDisplaySystem.EnsureLoaded", new System.Text.Json.Nodes.JsonObject
                {
                    ["error"] = "Failed to load shader",
                    ["exception"] = e.Message
                });
                _effect = null;
                _failed = true;
                return false;
            }
        }

        _overlay ??= new BrittleOverlay(_effect);
        return _overlay.IsAvailable;
    }

    private void ReleaseCaptureTargets()
    {
        _beforeCardLease?.Dispose();
        _afterCardLease?.Dispose();
        _beforeCardLease = null;
        _afterCardLease = null;
    }

}
