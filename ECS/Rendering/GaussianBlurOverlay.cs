using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public class GaussianBlurOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public Vector2 BlurDirection { get; set; }
    public float BlurRadius { get; set; } = 4f;

    public GaussianBlurOverlay(Effect effect)
    {
        _effect = effect;
    }

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];

        Viewport vp = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);

        var pMatrix = _effect.Parameters["MatrixTransform"]; if (pMatrix != null) pMatrix.SetValue(projection);
        var pViewport = _effect.Parameters["ViewportSize"]; if (pViewport != null) pViewport.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        var pDir = _effect.Parameters["BlurDirection"]; if (pDir != null) pDir.SetValue(BlurDirection);
        var pRadius = _effect.Parameters["BlurRadius"]; if (pRadius != null) pRadius.SetValue(BlurRadius);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _effect
        );
    }

    public void Draw(SpriteBatch spriteBatch, Texture2D source)
    {
        if (_effect == null || source == null) return;
        Rectangle bounds = spriteBatch.GraphicsDevice.Viewport.Bounds;
        spriteBatch.Draw(source, bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }
}
