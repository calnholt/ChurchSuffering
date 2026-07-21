using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering
{
	public sealed class WayStationPenanceBackdropOverlay
	{
		private readonly Effect _effect;
		public bool IsAvailable => _effect != null;
		public float LifecycleAlpha { get; set; }
		public float BaseZoom { get; set; } = 1f;
		public float ModalZoom { get; set; } = 1.004f;
		public float BaseSaturation { get; set; } = 0.90f;
		public float ModalSaturation { get; set; } = 0.62f;
		public float BaseContrast { get; set; } = 1.02f;
		public float ModalContrast { get; set; } = 1.05f;
		public Vector2 ParallaxOffset { get; set; }
		public float VignetteStrength { get; set; } = 1f;
		public float DimStrength { get; set; } = 1f;

		public WayStationPenanceBackdropOverlay(Effect effect)
		{
			_effect = effect;
		}

		public void Begin(SpriteBatch spriteBatch)
		{
			if (_effect == null) return;
			_effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
			Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
			_effect.Parameters["MatrixTransform"]?.SetValue(Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1));
			_effect.Parameters["LifecycleAlpha"]?.SetValue(LifecycleAlpha);
			_effect.Parameters["BaseZoom"]?.SetValue(BaseZoom);
			_effect.Parameters["ModalZoom"]?.SetValue(ModalZoom);
			_effect.Parameters["BaseSaturation"]?.SetValue(BaseSaturation);
			_effect.Parameters["ModalSaturation"]?.SetValue(ModalSaturation);
			_effect.Parameters["BaseContrast"]?.SetValue(BaseContrast);
			_effect.Parameters["ModalContrast"]?.SetValue(ModalContrast);
			_effect.Parameters["ParallaxOffset"]?.SetValue(ParallaxOffset);
			_effect.Parameters["VignetteStrength"]?.SetValue(VignetteStrength);
			_effect.Parameters["DimStrength"]?.SetValue(DimStrength);
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
}
