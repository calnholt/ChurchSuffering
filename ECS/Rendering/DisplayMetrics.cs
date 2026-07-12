using System;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Rendering;

/// <summary>
/// Maps the fixed 1920x1080 logical canvas to the current physical render target
/// and letterboxed backbuffer destination.
/// </summary>
public sealed class DisplayMetrics
{
	public const int LogicalWidth = 1920;
	public const int LogicalHeight = 1080;
	public const float MaximumRenderScale = 2f;

	private DisplayMetrics(
		int backBufferWidth,
		int backBufferHeight,
		Rectangle renderDestination,
		int renderWidth,
		int renderHeight)
	{
		BackBufferWidth = backBufferWidth;
		BackBufferHeight = backBufferHeight;
		RenderDestination = renderDestination;
		RenderWidth = renderWidth;
		RenderHeight = renderHeight;
		RenderScaleX = renderWidth / (float)LogicalWidth;
		RenderScaleY = renderHeight / (float)LogicalHeight;
		LogicalToRenderMatrix = Matrix.CreateScale(RenderScaleX, RenderScaleY, 1f);
	}

	public int BackBufferWidth { get; }
	public int BackBufferHeight { get; }
	public Rectangle RenderDestination { get; }
	public int RenderWidth { get; }
	public int RenderHeight { get; }
	public float RenderScaleX { get; }
	public float RenderScaleY { get; }
	public Matrix LogicalToRenderMatrix { get; }
	public Matrix? SpriteBatchTransform =>
		RenderWidth == LogicalWidth && RenderHeight == LogicalHeight
			? null
			: LogicalToRenderMatrix;

	public static DisplayMetrics Calculate(
		int backBufferWidth,
		int backBufferHeight,
		float? renderScaleOverride = null)
	{
		int safeWidth = Math.Max(1, backBufferWidth);
		int safeHeight = Math.Max(1, backBufferHeight);
		float logicalAspect = LogicalWidth / (float)LogicalHeight;
		float backBufferAspect = safeWidth / (float)safeHeight;

		int destinationWidth;
		int destinationHeight;
		if (backBufferAspect > logicalAspect)
		{
			destinationHeight = safeHeight;
			destinationWidth = Math.Max(1, (int)MathF.Floor(destinationHeight * logicalAspect));
		}
		else
		{
			destinationWidth = safeWidth;
			destinationHeight = Math.Max(1, (int)MathF.Floor(destinationWidth / logicalAspect));
		}

		var destination = new Rectangle(
			(safeWidth - destinationWidth) / 2,
			(safeHeight - destinationHeight) / 2,
			destinationWidth,
			destinationHeight);

		int renderWidth;
		int renderHeight;
		if (renderScaleOverride.HasValue)
		{
			float renderScale = Math.Clamp(renderScaleOverride.Value, 0.05f, MaximumRenderScale);
			renderWidth = Math.Max(1, (int)MathF.Round(LogicalWidth * renderScale));
			renderHeight = Math.Max(1, (int)MathF.Round(LogicalHeight * renderScale));
		}
		else if (destinationWidth <= LogicalWidth * MaximumRenderScale &&
			destinationHeight <= LogicalHeight * MaximumRenderScale)
		{
			renderWidth = destinationWidth;
			renderHeight = destinationHeight;
		}
		else
		{
			renderWidth = (int)(LogicalWidth * MaximumRenderScale);
			renderHeight = (int)(LogicalHeight * MaximumRenderScale);
		}

		return new DisplayMetrics(
			safeWidth,
			safeHeight,
			destination,
			renderWidth,
			renderHeight);
	}

	public Rectangle LogicalToRender(Rectangle logicalRectangle)
	{
		int left = (int)MathF.Floor(logicalRectangle.Left * RenderScaleX);
		int top = (int)MathF.Floor(logicalRectangle.Top * RenderScaleY);
		int right = (int)MathF.Ceiling(logicalRectangle.Right * RenderScaleX);
		int bottom = (int)MathF.Ceiling(logicalRectangle.Bottom * RenderScaleY);
		return Rectangle.Intersect(
			new Rectangle(0, 0, RenderWidth, RenderHeight),
			new Rectangle(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top)));
	}

	public Vector2 ScreenToLogical(Point screenPosition)
	{
		float scaleX = RenderDestination.Width / (float)LogicalWidth;
		float scaleY = RenderDestination.Height / (float)LogicalHeight;
		return new Vector2(
			MathHelper.Clamp((screenPosition.X - RenderDestination.X) / Math.Max(0.001f, scaleX), 0f, LogicalWidth),
			MathHelper.Clamp((screenPosition.Y - RenderDestination.Y) / Math.Max(0.001f, scaleY), 0f, LogicalHeight));
	}
}
