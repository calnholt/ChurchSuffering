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
	private readonly BasicEffect _dualColorEffect;

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
		_dualColorEffect = new BasicEffect(graphicsDevice)
		{
			TextureEnabled = true,
			VertexColorEnabled = true,
		};
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

	public void Render(in CardRenderRequest request, Action drawBase, Action drawSecondaryColor)
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
		CardShaderSurfacePool.Lease dualComposite = null;

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

			RenderTarget2D source = first.Target;
			RenderTarget2D destination = second.Target;
			bool hasDualColor = request.Card.GetComponent<DualColor>() != null
				&& !request.Card.HasComponent<Colorless>()
				&& drawSecondaryColor != null;
			if (hasDualColor)
			{
				_graphicsDevice.SetRenderTarget(second.Target);
				_graphicsDevice.Clear(Color.Transparent);
				_spriteBatch.Begin(
					SpriteSortMode.Immediate,
					BlendState.AlphaBlend,
					SamplerState.AnisotropicClamp,
					DepthStencilState.None,
					RasterizerState.CullNone,
					null,
					localTransform);
				drawSecondaryColor();
				_spriteBatch.End();

				dualComposite = CardShaderSurfacePool.Acquire(_graphicsDevice, physicalWidth, physicalHeight);
				CompositeDualColor(
					request,
					origin,
					first.Target,
					second.Target,
					dualComposite.Target);
				source = dualComposite.Target;
				destination = first.Target;
			}

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
				source,
				destination);
            CardShaderPipelineDiagnostics.RecordCard(first.Target.Width * (long)first.Target.Height);

			bool renderOverlayPasses = ShaderRuntimeOptions.ShadersEnabled
				&& request.Card.GetComponent<SuppressCardVisualEffects>() == null;
			foreach (ICardOverlayPass pass in _passes)
            {
				if (!renderOverlayPasses) break;
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
			dualComposite?.Dispose();
        }
    }

	private void CompositeDualColor(
		in CardRenderRequest request,
		Vector2 surfaceOrigin,
		Texture2D primary,
		Texture2D secondary,
		RenderTarget2D destination)
	{
		_graphicsDevice.Textures[0] = null;
		_graphicsDevice.SetRenderTarget(destination);
		_graphicsDevice.Clear(Color.Transparent);
		_spriteBatch.Begin(
			SpriteSortMode.Immediate,
			BlendState.Opaque,
			SamplerState.PointClamp,
			DepthStencilState.None,
			RasterizerState.CullNone);
		_spriteBatch.Draw(primary, _graphicsDevice.Viewport.Bounds, Color.White);
		_spriteBatch.End();

		CardVisualGeometry geometry = CardGeometryService.GetVisualGeometry(
			_entityManager,
			request.Card,
			request.Position,
			request.Scale,
			request.Rotation);
		float halfWidth = geometry.Bounds.Width * 0.5f;
		float halfHeight = geometry.Bounds.Height * 0.5f;
		float cosine = MathF.Cos(request.Rotation);
		float sine = MathF.Sin(request.Rotation);
		Vector2 ToPhysical(Vector2 local)
		{
			var rotated = new Vector2(
				local.X * cosine - local.Y * sine,
				local.X * sine + local.Y * cosine);
			var logical = geometry.Center + rotated - surfaceOrigin;
			return new Vector2(
				logical.X * Game1.Display.RenderScaleX,
				logical.Y * Game1.Display.RenderScaleY);
		}

		Vector2 topLeft = ToPhysical(new Vector2(-halfWidth, -halfHeight));
		Vector2 topRight = ToPhysical(new Vector2(halfWidth, -halfHeight));
		Vector2 bottomRight = ToPhysical(new Vector2(halfWidth, halfHeight));
		var vertices = new[]
		{
			Vertex(topLeft, secondary),
			Vertex(topRight, secondary),
			Vertex(bottomRight, secondary),
		};

		_dualColorEffect.Texture = secondary;
		_dualColorEffect.World = Matrix.Identity;
		_dualColorEffect.View = Matrix.Identity;
		_dualColorEffect.Projection = Matrix.CreateOrthographicOffCenter(
			0f,
			destination.Width,
			destination.Height,
			0f,
			0f,
			1f);
		_graphicsDevice.BlendState = BlendState.AlphaBlend;
		_graphicsDevice.DepthStencilState = DepthStencilState.None;
		_graphicsDevice.RasterizerState = RasterizerState.CullNone;
		_graphicsDevice.SamplerStates[0] = SamplerState.PointClamp;
		foreach (EffectPass pass in _dualColorEffect.CurrentTechnique.Passes)
		{
			pass.Apply();
			_graphicsDevice.DrawUserPrimitives(
				PrimitiveType.TriangleList,
				vertices,
				0,
				1);
		}
		_graphicsDevice.Textures[0] = null;

		VertexPositionColorTexture Vertex(Vector2 position, Texture2D texture) => new(
			new Vector3(position, 0f),
			Color.White,
			new Vector2(position.X / texture.Width, position.Y / texture.Height));
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
		if (card == null)
        {
            return false;
        }
		if (card.GetComponent<DualColor>() != null && !card.HasComponent<Colorless>()) return true;
		if (!shadersEnabled || card.GetComponent<SuppressCardVisualEffects>() != null) return false;

        foreach (ICardOverlayPass pass in passes)
        {
            if (pass.AppliesTo(card)) return true;
        }

        return false;
    }
}
