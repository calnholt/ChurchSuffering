using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class LayeredHolesOverlay
{
	private const int MaxHoles = 30;

	private readonly Effect _effect;
	private readonly Vector4[] _holes = new Vector4[MaxHoles];

	public bool IsAvailable => _effect != null;

	public float Time { get; set; }
	public int HoleCount { get; set; } = 30;
	public float HolePeriodMin { get; set; } = 10f;
	public float HolePeriodMax { get; set; } = 20f;
	public float HoleLifeMin { get; set; } = 0.45f;
	public float HoleLifeMax { get; set; } = 0.75f;
	public float HoleOpenFrac { get; set; } = 0.25f;
	public float HoleCloseFrac { get; set; } = 0.30f;
	public float HoleRadiusMin { get; set; } = 0.10f;
	public float HoleRadiusMax { get; set; } = 0.50f;
	public float RadiusFluxAmp { get; set; } = 0.12f;
	public float RadiusFluxRate { get; set; } = 2.20f;
	public float HoleMargin { get; set; } = 0.02f;
	public float HoleFeather { get; set; } = 0.045f;
	public float FeatherVary { get; set; } = 0.70f;
	public float RimWarpAmp { get; set; } = 0.340f;
	public float RimWarpScale { get; set; } = 3.5f;
	public float RimWarpSpeed { get; set; } = 0.35f;
	public float RevealRefract { get; set; } = 0.35f;
	public float LayerSplit { get; set; } = 0.50f;
	public float RevealDarken { get; set; }
	public Texture2D MiddleTexture { get; set; }
	public Texture2D BottomTexture { get; set; }

	public LayeredHolesOverlay(Effect effect)
	{
		_effect = effect;
	}

	public void Begin(SpriteBatch spriteBatch)
	{
		if (_effect == null) return;

		_effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];

		Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
		Matrix projection = Matrix.CreateOrthographicOffCenter(
			0,
			viewport.Width,
			viewport.Height,
			0,
			0,
			1);

		Set("MatrixTransform", projection);
		Set("ViewportSize", new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
		Set("Time", Time);
		Set("HoleCount", Math.Clamp(HoleCount, 0, MaxHoles));
		Set("HoleFeather", HoleFeather);
		Set("FeatherVary", FeatherVary);
		Set("RimWarpAmp", RimWarpAmp);
		Set("RimWarpScale", RimWarpScale);
		Set("RimWarpSpeed", RimWarpSpeed);
		Set("RevealRefract", RevealRefract);
		Set("RevealDarken", RevealDarken);
		PopulateHoleData(_holes, GetAspect(viewport));
		Set("Holes", _holes);
		Set("MiddleTexture", MiddleTexture);
		Set("BottomTexture", BottomTexture);

		spriteBatch.Begin(
			SpriteSortMode.Immediate,
			BlendState.Opaque,
			SamplerState.LinearClamp,
			DepthStencilState.None,
			RasterizerState.CullNone,
			_effect);
	}

	public void Draw(SpriteBatch spriteBatch, Texture2D topTexture, Rectangle destination)
	{
		if (_effect == null || topTexture == null) return;
		spriteBatch.Draw(topTexture, destination, Color.White);
	}

	public void End(SpriteBatch spriteBatch)
	{
		if (_effect == null) return;
		spriteBatch.End();
	}

	internal Vector4[] BuildHoleData(float aspect)
	{
		var holes = new Vector4[MaxHoles];
		PopulateHoleData(holes, aspect);
		return holes;
	}

	private void PopulateHoleData(Vector4[] holes, double aspect)
	{
		Array.Clear(holes, 0, holes.Length);

		int holeCount = Math.Clamp(HoleCount, 0, Math.Min(MaxHoles, holes.Length));
		double safeAspect = Math.Max(aspect, 0.001d);
		for (int i = 0; i < holeCount; i++)
		{
			holes[i] = BuildHole(i, safeAspect);
		}
	}

	private Vector4 BuildHole(int index, double aspect)
	{
		double fid = index;
		double periodMin = Math.Max(HolePeriodMin, 0.001d);
		double periodMax = Math.Max(HolePeriodMax, periodMin);
		double period = Lerp(periodMin, periodMax, Hash11(fid * 1.7d + 0.3d));
		double phase = Hash11(fid * 3.1d + 0.9d) * period;
		double elapsed = Time + phase;
		double cycle = Math.Floor(elapsed / period);
		double local = elapsed - cycle * period;
		double openDur = period * Lerp(Saturate(HoleLifeMin), Saturate(HoleLifeMax), Hash11(fid * 5.3d + cycle));

		if (local > openDur)
		{
			return Vector4.Zero;
		}

		double t = local / Math.Max(openDur, 0.001d);
		double openFrac = Math.Max(HoleOpenFrac, 0.001d);
		double closeFrac = Math.Max(HoleCloseFrac, 0.001d);
		double grow = SmoothStep(0d, openFrac, t);
		double close = 1d - SmoothStep(1d - closeFrac, 1d, t);
		double env = grow * close;

		double radiusMin = Math.Max(HoleRadiusMin, 0.001d);
		double radiusMax = Math.Max(HoleRadiusMax, radiusMin);
		double maxRadius = Lerp(radiusMin, radiusMax, Hash11(fid * 7.7d + cycle));
		double flux = 1d + RadiusFluxAmp * Math.Sin(Time * RadiusFluxRate + fid * 2.399d);
		double radius = maxRadius * env * Math.Max(flux, 0.001d);
		if (radius <= 0d)
		{
			return Vector4.Zero;
		}

		double margin = Math.Max(HoleMargin, 0d);
		double centerX = Lerp(margin, Math.Max(aspect - margin, margin), Hash11(fid * 11.1d + cycle));
		double centerY = Lerp(margin, Math.Max(1d - margin, margin), Hash11(fid * 13.3d + cycle));
		float pickMiddle = Hash11(fid * 17.7d + cycle) <= Saturate(LayerSplit) ? 1f : 0f;

		return new Vector4((float)centerX, (float)centerY, (float)radius, pickMiddle);
	}

	private static double GetAspect(Viewport viewport)
	{
		return viewport.Width / Math.Max((double)viewport.Height, 1d);
	}

	private static double Hash11(double value)
	{
		return FractionalPart(Math.Sin(value) * 43758.5453123d);
	}

	private static double FractionalPart(double value)
	{
		return value - Math.Floor(value);
	}

	private static double Lerp(double a, double b, double amount)
	{
		return a + (b - a) * amount;
	}

	private static double SmoothStep(double edge0, double edge1, double value)
	{
		double t = Saturate((value - edge0) / Math.Max(edge1 - edge0, 0.001d));
		return t * t * (3d - 2d * t);
	}

	private static double Saturate(double value)
	{
		return Math.Clamp(value, 0d, 1d);
	}

	private void Set(string name, float value)
	{
		_effect.Parameters[name]?.SetValue(value);
	}

	private void Set(string name, int value)
	{
		_effect.Parameters[name]?.SetValue(value);
	}

	private void Set(string name, Vector2 value)
	{
		_effect.Parameters[name]?.SetValue(value);
	}

	private void Set(string name, Matrix value)
	{
		_effect.Parameters[name]?.SetValue(value);
	}

	private void Set(string name, Vector4[] values)
	{
		_effect.Parameters[name]?.SetValue(values);
	}

	private void Set(string name, Texture2D value)
	{
		if (value != null) _effect.Parameters[name]?.SetValue(value);
	}
}
