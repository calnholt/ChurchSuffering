using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public class VolcanoEmbersOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public float UseSourceTexture { get; set; } = 1f;
    public float TimeScale { get; set; } = 1f;

    public float HazeAmp { get; set; } = 0.014f;
    public float HazeScale { get; set; } = 3.2f;
    public float HazeRise { get; set; } = 0.55f;
    public float HazeWaveFrequency { get; set; } = 11f;
    public float HazeWaveSpeed { get; set; } = 2.2f;
    public float HazeNoiseMix { get; set; } = 0.65f;
    public float HazeWaveMix { get; set; } = 0.45f;
    public float HazeOctaves { get; set; } = 3f;
    public float HazeReach { get; set; } = 1.10f;
    public float HazeFloor { get; set; } = 0.18f;

    public float EmberLayers { get; set; } = 5f;
    public float ScaleFar { get; set; } = 16f;
    public float ScaleNear { get; set; } = 5f;
    public float SizeFar { get; set; } = 0.030f;
    public float SizeNear { get; set; } = 0.090f;
    public float SizeVariation { get; set; } = 0.60f;
    public float DensityFar { get; set; } = 0.70f;
    public float DensityNear { get; set; } = 0.32f;
    public float RiseFar { get; set; } = 0.045f;
    public float RiseNear { get; set; } = 0.150f;
    public float EmberDrift { get; set; } = 0.010f;
    public float WanderAmp { get; set; } = 0.040f;
    public float WanderScale { get; set; } = 1.4f;
    public float WanderSpeed { get; set; } = 0.20f;
    public float SwayAmp { get; set; } = 0.060f;
    public float SwayRateMin { get; set; } = 0.5f;
    public float SwayRateMax { get; set; } = 2.4f;
    public float TwinkleMinBrightness { get; set; } = 0.25f;
    public float TwinkleRateMin { get; set; } = 0.8f;
    public float TwinkleRateMax { get; set; } = 5.0f;
    public float EmberCore { get; set; } = 0.32f;
    public float HaloGain { get; set; } = 0.85f;
    public float CoreGain { get; set; } = 1.30f;
    public float EmberBloom { get; set; } = 0.35f;
    public Vector3 CoreColor { get; set; } = new(1.00f, 0.92f, 0.62f);
    public Vector3 HotColor { get; set; } = new(1.00f, 0.48f, 0.12f);
    public Vector3 CoolColor { get; set; } = new(0.75f, 0.10f, 0.02f);
    public float GainFar { get; set; } = 0.45f;
    public float GainNear { get; set; } = 1.20f;
    public float EmberGain { get; set; } = 1f;
    public float EmberTopDim { get; set; } = 0.15f;
    public float EmberFadeLow { get; set; } = 0.10f;
    public float EmberFadeHigh { get; set; } = 1.05f;

    public Vector3 BackgroundTop { get; set; } = new(0.04f, 0.02f, 0.05f);
    public Vector3 BackgroundBottom { get; set; } = new(0.55f, 0.12f, 0.02f);
    public float BackgroundGlowScale { get; set; } = 2f;

    public VolcanoEmbersOverlay(Effect effect)
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
        _effect.Parameters["UseSourceTexture"]?.SetValue(UseSourceTexture);
        _effect.Parameters["TimeScale"]?.SetValue(TimeScale);

        _effect.Parameters["HazeAmp"]?.SetValue(HazeAmp);
        _effect.Parameters["HazeScale"]?.SetValue(HazeScale);
        _effect.Parameters["HazeRise"]?.SetValue(HazeRise);
        _effect.Parameters["HazeWaveFrequency"]?.SetValue(HazeWaveFrequency);
        _effect.Parameters["HazeWaveSpeed"]?.SetValue(HazeWaveSpeed);
        _effect.Parameters["HazeNoiseMix"]?.SetValue(HazeNoiseMix);
        _effect.Parameters["HazeWaveMix"]?.SetValue(HazeWaveMix);
        _effect.Parameters["HazeOctaves"]?.SetValue(HazeOctaves);
        _effect.Parameters["HazeReach"]?.SetValue(HazeReach);
        _effect.Parameters["HazeFloor"]?.SetValue(HazeFloor);

        _effect.Parameters["EmberLayers"]?.SetValue(EmberLayers);
        _effect.Parameters["ScaleFar"]?.SetValue(ScaleFar);
        _effect.Parameters["ScaleNear"]?.SetValue(ScaleNear);
        _effect.Parameters["SizeFar"]?.SetValue(SizeFar);
        _effect.Parameters["SizeNear"]?.SetValue(SizeNear);
        _effect.Parameters["SizeVariation"]?.SetValue(SizeVariation);
        _effect.Parameters["DensityFar"]?.SetValue(DensityFar);
        _effect.Parameters["DensityNear"]?.SetValue(DensityNear);
        _effect.Parameters["RiseFar"]?.SetValue(RiseFar);
        _effect.Parameters["RiseNear"]?.SetValue(RiseNear);
        _effect.Parameters["EmberDrift"]?.SetValue(EmberDrift);
        _effect.Parameters["WanderAmp"]?.SetValue(WanderAmp);
        _effect.Parameters["WanderScale"]?.SetValue(WanderScale);
        _effect.Parameters["WanderSpeed"]?.SetValue(WanderSpeed);
        _effect.Parameters["SwayAmp"]?.SetValue(SwayAmp);
        _effect.Parameters["SwayRateMin"]?.SetValue(SwayRateMin);
        _effect.Parameters["SwayRateMax"]?.SetValue(SwayRateMax);
        _effect.Parameters["TwinkleMinBrightness"]?.SetValue(TwinkleMinBrightness);
        _effect.Parameters["TwinkleRateMin"]?.SetValue(TwinkleRateMin);
        _effect.Parameters["TwinkleRateMax"]?.SetValue(TwinkleRateMax);
        _effect.Parameters["EmberCore"]?.SetValue(EmberCore);
        _effect.Parameters["HaloGain"]?.SetValue(HaloGain);
        _effect.Parameters["CoreGain"]?.SetValue(CoreGain);
        _effect.Parameters["EmberBloom"]?.SetValue(EmberBloom);
        _effect.Parameters["CoreColor"]?.SetValue(CoreColor);
        _effect.Parameters["HotColor"]?.SetValue(HotColor);
        _effect.Parameters["CoolColor"]?.SetValue(CoolColor);
        _effect.Parameters["GainFar"]?.SetValue(GainFar);
        _effect.Parameters["GainNear"]?.SetValue(GainNear);
        _effect.Parameters["EmberGain"]?.SetValue(EmberGain);
        _effect.Parameters["EmberTopDim"]?.SetValue(EmberTopDim);
        _effect.Parameters["EmberFadeLow"]?.SetValue(EmberFadeLow);
        _effect.Parameters["EmberFadeHigh"]?.SetValue(EmberFadeHigh);

        _effect.Parameters["BackgroundTop"]?.SetValue(BackgroundTop);
        _effect.Parameters["BackgroundBottom"]?.SetValue(BackgroundBottom);
        _effect.Parameters["BackgroundGlowScale"]?.SetValue(BackgroundGlowScale);

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
