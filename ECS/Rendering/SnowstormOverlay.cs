using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public class SnowstormOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public float UseSourceTexture { get; set; } = 1f;
    public float TimeScale { get; set; } = 1f;

    public float SnowLayers { get; set; } = 6f;
    public float ScaleFar { get; set; } = 24f;
    public float ScaleNear { get; set; } = 5f;
    public float SizeFar { get; set; } = 0.040f;
    public float SizeNear { get; set; } = 0.190f;
    public float FlakeJitter { get; set; } = 0.85f;
    public float FlakeSizeVariation { get; set; } = 0.55f;
    public float DensityFar { get; set; } = 0.95f;
    public float DensityNear { get; set; } = 0.45f;
    public float FallFar { get; set; } = 0.08f;
    public float FallNear { get; set; } = 0.45f;

    public float FlowStrength { get; set; } = 0.10f;
    public float FlowScale { get; set; } = 1.3f;
    public float FlowScrollX { get; set; } = 0.10f;
    public float FlowScrollY { get; set; } = 0.16f;
    public float FlowDepthMin { get; set; } = 0.40f;
    public float WindDrift { get; set; } = 0.14f;
    public float WindGust { get; set; } = 0.40f;
    public float WindGustRate { get; set; } = 0.21f;
    public float WindParallax { get; set; } = 1f;

    public Vector3 SheetColor { get; set; } = new(0.86f, 0.90f, 0.97f);
    public float SheetBase { get; set; } = 0.05f;
    public float SheetGust { get; set; } = 0.26f;
    public float SheetScale { get; set; } = 1.7f;
    public float SheetStretch { get; set; } = 7f;
    public float SheetSpeed { get; set; } = 1.9f;
    public float SheetLow { get; set; } = 0.42f;
    public float SheetHigh { get; set; } = 0.86f;

    public float SwayAmp { get; set; } = 0.16f;
    public float SwayRateMin { get; set; } = 0.8f;
    public float SwayRateMax { get; set; } = 2.6f;
    public float TwinkleMinBrightness { get; set; } = 0.40f;
    public float TwinkleRateMin { get; set; } = 0.6f;
    public float TwinkleRateMax { get; set; } = 4f;
    public float TwinkleDepthBias { get; set; } = 1.4f;

    public float CrystalMin { get; set; } = 0.60f;
    public float CrystalArm { get; set; } = 0.80f;
    public float CrystalThick { get; set; } = 0.060f;
    public float CrystalSpinMin { get; set; } = -0.5f;
    public float CrystalSpinMax { get; set; } = 0.5f;
    public float CrystalVariety { get; set; } = 0.40f;

    public float DofFocus { get; set; } = 0.85f;
    public float DofSpread { get; set; } = 0.10f;
    public float EdgeSharp { get; set; } = 0.010f;
    public float EdgeSoft { get; set; } = 0.110f;

    public float FlakeGain { get; set; } = 1.25f;
    public float SparkleGlow { get; set; } = 0.30f;
    public Vector3 FlakeColorFar { get; set; } = new(0.60f, 0.69f, 0.85f);
    public Vector3 FlakeColorNear { get; set; } = new(0.93f, 0.97f, 1.00f);
    public float FarFade { get; set; } = 0.55f;

    public Vector3 SkyTop { get; set; } = new(0.03f, 0.05f, 0.13f);
    public Vector3 SkyBottom { get; set; } = new(0.12f, 0.15f, 0.22f);
    public float SkyGradient { get; set; } = 1f;

    public Vector3 HazeColor { get; set; } = new(0.55f, 0.60f, 0.68f);
    public float HazeBase { get; set; } = 0.10f;
    public float HazeGust { get; set; } = 0.14f;
    public float HazeScale { get; set; } = 2.2f;
    public float HazeDrift { get; set; } = 0.06f;

    public float Dither { get; set; } = 0.012f;

    public SnowstormOverlay(Effect effect)
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

        _effect.Parameters["SnowLayers"]?.SetValue(SnowLayers);
        _effect.Parameters["ScaleFar"]?.SetValue(ScaleFar);
        _effect.Parameters["ScaleNear"]?.SetValue(ScaleNear);
        _effect.Parameters["SizeFar"]?.SetValue(SizeFar);
        _effect.Parameters["SizeNear"]?.SetValue(SizeNear);
        _effect.Parameters["FlakeJitter"]?.SetValue(FlakeJitter);
        _effect.Parameters["FlakeSizeVariation"]?.SetValue(FlakeSizeVariation);
        _effect.Parameters["DensityFar"]?.SetValue(DensityFar);
        _effect.Parameters["DensityNear"]?.SetValue(DensityNear);
        _effect.Parameters["FallFar"]?.SetValue(FallFar);
        _effect.Parameters["FallNear"]?.SetValue(FallNear);

        _effect.Parameters["FlowStrength"]?.SetValue(FlowStrength);
        _effect.Parameters["FlowScale"]?.SetValue(FlowScale);
        _effect.Parameters["FlowScrollX"]?.SetValue(FlowScrollX);
        _effect.Parameters["FlowScrollY"]?.SetValue(FlowScrollY);
        _effect.Parameters["FlowDepthMin"]?.SetValue(FlowDepthMin);
        _effect.Parameters["WindDrift"]?.SetValue(WindDrift);
        _effect.Parameters["WindGust"]?.SetValue(WindGust);
        _effect.Parameters["WindGustRate"]?.SetValue(WindGustRate);
        _effect.Parameters["WindParallax"]?.SetValue(WindParallax);

        _effect.Parameters["SheetColor"]?.SetValue(SheetColor);
        _effect.Parameters["SheetBase"]?.SetValue(SheetBase);
        _effect.Parameters["SheetGust"]?.SetValue(SheetGust);
        _effect.Parameters["SheetScale"]?.SetValue(SheetScale);
        _effect.Parameters["SheetStretch"]?.SetValue(SheetStretch);
        _effect.Parameters["SheetSpeed"]?.SetValue(SheetSpeed);
        _effect.Parameters["SheetLow"]?.SetValue(SheetLow);
        _effect.Parameters["SheetHigh"]?.SetValue(SheetHigh);

        _effect.Parameters["SwayAmp"]?.SetValue(SwayAmp);
        _effect.Parameters["SwayRateMin"]?.SetValue(SwayRateMin);
        _effect.Parameters["SwayRateMax"]?.SetValue(SwayRateMax);
        _effect.Parameters["TwinkleMinBrightness"]?.SetValue(TwinkleMinBrightness);
        _effect.Parameters["TwinkleRateMin"]?.SetValue(TwinkleRateMin);
        _effect.Parameters["TwinkleRateMax"]?.SetValue(TwinkleRateMax);
        _effect.Parameters["TwinkleDepthBias"]?.SetValue(TwinkleDepthBias);

        _effect.Parameters["CrystalMin"]?.SetValue(CrystalMin);
        _effect.Parameters["CrystalArm"]?.SetValue(CrystalArm);
        _effect.Parameters["CrystalThick"]?.SetValue(CrystalThick);
        _effect.Parameters["CrystalSpinMin"]?.SetValue(CrystalSpinMin);
        _effect.Parameters["CrystalSpinMax"]?.SetValue(CrystalSpinMax);
        _effect.Parameters["CrystalVariety"]?.SetValue(CrystalVariety);

        _effect.Parameters["DofFocus"]?.SetValue(DofFocus);
        _effect.Parameters["DofSpread"]?.SetValue(DofSpread);
        _effect.Parameters["EdgeSharp"]?.SetValue(EdgeSharp);
        _effect.Parameters["EdgeSoft"]?.SetValue(EdgeSoft);

        _effect.Parameters["FlakeGain"]?.SetValue(FlakeGain);
        _effect.Parameters["SparkleGlow"]?.SetValue(SparkleGlow);
        _effect.Parameters["FlakeColorFar"]?.SetValue(FlakeColorFar);
        _effect.Parameters["FlakeColorNear"]?.SetValue(FlakeColorNear);
        _effect.Parameters["FarFade"]?.SetValue(FarFade);

        _effect.Parameters["SkyTop"]?.SetValue(SkyTop);
        _effect.Parameters["SkyBottom"]?.SetValue(SkyBottom);
        _effect.Parameters["SkyGradient"]?.SetValue(SkyGradient);

        _effect.Parameters["HazeColor"]?.SetValue(HazeColor);
        _effect.Parameters["HazeBase"]?.SetValue(HazeBase);
        _effect.Parameters["HazeGust"]?.SetValue(HazeGust);
        _effect.Parameters["HazeScale"]?.SetValue(HazeScale);
        _effect.Parameters["HazeDrift"]?.SetValue(HazeDrift);
        _effect.Parameters["Dither"]?.SetValue(Dither);

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
