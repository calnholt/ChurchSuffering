using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public class IncenseOverlay
{
    private readonly Effect _effect;
    private readonly Texture2D _whitePixel;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public float Opacity { get; set; } = 0.65f;
    public float SmokeScale { get; set; } = 3.2f;
    public float WarpStrength { get; set; } = 2.6f;
    public float SmokeLow { get; set; } = 0.30f;
    public float SmokeHigh { get; set; } = 0.85f;
    public float DepthParallax { get; set; } = 0.55f;
    public float RiseSpeed { get; set; } = 0.055f;
    public float ChurnSpeed { get; set; } = 0.040f;
    public float DriftX { get; set; } = 0.010f;
    public Vector3 GloomColor { get; set; } = new(0.030f, 0.034f, 0.045f);
    public Vector3 SmokeColor { get; set; } = new(0.34f, 0.36f, 0.42f);
    public Vector3 GlintColor { get; set; } = new(1.00f, 0.82f, 0.55f);
    public float MoteAmount { get; set; } = 0.1f;
    public float MoteScale { get; set; } = 190.0f;
    public float MoteDriftMin { get; set; } = 0.008f;
    public float MoteDriftMax { get; set; } = 0.045f;
    public float MoteFlashMin { get; set; } = 0.6f;
    public float MoteFlashMax { get; set; } = 4.5f;
    public float MoteFlashDepth { get; set; } = 0.9f;
    public float VignetteAmount { get; set; } = 1.05f;
    public float GrainAmount { get; set; } = 0.035f;
    public float Exposure { get; set; } = 1.15f;

    public IncenseOverlay(GraphicsDevice graphicsDevice, Effect effect)
    {
        _effect = effect;
        _whitePixel = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whitePixel.SetData(new[] { Color.White });
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

        _effect.Parameters["MatrixTransform"]?.SetValue(projection);
        _effect.Parameters["ViewportSize"]?.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        _effect.Parameters["Time"]?.SetValue(Time);
        _effect.Parameters["Opacity"]?.SetValue(Opacity);
        _effect.Parameters["SmokeScale"]?.SetValue(SmokeScale);
        _effect.Parameters["WarpStrength"]?.SetValue(WarpStrength);
        _effect.Parameters["SmokeLow"]?.SetValue(SmokeLow);
        _effect.Parameters["SmokeHigh"]?.SetValue(SmokeHigh);
        _effect.Parameters["DepthParallax"]?.SetValue(DepthParallax);
        _effect.Parameters["RiseSpeed"]?.SetValue(RiseSpeed);
        _effect.Parameters["ChurnSpeed"]?.SetValue(ChurnSpeed);
        _effect.Parameters["DriftX"]?.SetValue(DriftX);
        _effect.Parameters["GloomColor"]?.SetValue(GloomColor);
        _effect.Parameters["SmokeColor"]?.SetValue(SmokeColor);
        _effect.Parameters["GlintColor"]?.SetValue(GlintColor);
        _effect.Parameters["MoteAmount"]?.SetValue(MoteAmount);
        _effect.Parameters["MoteScale"]?.SetValue(MoteScale);
        _effect.Parameters["MoteDriftMin"]?.SetValue(MoteDriftMin);
        _effect.Parameters["MoteDriftMax"]?.SetValue(MoteDriftMax);
        _effect.Parameters["MoteFlashMin"]?.SetValue(MoteFlashMin);
        _effect.Parameters["MoteFlashMax"]?.SetValue(MoteFlashMax);
        _effect.Parameters["MoteFlashDepth"]?.SetValue(MoteFlashDepth);
        _effect.Parameters["VignetteAmount"]?.SetValue(VignetteAmount);
        _effect.Parameters["GrainAmount"]?.SetValue(GrainAmount);
        _effect.Parameters["Exposure"]?.SetValue(Exposure);

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _effect);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_effect == null || _whitePixel == null) return;
        spriteBatch.Draw(_whitePixel, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }
}
