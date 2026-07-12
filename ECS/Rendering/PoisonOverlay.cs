using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

public class PoisonOverlay
{
    private readonly Effect _effect;

    public bool IsAvailable => _effect != null;

    public float TimeNorm { get; set; } // 0..1
    
    // Fade curve parameters
    public float AttackDuration { get; set; } = 0.1f;
    public float DecayRate { get; set; } = 4.0f;
    
    // Screen shake parameters
    public float ShakeFrequency { get; set; } = 50.0f;
    public float ShakeAmplitude { get; set; } = 0.006f;
    
    // Radial distortion wave parameters
    public float WaveFrequency { get; set; } = 20.0f;
    public float WaveSpeed { get; set; } = 25.0f;
    public float WaveAmplitude { get; set; } = 0.008f;
    
    // Vignette parameters
    public float VignetteStart { get; set; } = 0.2f;
    public float VignetteIntensity { get; set; } = 0.8f;
    
    // Color grading parameters
    public Vector3 PoisonTint { get; set; } = new Vector3(0.15f, 0.9f, 0.3f);
    public float PoisonMixAmount { get; set; } = 0.4f;
    public float DesaturationAmount { get; set; } = 0.5f;

    public PoisonOverlay(Effect effect)
    {
        _effect = effect;
    }

    public void Begin(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;

        _effect.CurrentTechnique = _effect.Techniques["SpriteDrawing"];

        Viewport vp = spriteBatch.GraphicsDevice.Viewport;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, vp.Width, vp.Height, 0, 0, 1);

        var pMatrix = _effect.Parameters["MatrixTransform"]; if (pMatrix != null) pMatrix.SetValue(projection);
        var pViewport = _effect.Parameters["ViewportSize"]; if (pViewport != null) pViewport.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
        var pT = _effect.Parameters["t"]; if (pT != null) pT.SetValue(TimeNorm);
        var pAttack = _effect.Parameters["AttackDuration"]; if (pAttack != null) pAttack.SetValue(AttackDuration);
        var pDecay = _effect.Parameters["DecayRate"]; if (pDecay != null) pDecay.SetValue(DecayRate);
        var pShakeFreq = _effect.Parameters["ShakeFrequency"]; if (pShakeFreq != null) pShakeFreq.SetValue(ShakeFrequency);
        var pShakeAmp = _effect.Parameters["ShakeAmplitude"]; if (pShakeAmp != null) pShakeAmp.SetValue(ShakeAmplitude);
        var pWaveFreq = _effect.Parameters["WaveFrequency"]; if (pWaveFreq != null) pWaveFreq.SetValue(WaveFrequency);
        var pWaveSpeed = _effect.Parameters["WaveSpeed"]; if (pWaveSpeed != null) pWaveSpeed.SetValue(WaveSpeed);
        var pWaveAmp = _effect.Parameters["WaveAmplitude"]; if (pWaveAmp != null) pWaveAmp.SetValue(WaveAmplitude);
        var pVigStart = _effect.Parameters["VignetteStart"]; if (pVigStart != null) pVigStart.SetValue(VignetteStart);
        var pVigInt = _effect.Parameters["VignetteIntensity"]; if (pVigInt != null) pVigInt.SetValue(VignetteIntensity);
        var pTint = _effect.Parameters["PoisonTint"]; if (pTint != null) pTint.SetValue(PoisonTint);
        var pMix = _effect.Parameters["PoisonMixAmount"]; if (pMix != null) pMix.SetValue(PoisonMixAmount);
        var pDesat = _effect.Parameters["DesaturationAmount"]; if (pDesat != null) pDesat.SetValue(DesaturationAmount);

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
        Rectangle bounds = spriteBatch.GraphicsDevice.Viewport.Bounds;
        spriteBatch.Draw(source, bounds, Color.White);
    }

    public void End(SpriteBatch spriteBatch)
    {
        if (_effect == null) return;
        spriteBatch.End();
    }
}
