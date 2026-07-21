using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public class AchievementBackgroundOverlay
{
    private readonly Effect _effect;
    private readonly Texture2D _whitePixel;

    public bool IsAvailable => _effect != null;

    // Time and animation
    public float TimeSeconds { get; set; } = 0f;

    // Noise parameters
    public float NoiseScale { get; set; } = 4.0f;
    public float TimeSpeed { get; set; } = 0.25f;

    // Turbulence parameters
    public float TurbInitialInc { get; set; } = 0.75f;
    public float TurbInitialDiv { get; set; } = 1.75f;
    public float TurbOctaveMultiplier { get; set; } = 2.13f;
    public float TurbIncDecay { get; set; } = 0.5f;

    // UV manipulation
    public float UVDistortFactor { get; set; } = 0.2f;
    public float RotationSpeed { get; set; } = 0.5f;
    public float RayDepth { get; set; } = 5.0f;

    // Color parameters
    public float ColorBrightness { get; set; } = 1.0f;
    public Vector3 TintColor { get; set; } = Vector3.One;
    public float ChannelWeightR { get; set; } = 1.0f;
    public float ChannelWeightG { get; set; } = 1.0f;
    public float ChannelWeightB { get; set; } = 1.0f;

    // Vignette parameters
    public float VignetteStrength { get; set; } = 0.0f;
    public float VignetteRadius { get; set; } = 0.8f;

    public AchievementBackgroundOverlay(GraphicsDevice device, Effect effect)
    {
        _effect = effect;
        _whitePixel = new Texture2D(device, 1, 1, false, SurfaceFormat.Color);
        _whitePixel.SetData(new[] { Color.White });
    }

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];

        Viewport vp = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);

        // Matrix and viewport
        var pMatrix = _effect.Parameters["MatrixTransform"]; if (pMatrix != null) pMatrix.SetValue(projection);
        var pViewport = _effect.Parameters["ViewportSize"]; if (pViewport != null) pViewport.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));

        // Time
        var pTime = _effect.Parameters["iTime"]; if (pTime != null) pTime.SetValue(TimeSeconds);

        // Noise parameters
        var pNoiseScale = _effect.Parameters["NoiseScale"]; if (pNoiseScale != null) pNoiseScale.SetValue(NoiseScale);
        var pTimeSpeed = _effect.Parameters["TimeSpeed"]; if (pTimeSpeed != null) pTimeSpeed.SetValue(TimeSpeed);

        // Turbulence parameters
        var pTurbInitialInc = _effect.Parameters["TurbInitialInc"]; if (pTurbInitialInc != null) pTurbInitialInc.SetValue(TurbInitialInc);
        var pTurbInitialDiv = _effect.Parameters["TurbInitialDiv"]; if (pTurbInitialDiv != null) pTurbInitialDiv.SetValue(TurbInitialDiv);
        var pTurbOctaveMultiplier = _effect.Parameters["TurbOctaveMultiplier"]; if (pTurbOctaveMultiplier != null) pTurbOctaveMultiplier.SetValue(TurbOctaveMultiplier);
        var pTurbIncDecay = _effect.Parameters["TurbIncDecay"]; if (pTurbIncDecay != null) pTurbIncDecay.SetValue(TurbIncDecay);

        // UV manipulation
        var pUVDistortFactor = _effect.Parameters["UVDistortFactor"]; if (pUVDistortFactor != null) pUVDistortFactor.SetValue(UVDistortFactor);
        var pRotationSpeed = _effect.Parameters["RotationSpeed"]; if (pRotationSpeed != null) pRotationSpeed.SetValue(RotationSpeed);
        var pRayDepth = _effect.Parameters["RayDepth"]; if (pRayDepth != null) pRayDepth.SetValue(RayDepth);

        // Color parameters
        var pColorBrightness = _effect.Parameters["ColorBrightness"]; if (pColorBrightness != null) pColorBrightness.SetValue(ColorBrightness);
        var pTintColor = _effect.Parameters["TintColor"]; if (pTintColor != null) pTintColor.SetValue(TintColor);
        var pChannelWeightR = _effect.Parameters["ChannelWeightR"]; if (pChannelWeightR != null) pChannelWeightR.SetValue(ChannelWeightR);
        var pChannelWeightG = _effect.Parameters["ChannelWeightG"]; if (pChannelWeightG != null) pChannelWeightG.SetValue(ChannelWeightG);
        var pChannelWeightB = _effect.Parameters["ChannelWeightB"]; if (pChannelWeightB != null) pChannelWeightB.SetValue(ChannelWeightB);

        // Vignette parameters
        var pVignetteStrength = _effect.Parameters["VignetteStrength"]; if (pVignetteStrength != null) pVignetteStrength.SetValue(VignetteStrength);
        var pVignetteRadius = _effect.Parameters["VignetteRadius"]; if (pVignetteRadius != null) pVignetteRadius.SetValue(VignetteRadius);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _effect
        );
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        Rectangle bounds = spriteBatch.GraphicsDevice.Viewport.Bounds;
        spriteBatch.Draw(_whitePixel, bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }
}
