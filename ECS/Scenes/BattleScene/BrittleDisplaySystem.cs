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
    private const int PassPriority = 100;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly ContentManager _content;

    private Effect _effect;
    private BrittleOverlay _overlay;
    private bool _failed;
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

        EventManager.Subscribe<CardShaderPassEvent>(OnShaderPass, PassPriority);
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
        _effect = null;
        _overlay = null;
        _failed = false;
    }

    private void OnShaderPass(CardShaderPassEvent evt)
    {
        CardShaderPassContext context = evt?.Context;
        if (context == null || !ShouldRender(context.Card)) return;
        if (!EnsureLoaded()) return;

        float safeScale = Math.Max(0.001f, context.Scale);
        Vector2 center = CardGeometryService.GetVisualGeometry(
            EntityManager,
            context.Card,
            context.Position,
            safeScale,
            context.Rotation).Center;

        _overlay.Resolution = context.LogicalSize;
        _overlay.Time = _timeSeconds;
        _overlay.CardCenter = context.ToSurface(center);
        _overlay.CardScale = safeScale;
        _overlay.CardRotation = context.Rotation;
        _overlay.ChunkSizePx = ChunkSizePx;
        _overlay.MaskThreshold = MaskThreshold;
        _overlay.FallFraction = FallFraction;
        _overlay.MaxFall = MaxFall;
        _overlay.MaxDrift = MaxDrift;
        _overlay.EdgeGlowAmount = EdgeGlowAmount;
        _overlay.HoleDarken = HoleDarken;

        context.Apply("Brittle", (spriteBatch, source) =>
        {
            _overlay.Begin(spriteBatch);
            _overlay.Draw(spriteBatch, source);
            _overlay.End(spriteBatch);
        });
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

}
