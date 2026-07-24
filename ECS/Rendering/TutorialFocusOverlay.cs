using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering;

public sealed class TutorialFocusOverlay
{
    public const int MaxCutouts = 16;

    private readonly Effect _effect;
    private readonly Texture2D _whitePixel;
    private readonly Vector4[] _packedCutouts = new Vector4[MaxCutouts];

    public bool IsAvailable => _effect != null;
    public IReadOnlyList<Rectangle> Cutouts { get; set; } = Array.Empty<Rectangle>();
    public float Time { get; set; }
    public float CutoutPadding { get; set; } = 8f;
    public float CutoutCornerRadius { get; set; } = 18f;
    public float CutoutFeather { get; set; } = 20f;
    public float OverlayAlpha { get; set; } = 180f / 255f;
    public float RimWidth { get; set; } = 14f;
    public float RimAlpha { get; set; } = 0.16f;
    public Vector3 RimColor { get; set; } = new(0.78f, 0.62f, 0.36f);
    public float GrainStrength { get; set; } = 0.05f;
    public float GrainScale { get; set; } = 0.04f;
    public float GrainDriftSpeed { get; set; } = 2f;
    public float BreathSpeed { get; set; } = 0.15f;
    public float BreathAmount { get; set; } = 0.12f;

    public TutorialFocusOverlay(GraphicsDevice graphicsDevice, Effect effect)
    {
        _effect = effect;
        _whitePixel = new Texture2D(graphicsDevice, 1, 1, false, SurfaceFormat.Color);
        _whitePixel.SetData([Color.White]);
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
            1);

        int cutoutCount = Math.Min(Cutouts?.Count ?? 0, MaxCutouts);
        Array.Clear(_packedCutouts);
        for (int i = 0; i < cutoutCount; i++)
        {
            Rectangle bounds = Cutouts[i];
            _packedCutouts[i] = new Vector4(
                bounds.X + bounds.Width * 0.5f,
                bounds.Y + bounds.Height * 0.5f,
                Math.Max(0.5f, bounds.Width * 0.5f),
                Math.Max(0.5f, bounds.Height * 0.5f));
        }

        _effect.Parameters["MatrixTransform"]?.SetValue(projection);
        _effect.Parameters["ViewportSize"]?.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        _effect.Parameters["Time"]?.SetValue(Time);
        _effect.Parameters["CutoutRects"]?.SetValue(_packedCutouts);
        _effect.Parameters["CutoutCount"]?.SetValue(cutoutCount);
        _effect.Parameters["CutoutPadding"]?.SetValue(CutoutPadding);
        _effect.Parameters["CutoutCornerRadius"]?.SetValue(CutoutCornerRadius);
        _effect.Parameters["CutoutFeather"]?.SetValue(CutoutFeather);
        _effect.Parameters["OverlayAlpha"]?.SetValue(OverlayAlpha);
        _effect.Parameters["RimWidth"]?.SetValue(RimWidth);
        _effect.Parameters["RimAlpha"]?.SetValue(RimAlpha);
        _effect.Parameters["RimColor"]?.SetValue(RimColor);
        _effect.Parameters["GrainStrength"]?.SetValue(GrainStrength);
        _effect.Parameters["GrainScale"]?.SetValue(GrainScale);
        _effect.Parameters["GrainDriftSpeed"]?.SetValue(GrainDriftSpeed);
        _effect.Parameters["BreathSpeed"]?.SetValue(BreathSpeed);
        _effect.Parameters["BreathAmount"]?.SetValue(BreathAmount);

        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            _effect);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.Draw(_whitePixel, spriteBatch.GraphicsDevice.Viewport.Bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect != null) spriteBatch.End();
    }
}
