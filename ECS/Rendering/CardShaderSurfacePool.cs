using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

internal static class CardShaderSurfacePool
{
	private const int SizeBucket = 64;
	private static readonly List<Entry> Entries = new();

	public static Lease Acquire(GraphicsDevice graphicsDevice, int width, int height)
	{
		if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
		width = RoundUp(Math.Max(1, width));
		height = RoundUp(Math.Max(1, height));

		for (int i = Entries.Count - 1; i >= 0; i--)
		{
			Entry entry = Entries[i];
			if (entry.InUse || !ReferenceEquals(entry.GraphicsDevice, graphicsDevice)) continue;
			if (!entry.Target.IsDisposed && entry.Width == width && entry.Height == height)
			{
				entry.InUse = true;
				return new Lease(entry);
			}
		}

		var created = new Entry(
			graphicsDevice,
			new RenderTarget2D(
				graphicsDevice,
				width,
				height,
				false,
				SurfaceFormat.Color,
				DepthFormat.None,
				0,
				RenderTargetUsage.DiscardContents),
			width,
			height)
		{
			InUse = true,
		};
		Entries.Add(created);
		return new Lease(created);
	}

	public static void DisposeAll(GraphicsDevice graphicsDevice)
	{
		for (int i = Entries.Count - 1; i >= 0; i--)
		{
			Entry entry = Entries[i];
			if (graphicsDevice != null && !ReferenceEquals(entry.GraphicsDevice, graphicsDevice)) continue;
			entry.Target.Dispose();
			Entries.RemoveAt(i);
		}
	}

	private static int RoundUp(int value) => ((value + SizeBucket - 1) / SizeBucket) * SizeBucket;

	internal sealed class Lease : IDisposable
	{
		private Entry _entry;

		internal Lease(Entry entry) => _entry = entry;

		public RenderTarget2D Target => _entry?.Target;

		public void Dispose()
		{
			if (_entry == null) return;
			_entry.InUse = false;
			_entry = null;
		}
	}

	internal sealed class Entry
	{
		public Entry(GraphicsDevice graphicsDevice, RenderTarget2D target, int width, int height)
		{
			GraphicsDevice = graphicsDevice;
			Target = target;
			Width = width;
			Height = height;
		}

		public GraphicsDevice GraphicsDevice { get; }
		public RenderTarget2D Target { get; }
		public int Width { get; }
		public int Height { get; }
		public bool InUse { get; set; }
	}
}
