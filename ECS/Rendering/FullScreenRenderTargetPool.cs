using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

/// <summary>
/// Reuses temporary full-screen color targets across synchronous render passes.
/// Leases must be disposed only after the caller has restored the prior target.
/// </summary>
public static class FullScreenRenderTargetPool
{
	private static readonly List<Entry> Entries = new();

	public static Lease Acquire(
		GraphicsDevice graphicsDevice,
		int width,
		int height,
		RenderTargetUsage usage = RenderTargetUsage.DiscardContents)
	{
		if (graphicsDevice == null) throw new ArgumentNullException(nameof(graphicsDevice));
		width = Math.Max(1, width);
		height = Math.Max(1, height);

		for (int i = Entries.Count - 1; i >= 0; i--)
		{
			Entry stale = Entries[i];
			if (stale.InUse || !ReferenceEquals(stale.GraphicsDevice, graphicsDevice)) continue;
			if (!stale.Target.IsDisposed && stale.Width == width && stale.Height == height && stale.Usage == usage)
			{
				stale.InUse = true;
				return new Lease(stale);
			}

			stale.Target.Dispose();
			Entries.RemoveAt(i);
		}

		var entry = new Entry(
			graphicsDevice,
			new RenderTarget2D(
				graphicsDevice,
				width,
				height,
				false,
				SurfaceFormat.Color,
				DepthFormat.None,
				0,
				usage),
			width,
			height,
			usage)
		{
			InUse = true,
		};
		Entries.Add(entry);
		return new Lease(entry);
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

	public sealed class Lease : IDisposable
	{
		private Entry _entry;

		internal Lease(Entry entry)
		{
			_entry = entry;
		}

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
		public Entry(
			GraphicsDevice graphicsDevice,
			RenderTarget2D target,
			int width,
			int height,
			RenderTargetUsage usage)
		{
			GraphicsDevice = graphicsDevice;
			Target = target;
			Width = width;
			Height = height;
			Usage = usage;
		}

		public GraphicsDevice GraphicsDevice { get; }
		public RenderTarget2D Target { get; }
		public int Width { get; }
		public int Height { get; }
		public RenderTargetUsage Usage { get; }
		public bool InUse { get; set; }
	}
}
