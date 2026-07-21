using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Data.VisualEffects
{
	public readonly record struct VisualEffectColors(
		Color Primary,
		Color Highlight,
		Color Shadow,
		Color Smoke,
		Color Glow);

	public static class VisualEffectPaletteResolver
	{
		public static VisualEffectColors Resolve(VisualEffectPalette palette)
		{
			return palette switch
			{
				VisualEffectPalette.Holy => Colors(217, 182, 99, 255, 250, 224, 67, 48, 29, 170, 146, 94, 255, 238, 167),
				VisualEffectPalette.Blood => Colors(188, 24, 48, 255, 92, 103, 45, 6, 15, 92, 20, 34, 235, 37, 68),
				VisualEffectPalette.Fire => Colors(238, 74, 28, 255, 215, 83, 72, 18, 8, 101, 42, 23, 255, 133, 36),
				VisualEffectPalette.Ice => Colors(74, 180, 232, 220, 249, 255, 17, 51, 79, 73, 122, 145, 130, 225, 255),
				VisualEffectPalette.Shadow => Colors(119, 58, 183, 222, 160, 255, 16, 8, 28, 48, 34, 64, 162, 70, 224),
				VisualEffectPalette.Earth => Colors(173, 117, 52, 244, 207, 136, 50, 35, 27, 91, 72, 55, 207, 148, 65),
				VisualEffectPalette.Poison => Colors(103, 180, 67, 220, 255, 147, 18, 49, 22, 60, 91, 51, 133, 222, 75),
				VisualEffectPalette.Arcane => Colors(54, 132, 225, 217, 191, 255, 21, 37, 83, 57, 63, 112, 104, 190, 255),
				_ => Colors(199, 34, 50, 255, 245, 223, 29, 23, 25, 93, 77, 79, 239, 52, 72)
			};
		}

		private static VisualEffectColors Colors(
			byte pr, byte pg, byte pb,
			byte hr, byte hg, byte hb,
			byte sr, byte sg, byte sb,
			byte mr, byte mg, byte mb,
			byte gr, byte gg, byte gb)
		{
			return new VisualEffectColors(
				new Color(pr, pg, pb),
				new Color(hr, hg, hb),
				new Color(sr, sg, sb),
				new Color(mr, mg, mb),
				new Color(gr, gg, gb));
		}
	}
}
