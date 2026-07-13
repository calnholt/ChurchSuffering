using System;
using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems;

public sealed class CardShaderCompositorSystem : Core.System
{
	private const int LifecyclePriority = 1000;
	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;

	private ActiveRender _active;

	public CardShaderCompositorSystem(
		EntityManager entityManager,
		GraphicsDevice graphicsDevice,
		SpriteBatch spriteBatch)
		: base(entityManager)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		EventManager.Subscribe<CardBaseRenderStartedEvent>(OnStarted, LifecyclePriority);
		EventManager.Subscribe<CardBaseRenderCompletedEvent>(OnCompleted, LifecyclePriority);
		EventManager.Subscribe<DeleteCachesEvent>(_ => AbortActiveRender());
	}

	protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

	private void OnStarted(CardBaseRenderStartedEvent evt)
	{
		if (!ShouldProcess(evt?.Card)) return;
		AbortActiveRender();

		if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
				_graphicsDevice,
				out RenderTargetBinding[] sceneTargets,
				out _)) return;

		CardSurfaceBounds bounds = CalculateSurfaceBounds(
			EntityManager,
			evt.Card,
			evt.Position,
			evt.Scale,
			evt.Rotation);
		int physicalWidth = Math.Max(1, (int)MathF.Ceiling(bounds.Size.X * Game1.Display.RenderScaleX));
		int physicalHeight = Math.Max(1, (int)MathF.Ceiling(bounds.Size.Y * Game1.Display.RenderScaleY));

		var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
		CardShaderSurfacePool.Lease first = null;
		CardShaderSurfacePool.Lease second = null;
		try
		{
			_spriteBatch.End();
			first = CardShaderSurfacePool.Acquire(_graphicsDevice, physicalWidth, physicalHeight);
			second = CardShaderSurfacePool.Acquire(_graphicsDevice, physicalWidth, physicalHeight);
			_graphicsDevice.SetRenderTarget(first.Target);
			_graphicsDevice.Clear(Color.Transparent);

			Matrix localTransform = Matrix.CreateTranslation(-bounds.Origin.X, -bounds.Origin.Y, 0f) *
				(Game1.Display.SpriteBatchTransform ?? Matrix.Identity);
			_spriteBatch.Begin(
				SpriteSortMode.Immediate,
				BlendState.AlphaBlend,
				SamplerState.AnisotropicClamp,
				DepthStencilState.None,
				RasterizerState.CullNone,
				null,
				localTransform);

			_active = new ActiveRender(
				evt.Card,
				evt.Position,
				evt.Scale,
				evt.Rotation,
				bounds.Origin,
				new Vector2(
					first.Target.Width / Game1.Display.RenderScaleX,
					first.Target.Height / Game1.Display.RenderScaleY),
				sceneTargets,
				state,
				first,
				second);
			CardShaderPipelineDiagnostics.RecordCard(first.Target.Width * (long)first.Target.Height);
		}
		catch
		{
			try { _spriteBatch.End(); } catch { }
			first?.Dispose();
			second?.Dispose();
			SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, sceneTargets);
			SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
			throw;
		}
	}

	private void OnCompleted(CardBaseRenderCompletedEvent evt)
	{
		if (_active == null || !ReferenceEquals(_active.Card, evt?.Card)) return;
		ActiveRender active = _active;
		_active = null;

		try
		{
			_spriteBatch.End();
			var context = new CardShaderPassContext(
				_graphicsDevice,
				_spriteBatch,
				active.Card,
				active.Position,
				active.Scale,
				active.Rotation,
				active.Origin,
				active.LogicalSize,
				active.First.Target,
				active.Second.Target);
			EventManager.Publish(new CardShaderPassEvent { Context = context });

			SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, active.SceneTargets);
			SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, active.SceneState);
			_spriteBatch.Draw(
				context.Result,
				active.Origin,
				null,
				Color.White,
				0f,
				Vector2.Zero,
				new Vector2(1f / Game1.Display.RenderScaleX, 1f / Game1.Display.RenderScaleY),
				SpriteEffects.None,
				0f);
		}
		finally
		{
			active.First.Dispose();
			active.Second.Dispose();
		}
	}

	private void AbortActiveRender()
	{
		if (_active == null) return;
		ActiveRender active = _active;
		_active = null;
		try { _spriteBatch.End(); } catch { }
		SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, active.SceneTargets);
		SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, active.SceneState);
		active.First.Dispose();
		active.Second.Dispose();
	}

	private static bool ShouldProcess(Entity card)
	{
		return ShaderRuntimeOptions.ShadersEnabled &&
			card != null &&
			card.GetComponent<SuppressCardVisualEffects>() == null &&
			(card.GetComponent<Brittle>() != null ||
			 card.GetComponent<Frozen>() != null ||
			 card.GetComponent<Thorned>() != null ||
			 card.GetComponent<Scorched>() != null ||
			 card.GetComponent<Cursed>() != null);
	}

	internal static CardSurfaceBounds CalculateSurfaceBounds(
		EntityManager entityManager,
		Entity card,
		Vector2 position,
		float scale,
		float rotation)
	{
		Rectangle bounds = CardRenderBoundsService.GetBounds(
			entityManager,
			card,
			position,
			scale,
			rotation);
		var origin = new Vector2(bounds.X, bounds.Y);
		return new CardSurfaceBounds(
			origin,
			new Vector2(bounds.Width, bounds.Height));
	}

	internal readonly record struct CardSurfaceBounds(Vector2 Origin, Vector2 Size);

	private sealed record ActiveRender(
		Entity Card,
		Vector2 Position,
		float Scale,
		float Rotation,
		Vector2 Origin,
		Vector2 LogicalSize,
		RenderTargetBinding[] SceneTargets,
		SpriteBatchRenderTargetCompositor.SpriteBatchState SceneState,
		CardShaderSurfacePool.Lease First,
		CardShaderSurfacePool.Lease Second);
}
