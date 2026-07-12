using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Utils;
using Crusaders30XX.ECS.Utils.RichText;

namespace Crusaders30XX.ECS.Scenes.BattleScene
{
    public sealed class DialogTextEffectSettings
    {
        public bool EnableEffects = true;

        public float JitterAmplitudePx = 2f;
        public float JitterFrequencyHz = 12f;

        public float ShakeAmplitudePx = 4f;
        public float ShakeFrequencyHz = 6f;

        public float NodAmplitudePx = 3f;
        public float NodFrequencyHz = 2.5f;

        public float RippleAmplitudePx = 3f;
        public float RippleWavelengthPx = 40f;
        public float RippleSpeedHz = 1.2f;

        public float BigScale = 1.5f;
        public float SmallScale = 0.75f;

        public float PopDurationSec = 0.25f;
        public float PopScaleOnReveal = 1.35f;

        public float BloomRadiusPx = 2f;
        public float BloomIntensity01 = 0.35f;
        public int BloomPasses = 3;

        public float FastSpeedFactor = 2.0f;
        public float SlowSpeedFactor = 0.5f;
    }

    public struct EffectInstance
    {
        public TextEffectType Type;
        public Dictionary<string, float> Params; // optional per effect
    }

    public struct GlyphInfo
    {
        public char Character;
        public List<EffectInstance> Effects; // accumulated from tag stack
        public float SpeedFactor; // accumulated multiplier (default 1)
    }

    public sealed class FlattenedRichText
    {
        public string OriginalPlain = string.Empty; // from parsed document
        public string FilteredPlain = string.Empty; // after font glyph filtering
        public List<GlyphInfo> Glyphs = new List<GlyphInfo>(); // length == FilteredPlain (excluding newlines)
    }

    public struct GlyphLayoutInfo
    {
        public Vector2 BasePosition; // upper-left in world coordinates
        public char Character;
    }

    public sealed class LaidOutText
    {
        public List<GlyphLayoutInfo> GlyphLayouts = new List<GlyphLayoutInfo>();
        public float LineHeightPx;
        public int LineCount;
        public float ContentHeightPx;
    }

    public static class RichTextFlattener
    {
        public static FlattenedRichText Flatten(RichTextDocument doc, DialogTextEffectSettings debug)
        {
            var result = new FlattenedRichText();
            var originalChars = new List<GlyphInfo>();
            var effectStack = new Stack<EffectInstance>();
            float speedMulStack = 1f;

            void PushEffectFromTag(TagNode tag)
            {
                var type = ParseEffectType(tag.Name);
                if (type == TextEffectType.Speed)
                {
                    float f = 1f;
                    if (tag.Attributes.TryGetValue("factor", out var s) && float.TryParse(Normalize(s), out var v)) f = v;
                    if (string.Equals(tag.Name, "fast", StringComparison.OrdinalIgnoreCase)) f = debug.FastSpeedFactor;
                    if (string.Equals(tag.Name, "slow", StringComparison.OrdinalIgnoreCase)) f = debug.SlowSpeedFactor;
                    speedMulStack *= Math.Max(0.01f, f);
                    // still push an instance so nested children can read that a speed tag exists
                    effectStack.Push(new EffectInstance { Type = TextEffectType.Speed, Params = new Dictionary<string, float> { { "factor", f } } });
                    return;
                }
                var inst = new EffectInstance { Type = type, Params = ToFloatDict(tag.Attributes) };
                effectStack.Push(inst);
            }

            void PopEffectFromTag(TagNode tag)
            {
                var type = ParseEffectType(tag.Name);
                if (type == TextEffectType.Speed)
                {
                    float f = 1f;
                    if (tag.Attributes.TryGetValue("factor", out var s) && float.TryParse(Normalize(s), out var v)) f = v;
                    if (string.Equals(tag.Name, "fast", StringComparison.OrdinalIgnoreCase)) f = debug.FastSpeedFactor;
                    if (string.Equals(tag.Name, "slow", StringComparison.OrdinalIgnoreCase)) f = debug.SlowSpeedFactor;
                    speedMulStack /= Math.Max(0.01f, f);
                }
                if (effectStack.Count > 0) effectStack.Pop();
            }

            void Visit(IRichTextNode node)
            {
                if (node is TextRunNode tr)
                {
                    foreach (var ch in tr.Text)
                    {
                        // newlines are kept in plain string but not as glyph entries
                        if (ch == '\r' || ch == '\n')
                        {
                            result.OriginalPlain += ch;
                            continue;
                        }
                        var glyph = new GlyphInfo
                        {
                            Character = ch,
                            Effects = new List<EffectInstance>(effectStack),
                            SpeedFactor = Math.Max(0.01f, speedMulStack),
                        };
                        result.OriginalPlain += ch;
                        originalChars.Add(glyph);
                    }
                }
                else if (node is TagNode tn)
                {
                    PushEffectFromTag(tn);
                    foreach (var child in tn.Children)
                        Visit(child);
                    PopEffectFromTag(tn);
                }
            }

            foreach (var n in doc.Children) Visit(n);

            // Map through FilterUnsupportedGlyphs rules to filtered string
            string filtered = TextUtils.FilterUnsupportedGlyphs(null, result.OriginalPlain); // will early-return if font is null: treat as identity
            result.FilteredPlain = filtered;
            result.Glyphs = originalChars;

            // Since we passed null font, FilterUnsupportedGlyphs returned input. We want actual font-specific filtering later.
            // Callers should replace FilteredPlain using actual font and then map; here we provide a mapping-aware helper as overload.
            return result;
        }

        public static FlattenedRichText FlattenAndFilter(RichTextDocument doc, SpriteFont font, DialogTextEffectSettings debug)
        {
            var baseFlat = Flatten(doc, debug);
            // Now perform actual filtering with font
            string filtered = TextUtils.FilterUnsupportedGlyphs(font, baseFlat.OriginalPlain ?? string.Empty);
            var mapped = new FlattenedRichText
            {
                OriginalPlain = baseFlat.OriginalPlain,
                FilteredPlain = filtered,
            };

            // Build mapped glyphs
            var outGlyphs = new List<GlyphInfo>();
            int srcIndex = 0;
            int dstIndex = 0;
            while (srcIndex < baseFlat.OriginalPlain.Length)
            {
                char src = baseFlat.OriginalPlain[srcIndex];
                if (src == '\r' || src == '\n') { srcIndex++; continue; }
                var srcGlyph = baseFlatGlyphAt(baseFlat, srcIndex);
                if (src == '…')
                {
                    // maps to "..."
                    if (dstIndex + 3 <= filtered.Length && filtered.Substring(dstIndex, 3) == "...")
                    {
                        outGlyphs.Add(CloneWithChar(srcGlyph, '.'));
                        outGlyphs.Add(CloneWithChar(srcGlyph, '.'));
                        outGlyphs.Add(CloneWithChar(srcGlyph, '.'));
                        dstIndex += 3;
                        srcIndex++;
                        continue;
                    }
                }

                // default 1:1 map
                if (dstIndex < filtered.Length)
                {
                    char dst = filtered[dstIndex];
                    outGlyphs.Add(CloneWithChar(srcGlyph, dst));
                    dstIndex++;
                }
                srcIndex++;
            }
            // In case filtering introduced extra chars (unlikely except spaces), append with last effect
            while (dstIndex < filtered.Length)
            {
                char dst = filtered[dstIndex++];
                outGlyphs.Add(new GlyphInfo { Character = dst, Effects = new List<EffectInstance>(), SpeedFactor = 1f });
            }
            mapped.Glyphs = outGlyphs;
            return mapped;

            static GlyphInfo baseFlatGlyphAt(FlattenedRichText f, int srcIndex)
            {
                // Traverse counting non-newline characters
                int count = 0;
                for (int i = 0; i < f.OriginalPlain.Length && i <= srcIndex; i++)
                {
                    char ch = f.OriginalPlain[i];
                    if (ch == '\r' || ch == '\n') continue;
                    if (i == srcIndex)
                    {
                        return f.Glyphs[count];
                    }
                    count++;
                }
                return new GlyphInfo { Character = ' ', Effects = new List<EffectInstance>(), SpeedFactor = 1f };
            }

            static GlyphInfo CloneWithChar(GlyphInfo g, char c)
            {
                return new GlyphInfo { Character = c, Effects = new List<EffectInstance>(g.Effects), SpeedFactor = g.SpeedFactor };
            }
        }

        private static string Normalize(string s) => s?.Trim().Trim('\"').Trim('\'') ?? string.Empty;

        private static Dictionary<string, float> ToFloatDict(Dictionary<string, string> attrs)
        {
            var d = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in attrs)
            {
                if (float.TryParse(Normalize(kv.Value), out var v)) d[kv.Key] = v;
            }
            return d;
        }

        private static TextEffectType ParseEffectType(string name)
        {
            switch ((name ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "jitter": return TextEffectType.Jitter;
                case "shake": return TextEffectType.Shake;
                case "big": return TextEffectType.Big;
                case "small": return TextEffectType.Small;
                case "explode": return TextEffectType.Explode;
                case "bloom": return TextEffectType.Bloom;
                case "nod": return TextEffectType.Nod;
                case "ripple": return TextEffectType.Ripple;
                case "speed":
                case "fast":
                case "slow": return TextEffectType.Speed;
                default: return TextEffectType.None;
            }
        }
    }

    public static class RichTextLayout
    {
        public static LaidOutText Layout(SpriteFont font, string text, float scale, int maxWidth, int startX, int startY, int lineSpacingPx)
        {
            var laid = new LaidOutText();
            if (font == null)
            {
                return laid;
            }
            var lines = TextUtils.WrapText(font, text ?? string.Empty, scale, maxWidth);
            float lineHeight = font.MeasureString("Mg").Y * scale + lineSpacingPx;
            laid.LineHeightPx = lineHeight;
            laid.LineCount = lines.Count;
            laid.ContentHeightPx = lines.Count * lineHeight;

            int glyphIndex = 0;
            float y = startY;
            foreach (var line in lines)
            {
                // Precompute prefix widths to include kerning
                var prefix = new float[line.Length + 1];
                prefix[0] = 0f;
                for (int i = 1; i <= line.Length; i++)
                {
                    prefix[i] = font.MeasureString(line.Substring(0, i)).X * scale;
                }
                for (int i = 0; i < line.Length; i++)
                {
                    char ch = line[i];
                    var pos = new Vector2(startX + prefix[i], y);
                    laid.GlyphLayouts.Add(new GlyphLayoutInfo { BasePosition = pos, Character = ch });
                    glyphIndex++;
                }
                y += lineHeight;
            }
            return laid;
        }
    }
}

