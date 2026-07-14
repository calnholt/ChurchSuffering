using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

internal readonly record struct CardRenderRequest(
    Entity Card,
    Vector2 Position,
    float Scale,
    float Rotation);

internal interface ICardOverlayPass
{
    string Name { get; }
    bool AppliesTo(Entity card);
    void Update(GameTime gameTime);
    void Render(CardOverlayPassContext context);
    void Reset();
}

internal interface ICardOverlaySnapshotTimeControl
{
    void SetSnapshotTime(float timeSeconds);
}

internal static class CardOverlayPassCatalog
{
    public static ICardOverlayPass[] Create(EntityManager entityManager, ContentManager content)
    {
        return
        [
            new BrittleOverlayPass(entityManager, content),
            new FrozenOverlayPass(entityManager, content),
            new ThornedOverlayPass(entityManager, content),
            new ScorchedOverlayPass(entityManager, content),
            new CursedOverlayPass(entityManager, content),
            new PoisonCardOverlayPass(entityManager, content),
            new CardSheenOverlayPass(entityManager, content),
        ];
    }
}

internal sealed class CardRenderPipeline
{
    private readonly EntityManager _entityManager;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly SpriteBatch _spriteBatch;
    private readonly IReadOnlyList<ICardOverlayPass> _passes;

    public CardRenderPipeline(
        EntityManager entityManager,
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        IReadOnlyList<ICardOverlayPass> passes)
    {
        _entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        _passes = passes ?? throw new ArgumentNullException(nameof(passes));
    }

    internal IReadOnlyList<ICardOverlayPass> Passes => _passes;

    public void Update(GameTime gameTime)
    {
        foreach (ICardOverlayPass pass in _passes)
        {
            pass.Update(gameTime);
        }
    }

    public void Reset()
    {
        foreach (ICardOverlayPass pass in _passes)
        {
            pass.Reset();
        }
    }

    internal void SetSnapshotTime<TPass>(float timeSeconds)
        where TPass : ICardOverlayPass
    {
        foreach (ICardOverlayPass pass in _passes)
        {
            if (pass is TPass && pass is ICardOverlaySnapshotTimeControl timeControl)
            {
                timeControl.SetSnapshotTime(timeSeconds);
                return;
            }
        }
    }

    internal void SetAllSnapshotTimes(float timeSeconds)
    {
        foreach (ICardOverlayPass pass in _passes)
        {
            if (pass is ICardOverlaySnapshotTimeControl timeControl)
            {
                timeControl.SetSnapshotTime(timeSeconds);
            }
        }
    }

    public void Render(in CardRenderRequest request, Action drawBase)
    {
        if (drawBase == null) throw new ArgumentNullException(nameof(drawBase));
        if (!ShouldComposite(request.Card))
        {
            drawBase();
            return;
        }

        if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
                _graphicsDevice,
                out RenderTargetBinding[] sceneTargets,
                out _))
        {
            drawBase();
            return;
        }

        Rectangle bounds = CardRenderBoundsService.GetBounds(
            _entityManager,
            request.Card,
            request.Position,
            request.Scale,
            request.Rotation);
        Vector2 origin = new(bounds.X, bounds.Y);
        int physicalWidth = Math.Max(1, (int)MathF.Ceiling(bounds.Width * Game1.Display.RenderScaleX));
        int physicalHeight = Math.Max(1, (int)MathF.Ceiling(bounds.Height * Game1.Display.RenderScaleY));
        var sceneState = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
        CardShaderSurfacePool.Lease first = null;
        CardShaderSurfacePool.Lease second = null;

        try
        {
            _spriteBatch.End();
            first = CardShaderSurfacePool.Acquire(_graphicsDevice, physicalWidth, physicalHeight);
            second = CardShaderSurfacePool.Acquire(_graphicsDevice, physicalWidth, physicalHeight);
            _graphicsDevice.SetRenderTarget(first.Target);
            _graphicsDevice.Clear(Color.Transparent);

            Matrix localTransform = Matrix.CreateTranslation(-origin.X, -origin.Y, 0f) *
                (Game1.Display.SpriteBatchTransform ?? Matrix.Identity);
            _spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.AlphaBlend,
                SamplerState.AnisotropicClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                localTransform);
            drawBase();
            _spriteBatch.End();

            var context = new CardOverlayPassContext(
                _graphicsDevice,
                _spriteBatch,
                request.Card,
                request.Position,
                request.Scale,
                request.Rotation,
                origin,
                new Vector2(
                    first.Target.Width / Game1.Display.RenderScaleX,
                    first.Target.Height / Game1.Display.RenderScaleY),
                first.Target,
                second.Target);
            CardShaderPipelineDiagnostics.RecordCard(first.Target.Width * (long)first.Target.Height);

            foreach (ICardOverlayPass pass in _passes)
            {
                if (!pass.AppliesTo(request.Card)) continue;
                try
                {
                    pass.Render(context);
                }
                catch (Exception exception)
                {
                    try { _spriteBatch.End(); } catch { }
                    LoggingService.Append("CardRenderPipeline.RenderPass", new JsonObject
                    {
                        ["pass"] = pass.Name,
                        ["exception"] = exception.Message,
                    });
                }
            }

            SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, sceneTargets);
            SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(
                _graphicsDevice,
                _spriteBatch,
                sceneState);
            _spriteBatch.Draw(
                context.Result,
                origin,
                null,
                Color.White,
                0f,
                Vector2.Zero,
                new Vector2(1f / Game1.Display.RenderScaleX, 1f / Game1.Display.RenderScaleY),
                SpriteEffects.None,
                0f);
        }
        catch
        {
            try { _spriteBatch.End(); } catch { }
            SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, sceneTargets);
            SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(
                _graphicsDevice,
                _spriteBatch,
                sceneState);
            throw;
        }
        finally
        {
            first?.Dispose();
            second?.Dispose();
        }
    }

    internal bool ShouldComposite(Entity card)
    {
        return ShouldComposite(card, _passes, ShaderRuntimeOptions.ShadersEnabled);
    }

    internal static bool ShouldComposite(
        Entity card,
        IReadOnlyList<ICardOverlayPass> passes,
        bool shadersEnabled)
    {
        if (!shadersEnabled ||
            card == null ||
            card.GetComponent<SuppressCardVisualEffects>() != null)
        {
            return false;
        }

        foreach (ICardOverlayPass pass in passes)
        {
            if (pass.AppliesTo(card)) return true;
        }

        return false;
    }
}
