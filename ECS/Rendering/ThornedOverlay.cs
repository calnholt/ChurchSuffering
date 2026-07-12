using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class ThornedOverlay
{
    private readonly Effect _effect;

    public ThornedOverlay(Effect effect)
    {
        _effect = effect;
    }

    public bool IsAvailable => _effect != null;
    public float Time { get; set; }
    public Vector2 CardCenter { get; set; }
    public Vector2 CardSize { get; set; }
    public float CardRotation { get; set; }
    public float CardRadius { get; set; } = 0.04f;
    public float CurseTintStrength { get; set; } = 0.10f;
    public Vector3 CurseTint { get; set; } = new(0.16f, 0.27f, 0.13f);
    public float EdgeDarken { get; set; } = 0.18f;
    public float VineThicknessA { get; set; } = 0.01f;
    public float VineThicknessB { get; set; } = 0.01f;
    public float OutlineExtra { get; set; }
    public float LineSoft { get; set; } = 0.0035f;
    public float DiagonalOpacity { get; set; } = 1f;
    public float DiagonalOvershoot { get; set; } = 0.14f;
    public float SquirmAmountA { get; set; } = 0.025f;
    public float SquirmAmountB { get; set; } = 0.025f;
    public float SquirmFrequencyA { get; set; } = 7f;
    public float SquirmFrequencyB { get; set; } = 8.5f;
    public float SquirmSpeedA { get; set; } = 0.18f;
    public float SquirmSpeedB { get; set; } = 0.14f;
    public float SquirmPhaseB { get; set; } = 2.35f;
    public Vector3 OutlineColor { get; set; } = new(0.010f, 0.020f, 0.010f);
    public Vector3 VineDark { get; set; } = new(0.040f, 0.095f, 0.035f);
    public Vector3 VineMid { get; set; } = new(0.115f, 0.210f, 0.085f);
    public Vector3 VineLight { get; set; } = new(0.245f, 0.355f, 0.160f);
    public float VineOpacity { get; set; } = 0.96f;
    public float VineShadow { get; set; } = 0.30f;
    public float ThornsPerVine { get; set; } = 10f;
    public float ThornLength { get; set; } = 0.050f;
    public float ThornBase { get; set; } = 0.012f;
    public Vector3 ThornWhite { get; set; } = new(0.940f, 0.930f, 0.865f);
    public Vector3 ThornLight { get; set; } = new(1.000f, 0.985f, 0.920f);
    public float EdgeCreep { get; set; } = 0.075f;
    public float EdgeRootDensity { get; set; } = 0.48f;
    public float EdgeRootScale { get; set; } = 35f;
    public float TimeSpeed { get; set; } = 1f;

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
        Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);

        Set("MatrixTransform", projection);
        Set("iResolution", new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        Set("iTime", Time);
        Set("CARD_CENTER", CardCenter);
        Set("CARD_SIZE", CardSize);
        Set("CARD_ROTATION", CardRotation);
        Set("CARD_LEFT", 0.40f);
        Set("CARD_RIGHT", 0.60f);
        Set("CARD_BOTTOM", 0.05f);
        Set("CARD_TOP", 0.50f);
        Set("CARD_RADIUS", CardRadius);
        Set("CURSE_TINT_STR", CurseTintStrength);
        Set("CURSE_TINT", CurseTint);
        Set("EDGE_DARKEN", EdgeDarken);
        Set("VINE_THICKNESS_A", VineThicknessA);
        Set("VINE_THICKNESS_B", VineThicknessB);
        Set("OUTLINE_EXTRA", OutlineExtra);
        Set("LINE_SOFT", LineSoft);
        Set("DIAGONAL_OPACITY", DiagonalOpacity);
        Set("DIAGONAL_OVERSHOOT", DiagonalOvershoot);
        Set("SQUIRM_AMOUNT_A", SquirmAmountA);
        Set("SQUIRM_AMOUNT_B", SquirmAmountB);
        Set("SQUIRM_FREQ_A", SquirmFrequencyA);
        Set("SQUIRM_FREQ_B", SquirmFrequencyB);
        Set("SQUIRM_SPEED_A", SquirmSpeedA);
        Set("SQUIRM_SPEED_B", SquirmSpeedB);
        Set("SQUIRM_PHASE_B", SquirmPhaseB);
        Set("OUTLINE_COLOR", OutlineColor);
        Set("VINE_DARK", VineDark);
        Set("VINE_MID", VineMid);
        Set("VINE_LIGHT", VineLight);
        Set("VINE_OPACITY", VineOpacity);
        Set("VINE_SHADOW", VineShadow);
        Set("THORNS_PER_VINE", ThornsPerVine);
        Set("THORN_LEN", ThornLength);
        Set("THORN_BASE", ThornBase);
        Set("THORN_WHITE", ThornWhite);
        Set("THORN_LIGHT", ThornLight);
        Set("EDGE_CREEP", EdgeCreep);
        Set("EDGE_ROOT_DENS", EdgeRootDensity);
        Set("EDGE_ROOT_SCALE", EdgeRootScale);
        Set("TIME_SPEED", TimeSpeed);

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
