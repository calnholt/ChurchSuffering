using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public class DesertStormOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public float BaseScale { get; set; } = 1.5f;
    public float Lacunarity { get; set; } = 2f;
    public float Persistence { get; set; } = 0.5f;
    public float WarpStrength { get; set; } = 3.5f;
    public float DensityRemapLow { get; set; } = 0.2f;
    public float DensityRemapHigh { get; set; } = 0.8f;
    public float DriftSpeed { get; set; } = 0.025f;
    public float DriftVertical { get; set; } = 0.006f;
    public float WarpDriftA { get; set; } = 0.018f;
    public float WarpDriftB { get; set; } = 0.012f;
    public float MorphSpeed { get; set; } = 0.008f;
    public Vector3 ShadowColor { get; set; } = new(0.55f, 0.47f, 0.37f);
    public Vector3 MidColor { get; set; } = new(0.70f, 0.62f, 0.51f);
    public Vector3 HighlightColor { get; set; } = new(0.82f, 0.75f, 0.64f);
    public Vector3 BrightColor { get; set; } = new(0.89f, 0.82f, 0.71f);
    public float VerticalGradient { get; set; } = 0.08f;
    public float DustBase { get; set; } = 0.55f;
    public float DustDensity { get; set; } = 0.45f;
    public Vector3 SceneTint { get; set; } = new(0.90f, 0.82f, 0.68f);
    public float SceneTintStrength { get; set; } = 0.40f;
    public float GrainIntensity { get; set; } = 0.10f;
    public float GrainFineness { get; set; } = 1f;
    public float VignetteAmount { get; set; } = 0.20f;

    public DesertStormOverlay(Effect effect)
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

        _effect.Parameters["MatrixTransform"]?.SetValue(projection);
        _effect.Parameters["ViewportSize"]?.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        _effect.Parameters["Time"]?.SetValue(Time);
        _effect.Parameters["BaseScale"]?.SetValue(BaseScale);
        _effect.Parameters["Lacunarity"]?.SetValue(Lacunarity);
        _effect.Parameters["Persistence"]?.SetValue(Persistence);
        _effect.Parameters["WarpStrength"]?.SetValue(WarpStrength);
        _effect.Parameters["DensityRemapLow"]?.SetValue(DensityRemapLow);
        _effect.Parameters["DensityRemapHigh"]?.SetValue(DensityRemapHigh);
        _effect.Parameters["DriftSpeed"]?.SetValue(DriftSpeed);
        _effect.Parameters["DriftVertical"]?.SetValue(DriftVertical);
        _effect.Parameters["WarpDriftA"]?.SetValue(WarpDriftA);
        _effect.Parameters["WarpDriftB"]?.SetValue(WarpDriftB);
        _effect.Parameters["MorphSpeed"]?.SetValue(MorphSpeed);
        _effect.Parameters["ShadowColor"]?.SetValue(ShadowColor);
        _effect.Parameters["MidColor"]?.SetValue(MidColor);
        _effect.Parameters["HighlightColor"]?.SetValue(HighlightColor);
        _effect.Parameters["BrightColor"]?.SetValue(BrightColor);
        _effect.Parameters["VerticalGradient"]?.SetValue(VerticalGradient);
        _effect.Parameters["DustBase"]?.SetValue(DustBase);
        _effect.Parameters["DustDensity"]?.SetValue(DustDensity);
        _effect.Parameters["SceneTint"]?.SetValue(SceneTint);
        _effect.Parameters["SceneTintStrength"]?.SetValue(SceneTintStrength);
        _effect.Parameters["GrainIntensity"]?.SetValue(GrainIntensity);
        _effect.Parameters["GrainFineness"]?.SetValue(GrainFineness);
        _effect.Parameters["VignetteAmount"]?.SetValue(VignetteAmount);

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
}
