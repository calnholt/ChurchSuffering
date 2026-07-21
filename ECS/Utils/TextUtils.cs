using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Utils
{
	public static class TextUtils
	{
        public static string FilterUnsupportedGlyphs(SpriteFont font, string text)
        {
            if (font == null || string.IsNullOrEmpty(text)) return text ?? string.Empty;
            var allowed = font.Characters;
            var sb = new StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (ch == '\r' || ch == '\n') { sb.Append(ch); continue; }
                if (allowed.Contains(ch)) { sb.Append(ch); continue; }
                // Common replacements
                switch (ch)
                {
                    case '—':
                    case '–': sb.Append('-'); break;
                    case '…': sb.Append("..."); break;
                    case '’':
                    case '‘': sb.Append('\''); break;
                    case '“':
                    case '”': sb.Append('"'); break;
                    default: sb.Append(' '); break;
                }
            }
            return sb.ToString();
        }

		public static List<string> WrapText(SpriteFont font, string text, float scale, int maxWidth)
		{
			var lines = new List<string>();
			if (font == null)
			{
				lines.Add(string.Empty);
				return lines;
			}
            if (string.IsNullOrWhiteSpace(text))
			{
				lines.Add(string.Empty);
				return lines;
			}
            string safeText = FilterUnsupportedGlyphs(font, text);
            string[] rawLines = safeText.Replace("\r", string.Empty).Split('\n');
			foreach (var raw in rawLines)
			{
				string line = string.Empty;
				foreach (var word in raw.Split(' '))
				{
					string candidate = string.IsNullOrEmpty(line) ? word : (line + " " + word);
					float width = font.MeasureString(candidate).X * scale;
					if (width > maxWidth && !string.IsNullOrEmpty(line))
					{
						lines.Add(line);
						line = word;
					}
					else
					{
						line = candidate;
					}
				}
				lines.Add(line);
			}
			return lines;
		}
	}
}


