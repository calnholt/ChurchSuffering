using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public class PurpleZapsOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public float UseSourceTexture { get; set; } = 1f;
    public float Zoom { get; set; } = 0.10f;
    public float ZapWarp { get; set; } = 1.50f;
    public float ZapSwirl { get; set; } = 9.00f;
    public float ZapGrowth { get; set; } = 0.02f;
    public float ZapSpeed { get; set; } = 1.00f;
    public float ZapFloor { get; set; } = 0.55f;
    public float ZapGain { get; set; } = 1.60f;
    public Vector3 ZapGlowColor { get; set; } = new(0.35f, 0.05f, 0.70f);
    public Vector3 ZapCoreColor { get; set; } = new(0.85f, 0.60f, 1.00f);
    public float ZapCoreLow { get; set; } = 0.80f;
    public float ZapCoreHigh { get; set; } = 2.00f;
    public float BackgroundDim { get; set; } = 0.40f;
    public Vector3 BackgroundFallbackTop { get; set; } = new(0.05f, 0.02f, 0.12f);
    public Vector3 BackgroundFallbackBottom { get; set; } = new(0.00f, 0.00f, 0.00f);

    public PurpleZapsOverlay(Effect effect)
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
            1
        );

        Set("MatrixTransform", projection);
        Set("ViewportSize", new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        Set("Time", Time);
        Set("UseSourceTexture", UseSourceTexture);
        Set("Zoom", Zoom);
        Set("ZapWarp", ZapWarp);
        Set("ZapSwirl", ZapSwirl);
        Set("ZapGrowth", ZapGrowth);
        Set("ZapSpeed", ZapSpeed);
        Set("ZapFloor", ZapFloor);
        Set("ZapGain", ZapGain);
        Set("ZapGlowColor", ZapGlowColor);
        Set("ZapCoreColor", ZapCoreColor);
        Set("ZapCoreLow", ZapCoreLow);
        Set("ZapCoreHigh", ZapCoreHigh);
        Set("BackgroundDim", BackgroundDim);
        Set("BackgroundFallbackTop", BackgroundFallbackTop);
        Set("BackgroundFallbackBottom", BackgroundFallbackBottom);

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
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
        spriteBatch.Draw(source, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }

    private void Set(string parameterName, float value) => _effect.Parameters[parameterName]?.SetValue(value);
    private void Set(string parameterName, Vector2 value) => _effect.Parameters[parameterName]?.SetValue(value);
    private void Set(string parameterName, Vector3 value) => _effect.Parameters[parameterName]?.SetValue(value);
    private void Set(string parameterName, Matrix value) => _effect.Parameters[parameterName]?.SetValue(value);
}
