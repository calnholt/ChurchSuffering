using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation")]
	public class WayStationDisplaySystem : Core.System
	{
		private readonly World _world;
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private readonly Texture2D _background;
		private readonly Texture2D _swordArt;
		private readonly Texture2D _daggerArt;
		private readonly Texture2D _hammerArt;
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;

		private bool _departInProgress;

		private static readonly Color PanelFill = new Color(8, 8, 8) * 0.92f;
		private static readonly Color PanelBorder = Color.White * 0.85f;
		private static readonly Color InsetHighlight = Color.White * 0.08f;
		private static readonly Color FooterFill = Color.Black * 0.25f;
		private static readonly Color FooterBorder = Color.White * 0.12f;
		private static readonly Color ChoiceFill = new Color(30, 30, 30);
		private static readonly Color SelectedFill = new Color(160, 0, 0);
		private static readonly Color SelectedBorder = new Color(196, 30, 58);
		private static readonly Color SelectedGlow = new Color(196, 30, 58) * 0.45f;
		private static readonly Color BodyText = new Color(240, 236, 230);
		private static readonly Color MutedText = new Color(200, 192, 184);

		private const string SwordButtonName = "WayStation_Button_Sword";
		private const string DaggerButtonName = "WayStation_Button_Dagger";
		private const string HammerButtonName = "WayStation_Button_Hammer";
		private const string EasyButtonName = "WayStation_Button_Easy";
		private const string NormalButtonName = "WayStation_Button_Normal";
		private const string HardButtonName = "WayStation_Button_Hard";
		private const string DepartButtonName = "WayStation_Button_Depart";

		[DebugEditable(DisplayName = "Panel Width", Step = 10, Min = 400, Max = 1600)]
		public int PanelWidth { get; set; } = 920;
		[DebugEditable(DisplayName = "Panel Height", Step = 10, Min = 300, Max = 1000)]
		public int PanelHeight { get; set; } = 627;
		[DebugEditable(DisplayName = "Border Thickness", Step = 1, Min = 1, Max = 8)]
		public int BorderThickness { get; set; } = 2;
		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 1, Min = 0, Max = 80)]
		public int ShadowOffsetY { get; set; } = 32;
		[DebugEditable(DisplayName = "Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ShadowAlpha { get; set; } = 0.75f;

		[DebugEditable(DisplayName = "Body Padding X", Step = 2, Min = 0, Max = 120)]
		public int BodyPaddingX { get; set; } = 48;
		[DebugEditable(DisplayName = "Body Padding Top", Step = 2, Min = 0, Max = 120)]
		public int BodyPaddingTop { get; set; } = 40;
		[DebugEditable(DisplayName = "Body Padding Bottom", Step = 2, Min = 0, Max = 120)]
		public int BodyPaddingBottom { get; set; } = 28;
		[DebugEditable(DisplayName = "Footer Height", Step = 2, Min = 40, Max = 220)]
		public int FooterHeight { get; set; } = 113;
		[DebugEditable(DisplayName = "Footer Padding", Step = 2, Min = 0, Max = 80)]
		public int FooterPadding { get; set; } = 24;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float TitleScale { get; set; } = 0.31f;
		[DebugEditable(DisplayName = "Label Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float LabelScale { get; set; } = 0.11f;
		[DebugEditable(DisplayName = "Choice Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float ChoiceScale { get; set; } = 0.13f;
		[DebugEditable(DisplayName = "Weapon Choice Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float WeaponChoiceScale { get; set; } = 0.19f;
		[DebugEditable(DisplayName = "Proceed Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float ProceedScale { get; set; } = 0.22f;

		[DebugEditable(DisplayName = "Red Rule Width", Step = 2, Min = 20, Max = 200)]
		public int RedRuleWidth { get; set; } = 80;
		[DebugEditable(DisplayName = "Red Rule Height", Step = 1, Min = 1, Max = 12)]
		public int RedRuleHeight { get; set; } = 3;
		[DebugEditable(DisplayName = "Title Gap", Step = 2, Min = 0, Max = 80)]
		public int TitleGap { get; set; } = 28;
		[DebugEditable(DisplayName = "Block Gap", Step = 2, Min = 0, Max = 80)]
		public int BlockGap { get; set; } = 28;
		[DebugEditable(DisplayName = "Label Gap", Step = 2, Min = 0, Max = 60)]
		public int LabelGap { get; set; } = 16;
		[DebugEditable(DisplayName = "Choice Gap", Step = 2, Min = 0, Max = 80)]
		public int ChoiceGap { get; set; } = 12;
		[DebugEditable(DisplayName = "Difficulty Choice Gap", Step = 2, Min = 0, Max = 80)]
		public int DifficultyChoiceGap { get; set; } = 16;

		[DebugEditable(DisplayName = "Weapon Button Size", Step = 2, Min = 80, Max = 400)]
		public int WeaponButtonSize { get; set; } = 200;
		[DebugEditable(DisplayName = "Weapon Art Padding", Step = 2, Min = 0, Max = 60)]
		public int WeaponArtPadding { get; set; } = 12;
		[DebugEditable(DisplayName = "Weapon Label Height", Step = 2, Min = 12, Max = 80)]
		public int WeaponLabelHeight { get; set; } = 28;
		[DebugEditable(DisplayName = "Weapon Label Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float WeaponLabelScale { get; set; } = 0.10f;
		[DebugEditable(DisplayName = "Difficulty Row Width", Step = 2, Min = 200, Max = 900)]
		public int DifficultyRowWidth { get; set; } = 520;
		[DebugEditable(DisplayName = "Difficulty Label Offset Y", Step = 2, Min = 200, Max = 560)]
		public int DifficultyLabelOffsetY { get; set; } = 404;
		[DebugEditable(DisplayName = "Difficulty Row Offset Y", Step = 2, Min = 220, Max = 600)]
		public int DifficultyRowOffsetY { get; set; } = 437;
		[DebugEditable(DisplayName = "Difficulty Button Height", Step = 2, Min = 30, Max = 120)]
		public int DifficultyButtonHeight { get; set; } = 52;
		[DebugEditable(DisplayName = "Proceed Button Width", Step = 2, Min = 80, Max = 500)]
		public int ProceedButtonWidth { get; set; } = 220;
		[DebugEditable(DisplayName = "Proceed Button Height", Step = 2, Min = 30, Max = 160)]
		public int ProceedButtonHeight { get; set; } = 64;

		private struct WayStationLayout
		{
			public Rectangle Panel;
			public Rectangle Body;
			public Rectangle Footer;
			public Rectangle Rule;
			public Rectangle SwordButton;
			public Rectangle DaggerButton;
			public Rectangle HammerButton;
			public Rectangle EasyButton;
			public Rectangle NormalButton;
			public Rectangle HardButton;
			public Rectangle DepartButton;
			public Vector2 TitlePosition;
			public Vector2 WeaponLabelPosition;
			public Vector2 DifficultyLabelPosition;
		}

		public WayStationDisplaySystem(World world, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(world.EntityManager)
		{
			_world = world;
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = imageAssets.GetPixel(Color.White);
			_background = imageAssets.GetRequiredTexture("waystation");
			_swordArt = imageAssets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("sword"));
			_daggerArt = imageAssets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("dagger"));
			_hammerArt = imageAssets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("hammer"));
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		private void OnLoadScene(LoadSceneEvent e)
		{
			if (e.Scene == SceneId.WayStation)
			{
				_departInProgress = false;
			}
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.WayStation)
			{
				SetButtonsInteractable(false);
				return;
			}

			var layout = ComputeLayout(Game1.VirtualWidth, Game1.VirtualHeight);
			SyncButton(SwordButtonName, layout.SwordButton);
			SyncButton(DaggerButtonName, layout.DaggerButton);
			SyncButton(HammerButtonName, layout.HammerButton);
			SyncButton(EasyButtonName, layout.EasyButton);
			SyncButton(NormalButtonName, layout.NormalButton);
			SyncButton(HardButtonName, layout.HardButton);
			SyncButton(DepartButtonName, layout.DepartButton);

			if (WasClicked(SwordButtonName)) WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
			if (WasClicked(DaggerButtonName)) WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Dagger;
			if (WasClicked(HammerButtonName)) WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Hammer;
			if (WasClicked(EasyButtonName)) WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
			if (WasClicked(NormalButtonName)) WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Normal;
			if (WasClicked(HardButtonName)) WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Hard;

			if (!_departInProgress && WasClicked(DepartButtonName))
			{
				_departInProgress = true;
				WayStationRunSetupService.Depart(_world);
			}
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || (scene.Current != SceneId.WayStation && scene.Current != SceneId.Snapshot)) return;

			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			var layout = ComputeLayout(vw, vh);

			DrawBackground(vw, vh);
			DrawPanel(layout);
			DrawText(layout);
			DrawButtons(layout);
		}

		private WayStationLayout ComputeLayout(int vw, int vh)
		{
			var panel = new Rectangle((vw - PanelWidth) / 2, (vh - PanelHeight) / 2, PanelWidth, PanelHeight);
			var footer = new Rectangle(panel.X, panel.Bottom - FooterHeight, panel.Width, FooterHeight);
			var body = new Rectangle(panel.X, panel.Y, panel.Width, panel.Height - FooterHeight);

			float cursorY = body.Y + BodyPaddingTop;
			var titleSize = Measure(_titleFont, "Begin the climb", TitleScale);
			var titlePos = new Vector2(panel.Center.X - titleSize.X / 2f, cursorY);
			cursorY += titleSize.Y + TitleGap;

			var rule = new Rectangle(panel.Center.X - RedRuleWidth / 2, (int)System.Math.Round(cursorY), RedRuleWidth, RedRuleHeight);
			cursorY += RedRuleHeight + BlockGap;

			var weaponLabelSize = Measure(_bodyFont, "STARTING WEAPON", LabelScale);
			var weaponLabelPos = new Vector2(panel.Center.X - weaponLabelSize.X / 2f, cursorY);
			cursorY += weaponLabelSize.Y + LabelGap;

			int weaponRowWidth = WeaponButtonSize * 3 + ChoiceGap * 2;
			int weaponX = panel.Center.X - weaponRowWidth / 2;
			var sword = new Rectangle(weaponX, (int)System.Math.Round(cursorY), WeaponButtonSize, WeaponButtonSize);
			var dagger = new Rectangle(sword.Right + ChoiceGap, sword.Y, WeaponButtonSize, WeaponButtonSize);
			var hammer = new Rectangle(dagger.Right + ChoiceGap, sword.Y, WeaponButtonSize, WeaponButtonSize);
			cursorY += WeaponButtonSize + BlockGap;

			var difficultyLabelSize = Measure(_bodyFont, "DIFFICULTY", LabelScale);
			var difficultyLabelPos = new Vector2(panel.Center.X - difficultyLabelSize.X / 2f, panel.Y + DifficultyLabelOffsetY);

			int difficultyButtonWidth = (DifficultyRowWidth - DifficultyChoiceGap * 2) / 3;
			int difficultyX = panel.Center.X - DifficultyRowWidth / 2;
			var easy = new Rectangle(difficultyX, panel.Y + DifficultyRowOffsetY, difficultyButtonWidth, DifficultyButtonHeight);
			var normal = new Rectangle(easy.Right + DifficultyChoiceGap, easy.Y, difficultyButtonWidth, DifficultyButtonHeight);
			var hard = new Rectangle(normal.Right + DifficultyChoiceGap, easy.Y, difficultyButtonWidth, DifficultyButtonHeight);

			var depart = new Rectangle(
				panel.Center.X - ProceedButtonWidth / 2,
				footer.Y + FooterPadding,
				ProceedButtonWidth,
				ProceedButtonHeight);

			return new WayStationLayout
			{
				Panel = panel,
				Body = body,
				Footer = footer,
				Rule = rule,
				SwordButton = sword,
				DaggerButton = dagger,
				HammerButton = hammer,
				EasyButton = easy,
				NormalButton = normal,
				HardButton = hard,
				DepartButton = depart,
				TitlePosition = titlePos,
				WeaponLabelPosition = weaponLabelPos,
				DifficultyLabelPosition = difficultyLabelPos
			};
		}

		private void DrawBackground(int vw, int vh)
		{
			var src = ComputeCoverSource(_background, vw, vh);
			_spriteBatch.Draw(_background, new Rectangle(0, 0, vw, vh), src, Color.White);
		}

		private static Rectangle ComputeCoverSource(Texture2D texture, int targetWidth, int targetHeight)
		{
			float targetAspect = targetWidth / (float)targetHeight;
			float textureAspect = texture.Width / (float)texture.Height;
			if (textureAspect > targetAspect)
			{
				int sourceWidth = (int)System.Math.Round(texture.Height * targetAspect);
				return new Rectangle((texture.Width - sourceWidth) / 2, 0, sourceWidth, texture.Height);
			}

			int sourceHeight = (int)System.Math.Round(texture.Width / targetAspect);
			return new Rectangle(0, (texture.Height - sourceHeight) / 2, texture.Width, sourceHeight);
		}

		private void DrawPanel(WayStationLayout layout)
		{
			var shadow = new Rectangle(
				layout.Panel.X,
				layout.Panel.Y + ShadowOffsetY,
				layout.Panel.Width,
				System.Math.Max(1, layout.Panel.Height - ShadowOffsetY));
			_spriteBatch.Draw(_pixel, shadow, Color.Black * MathHelper.Clamp(ShadowAlpha, 0f, 1f));
			_spriteBatch.Draw(_pixel, layout.Panel, PanelFill);
			_spriteBatch.Draw(_pixel, layout.Footer, FooterFill);
			DrawHorizontalLine(layout.Footer.X, layout.Footer.Y, layout.Footer.Width, FooterBorder, 1);
			DrawBorder(layout.Panel, PanelBorder, BorderThickness);
			DrawBorder(new Rectangle(layout.Panel.X + 1, layout.Panel.Y + 1, layout.Panel.Width - 2, layout.Panel.Height - 2), InsetHighlight, 1);
			DrawGradientRule(layout.Rule);
		}

		private void DrawText(WayStationLayout layout)
		{
			DrawStringWithShadow(_titleFont, "Begin the climb", layout.TitlePosition, Color.White, TitleScale);
			_spriteBatch.DrawString(_bodyFont, "STARTING WEAPON", layout.WeaponLabelPosition, MutedText, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_bodyFont, "DIFFICULTY", layout.DifficultyLabelPosition, MutedText, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);
		}

		private void DrawButtons(WayStationLayout layout)
		{
			DrawWeaponChoiceButton(layout.SwordButton, _swordArt, "Sword", IsSelected(StartingWeapon.Sword), IsHovered(SwordButtonName));
			DrawWeaponChoiceButton(layout.DaggerButton, _daggerArt, "Dagger", IsSelected(StartingWeapon.Dagger), IsHovered(DaggerButtonName));
			DrawWeaponChoiceButton(layout.HammerButton, _hammerArt, "Hammer", IsSelected(StartingWeapon.Hammer), IsHovered(HammerButtonName));
			DrawChoiceButton(layout.EasyButton, "Easy", ChoiceScale, IsSelected(RunDifficulty.Easy), IsHovered(EasyButtonName));
			DrawChoiceButton(layout.NormalButton, "Normal", ChoiceScale, IsSelected(RunDifficulty.Normal), IsHovered(NormalButtonName));
			DrawChoiceButton(layout.HardButton, "Hard", ChoiceScale, IsSelected(RunDifficulty.Hard), IsHovered(HardButtonName));
			DrawProceedButton(layout.DepartButton, IsHovered(DepartButtonName));
		}

		private void DrawChoiceButton(Rectangle rect, string label, float scale, bool selected, bool hovered)
		{
			if (selected)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), SelectedGlow);
			}

			var fill = selected ? SelectedFill : ChoiceFill;
			var border = selected ? SelectedBorder : (hovered ? Color.White : Color.White * 0.5f);
			var text = selected || hovered ? Color.White : BodyText;

			_spriteBatch.Draw(_pixel, rect, fill);
			DrawBorder(rect, border, 2);
			DrawCenteredString(_bodyFont, label, rect, text, scale);
		}

		private void DrawWeaponChoiceButton(Rectangle rect, Texture2D art, string label, bool selected, bool hovered)
		{
			if (selected)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), SelectedGlow);
			}

			var fill = selected ? SelectedFill : ChoiceFill;
			var border = selected ? SelectedBorder : (hovered ? Color.White : Color.White * 0.5f);
			var text = selected || hovered ? Color.White : BodyText;
			var artTint = selected || hovered ? Color.White : BodyText;

			_spriteBatch.Draw(_pixel, rect, fill);
			DrawBorder(rect, border, 2);

			int padding = System.Math.Max(0, WeaponArtPadding);
			int labelHeight = System.Math.Max(12, WeaponLabelHeight);
			var artBounds = new Rectangle(
				rect.X + padding,
				rect.Y + padding,
				System.Math.Max(1, rect.Width - padding * 2),
				System.Math.Max(1, rect.Height - padding * 2 - labelHeight));
			if (art != null)
			{
				DrawTextureFitted(artBounds, art, artTint);
			}

			var labelRect = new Rectangle(rect.X, rect.Bottom - labelHeight, rect.Width, labelHeight);
			DrawCenteredString(_bodyFont, label, labelRect, text, WeaponLabelScale);
		}

		private void DrawTextureFitted(Rectangle bounds, Texture2D texture, Color tint)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0 || texture == null) return;

			float scale = System.Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
			int drawW = System.Math.Max(1, (int)System.Math.Round(texture.Width * scale));
			int drawH = System.Math.Max(1, (int)System.Math.Round(texture.Height * scale));
			var dst = new Rectangle(
				bounds.X + (bounds.Width - drawW) / 2,
				bounds.Y + (bounds.Height - drawH) / 2,
				drawW,
				drawH);
			_spriteBatch.Draw(texture, dst, tint);
		}

		private void DrawProceedButton(Rectangle rect, bool hovered)
		{
			var fill = hovered ? SelectedFill : ChoiceFill;
			var border = hovered ? SelectedBorder : Color.White;
			if (hovered)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), SelectedGlow);
			}

			_spriteBatch.Draw(_pixel, rect, fill);
			DrawBorder(rect, border, 2);
			DrawCenteredString(_titleFont, "Depart", rect, Color.White, ProceedScale);
		}

		private void DrawGradientRule(Rectangle rect)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			for (int i = 0; i < rect.Width; i++)
			{
				float t = rect.Width <= 1 ? 1f : i / (float)(rect.Width - 1);
				float alpha = t <= 0.5f ? t * 2f : (1f - t) * 2f;
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X + i, rect.Y, 1, rect.Height), SelectedBorder * alpha);
			}
		}

		private void DrawStringWithShadow(SpriteFont font, string text, Vector2 pos, Color color, float scale)
		{
			_spriteBatch.DrawString(font, text, pos + new Vector2(0, 2), Color.Black * 0.8f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawCenteredString(SpriteFont font, string text, Rectangle rect, Color color, float scale)
		{
			var size = Measure(font, text, scale);
			var pos = new Vector2(
				rect.Center.X - size.X / 2f,
				rect.Center.Y - size.Y / 2f);
			_spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private static Vector2 Measure(SpriteFont font, string text, float scale)
		{
			return font.MeasureString(text ?? string.Empty) * scale;
		}

		private void DrawBorder(Rectangle rect, Color color, int thickness)
		{
			thickness = System.Math.Max(1, thickness);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private void DrawHorizontalLine(int x, int y, int width, Color color, int thickness)
		{
			_spriteBatch.Draw(_pixel, new Rectangle(x, y, width, System.Math.Max(1, thickness)), color);
		}

		private void SyncButton(string name, Rectangle bounds)
		{
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform { Position = new Vector2(bounds.X, bounds.Y), ZOrder = 10000 });
				EntityManager.AddComponent(entity, new UIElement
				{
					Bounds = bounds,
					IsInteractable = true,
					LayerType = UILayerType.Overlay,
					TooltipType = TooltipType.None
				});
				return;
			}

			var transform = entity.GetComponent<Transform>();
			if (transform == null)
			{
				EntityManager.AddComponent(entity, new Transform { Position = new Vector2(bounds.X, bounds.Y), ZOrder = 10000 });
			}
			else
			{
				transform.Position = new Vector2(bounds.X, bounds.Y);
				transform.ZOrder = 10000;
			}

			var ui = entity.GetComponent<UIElement>();
			if (ui == null)
			{
				EntityManager.AddComponent(entity, new UIElement { Bounds = bounds, IsInteractable = true, LayerType = UILayerType.Overlay, TooltipType = TooltipType.None });
			}
			else
			{
				ui.Bounds = bounds;
				ui.IsInteractable = true;
				ui.IsHidden = false;
				ui.LayerType = UILayerType.Overlay;
				ui.TooltipType = TooltipType.None;
			}
		}

		private void SetButtonsInteractable(bool interactable)
		{
			foreach (var name in ButtonNames())
			{
				var ui = EntityManager.GetEntity(name)?.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.IsInteractable = interactable;
				ui.IsHidden = !interactable;
			}
		}

		private static IEnumerable<string> ButtonNames()
		{
			yield return SwordButtonName;
			yield return DaggerButtonName;
			yield return HammerButtonName;
			yield return EasyButtonName;
			yield return NormalButtonName;
			yield return HardButtonName;
			yield return DepartButtonName;
		}

		private bool WasClicked(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsClicked == true;
		}

		private bool IsHovered(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsHovered == true;
		}

		private static bool IsSelected(StartingWeapon weapon)
		{
			return WayStationRunSetupSingleton.SelectedWeapon == weapon;
		}

		private static bool IsSelected(RunDifficulty difficulty)
		{
			return WayStationRunSetupSingleton.SelectedDifficulty == difficulty;
		}
	}
}
