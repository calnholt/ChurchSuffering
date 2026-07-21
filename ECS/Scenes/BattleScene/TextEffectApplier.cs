using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Scenes.BattleScene;
using ChurchSuffering.ECS.Utils.RichText;

namespace ChurchSuffering.ECS.Scenes.BattleScene
{
    public static class TextEffectApplier
    {
        public struct GlyphTransform
        {
            public Vector2 Offset;
            public float Rotation;
            public float Scale;
            public Color Color;
            public float Alpha;
        }

        public static GlyphTransform ComposeTransforms(List<EffectInstance> effects, DialogTextEffectSettings s, float time, int glyphIndex, float revealTimeSince)
        {
            var t = new GlyphTransform
            {
                Offset = Vector2.Zero,
                Rotation = 0f,
                Scale = 1f,
                Color = Color.White,
                Alpha = 1f,
            };
            if (effects == null || !s.EnableEffects) return t;

            foreach (var e in effects)
            {
                switch (e.Type)
                {
                    case TextEffectType.Jitter:
                    {
                        float amp = Get(e, "amp", s.JitterAmplitudePx);
                        float freq = Get(e, "freq", s.JitterFrequencyHz);
                        float phase = (glyphIndex * 17.0f) % MathF.PI;
                        t.Offset += new Vector2((float)(Noise(time * freq + phase) * amp), (float)(Noise((time + 1000f) * freq + phase) * amp));
                        break;
                    }
                    case TextEffectType.Shake:
                    {
                        float amp = Get(e, "amp", s.ShakeAmplitudePx);
                        float freq = Get(e, "freq", s.ShakeFrequencyHz);
                        t.Offset += new Vector2(MathF.Sin((time + glyphIndex * 0.03f) * MathF.Tau * freq) * amp, 0f);
                        break;
                    }
                    case TextEffectType.Nod:
                    {
                        float amp = Get(e, "amp", s.NodAmplitudePx);
                        float freq = Get(e, "freq", s.NodFrequencyHz);
                        t.Offset += new Vector2(0f, MathF.Sin((time + glyphIndex * 0.05f) * MathF.Tau * freq) * amp);
                        break;
                    }
                    case TextEffectType.Ripple:
                    {
                        float amp = Get(e, "amp", s.RippleAmplitudePx);
                        float wave = Get(e, "wavelength", s.RippleWavelengthPx);
                        float speed = Get(e, "speed", s.RippleSpeedHz);
                        float phase = glyphIndex / MathF.Max(1f, wave);
                        t.Offset += new Vector2(0f, MathF.Sin((time * speed + phase) * MathF.Tau) * amp);
                        break;
                    }
                    case TextEffectType.Big:
                    {
                        float sc = Get(e, "scale", s.BigScale);
                        t.Scale *= sc;
                        break;
                    }
                    case TextEffectType.Small:
                    {
                        float sc = Get(e, "scale", s.SmallScale);
                        t.Scale *= sc;
                        break;
                    }
                    case TextEffectType.Explode:
                    {
                        float dur = MathF.Max(0.01f, Get(e, "duration", s.PopDurationSec));
                        float strength = Get(e, "strength", s.PopScaleOnReveal);
                        float u = MathHelper.Clamp(revealTimeSince / dur, 0f, 1f);
                        float pop = 1f + (1f - EaseOutBack(u)) * (strength - 1f);
                        t.Scale *= pop;
                        break;
                    }
                    case TextEffectType.Bloom:
                    {
                        // Rendering for bloom underlay handled in Draw using s.* settings
                        break;
                    }
                }
            }
            return t;
        }

        private static float Get(EffectInstance e, string key, float def)
        {
            if (e.Params != null && e.Params.TryGetValue(key, out var v)) return v;
            return def;
        }

        private static float Noise(float x)
        {
            // Simple hash-based noise [-1,1]
            unchecked
            {
                int n = (int)MathF.Floor(x * 137.0f);
                n = (n << 13) ^ n;
                float res = (1.0f - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0f);
                return res;
            }
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1 + c3 * u * u * u + c1 * u * u;
        }
    }
}


