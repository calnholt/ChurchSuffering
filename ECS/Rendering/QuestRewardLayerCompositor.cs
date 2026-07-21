using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public sealed class QuestRewardLayerCompositor
{
	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;
	private readonly GaussianBlurOverlay _blur;
	private readonly QuestRewardLayerFilter _filter;

	public QuestRewardLayerCompositor(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Effect blurEffect, Effect filterEffect)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		_blur = new GaussianBlurOverlay(blurEffect);
		_filter = new QuestRewardLayerFilter(filterEffect);
	}

	public bool IsAvailable => _blur.IsAvailable && _filter.IsAvailable;

	public void DrawLayer(Action drawLayer, float blurRadius, float grayscale, float brightness, float opacity)
	{
		if (!IsAvailable || drawLayer == null || opacity <= 0.001f) return;

		RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
		SamplerState savedSampler = _graphicsDevice.SamplerStates[0];
		DepthStencilState savedDepth = _graphicsDevice.DepthStencilState;
		RasterizerState savedRasterizer = _graphicsDevice.RasterizerState;
		Rectangle savedScissor = _graphicsDevice.ScissorRectangle;
		int width = Game1.Display.RenderWidth;
		int height = Game1.Display.RenderHeight;

		_spriteBatch.End();
		// Drawing a reward layer can invoke the card shader pipeline, which temporarily
		// switches to card-sized render targets. Preserve this partially drawn layer
		// when those nested passes restore it.
		using var sourceLease = FullScreenRenderTargetPool.Acquire(
			_graphicsDevice,
			width,
			height,
			RenderTargetUsage.PreserveContents);
		FullScreenRenderTargetPool.Lease tempLease = null;
		try
		{
			RenderTarget2D source = sourceLease.Target;
			_graphicsDevice.SetRenderTarget(source);
			_graphicsDevice.Clear(Color.Transparent);
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Game1.Display.SpriteBatchTransform);
			drawLayer();
			_spriteBatch.End();

			if (blurRadius > 0.05f)
			{
				tempLease = FullScreenRenderTargetPool.Acquire(_graphicsDevice, width, height);
				RenderTarget2D temp = tempLease.Target;
				_graphicsDevice.SetRenderTarget(temp);
				_graphicsDevice.Clear(Color.Transparent);
				_blur.BlurDirection = Vector2.UnitX;
				_blur.BlurRadius = blurRadius;
				_blur.Begin(_spriteBatch);
				_blur.Draw(_spriteBatch, source);
				_blur.End(_spriteBatch);

				_graphicsDevice.SetRenderTarget(source);
				_graphicsDevice.Clear(Color.Transparent);
				_blur.BlurDirection = Vector2.UnitY;
				_blur.Begin(_spriteBatch);
				_blur.Draw(_spriteBatch, temp);
				_blur.End(_spriteBatch);
			}

			RestoreTargets(previousTargets);
			_filter.GrayscaleAmount = MathHelper.Clamp(grayscale, 0f, 1f);
			_filter.Brightness = Math.Max(0f, brightness);
			_filter.Opacity = MathHelper.Clamp(opacity, 0f, 1f);
			_filter.Begin(_spriteBatch);
			_filter.Draw(_spriteBatch, source);
			_filter.End(_spriteBatch);
		}
		finally
		{
			tempLease?.Dispose();
			RestoreTargets(previousTargets);
			_graphicsDevice.Textures[0] = null;
			_graphicsDevice.SamplerStates[0] = savedSampler;
			_graphicsDevice.ScissorRectangle = savedScissor;
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, savedSampler, savedDepth, savedRasterizer, null, Game1.Display.SpriteBatchTransform);
		}
	}

	private void RestoreTargets(RenderTargetBinding[] targets)
	{
		if (targets != null && targets.Length > 0) _graphicsDevice.SetRenderTargets(targets);
		else _graphicsDevice.SetRenderTarget(null);
	}
}
