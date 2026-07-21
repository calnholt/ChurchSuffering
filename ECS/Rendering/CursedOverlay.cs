using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public sealed class CursedOverlay
{
    private readonly Effect _effect;
    private readonly EffectParameterCache _parameters;

    public CursedOverlay(Effect effect)
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
    public float ShapeCount { get; set; } = 28f;
    public float ShapeSizeMin { get; set; } = 0.018f;
    public float ShapeSizeMax { get; set; } = 0.070f;
    public float ShapeRiseSpeedMin { get; set; } = 0.045f;
    public float ShapeRiseSpeedMax { get; set; } = 0.155f;
    public float ShapeOpacity { get; set; } = 0.55f;
    public float ShapeEdgeSoftness { get; set; } = 0.16f;
    public float ShapeVerticalFade { get; set; } = 0.14f;
    public Vector3 ShapeColor { get; set; } = new(0.72f, 0.16f, 0.96f);
    public float EffectSeed { get; set; } = 1f;
    public float TimeSpeed { get; set; } = 1f;

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
        Set("CARD_RADIUS", CardRadius);
        Set("SHAPE_COUNT", ShapeCount);
        Set("SHAPE_SIZE_MIN", ShapeSizeMin);
        Set("SHAPE_SIZE_MAX", ShapeSizeMax);
        Set("SHAPE_RISE_SPEED_MIN", ShapeRiseSpeedMin);
        Set("SHAPE_RISE_SPEED_MAX", ShapeRiseSpeedMax);
        Set("SHAPE_OPACITY", ShapeOpacity);
        Set("SHAPE_EDGE_SOFTNESS", ShapeEdgeSoftness);
        Set("SHAPE_VERTICAL_FADE", ShapeVerticalFade);
        Set("SHAPE_COLOR", ShapeColor);
        Set("EFFECT_SEED", EffectSeed);
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
