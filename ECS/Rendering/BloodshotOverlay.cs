using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering
{
    public class BloodshotOverlay
    {
        private readonly Effect _effect;

        public bool IsAvailable => _effect != null;

        public float Time { get; set; }

        // Oval shape
        public float OvalHorizontalScale { get; set; } = 0.4f;
        public float OvalVerticalScale { get; set; } = 0.9f;

        // Blur effect
        public float BlurRadius { get; set; } = 0.001f;
        public float BlurStart { get; set; } = 0.4f;
        public float BlurEnd { get; set; } = 0.8f;

        // Vein generation
        public float VeinBaseFrequency { get; set; } = 10.0f;
        public float VeinAnimationSpeed { get; set; } = 0.001f;
        public float VeinRadialFrequency { get; set; } = 8.0f;
        public float VeinRadialScale { get; set; } = 10.0f;
        public float VeinTimeScale { get; set; } = 0.5f;

        // Vein appearance
        public float VeinEdgeStart { get; set; } = 0.2f;
        public float VeinEdgeEnd { get; set; } = 0.9f;
        public float VeinSharpnessPow { get; set; } = 1.0f;
        public float VeinSharpnessMult { get; set; } = 2.0f;
        public float VeinThresholdLow { get; set; } = 0.3f;
        public float VeinThresholdHigh { get; set; } = 0.7f;
        public float VeinColorStrength { get; set; } = 0.4f;

        // Redness effect
        public float RednessIntensity { get; set; } = 0.2f;
        public float RedTintR { get; set; } = 1.0f;
        public float RedTintG { get; set; } = 0.7f;
        public float RedTintB { get; set; } = 0.7f;

        // Blood color
        public Vector3 BloodColor { get; set; } = new Vector3(1.0f, 0.0f, 0.0f);

        // Clarity/blur
        public float ClarityStart { get; set; } = 0.8f;
        public float ClarityEnd { get; set; } = 0.2f;
        public float BlurDarkness { get; set; } = 0.7f;

        // Fade intensity (0 = no effect, 1 = full effect)
        public float FadeIntensity { get; set; } = 1.0f;

        public BloodshotOverlay(Effect effect)
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
            _effect.Parameters["ViewportSize"]?.SetValue(new Vector2(Game1.VirtualWidth, Game1.VirtualHeight));
            _effect.Parameters["Time"]?.SetValue(Time);

            // Oval shape
            _effect.Parameters["OvalHorizontalScale"]?.SetValue(OvalHorizontalScale);
            _effect.Parameters["OvalVerticalScale"]?.SetValue(OvalVerticalScale);

            // Blur effect
            _effect.Parameters["BlurRadius"]?.SetValue(BlurRadius);
            _effect.Parameters["BlurStart"]?.SetValue(BlurStart);
            _effect.Parameters["BlurEnd"]?.SetValue(BlurEnd);

            // Vein generation
            _effect.Parameters["VeinBaseFrequency"]?.SetValue(VeinBaseFrequency);
            _effect.Parameters["VeinAnimationSpeed"]?.SetValue(VeinAnimationSpeed);
            _effect.Parameters["VeinRadialFrequency"]?.SetValue(VeinRadialFrequency);
            _effect.Parameters["VeinRadialScale"]?.SetValue(VeinRadialScale);
            _effect.Parameters["VeinTimeScale"]?.SetValue(VeinTimeScale);

            // Vein appearance
            _effect.Parameters["VeinEdgeStart"]?.SetValue(VeinEdgeStart);
            _effect.Parameters["VeinEdgeEnd"]?.SetValue(VeinEdgeEnd);
            _effect.Parameters["VeinSharpnessPow"]?.SetValue(VeinSharpnessPow);
            _effect.Parameters["VeinSharpnessMult"]?.SetValue(VeinSharpnessMult);
            _effect.Parameters["VeinThresholdLow"]?.SetValue(VeinThresholdLow);
            _effect.Parameters["VeinThresholdHigh"]?.SetValue(VeinThresholdHigh);
            _effect.Parameters["VeinColorStrength"]?.SetValue(VeinColorStrength);

            // Redness effect
            _effect.Parameters["RednessIntensity"]?.SetValue(RednessIntensity);
            _effect.Parameters["RedTintR"]?.SetValue(RedTintR);
            _effect.Parameters["RedTintG"]?.SetValue(RedTintG);
            _effect.Parameters["RedTintB"]?.SetValue(RedTintB);

            // Blood color
            _effect.Parameters["BloodColor"]?.SetValue(BloodColor);

            // Clarity/blur
            _effect.Parameters["ClarityStart"]?.SetValue(ClarityStart);
            _effect.Parameters["ClarityEnd"]?.SetValue(ClarityEnd);
            _effect.Parameters["BlurDarkness"]?.SetValue(BlurDarkness);

            // Fade intensity
            _effect.Parameters["FadeIntensity"]?.SetValue(FadeIntensity);

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
}
