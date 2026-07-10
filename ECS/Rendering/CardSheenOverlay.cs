using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class CardSheenOverlay
{
	private readonly Effect _effect;

	public CardSheenOverlay(Effect effect)
	{
		_effect = effect;
	}

	public bool IsAvailable => _effect != null;
	public float Progress { get; set; }
	public float Alpha { get; set; }
	public float AngleRadians { get; set; }
	public float BandWidthNormalized { get; set; }
	public float FeatherNormalized { get; set; }
	public float CoreWidthNormalized { get; set; }
	public float CornerRadiusPx { get; set; }
	public float Intensity { get; set; }
	public Vector3 GoldFringeColor { get; set; }
	public Vector3 BlueFringeColor { get; set; }
	public Vector3 CoreColor { get; set; }

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
		Set("Progress", Progress);
		Set("Alpha", Alpha);
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

	private void Set(string name, float value) => _effect.Parameters[name]?.SetValue(value);
	private void Set(string name, Vector2 value) => _effect.Parameters[name]?.SetValue(value);
	private void Set(string name, Vector3 value) => _effect.Parameters[name]?.SetValue(value);
	private void Set(string name, Matrix value) => _effect.Parameters[name]?.SetValue(value);

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
