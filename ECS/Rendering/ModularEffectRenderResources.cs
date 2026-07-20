using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering
{
	/// <summary>
	/// Process-lifetime, preloaded procedural masks shared by the modular battle FX renderers.
	/// Animated sizes are produced by scaling these fixed textures, never by creating textures in Draw().
	/// </summary>
	public sealed class ModularEffectRenderResources
	{
		private const int RadialMaskDiameter = 256;

		private static readonly Vector2[] SwordArcMaskPoints =
		{
			new(0.00f, 0.48f),
			new(0.10f, 0.30f),
			new(0.48f, 0.40f),
			new(0.94f, 0.00f),
			new(1.00f, 0.14f),
			new(0.58f, 0.62f),
			new(0.12f, 0.72f)
		};

		private static readonly Vector2[] JaggedShardMaskPoints =
		{
			new(0.50f, 0.00f),
			new(1.00f, 0.76f),
			new(0.62f, 1.00f),
			new(0.00f, 0.42f)
		};

		private static readonly Vector2[] HammerHeadMaskPoints =
		{
			new(0.24f, 0.00f),
			new(0.76f, 0.00f),
			new(0.94f, 0.08f),
			new(1.00f, 0.24f),
			new(0.78f, 0.34f),
			new(0.68f, 0.40f),
			new(0.68f, 0.60f),
			new(0.78f, 0.66f),
			new(1.00f, 0.76f),
			new(0.94f, 0.92f),
			new(0.76f, 1.00f),
			new(0.24f, 1.00f),
			new(0.06f, 0.92f),
			new(0.00f, 0.76f),
			new(0.22f, 0.66f),
			new(0.32f, 0.60f),
			new(0.32f, 0.40f),
			new(0.22f, 0.34f),
			new(0.00f, 0.24f),
			new(0.06f, 0.08f)
		};

		private static readonly Vector2[] ShieldMaskPoints =
		{
			new(0.50f, 0.00f),
			new(0.95f, 0.16f),
			new(0.86f, 0.67f),
			new(0.50f, 1.00f),
			new(0.14f, 0.67f),
			new(0.05f, 0.16f)
		};

		private static readonly Vector2[] ToothTopMaskPoints =
		{
			new(0.10f, 0.00f),
			new(0.90f, 0.00f),
			new(0.50f, 1.00f)
		};

		private static readonly Vector2[] ToothBottomMaskPoints =
		{
			new(0.50f, 0.00f),
			new(0.90f, 1.00f),
			new(0.10f, 1.00f)
		};

		private static readonly Vector2[] JaggedParticleMaskPoints =
		{
			new(0.50f, 0.00f),
			new(1.00f, 0.76f),
			new(0.62f, 1.00f),
			new(0.00f, 0.42f)
		};

		private static readonly (float InnerStop, float OuterStop)[] RadialProfiles =
		{
			(0.00f, 0.86f),
			(0.00f, 0.64f),
			(0.00f, 0.42f),
			(0.00f, 0.78f),
			(0.00f, 0.68f),
			(0.16f, 1.00f),
			(0.00f, 0.82f),
			(0.00f, 0.62f),
			(0.00f, 0.72f),
			(0.00f, 0.70f),
			(0.00f, 0.52f),
			(0.00f, 0.34f),
			(0.08f, 1.00f),
			(0.00f, 0.56f),
			(0.22f, 0.82f)
		};

		private readonly GraphicsDevice _graphicsDevice;
		private readonly Dictionary<(float InnerStop, float OuterStop), Texture2D> _radialMasks = new();

		public Texture2D Pixel { get; }
		public Texture2D SwordArcMask { get; }
		public Texture2D BiteRingMask { get; }
		public Texture2D RockChunkMask { get; }
		public Texture2D DefaultRingMask { get; }
		public Texture2D HaloRingMask { get; }
		public Texture2D ThrownBladeMask { get; }
		public Texture2D ShieldMask { get; }
		public Texture2D ShieldFragmentMask { get; }
		public Texture2D SealRingMask { get; }
		public Texture2D ColorDrainRingMask { get; }
		public Texture2D ToothTopMask { get; }
		public Texture2D ToothBottomMask { get; }
		public Texture2D HammerHeadMask { get; }
		public Texture2D JaggedParticleMask { get; }
		public Texture2D ShockwaveRingMask { get; }

		public int PreloadedRadialMaskCount => _radialMasks.Count;
		public long EstimatedRadialMaskBytes => (long)_radialMasks.Count * RadialMaskDiameter * RadialMaskDiameter * 4L;

		public ModularEffectRenderResources(GraphicsDevice graphicsDevice, Texture2D pixel)
		{
			_graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
			Pixel = pixel ?? throw new ArgumentNullException(nameof(pixel));

			SwordArcMask = Polygon(280, 64, "modular_fx_sword_arc", SwordArcMaskPoints);
			BiteRingMask = Ring(310, 270, 7f);
			RockChunkMask = Polygon(42, 38, "modular_fx_rock_chunk", JaggedShardMaskPoints);
			DefaultRingMask = Ring(80, 80, 5f);
			HaloRingMask = Ring(230, 58, 7f);
			ThrownBladeMask = Polygon(68, 24, "modular_fx_thrown_blade", SwordArcMaskPoints);
			ShieldMask = Polygon(250, 300, "modular_fx_shield", ShieldMaskPoints);
			ShieldFragmentMask = Polygon(42, 54, "modular_fx_shield_fragment", JaggedShardMaskPoints);
			SealRingMask = Ring(220, 220, 10f);
			ColorDrainRingMask = Ring(190, 190, 8f);
			ToothTopMask = Polygon(26, 52, "modular_fx_tooth_top", ToothTopMaskPoints);
			ToothBottomMask = Polygon(26, 52, "modular_fx_tooth_bottom", ToothBottomMaskPoints);
			HammerHeadMask = Polygon(108, 180, "modular_fx_hammer_head", HammerHeadMaskPoints);
			JaggedParticleMask = Polygon(22, 34, "modular_fx_jagged_particle", JaggedParticleMaskPoints);
			ShockwaveRingMask = Ring(48, 48, 3f);

			foreach (var profile in RadialProfiles)
			{
				_radialMasks[profile] = PrimitiveTextureFactory.GetSoftRadialCircle(
					_graphicsDevice,
					RadialMaskDiameter,
					profile.InnerStop,
					profile.OuterStop);
			}
		}

		public Texture2D GetRadialMask(float innerStop, float outerStop)
		{
			var key = NormalizeProfile(innerStop, outerStop);
			if (_radialMasks.TryGetValue(key, out var texture)) return texture;
			throw new InvalidOperationException($"Modular FX radial profile ({key.Item1:0.###}, {key.Item2:0.###}) was not preloaded.");
		}

		public Texture2D GetEditableRingMask(float thickness)
		{
			return PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 80, 80, thickness);
		}

		private Texture2D Polygon(int width, int height, string key, IReadOnlyList<Vector2> points)
		{
			return PrimitiveTextureFactory.GetAntialiasedPolygonMask(_graphicsDevice, width, height, key, points);
		}

		private Texture2D Ring(int width, int height, float thickness)
		{
			return PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, width, height, thickness);
		}

		private static (float InnerStop, float OuterStop) NormalizeProfile(float innerStop, float outerStop)
		{
			return (
				(float)Math.Round(MathHelper.Clamp(innerStop, 0f, 1f), 3),
				(float)Math.Round(MathHelper.Clamp(outerStop, innerStop + 0.001f, 1f), 3));
		}
	}
}
