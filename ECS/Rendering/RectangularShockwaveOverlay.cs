using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public class RectangularShockwaveOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public Vector2 BoundsCenterPx { get; set; }
    public Vector2 BoundsSizePx { get; set; }
    public float TimeNorm { get; set; } // 0..1
    public float MaxRadiusPx { get; set; } = 600f;
    public float RippleWidthPx { get; set; } = 24f;
    public float Strength { get; set; } = 1f;
    public float ChromaticAberrationAmp { get; set; } = 0.05f;
    public float ChromaticAberrationFreq { get; set; } = 3.14159f;
    public float ShadingIntensity { get; set; } = 0.6f;

    public RectangularShockwaveOverlay(Effect effect)
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
        var pCenter = _effect.Parameters["BoundsCenterPx"]; if (pCenter != null) pCenter.SetValue(BoundsCenterPx);
        var pSize = _effect.Parameters["BoundsSizePx"]; if (pSize != null) pSize.SetValue(BoundsSizePx);
        var pT = _effect.Parameters["t"]; if (pT != null) pT.SetValue(TimeNorm);
        var pMaxR = _effect.Parameters["MaxRadiusPx"]; if (pMaxR != null) pMaxR.SetValue(MaxRadiusPx);
        var pRipple = _effect.Parameters["RippleWidthPx"]; if (pRipple != null) pRipple.SetValue(RippleWidthPx);
        var pStr = _effect.Parameters["Strength"]; if (pStr != null) pStr.SetValue(Strength);
        var pCAmp = _effect.Parameters["ChromaticAberrationAmp"]; if (pCAmp != null) pCAmp.SetValue(ChromaticAberrationAmp);
        var pCFreq = _effect.Parameters["ChromaticAberrationFreq"]; if (pCFreq != null) pCFreq.SetValue(ChromaticAberrationFreq);
        var pShade = _effect.Parameters["ShadingIntensity"]; if (pShade != null) pShade.SetValue(ShadingIntensity);

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
