using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class QuestRewardOverlayEffect
{
	private readonly Effect _effect;

	public QuestRewardOverlayEffect(GraphicsDevice graphicsDevice, Effect effect)
	{
		_effect = effect;
	}

	public bool IsAvailable => _effect != null;
	public float Time { get; set; }
	public float OverlayAlpha { get; set; } = 1f;
	public float FlashProgress { get; set; } = -1f;

	public void Begin(SpriteBatch spriteBatch)
	{
		if (_effect == null) return;
		_effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
		Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
		_effect.Parameters["MatrixTransform"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1));
		_effect.Parameters["ViewportSize"]?.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
		_effect.Parameters["Time"]?.SetValue(Time);
		_effect.Parameters["OverlayAlpha"]?.SetValue(OverlayAlpha);
		_effect.Parameters["FlashProgress"]?.SetValue(FlashProgress);
		spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, _effect);
	}

	public void Draw(SpriteBatch spriteBatch, Texture2D source)
	{
		if (_effect == null || source == null) return;
		spriteBatch.Draw(source, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
	}

	public void End(SpriteBatch spriteBatch)
	{
		if (_effect != null) spriteBatch.End();
	}
}
