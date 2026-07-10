using System;
using System.Collections.Generic;
using System.IO;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Services
{
	public sealed class ImageAssetService : IDisposable
	{
		private readonly ContentManager _content;
		private readonly GraphicsDevice _graphicsDevice;
		private readonly HashSet<string> _missingTextureAssets = new(StringComparer.Ordinal);
		private readonly Dictionary<Color, Texture2D> _pixelCache = new();
		private readonly Dictionary<(int Width, int Height, int Radius), Texture2D> _roundedRectCache = new();
		private readonly Dictionary<(int Width, int Height, int Tl, int Tr, int Br, int Bl), Texture2D> _roundedRectPerCornerCache = new();
		private readonly Dictionary<Texture2D, Color[]> _pixelDataCache = new();
		private readonly Dictionary<(string CacheKey, int Width, int Height, float SoftenStrength), Texture2D> _scaledMipmappedCache = new();
		private readonly Dictionary<string, Texture2D> _rawTextureCache = new(StringComparer.Ordinal);
		private bool _disposed;

		public ImageAssetService(ContentManager content, GraphicsDevice graphicsDevice)
		{
			_content = content ?? throw new ArgumentNullException(nameof(content));
			_graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
		}

		public Texture2D GetRequiredTexture(string assetName)
		{
			if (string.IsNullOrWhiteSpace(assetName))
			{
				throw new ArgumentException("Texture asset name cannot be empty.", nameof(assetName));
			}

			try
			{
				return _content.Load<Texture2D>(assetName);
			}
			catch (Exception ex)
			{
				_missingTextureAssets.Add(assetName);
				throw new InvalidOperationException($"Failed to load required texture asset '{assetName}'.", ex);
			}
		}

		public Texture2D TryGetTexture(string assetName)
		{
			if (string.IsNullOrWhiteSpace(assetName)) return null;
			if (_missingTextureAssets.Contains(assetName)) return null;

			try
			{
				return _content.Load<Texture2D>(assetName);
			}
			catch
			{
				try
				{
					if (_rawTextureCache.TryGetValue(assetName, out var cachedRaw)) return cachedRaw;

					string rawPath = Path.Combine(_content.RootDirectory, assetName + ".png");
					if (!File.Exists(rawPath))
					{
						_missingTextureAssets.Add(assetName);
						return null;
					}

					using var stream = File.OpenRead(rawPath);
					var texture = Texture2D.FromStream(_graphicsDevice, stream);
					PremultiplyAlpha(texture);
					_rawTextureCache[assetName] = texture;
					return texture;
				}
				catch
				{
					_missingTextureAssets.Add(assetName);
					return null;
				}
			}
		}

		public Texture2D GetTextureOrFallback(string assetName, string fallbackAssetName)
		{
			return TryGetTexture(assetName) ?? TryGetTexture(fallbackAssetName);
		}

		public Texture2D GetPixel(Color color)
		{
			if (_pixelCache.TryGetValue(color, out var cached)) return cached;

			var texture = new Texture2D(_graphicsDevice, 1, 1, false, SurfaceFormat.Color);
			texture.SetData(new[] { color });
			_pixelCache[color] = texture;
			return texture;
		}

		public Texture2D GetRoundedRect(int width, int height, int radius)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			radius = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
			var key = (width, height, radius);
			if (_roundedRectCache.TryGetValue(key, out var cached)) return cached;

			var texture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
			_roundedRectCache[key] = texture;
			return texture;
		}

		public Texture2D GetRoundedRectPerCorner(int width, int height, int tl, int tr, int br, int bl)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			tl = ClampRadius(width, height, tl);
			tr = ClampRadius(width, height, tr);
			br = ClampRadius(width, height, br);
			bl = ClampRadius(width, height, bl);
			var key = (width, height, tl, tr, br, bl);
			if (_roundedRectPerCornerCache.TryGetValue(key, out var cached)) return cached;

			var texture = RoundedRectTextureFactory.CreateRoundedRectPerCorner(_graphicsDevice, width, height, tl, tr, br, bl);
			_roundedRectPerCornerCache[key] = texture;
			return texture;
		}

		public Texture2D GetAntiAliasedCircle(int radius)
		{
			return PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
		}

		public Texture2D GetScaledMipmappedTexture(string cacheKey, Texture2D source, int width, int height, float softenStrength = 0f)
		{
			if (source == null) return null;
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			var key = (cacheKey ?? string.Empty, width, height, softenStrength);
			if (_scaledMipmappedCache.TryGetValue(key, out var cached)) return cached;

			var sourceData = GetPixelData(source);
			var resampled = MipmappedTextureUtility.ResampleBilinear(sourceData, source.Width, source.Height, width, height);
			var softened = MipmappedTextureUtility.Soften(resampled, width, height, softenStrength);
			var texture = MipmappedTextureUtility.CreateMipmappedTexture(_graphicsDevice, softened, width, height);
			_scaledMipmappedCache[key] = texture;
			return texture;
		}

		public Color[] GetPixelData(Texture2D texture)
		{
			if (texture == null) return Array.Empty<Color>();
			if (_pixelDataCache.TryGetValue(texture, out var cached)) return cached;

			var data = new Color[texture.Width * texture.Height];
			texture.GetData(data);
			_pixelDataCache[texture] = data;
			return data;
		}

		public void ClearPixelDataCache()
		{
			_pixelDataCache.Clear();
		}

		public void ClearGeneratedTextures()
		{
			DisposeGeneratedTextures(_pixelCache.Values);
			DisposeGeneratedTextures(_roundedRectCache.Values);
			DisposeGeneratedTextures(_roundedRectPerCornerCache.Values);
			DisposeGeneratedTextures(_scaledMipmappedCache.Values);
			DisposeGeneratedTextures(_rawTextureCache.Values);
			_pixelCache.Clear();
			_roundedRectCache.Clear();
			_roundedRectPerCornerCache.Clear();
			_scaledMipmappedCache.Clear();
			_rawTextureCache.Clear();
		}

		public void ClearTransientCaches()
		{
			_missingTextureAssets.Clear();
			_pixelDataCache.Clear();
		}

		public void Dispose()
		{
			if (_disposed) return;
			ClearGeneratedTextures();
			_pixelDataCache.Clear();
			_disposed = true;
		}

		private static int ClampRadius(int width, int height, int radius)
		{
			return Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
		}

		private static void PremultiplyAlpha(Texture2D texture)
		{
			if (texture == null || texture.Format != SurfaceFormat.Color) return;
			var data = new Color[texture.Width * texture.Height];
			texture.GetData(data);
			for (int i = 0; i < data.Length; i++)
			{
				var c = data[i];
				float a = c.A / 255f;
				data[i] = new Color(
					(byte)Math.Round(c.R * a),
					(byte)Math.Round(c.G * a),
					(byte)Math.Round(c.B * a),
					c.A);
			}
			texture.SetData(data);
		}

		private static void DisposeGeneratedTextures(IEnumerable<Texture2D> textures)
		{
			foreach (var texture in textures)
			{
				try { texture?.Dispose(); } catch { }
			}
		}
	}
}
