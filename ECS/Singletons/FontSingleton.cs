using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Singletons
{
	public static class FontSingleton
	{
		private static SpriteFont _titleFont;
		private static SpriteFont _contentFont;
		private static SpriteFont _chakraPetchFont;
		private static SpriteFont _chakraPetchBoldItalicFont;
		private static SpriteFont _grenzeFont;
		private static readonly object _lock = new object();

		public static SpriteFont TitleFont => _titleFont;
		public static SpriteFont ContentFont => _contentFont;
		public static SpriteFont ChakraPetchFont => _chakraPetchFont;
		public static SpriteFont ChakraPetchBoldItalicFont => _chakraPetchBoldItalicFont;
		public static SpriteFont GrenzeFont => _grenzeFont;

		public static void Initialize(ContentManager content)
		{
			lock (_lock)
			{
				_titleFont = content.Load<SpriteFont>("Fonts/NewRocker");
				_contentFont = content.Load<SpriteFont>("Fonts/NewRocker");
				_chakraPetchFont = content.Load<SpriteFont>("Fonts/ChakraPetch");
				_chakraPetchBoldItalicFont = content.Load<SpriteFont>("Fonts/ChakraPetch-BoldItalic");
				_grenzeFont = content.Load<SpriteFont>("Fonts/Grenze");
			}
		}
	}
}
