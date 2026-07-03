using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class FrozenOverlay
{
    private readonly Effect _effect;

    public FrozenOverlay(Effect effect)
    {
        _effect = effect;
    }

    public bool IsAvailable => _effect != null;
    public float Time { get; set; }
    public Vector2 CardCenter { get; set; }
    public Vector2 CardSize { get; set; }
    public float CardRotation { get; set; }
    public float CardRadius { get; set; } = 0.04f;
    public float IceTintStrength { get; set; } = 0.42f;
    public Vector3 IceTint { get; set; } = new(0.62f, 0.80f, 0.95f);
    public float IceBrighten { get; set; } = 0.06f;
    public float RefractAmount { get; set; } = 0.0001f;
    public float RefractScale { get; set; } = 20f;
    public float RefractSpeed { get; set; } = 0.15f;
    public float FrostEdge { get; set; } = 0.1f;
    public float FrostDensity { get; set; } = 0.55f;
    public float FrostScale { get; set; } = 2f;
    public Vector3 FrostColor { get; set; } = new(0.88f, 0.94f, 1f);
    public float SparkleAmount { get; set; } = 0.45f;
    public float SparkleScale { get; set; } = 990f;
    public float SparkleSize { get; set; } = 0.12f;
    public float SparkleSpeed { get; set; } = 1.5f;
    public float CrackAmount { get; set; } = 1f;
    public float CrackScale { get; set; } = 10f;
    public Vector2 CrackSeed { get; set; } = new(3f, 3f);
    public float CrackSharpness { get; set; } = 43f;
    public float CrackDepth { get; set; } = 2.2f;
    public float CrackLight { get; set; } = 0.35f;
    public float CrackShade { get; set; } = 0.28f;
    public float CrackOcclusion { get; set; } = 0.8f;
    public Vector3 CrackDeepTint { get; set; } = new(0.30f, 0.55f, 0.80f);
    public Vector3 CrackLightDirection { get; set; } = new(-0.6f, 0.7f, 0.5f);
    public float FacetTilt { get; set; } = 0.35f;
    public float FacetRefract { get; set; } = 0.018f;
    public float FacetReflect { get; set; } = 0.30f;
    public float FacetWarble { get; set; } = 0.6f;
    public float BreathStrength { get; set; } = 0.85f;
    public float BreathOffset { get; set; } = -0.1f;
    public float BreathHeight { get; set; } = 0.422f;
    public float BreathWidth { get; set; } = 1f;
    public float BreathSpread { get; set; } = 0.45f;
    public float BreathEdgeSoftness { get; set; } = 0.35f;
    public float BreathRise { get; set; } = 0.03f;
    public float BreathScale { get; set; } = 5.5f;
    public float BreathSwirl { get; set; } = 1.8f;
    public float BreathSwirlSpeed { get; set; } = 1.6f;
    public float BreathPuff { get; set; } = 0.01f;
    public Vector3 BreathColor { get; set; } = new(0.90f, 0.95f, 1f);

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
        Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);

        Set("MatrixTransform", projection);
        Set("iResolution", new Vector2(viewport.Width, viewport.Height));
        Set("iTime", Time);
        Set("CARD_CENTER", CardCenter);
        Set("CARD_SIZE", CardSize);
        Set("CARD_ROTATION", CardRotation);
        Set("CARD_LEFT", 0.40f);
        Set("CARD_RIGHT", 0.60f);
        Set("CARD_BOTTOM", 0.05f);
        Set("CARD_TOP", 0.50f);
        Set("CARD_RADIUS", CardRadius);
        Set("ICE_TINT_STR", IceTintStrength);
        Set("ICE_TINT", IceTint);
        Set("ICE_BRIGHTEN", IceBrighten);
        Set("REFRACT_AMT", RefractAmount);
        Set("REFRACT_SCALE", RefractScale);
        Set("REFRACT_SPEED", RefractSpeed);
        Set("FROST_EDGE", FrostEdge);
        Set("FROST_DENSITY", FrostDensity);
        Set("FROST_SCALE", FrostScale);
        Set("FROST_COLOR", FrostColor);
        Set("SPARKLE_AMT", SparkleAmount);
        Set("SPARKLE_SCALE", SparkleScale);
        Set("SPARKLE_SIZE", SparkleSize);
        Set("SPARKLE_SPEED", SparkleSpeed);
        Set("CRACK_AMT", CrackAmount);
        Set("CRACK_SCALE", CrackScale);
        Set("CRACK_SEED", CrackSeed);
        Set("CRACK_SHARP", CrackSharpness);
        Set("CRACK_DEPTH", CrackDepth);
        Set("CRACK_LIGHT", CrackLight);
        Set("CRACK_SHADE", CrackShade);
        Set("CRACK_AO", CrackOcclusion);
        Set("CRACK_DEEP_TINT", CrackDeepTint);
        Set("CRACK_LIGHT_DIR", CrackLightDirection);
        Set("FACET_TILT", FacetTilt);
        Set("FACET_REFRACT", FacetRefract);
        Set("FACET_REFLECT", FacetReflect);
        Set("FACET_WARBLE", FacetWarble);
        Set("BREATH_STR", BreathStrength);
        Set("BREATH_OFFSET", BreathOffset);
        Set("BREATH_HEIGHT", BreathHeight);
        Set("BREATH_WIDTH", BreathWidth);
        Set("BREATH_SPREAD", BreathSpread);
        Set("BREATH_EDGE_SOFT", BreathEdgeSoftness);
        Set("BREATH_RISE", BreathRise);
        Set("BREATH_SCALE", BreathScale);
        Set("BREATH_SWIRL", BreathSwirl);
        Set("BREATH_SWIRL_SPEED", BreathSwirlSpeed);
        Set("BREATH_PUFF", BreathPuff);
        Set("BREATH_COLOR", BreathColor);

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

    private void Set(string parameterName, float value) => _effect.Parameters[parameterName]?.SetValue(value);
    private void Set(string parameterName, Vector2 value) => _effect.Parameters[parameterName]?.SetValue(value);
    private void Set(string parameterName, Vector3 value) => _effect.Parameters[parameterName]?.SetValue(value);
    private void Set(string parameterName, Matrix value) => _effect.Parameters[parameterName]?.SetValue(value);
}
