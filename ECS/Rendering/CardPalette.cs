using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Rendering
{
	public static class CardPalette
	{
		public static readonly Color CostPipRed = new(204, 34, 34);
		public static readonly Color CostPipWhite = Color.White;
		public static readonly Color CostPipBlack = new(19, 19, 19);
		public static readonly Color WeaponTextBackground = new(154, 112, 72);
		public static readonly Color AttackLabelSlabBackground = new(153, 26, 26);
		public static readonly Color AttackLabelSlabText = new(255, 204, 187);
		public static readonly Color AbilityRed = new(196, 30, 58);

		public static Color Background(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(78, 12, 12),
			CardData.CardColor.Black => new Color(19, 19, 19),
			_ => new Color(220, 215, 206),
		};

		public static Color Stripe(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(204, 34, 34),
			CardData.CardColor.Black => new Color(51, 51, 51),
			_ => new Color(153, 153, 153),
		};

		public static Color Gutter(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => Color.Black * 0.22f,
			CardData.CardColor.Black => Color.White * 0.025f,
			_ => Color.Black * 0.05f,
		};

		public static Color NameText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(240, 224, 216),
			CardData.CardColor.Black => new Color(232, 228, 224),
			_ => new Color(26, 26, 26),
		};

		public static Color TypeText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(136, 68, 51),
			CardData.CardColor.Black => new Color(85, 85, 85),
			_ => new Color(153, 153, 153),
		};

		public static Color CostLabel(CardData.CardColor color) => TypeText(color);

		public static Color CostPipAny(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(102, 102, 102),
			CardData.CardColor.Black => new Color(85, 85, 85),
			_ => new Color(160, 152, 136),
		};

		public static Color CostPipOutline(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Black => Color.White,
			_ => Color.Black,
		};

		public static Color RuleLine(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(68, 32, 32),
			CardData.CardColor.Black => new Color(51, 51, 51),
			_ => new Color(192, 184, 170),
		};

		public static Color TextBackground(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(44, 10, 10),
			CardData.CardColor.Black => new Color(8, 8, 8),
			_ => new Color(238, 233, 222),
		};

		public static Color BlockChipBackground(CardData.CardColor color) => color switch
		{
			CardData.CardColor.White => new Color(74, 122, 154),
			_ => new Color(42, 74, 94),
		};

		public static Color BlockChipText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.White => Color.White,
			_ => new Color(176, 212, 232),
		};

		public static Color BlockLabelSlabBackground(CardData.CardColor color) => color switch
		{
			CardData.CardColor.White => new Color(40, 80, 120) * 0.2f,
			_ => new Color(50, 100, 140) * 0.4f,
		};

		public static Color BlockLabelSlabText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.White => new Color(90, 138, 170),
			_ => new Color(138, 184, 216),
		};

		public static Color ActionPointChipBackground(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(51, 14, 14),
			CardData.CardColor.Black => Color.White * 0.15f,
			_ => new Color(68, 68, 68),
		};

		public static Color ActionPointChipText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(221, 68, 51),
			CardData.CardColor.Black => new Color(224, 224, 224),
			_ => new Color(221, 221, 221),
		};

		public static Color ActionPointLabelSlabBackground(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(74, 26, 26),
			CardData.CardColor.Black => Color.White * 0.06f,
			_ => new Color(85, 85, 85),
		};

		public static Color ActionPointLabelSlabText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(187, 102, 85),
			CardData.CardColor.Black => new Color(136, 136, 136),
			_ => new Color(221, 221, 221),
		};

		public static Color FreeChipBorder(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(136, 51, 34),
			CardData.CardColor.Black => Color.White * 0.25f,
			_ => new Color(170, 170, 170),
		};

		public static Color FreeChipText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(204, 85, 68),
			CardData.CardColor.Black => new Color(204, 204, 204),
			_ => new Color(136, 136, 136),
		};

		public static Color FreeLabelSlabText(CardData.CardColor color) => color switch
		{
			CardData.CardColor.Red => new Color(136, 51, 34),
			CardData.CardColor.Black => new Color(136, 136, 136),
			_ => new Color(170, 170, 170),
		};
	}
}
