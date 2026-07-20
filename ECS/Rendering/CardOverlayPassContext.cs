using System;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

internal sealed class CardOverlayPassContext
{
	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;
	private RenderTarget2D _source;
	private RenderTarget2D _destination;

	public CardOverlayPassContext(
		GraphicsDevice graphicsDevice,
		SpriteBatch spriteBatch,
		Entity card,
		Vector2 position,
		float scale,
		float rotation,
		Vector2 surfaceOrigin,
		Vector2 logicalSize,
		RenderTarget2D source,
		RenderTarget2D destination)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		Card = card;
		Position = position;
		Scale = scale;
		Rotation = rotation;
		SurfaceOrigin = surfaceOrigin;
		LogicalSize = logicalSize;
		_source = source;
		_destination = destination;
	}

	public Entity Card { get; }
	public Vector2 Position { get; }
	public float Scale { get; }
	public float Rotation { get; }
	public Vector2 SurfaceOrigin { get; }
	public Vector2 LogicalSize { get; }
	public Texture2D Result => _source;

	public Vector2 ToSurface(Vector2 logicalPosition) => logicalPosition - SurfaceOrigin;

	public void Apply(string passName, Action<SpriteBatch, Texture2D> drawPass)
	{
		if (drawPass == null || _source == null || _destination == null) return;

		// A target commonly becomes the next pass's destination after being sampled by the
		// previous pass. Explicitly unbind slot zero before rebinding it for rendering;
		// DesktopGL otherwise exhibits intermittent read/write feedback across long chains.
		_graphicsDevice.Textures[0] = null;
		_graphicsDevice.SetRenderTarget(_destination);
		_graphicsDevice.Clear(Color.Transparent);
		try
		{
			drawPass(_spriteBatch, _source);
			(_source, _destination) = (_destination, _source);
			CardShaderPipelineDiagnostics.RecordPass(_source.Width * (long)_source.Height);
		}
		catch (Exception exception)
		{
			try { _spriteBatch.End(); } catch { }
			LoggingService.Append("CardOverlayPassContext.Apply", new JsonObject
			{
				["pass"] = passName ?? string.Empty,
				["exception"] = exception.Message,
			});
		}
	}
}

internal static class CardShaderPipelineDiagnostics
{
	public static long CardsProcessed { get; private set; }
	public static long PassesApplied { get; private set; }
	public static long SceneInterruptions { get; private set; }
	public static long SurfacePixelsProcessed { get; private set; }

	public static void RecordCard(long surfacePixels)
	{
		CardsProcessed++;
		SceneInterruptions++;
		SurfacePixelsProcessed += Math.Max(0, surfacePixels);
	}

	public static void RecordPass(long surfacePixels)
	{
		PassesApplied++;
		SurfacePixelsProcessed += Math.Max(0, surfacePixels);
	}

	public static void Reset()
	{
		CardsProcessed = 0;
		PassesApplied = 0;
		SceneInterruptions = 0;
		SurfacePixelsProcessed = 0;
	}
}
