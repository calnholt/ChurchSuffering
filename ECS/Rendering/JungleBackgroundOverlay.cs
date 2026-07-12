using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public class JungleBackgroundOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public float Time { get; set; }
    public float LeafCount { get; set; } = 150f;
    public float FieldOverfill { get; set; } = 1.15f;
    public float TimeScale { get; set; } = 1f;
    public float FallBase { get; set; } = 0.05f;
    public float FallParallax { get; set; } = 0.9f;
    public float WindDrift { get; set; } = -0.05f;
    public float WindParallax { get; set; } = 1f;
    public float WindGust { get; set; } = 0.01f;
    public float WindGustRate { get; set; } = 0.02f;
    public Vector2 SpinRate { get; set; } = new(0.20f, 0.30f);
    public float SpinDesync { get; set; } = 1f;
    public float SwayAmp { get; set; } = 0.35f;
    public float SwayTilt { get; set; } = 0.45f;
    public float SwayRateMin { get; set; } = 0.5f;
    public float SwayRateMax { get; set; } = 1.6f;
    public float LeafRadiusMin { get; set; } = 0.3f;
    public float LeafRadiusVariation { get; set; } = 0.8f;
    public Vector3 LeafColorDark { get; set; } = new(0.05f, 0.22f, 0.03f);
    public Vector3 LeafColorLight { get; set; } = new(0.45f, 0.80f, 0.18f);
    public float LeafHueJitter { get; set; } = 0.12f;
    public float LeafBrightness { get; set; } = 1f;
    public float FarFade { get; set; } = 0.40f;
    public float CameraDistance { get; set; } = 15f;
    public float CameraFov { get; set; } = 1.5f;
    public float LeafZBack { get; set; } = 8f;
    public float BackgroundBrightness { get; set; } = 1f;
    public Vector3 BackgroundSkyLow { get; set; } = new(0.20f, 0.26f, 0.30f);
    public Vector3 BackgroundSkyHigh { get; set; } = new(0.45f, 0.52f, 0.55f);

    public JungleBackgroundOverlay(Effect effect)
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
        _effect.Parameters["LeafCount"]?.SetValue(LeafCount);
        _effect.Parameters["FieldOverfill"]?.SetValue(FieldOverfill);
        _effect.Parameters["TimeScale"]?.SetValue(TimeScale);
        _effect.Parameters["FallBase"]?.SetValue(FallBase);
        _effect.Parameters["FallParallax"]?.SetValue(FallParallax);
        _effect.Parameters["WindDrift"]?.SetValue(WindDrift);
        _effect.Parameters["WindParallax"]?.SetValue(WindParallax);
        _effect.Parameters["WindGust"]?.SetValue(WindGust);
        _effect.Parameters["WindGustRate"]?.SetValue(WindGustRate);
        _effect.Parameters["SpinRate"]?.SetValue(SpinRate);
        _effect.Parameters["SpinDesync"]?.SetValue(SpinDesync);
        _effect.Parameters["SwayAmp"]?.SetValue(SwayAmp);
        _effect.Parameters["SwayTilt"]?.SetValue(SwayTilt);
        _effect.Parameters["SwayRateMin"]?.SetValue(SwayRateMin);
        _effect.Parameters["SwayRateMax"]?.SetValue(SwayRateMax);
        _effect.Parameters["LeafRadiusMin"]?.SetValue(LeafRadiusMin);
        _effect.Parameters["LeafRadiusVariation"]?.SetValue(LeafRadiusVariation);
        _effect.Parameters["LeafColorDark"]?.SetValue(LeafColorDark);
        _effect.Parameters["LeafColorLight"]?.SetValue(LeafColorLight);
        _effect.Parameters["LeafHueJitter"]?.SetValue(LeafHueJitter);
        _effect.Parameters["LeafBrightness"]?.SetValue(LeafBrightness);
        _effect.Parameters["FarFade"]?.SetValue(FarFade);
        _effect.Parameters["CameraDistance"]?.SetValue(CameraDistance);
        _effect.Parameters["CameraFov"]?.SetValue(CameraFov);
        _effect.Parameters["LeafZBack"]?.SetValue(LeafZBack);
        _effect.Parameters["BackgroundBrightness"]?.SetValue(BackgroundBrightness);
        _effect.Parameters["BackgroundSkyLow"]?.SetValue(BackgroundSkyLow);
        _effect.Parameters["BackgroundSkyHigh"]?.SetValue(BackgroundSkyHigh);

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
