using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public class BrittleOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public Texture2D BackgroundTexture { get; set; }
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
    }

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];

        Viewport vp = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);

        _effect.Parameters["MatrixTransform"]?.SetValue(projection);
        _effect.Parameters["iResolution"]?.SetValue(new Vector2(vp.Width, vp.Height));
        _effect.Parameters["iTime"]?.SetValue(Time);
        _effect.Parameters["BackgroundTexture"]?.SetValue(BackgroundTexture);
        _effect.Parameters["CARD_CENTER"]?.SetValue(CardCenter);
        _effect.Parameters["CARD_SCALE"]?.SetValue(CardScale);
        _effect.Parameters["CARD_ROTATION"]?.SetValue(CardRotation);

        _effect.Parameters["GRID_MIN"]?.SetValue(GridMin);
        _effect.Parameters["GRID_MAX"]?.SetValue(GridMax);
        _effect.Parameters["GRID_SEED"]?.SetValue(GridSeed);
        _effect.Parameters["CELL_JITTER"]?.SetValue(CellJitter);
        _effect.Parameters["SEAM_WIDTH"]?.SetValue(SeamWidth);
        _effect.Parameters["FALL_FRACTION"]?.SetValue(FallFraction);
        _effect.Parameters["PERIOD_MIN"]?.SetValue(PeriodMin);
        _effect.Parameters["PERIOD_MAX"]?.SetValue(PeriodMax);
        _effect.Parameters["ATTACH_END"]?.SetValue(AttachEnd);
        _effect.Parameters["FALL_END"]?.SetValue(FallEnd);
        _effect.Parameters["MAX_FALL"]?.SetValue(MaxFall);
        _effect.Parameters["MAX_DRIFT"]?.SetValue(MaxDrift);
        _effect.Parameters["FALL_GRAVITY"]?.SetValue(FallGravity);
        _effect.Parameters["FALL_ROT"]?.SetValue(FallRot);
        _effect.Parameters["CHUNK_SIZE_PX"]?.SetValue(ChunkSizePx);
        _effect.Parameters["MASK_THRESHOLD"]?.SetValue(MaskThreshold);
        _effect.Parameters["DEBRIS_DARK"]?.SetValue(DebrisDark);
        _effect.Parameters["EDGE_GLOW"]?.SetValue(EdgeGlow);
        _effect.Parameters["EDGE_GLOW_AMT"]?.SetValue(EdgeGlowAmount);
        _effect.Parameters["HOLE_DARKEN"]?.SetValue(HoleDarken);

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
        if (_effect == null || source == null || BackgroundTexture == null) return;
        spriteBatch.Draw(source, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }
}
