using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class ClimbChoiceLayerFilter
{
	private readonly Effect _effect;
	public ClimbChoiceLayerFilter(Effect effect) => _effect = effect;
	public bool IsAvailable => _effect != null;
	public float Grayscale { get; set; }
	public float Sepia { get; set; }
	public float Brightness { get; set; } = 1f;
	public float Opacity { get; set; } = 1f;

	public void Begin(SpriteBatch batch)
	{
		if (_effect == null) return;
		_effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
		Viewport viewport = batch.GraphicsDevice.Viewport;
		_effect.Parameters["MatrixTransform"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1));
		_effect.Parameters["GrayscaleAmount"]?.SetValue(Grayscale);
		_effect.Parameters["SepiaAmount"]?.SetValue(Sepia);
		_effect.Parameters["Brightness"]?.SetValue(Brightness);
		_effect.Parameters["Opacity"]?.SetValue(Opacity);
		batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, _effect);
	}

	public void Draw(SpriteBatch batch, Texture2D source) => batch.Draw(source, batch.GraphicsDevice.Viewport.Bounds, Color.White);
	public void End(SpriteBatch batch) { if (_effect != null) batch.End(); }
}
