using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public sealed class CardSheenOverlay
{
	private readonly Effect _effect;
	private readonly EffectParameterCache _parameters;

	public CardSheenOverlay(Effect effect)
	{
		_effect = effect;
		_parameters = new EffectParameterCache(effect);
	}

	public bool IsAvailable => _effect != null;
	public float TimeSeconds { get; set; }
	public float SheenDurationSeconds { get; set; }
	public float RepeatDelaySeconds { get; set; }
	public float AngleRadians { get; set; }
	public float BandWidthNormalized { get; set; }
	public float FeatherNormalized { get; set; }
	public float CoreWidthNormalized { get; set; }
	public float CornerRadiusPx { get; set; }
	public float Intensity { get; set; }
	public Vector3 GoldFringeColor { get; set; }
	public Vector3 BlueFringeColor { get; set; }
	public Vector3 CoreColor { get; set; }
	public Vector2 Resolution { get; set; } = new(Game1.VirtualWidth, Game1.VirtualHeight);
	public Vector2 CardCenter { get; set; }
	public float CardRotation { get; set; }

	public void Begin(SpriteBatch spriteBatch)
	{
		if (_effect == null) return;
		_effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
		Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
		Matrix projection = Matrix.CreateOrthographicOffCenter(
			0,
			Game1.VirtualWidth,
			Game1.VirtualHeight,
			0,
			0,
			1);

		Set("MatrixTransform", projection);
		Set("TimeSeconds", TimeSeconds);
		Set("SheenDurationSeconds", SheenDurationSeconds);
		Set("RepeatDelaySeconds", RepeatDelaySeconds);
		Set("AngleRadians", AngleRadians);
		Set("BandWidthNormalized", BandWidthNormalized);
		Set("FeatherNormalized", FeatherNormalized);
		Set("CoreWidthNormalized", CoreWidthNormalized);
		Set("CornerRadiusPx", CornerRadiusPx);
		Set("Intensity", Intensity);
		Set("GoldFringeColor", GoldFringeColor);
		Set("BlueFringeColor", BlueFringeColor);
		Set("CoreColor", CoreColor);

		spriteBatch.Begin(
			SpriteSortMode.Immediate,
			CardSheenBlendState,
			SamplerState.LinearClamp,
			DepthStencilState.None,
			RasterizerState.CullNone,
			_effect);
	}

	public void BeginComposite(SpriteBatch spriteBatch)
	{
		if (_effect == null) return;
		_effect.CurrentTechnique = _effect.Techniques["CompositeDrawing"];
		Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
		Matrix projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);

		Set("MatrixTransform", projection);
		Set("TimeSeconds", TimeSeconds);
		Set("SheenDurationSeconds", SheenDurationSeconds);
		Set("RepeatDelaySeconds", RepeatDelaySeconds);
		Set("AngleRadians", AngleRadians);
		Set("BandWidthNormalized", BandWidthNormalized);
		Set("FeatherNormalized", FeatherNormalized);
		Set("CoreWidthNormalized", CoreWidthNormalized);
		Set("CornerRadiusPx", CornerRadiusPx);
		Set("Intensity", Intensity);
		Set("GoldFringeColor", GoldFringeColor);
		Set("BlueFringeColor", BlueFringeColor);
		Set("CoreColor", CoreColor);
		Set("Resolution", Resolution);
		Set("CardCenter", CardCenter);
		Set("CardRotation", CardRotation);

		spriteBatch.Begin(
			SpriteSortMode.Immediate,
			BlendState.Opaque,
			SamplerState.LinearClamp,
			DepthStencilState.None,
			RasterizerState.CullNone,
			_effect);
	}

	public void DrawComposite(SpriteBatch spriteBatch, Texture2D source, Vector2 cardSize)
	{
		if (_effect == null || source == null) return;
		Set("CardSizePx", cardSize);
		spriteBatch.Draw(source, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
	}

	public void Draw(
		SpriteBatch spriteBatch,
		Texture2D pixel,
		Vector2 cardCenter,
		Vector2 cardSize,
		float cardRotation)
	{
		if (_effect == null || pixel == null || cardSize.X <= 0f || cardSize.Y <= 0f) return;
		Set("CardSizePx", cardSize);
		spriteBatch.Draw(
			pixel,
			cardCenter,
			null,
			Color.White,
			cardRotation,
			new Vector2(pixel.Width / 2f, pixel.Height / 2f),
			cardSize,
			SpriteEffects.None,
			0f);
	}

	public void End(SpriteBatch spriteBatch)
	{
		if (_effect == null) return;
		spriteBatch.End();
	}

	private void Set(string name, float value) => _parameters.Set(name, value);
	private void Set(string name, Vector2 value) => _parameters.Set(name, value);
	private void Set(string name, Vector3 value) => _parameters.Set(name, value);
	private void Set(string name, Matrix value) => _parameters.Set(name, value);

	private static readonly BlendState CardSheenBlendState = new()
	{
		ColorSourceBlend = Blend.SourceAlpha,
		ColorDestinationBlend = Blend.One,
		ColorBlendFunction = BlendFunction.Add,
		AlphaSourceBlend = Blend.Zero,
		AlphaDestinationBlend = Blend.One,
		AlphaBlendFunction = BlendFunction.Add,
	};
}
