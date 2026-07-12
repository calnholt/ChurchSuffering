using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public class BrittleOverlay
{
    private readonly Effect _effect;
    private readonly EffectParameterCache _parameters;

    public bool IsAvailable => _effect != null;

    public Vector2 Resolution { get; set; } = new(Game1.VirtualWidth, Game1.VirtualHeight);
    public float Time { get; set; }
    public Vector2 CardCenter { get; set; }
    public float CardScale { get; set; } = 1f;
    public float CardRotation { get; set; }

    public float GridMin { get; set; } = 18f;
    public float GridMax { get; set; } = 18f;
    public float GridSeed { get; set; } = 12f;
    public float CellJitter { get; set; } = 0.9f;
    public float SeamWidth { get; set; } = 0f;
    public float FallFraction { get; set; } = 0.15f;
    public float PeriodMin { get; set; } = 2.5f;
    public float PeriodMax { get; set; } = 9f;
    public float AttachEnd { get; set; } = 0.45f;
    public float FallEnd { get; set; } = 0.8f;
    public float MaxFall { get; set; } = 12f;
    public float MaxDrift { get; set; } = 1.2f;
    public float FallGravity { get; set; } = 2f;
    public float FallRot { get; set; } = 2.2f;
    public float ChunkSizePx { get; set; } = 22f;
    public float MaskThreshold { get; set; } = 0.02f;
    public float DebrisDark { get; set; } = 0.95f;
    public Vector3 EdgeGlow { get; set; } = new(1f, 0.85f, 0.45f);
    public float EdgeGlowAmount { get; set; } = 0.6f;
    public float HoleDarken { get; set; } = 1f;

    public BrittleOverlay(Effect effect)
    {
        _effect = effect;
        _parameters = new EffectParameterCache(effect);
    }

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];

        Viewport vp = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);

        _parameters.Set("MatrixTransform", projection);
        _parameters.Set("iResolution", Resolution);
        _parameters.Set("iTime", Time);
        _parameters.Set("CARD_CENTER", CardCenter);
        _parameters.Set("CARD_SCALE", CardScale);
        _parameters.Set("CARD_ROTATION", CardRotation);

        _parameters.Set("GRID_MIN", GridMin);
        _parameters.Set("GRID_MAX", GridMax);
        _parameters.Set("GRID_SEED", GridSeed);
        _parameters.Set("CELL_JITTER", CellJitter);
        _parameters.Set("SEAM_WIDTH", SeamWidth);
        _parameters.Set("FALL_FRACTION", FallFraction);
        _parameters.Set("PERIOD_MIN", PeriodMin);
        _parameters.Set("PERIOD_MAX", PeriodMax);
        _parameters.Set("ATTACH_END", AttachEnd);
        _parameters.Set("FALL_END", FallEnd);
        _parameters.Set("MAX_FALL", MaxFall);
        _parameters.Set("MAX_DRIFT", MaxDrift);
        _parameters.Set("FALL_GRAVITY", FallGravity);
        _parameters.Set("FALL_ROT", FallRot);
        _parameters.Set("CHUNK_SIZE_PX", ChunkSizePx);
        _parameters.Set("MASK_THRESHOLD", MaskThreshold);
        _parameters.Set("DEBRIS_DARK", DebrisDark);
        _parameters.Set("EDGE_GLOW", EdgeGlow);
        _parameters.Set("EDGE_GLOW_AMT", EdgeGlowAmount);
        _parameters.Set("HOLE_DARKEN", HoleDarken);

        spriteBatch.Begin(
            SpriteSortMode.Deferred,
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
