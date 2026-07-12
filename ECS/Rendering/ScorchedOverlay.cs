using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public sealed class ScorchedOverlay
{
    private readonly Effect _effect;
    private readonly EffectParameterCache _parameters;

    public ScorchedOverlay(Effect effect)
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
    public float CardRadius { get; set; } = 0.04f;
    public float FireReach { get; set; } = 0.13f;
    public float FireInner { get; set; } = 0.01f;
    public float FlameShape { get; set; } = 0.30f;
    public float FlameSharp { get; set; } = 7f;
    public float FlameThreshold { get; set; }
    public float HeatFade { get; set; } = 0.45f;
    public float FireScale { get; set; } = 7.5f;
    public float FireRise { get; set; } = 1.7f;
    public float FireEvolve { get; set; } = 1.1f;
    public float FireTurbulence { get; set; } = 0.45f;
    public float FireLeanOut { get; set; } = 1.2f;
    public float FireFuel { get; set; } = 1f;
    public float TopBias { get; set; } = 0.55f;
    public float FireBrightness { get; set; } = 1.35f;
    public Vector3 FireTint { get; set; } = Vector3.One;
    public float EmberStrength { get; set; } = 1.3f;
    public float EmberReach { get; set; } = 0.11f;
    public float EmberGrid { get; set; } = 22f;
    public float EmberSize { get; set; } = 0.09f;
    public Vector3 EmberColor { get; set; } = new(1f, 0.45f, 0.10f);
    public float CardScorch { get; set; }
    public float CardGlow { get; set; } = 0.30f;
    public float TimeSpeed { get; set; } = 0.6f;

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];
        Viewport viewport = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);

        Set("MatrixTransform", projection);
        Set("iResolution", Resolution);
        Set("iTime", Time);
        Set("CARD_CENTER", CardCenter);
        Set("CARD_SIZE", CardSize);
        Set("CARD_ROTATION", CardRotation);
        Set("CARD_LEFT", 0.38f);
        Set("CARD_RIGHT", 0.62f);
        Set("CARD_BOTTOM", 0.18f);
        Set("CARD_TOP", 0.82f);
        Set("CARD_RADIUS", CardRadius);
        Set("FIRE_REACH", FireReach);
        Set("FIRE_INNER", FireInner);
        Set("FLAME_SHAPE", FlameShape);
        Set("FLAME_SHARP", FlameSharp);
        Set("FLAME_THRESH", FlameThreshold);
        Set("HEAT_FADE", HeatFade);
        Set("FIRE_SCALE", FireScale);
        Set("FIRE_RISE", FireRise);
        Set("FIRE_EVOLVE", FireEvolve);
        Set("FIRE_TURB", FireTurbulence);
        Set("FIRE_LEAN_OUT", FireLeanOut);
        Set("FIRE_FUEL", FireFuel);
        Set("TOP_BIAS", TopBias);
        Set("FIRE_BRIGHTNESS", FireBrightness);
        Set("FIRE_TINT", FireTint);
        Set("EMBER_STR", EmberStrength);
        Set("EMBER_REACH", EmberReach);
        Set("EMBER_GRID", EmberGrid);
        Set("EMBER_SIZE", EmberSize);
        Set("EMBER_COLOR", EmberColor);
        Set("CARD_SCORCH", CardScorch);
        Set("CARD_GLOW", CardGlow);
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

    private void Set(string parameterName, float value) => _parameters.Set(parameterName, value);
    private void Set(string parameterName, Vector2 value) => _parameters.Set(parameterName, value);
    private void Set(string parameterName, Vector3 value) => _parameters.Set(parameterName, value);
    private void Set(string parameterName, Matrix value) => _parameters.Set(parameterName, value);
}
