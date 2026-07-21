using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Components;

namespace ChurchSuffering.ECS.Services
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
		private readonly Dictionary<Guid, SceneTextureScope> _sceneScopes = new();
		private Guid _activeSceneScopeId;
		private Guid _preparedSceneScopeId;
		private bool _disposed;
		private const long DefaultSceneTextureBudgetBytes = 256L * 1024L * 1024L;
		private const int MaxRetainedSceneTextureScopes = 4;

		private sealed class SceneTextureScope : IDisposable
		{
			public Guid Id { get; init; }
			public SceneId Scene { get; init; }
			public ContentManager Content { get; init; }
			public Dictionary<string, Texture2D> Textures { get; } = new(StringComparer.Ordinal);
			public long EstimatedBytes { get; set; }
			public long LastUseSequence { get; set; }
			public void Dispose() => Content.Dispose();
		}

		private long _sceneScopeUseSequence;
		public long SceneTextureBudgetBytes { get; set; } = DefaultSceneTextureBudgetBytes;

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

			if (TryGetScopedTexture(assetName, out var scoped)) return scoped;

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
			if (TryGetScopedTexture(assetName, out var scoped)) return scoped;
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

		public void BeginSceneTexturePreparation(Guid preparationId, SceneId scene)
		{
			if (preparationId == Guid.Empty) throw new ArgumentException("Preparation ID is required.", nameof(preparationId));
			if (_preparedSceneScopeId != Guid.Empty && _preparedSceneScopeId != preparationId)
			{
				_preparedSceneScopeId = Guid.Empty;
			}

			if (!_sceneScopes.ContainsKey(preparationId))
			{
				_sceneScopes[preparationId] = new SceneTextureScope
				{
					Id = preparationId,
					Scene = scene,
					Content = new ContentManager(_content.ServiceProvider, _content.RootDirectory),
					LastUseSequence = ++_sceneScopeUseSequence,
				};
			}
			_preparedSceneScopeId = preparationId;
			EvictUnpinnedSceneScopes();
		}

		public Texture2D PrepareSceneTexture(Guid preparationId, string assetName)
		{
			if (!_sceneScopes.TryGetValue(preparationId, out var scope))
			{
				throw new InvalidOperationException($"Scene texture scope '{preparationId}' was not created.");
			}
			if (scope.Textures.TryGetValue(assetName, out var cached)) return cached;

			var texture = scope.Content.Load<Texture2D>(assetName);
			scope.Textures[assetName] = texture;
			scope.EstimatedBytes += EstimateTextureBytes(texture);
			scope.LastUseSequence = ++_sceneScopeUseSequence;
			EvictUnpinnedSceneScopes();
			return texture;
		}

		public void ActivateSceneTextureScope(Guid preparationId)
		{
			if (!_sceneScopes.ContainsKey(preparationId)) return;
			_activeSceneScopeId = preparationId;
			if (_preparedSceneScopeId == preparationId) _preparedSceneScopeId = Guid.Empty;
			_sceneScopes[preparationId].LastUseSequence = ++_sceneScopeUseSequence;
			EvictUnpinnedSceneScopes();
		}

		public void Dispose()
		{
			if (_disposed) return;
			foreach (var scope in _sceneScopes.Values) scope.Dispose();
			_sceneScopes.Clear();
			ClearGeneratedTextures();
			_pixelDataCache.Clear();
			_disposed = true;
		}

		private bool TryGetScopedTexture(string assetName, out Texture2D texture)
		{
			texture = null;
			if (string.IsNullOrWhiteSpace(assetName)) return false;
			if (_preparedSceneScopeId != Guid.Empty
				&& _sceneScopes.TryGetValue(_preparedSceneScopeId, out var prepared)
				&& prepared.Textures.TryGetValue(assetName, out texture))
			{
				prepared.LastUseSequence = ++_sceneScopeUseSequence;
				return true;
			}
			if (_activeSceneScopeId != Guid.Empty
				&& _sceneScopes.TryGetValue(_activeSceneScopeId, out var active)
				&& active.Textures.TryGetValue(assetName, out texture))
			{
				active.LastUseSequence = ++_sceneScopeUseSequence;
				return true;
			}
			return false;
		}

		private void EvictUnpinnedSceneScopes()
		{
			long totalBytes = 0;
			foreach (var scope in _sceneScopes.Values) totalBytes += scope.EstimatedBytes;
			if (totalBytes <= SceneTextureBudgetBytes && _sceneScopes.Count <= MaxRetainedSceneTextureScopes) return;

			var candidates = _sceneScopes.Values
				.Where(scope => scope.Id != _activeSceneScopeId && scope.Id != _preparedSceneScopeId)
				.OrderBy(scope => scope.LastUseSequence)
				.ToList();
			foreach (var scope in candidates)
			{
				if (totalBytes <= SceneTextureBudgetBytes && _sceneScopes.Count <= MaxRetainedSceneTextureScopes) break;
				totalBytes -= scope.EstimatedBytes;
				_sceneScopes.Remove(scope.Id);
				scope.Dispose();
			}
		}

		private static long EstimateTextureBytes(Texture2D texture)
		{
			if (texture == null) return 0;
			return (long)texture.Width * texture.Height * 4L;
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
