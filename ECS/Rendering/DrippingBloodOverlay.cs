using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class DrippingBloodOverlay
{
    private readonly Effect _effect;
    private readonly Texture2D _whitePixel;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public int DripCount { get; set; } = 20;
    public int LayerCount { get; set; } = 1;
    public float SpeedMin { get; set; } = 0.06f;
    public float SpeedMax { get; set; } = 0.15f;
    public float RestMin { get; set; } = 1.5f;
    public float RestMax { get; set; } = 5f;
    public float FadePower { get; set; } = 1.8f;
    public float OffscreenFade { get; set; } = 0.35f;
    public float WidthMin { get; set; } = 0.003f;
    public float WidthMax { get; set; } = 0.016f;
    public float TaperAtTop { get; set; } = 0.65f;
    public float TipRoundness { get; set; } = 1f;
    public float WobbleAmount { get; set; } = 0.0025f;
    public float WobbleFrequency { get; set; } = 14f;
    public float ThicknessVariation { get; set; } = 0.35f;
    public Vector3 BackgroundColor { get; set; } = new(0.05f, 0.003f, 0.003f);
    public Vector3 DripColor { get; set; } = new(0.70f, 0.02f, 0.02f);
    public float VignetteStrength { get; set; }

    public DrippingBloodOverlay(GraphicsDevice graphicsDevice, Effect effect)
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
        _effect.Parameters["DripCount"]?.SetValue(DripCount);
        _effect.Parameters["LayerCount"]?.SetValue(LayerCount);
        _effect.Parameters["SpeedMin"]?.SetValue(SpeedMin);
        _effect.Parameters["SpeedMax"]?.SetValue(SpeedMax);
        _effect.Parameters["RestMin"]?.SetValue(RestMin);
        _effect.Parameters["RestMax"]?.SetValue(RestMax);
        _effect.Parameters["FadePower"]?.SetValue(FadePower);
        _effect.Parameters["OffscreenFade"]?.SetValue(OffscreenFade);
        _effect.Parameters["WidthMin"]?.SetValue(WidthMin);
        _effect.Parameters["WidthMax"]?.SetValue(WidthMax);
        _effect.Parameters["TaperAtTop"]?.SetValue(TaperAtTop);
        _effect.Parameters["TipRoundness"]?.SetValue(TipRoundness);
        _effect.Parameters["WobbleAmount"]?.SetValue(WobbleAmount);
        _effect.Parameters["WobbleFrequency"]?.SetValue(WobbleFrequency);
        _effect.Parameters["ThicknessVariation"]?.SetValue(ThicknessVariation);
        _effect.Parameters["BackgroundColor"]?.SetValue(BackgroundColor);
        _effect.Parameters["DripColor"]?.SetValue(DripColor);
        _effect.Parameters["VignetteStrength"]?.SetValue(VignetteStrength);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _effect);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.Draw(_whitePixel, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }
}
