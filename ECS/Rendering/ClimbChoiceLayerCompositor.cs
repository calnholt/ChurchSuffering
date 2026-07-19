using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class ClimbChoiceLayerCompositor
{
	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _batch;
	private readonly GaussianBlurOverlay _blur;
	private readonly ClimbChoiceLayerFilter _filter;

	public ClimbChoiceLayerCompositor(GraphicsDevice graphicsDevice, SpriteBatch batch, Effect blur, Effect filter)
	{
		_graphicsDevice = graphicsDevice;
		_batch = batch;
		_blur = new GaussianBlurOverlay(blur);
		_filter = new ClimbChoiceLayerFilter(filter);
	}

	public bool IsAvailable => _blur.IsAvailable && _filter.IsAvailable;

	public void DrawLayer(Action draw, float blur, float grayscale, float sepia, float brightness)
	{
		if (!IsAvailable || draw == null) { draw?.Invoke(); return; }
		RenderTargetBinding[] targets = _graphicsDevice.GetRenderTargets();
		BlendState blend = _graphicsDevice.BlendState;
		SamplerState sampler = _graphicsDevice.SamplerStates[0];
		DepthStencilState depth = _graphicsDevice.DepthStencilState;
		RasterizerState rasterizer = _graphicsDevice.RasterizerState;
		Rectangle scissor = _graphicsDevice.ScissorRectangle;
		int width = Game1.Display.RenderWidth;
		int height = Game1.Display.RenderHeight;

		_batch.End();
		using var sourceLease = FullScreenRenderTargetPool.Acquire(_graphicsDevice, width, height, RenderTargetUsage.PreserveContents);
		FullScreenRenderTargetPool.Lease tempLease = null;
		try
		{
			RenderTarget2D source = sourceLease.Target;
			_graphicsDevice.SetRenderTarget(source);
			_graphicsDevice.Clear(Color.Transparent);
			_batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, RasterizerState.CullNone, null, Game1.Display.SpriteBatchTransform);
			draw();
			_batch.End();

			if (blur > 0.05f)
			{
				tempLease = FullScreenRenderTargetPool.Acquire(_graphicsDevice, width, height);
				_graphicsDevice.SetRenderTarget(tempLease.Target);
				_graphicsDevice.Clear(Color.Transparent);
				_blur.BlurDirection = Vector2.UnitX;
				_blur.BlurRadius = blur;
				_blur.Begin(_batch); _blur.Draw(_batch, source); _blur.End(_batch);
				_graphicsDevice.SetRenderTarget(source);
				_graphicsDevice.Clear(Color.Transparent);
				_blur.BlurDirection = Vector2.UnitY;
				_blur.Begin(_batch); _blur.Draw(_batch, tempLease.Target); _blur.End(_batch);
			}

			RestoreTargets(targets);
			_filter.Grayscale = MathHelper.Clamp(grayscale, 0f, 1f);
			_filter.Sepia = MathHelper.Clamp(sepia, 0f, 1f);
			_filter.Brightness = Math.Max(0f, brightness);
			_filter.Begin(_batch); _filter.Draw(_batch, source); _filter.End(_batch);
		}
		finally
		{
			tempLease?.Dispose();
			RestoreTargets(targets);
			_graphicsDevice.Textures[0] = null;
			_graphicsDevice.SamplerStates[0] = sampler;
			_graphicsDevice.ScissorRectangle = scissor;
			_batch.Begin(SpriteSortMode.Immediate, blend, sampler, depth, rasterizer, null, Game1.Display.SpriteBatchTransform);
		}
	}

	private void RestoreTargets(RenderTargetBinding[] targets)
	{
		if (targets is { Length: > 0 }) _graphicsDevice.SetRenderTargets(targets);
		else _graphicsDevice.SetRenderTarget(null);
	}
}
