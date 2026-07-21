using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public sealed class PoisonCardOverlay
{
    private readonly Effect _effect;
    private readonly EffectParameterCache _parameters;

    public PoisonCardOverlay(Effect effect)
    {
        _effect = effect;
        _parameters = new EffectParameterCache(effect);
    }

    public bool IsAvailable => _effect != null;
    public Vector2 Resolution { get; set; } = new(Game1.VirtualWidth, Game1.VirtualHeight);
    public float Time { get; set; }
    public Vector2 CardCenter { get; set; }
    public Vector2 CardSize { get; set; }
    public float CardRotation { get; set; }
    public float BlobFrequency { get; set; } = 6f;
    public float BlobStretch { get; set; } = 1.5f;
    public float BlobReachMin { get; set; } = 0.55f;
    public float BlobReachMax { get; set; } = 0.95f;
    public float ThresholdLow { get; set; } = 0.15f;
    public float ThresholdHigh { get; set; } = 0.90f;
    public float FlowSpeed { get; set; } = 0.10f;
    public float RefractionAmount { get; set; } = 0.16f;
    public float AlphaThin { get; set; } = 0.35f;
    public float AlphaThick { get; set; } = 0.90f;
    public float AbsorptionStrength { get; set; } = 0.90f;
    public Vector3 AbsorptionColor { get; set; } = new(0.30f, 0.80f, 0.34f);
    public Vector3 SlimeSurfaceColor { get; set; } = new(0.55f, 0.85f, 0.30f);
    public Vector3 SlimeDeepColor { get; set; } = new(0.00f, 0.26f, 0.08f);
    public Vector3 LightDirection { get; set; } = new(-0.40f, 0.65f, 0.80f);
    public float Ambient { get; set; } = 0.38f;
    public float Diffuse { get; set; } = 0.75f;
    public float SpecularPower { get; set; } = 46f;
    public float SpecularIntensity { get; set; } = 0.95f;
    public float RimPower { get; set; } = 2.6f;
    public float RimIntensity { get; set; } = 0.28f;

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

        Set("MatrixTransform", projection);
        Set("iResolution", Resolution);
        Set("iTime", Time);
        Set("CARD_CENTER", CardCenter);
        Set("CARD_SIZE", CardSize);
        Set("CARD_ROTATION", CardRotation);
        Set("BLOB_FREQ", BlobFrequency);
        Set("BLOB_STRETCH", BlobStretch);
        Set("BLOB_REACH_MIN", BlobReachMin);
        Set("BLOB_REACH_MAX", BlobReachMax);
        Set("THRESH_LO", ThresholdLow);
        Set("THRESH_HI", ThresholdHigh);
        Set("FLOW_SPEED", FlowSpeed);
        Set("REFRACT_AMT", RefractionAmount);
        Set("ALPHA_THIN", AlphaThin);
        Set("ALPHA_THICK", AlphaThick);
        Set("ABSORB_STR", AbsorptionStrength);
        Set("ABSORB_COLOR", AbsorptionColor);
        Set("SLIME_SURFACE", SlimeSurfaceColor);
        Set("SLIME_DEEP", SlimeDeepColor);
        Set("LIGHT_DIR", LightDirection);
        Set("AMBIENT", Ambient);
        Set("DIFFUSE", Diffuse);
        Set("SPEC_POWER", SpecularPower);
        Set("SPEC_INTENSITY", SpecularIntensity);
        Set("RIM_POWER", RimPower);
        Set("RIM_INTENSITY", RimIntensity);

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _effect);
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

    private void Set(string parameterName, float value) => _parameters.Set(parameterName, value);
    private void Set(string parameterName, Vector2 value) => _parameters.Set(parameterName, value);
    private void Set(string parameterName, Vector3 value) => _parameters.Set(parameterName, value);
    private void Set(string parameterName, Matrix value) => _parameters.Set(parameterName, value);
}
