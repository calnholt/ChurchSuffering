using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation Climb Modal")]
	public class WayStationClimbSettingsModalSystem : Core.System
	{
		private readonly World _world;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
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
		[DebugEditable(DisplayName = "Body Padding Top", Step = 2, Min = 0, Max = 120)]
		public int BodyPaddingTop { get; set; } = 40;
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
		[DebugEditable(DisplayName = "Close Button Size", Step = 2, Min = 24, Max = 96)]
		public int CloseButtonSize { get; set; } = 44;
		[DebugEditable(DisplayName = "Close Button Inset", Step = 1, Min = 4, Max = 80)]
		public int CloseButtonInset { get; set; } = 18;

		private struct WayStationLayout
		{
			public Rectangle Panel;
			public Rectangle Body;
			public Rectangle Footer;
			public Rectangle Rule;
			public Rectangle CloseButton;
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

		public WayStationClimbSettingsModalSystem(
			World world,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(world.EntityManager)
		{
			_world = world;
			_spriteBatch = spriteBatch;
			_pixel = imageAssets.GetPixel(Color.White);
			_swordArt = imageAssets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("sword"));
			_daggerArt = imageAssets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("dagger"));
			_hammerArt = imageAssets.GetRequiredTexture(CrusaderPortraitAssets.ResolveWeaponCardArtAsset("hammer"));
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<OpenWayStationClimbSettingsModalEvent>(OnOpenClimbSettingsModal);
		}

		private void OnLoadScene(LoadSceneEvent e)
		{
			if (e.Scene != SceneId.WayStation) return;
			_departInProgress = false;
			CloseModal(immediate: true);
		}

		private void OnOpenClimbSettingsModal(OpenWayStationClimbSettingsModalEvent e)
		{
			if (!IsWayStationActive()) return;
			var meta = SaveCache.GetWayStationMeta();
			if (!ClimbUnlockProgressionRules.ShouldShowSettingsModal(meta))
			{
				if (_departInProgress) return;
				WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
				WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
				_departInProgress = true;
				WayStationRunSetupService.Depart(_world);
				return;
			}

			NormalizeSelection(meta);
			OpenModal();
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
				CloseModal(immediate: true);
				SetButtonsInteractable(false);
				return;
			}

			EnsureModalRoot();

			var animation = GetModalAnimation();
			bool modalInteractive = animation?.Phase == ModalAnimationPhase.Visible;
			bool modalOpen = animation != null && (animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden);
			var meta = SaveCache.GetWayStationMeta();
			NormalizeSelection(meta);
			var layout = ComputeLayout(Game1.VirtualWidth, Game1.VirtualHeight, meta);
			var render = ModalAnimationRenderState.From(animation, layout.Panel);

			SyncModalPanel(render.Transform(layout.Panel), modalOpen);
			SyncButton(WayStationSceneConstants.CloseButtonName, render.Transform(layout.CloseButton), modalInteractive);
			SyncChoiceButton(WayStationSceneConstants.SwordButtonName, layout.SwordButton, render, modalInteractive);
			SyncChoiceButton(WayStationSceneConstants.DaggerButtonName, layout.DaggerButton, render, modalInteractive);
			SyncChoiceButton(WayStationSceneConstants.HammerButtonName, layout.HammerButton, render, modalInteractive);
			SyncChoiceButton(WayStationSceneConstants.EasyButtonName, layout.EasyButton, render, modalInteractive);
			SyncChoiceButton(WayStationSceneConstants.NormalButtonName, layout.NormalButton, render, modalInteractive);
			SyncChoiceButton(WayStationSceneConstants.HardButtonName, layout.HardButton, render, modalInteractive);
			SyncButton(WayStationSceneConstants.DepartButtonName, render.Transform(layout.DepartButton), modalInteractive);

			if (!modalInteractive) return;

			if (WasClicked(WayStationSceneConstants.ModalRootName) || WasClicked(WayStationSceneConstants.CloseButtonName))
			{
				CloseModal();
				return;
			}

			if (WasClicked(WayStationSceneConstants.SwordButtonName)) SelectWeapon(meta, StartingWeapon.Sword);
			if (WasClicked(WayStationSceneConstants.DaggerButtonName)) SelectWeapon(meta, StartingWeapon.Dagger);
			if (WasClicked(WayStationSceneConstants.HammerButtonName)) SelectWeapon(meta, StartingWeapon.Hammer);
			if (WasClicked(WayStationSceneConstants.EasyButtonName)) SelectDifficulty(meta, RunDifficulty.Easy);
			if (WasClicked(WayStationSceneConstants.NormalButtonName)) SelectDifficulty(meta, RunDifficulty.Normal);
			if (WasClicked(WayStationSceneConstants.HardButtonName)) SelectDifficulty(meta, RunDifficulty.Hard);

			if (!_departInProgress && WasClicked(WayStationSceneConstants.DepartButtonName))
			{
				NormalizeSelection(meta);
				_departInProgress = true;
				WayStationRunSetupService.Depart(_world);
			}
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			if (scene == null || (scene.Current != SceneId.WayStation && scene.Current != SceneId.Snapshot)) return;

			var layout = ComputeLayout(Game1.VirtualWidth, Game1.VirtualHeight, SaveCache.GetWayStationMeta());
			var animation = GetModalAnimation();
			if (animation == null) return;
			var render = ModalAnimationRenderState.From(animation, layout.Panel);
			if (!render.ShouldDraw) return;

			ModalOverlayChrome.DrawDim(_spriteBatch, _pixel, Game1.VirtualWidth, Game1.VirtualHeight, (int)(178 * render.DimAlphaMultiplier));
			DrawPanel(layout, render);
			DrawText(layout, render);
			DrawButtons(layout, render);
		}

		private WayStationLayout ComputeLayout(int vw, int vh, WayStationMetaSave meta)
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

			var unlockedWeapons = ClimbUnlockProgressionRules.GetUnlockedWeapons(meta);
			int weaponRowWidth = WeaponButtonSize * unlockedWeapons.Count + ChoiceGap * System.Math.Max(0, unlockedWeapons.Count - 1);
			int weaponX = panel.Center.X - weaponRowWidth / 2;
			int weaponY = (int)System.Math.Round(cursorY);
			var sword = Rectangle.Empty;
			var dagger = Rectangle.Empty;
			var hammer = Rectangle.Empty;
			for (int i = 0; i < unlockedWeapons.Count; i++)
			{
				var rect = new Rectangle(weaponX + i * (WeaponButtonSize + ChoiceGap), weaponY, WeaponButtonSize, WeaponButtonSize);
				switch (unlockedWeapons[i])
				{
					case StartingWeapon.Sword: sword = rect; break;
					case StartingWeapon.Dagger: dagger = rect; break;
					case StartingWeapon.Hammer: hammer = rect; break;
				}
			}

			var difficultyLabelSize = Measure(_bodyFont, "DIFFICULTY", LabelScale);
			var difficultyLabelPos = new Vector2(panel.Center.X - difficultyLabelSize.X / 2f, panel.Y + DifficultyLabelOffsetY);

			int difficultyButtonWidth = (DifficultyRowWidth - DifficultyChoiceGap * 2) / 3;
			var unlockedDifficulties = ClimbUnlockProgressionRules.GetUnlockedDifficulties(meta, WayStationRunSetupSingleton.SelectedWeapon);
			int difficultyWidth = difficultyButtonWidth * unlockedDifficulties.Count
				+ DifficultyChoiceGap * System.Math.Max(0, unlockedDifficulties.Count - 1);
			int difficultyX = panel.Center.X - difficultyWidth / 2;
			int difficultyY = panel.Y + DifficultyRowOffsetY;
			var easy = Rectangle.Empty;
			var normal = Rectangle.Empty;
			var hard = Rectangle.Empty;
			for (int i = 0; i < unlockedDifficulties.Count; i++)
			{
				var rect = new Rectangle(difficultyX + i * (difficultyButtonWidth + DifficultyChoiceGap), difficultyY, difficultyButtonWidth, DifficultyButtonHeight);
				switch (unlockedDifficulties[i])
				{
					case RunDifficulty.Easy: easy = rect; break;
					case RunDifficulty.Normal: normal = rect; break;
					case RunDifficulty.Hard: hard = rect; break;
				}
			}

			var depart = new Rectangle(
				panel.Center.X - ProceedButtonWidth / 2,
				footer.Y + FooterPadding,
				ProceedButtonWidth,
				ProceedButtonHeight);
			var close = new Rectangle(
				panel.Right - CloseButtonInset - CloseButtonSize,
				panel.Y + CloseButtonInset,
				CloseButtonSize,
				CloseButtonSize);

			return new WayStationLayout
			{
				Panel = panel,
				Body = body,
				Footer = footer,
				Rule = rule,
				CloseButton = close,
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

		private void DrawPanel(WayStationLayout layout, ModalAnimationRenderState render)
		{
			var panel = render.Transform(layout.Panel);
			var footer = render.Transform(layout.Footer);
			var rule = render.Transform(layout.Rule);
			var shadow = new Rectangle(
				panel.X,
				panel.Y + (int)System.Math.Round(ShadowOffsetY * render.ShellScale),
				panel.Width,
				System.Math.Max(1, panel.Height - (int)System.Math.Round(ShadowOffsetY * render.ShellScale)));
			_spriteBatch.Draw(_pixel, shadow, render.ApplyShadow(Color.Black * MathHelper.Clamp(ShadowAlpha, 0f, 1f)));
			_spriteBatch.Draw(_pixel, panel, render.ApplyShell(PanelFill));
			_spriteBatch.Draw(_pixel, footer, render.ApplyShell(FooterFill));
			DrawHorizontalLine(footer.X, footer.Y, footer.Width, render.ApplyShell(FooterBorder), 1);
			DrawBorder(panel, render.ApplyShell(PanelBorder), BorderThickness);
			DrawBorder(new Rectangle(panel.X + 1, panel.Y + 1, panel.Width - 2, panel.Height - 2), render.ApplyShell(InsetHighlight), 1);
			DrawGradientRule(rule, render);
		}

		private void DrawText(WayStationLayout layout, ModalAnimationRenderState render)
		{
			DrawStringWithShadow(_titleFont, "Begin the climb", render.Transform(layout.TitlePosition), render.ApplyShell(Color.White), render.TransformScale(TitleScale));
			_spriteBatch.DrawString(_bodyFont, "STARTING WEAPON", render.Transform(layout.WeaponLabelPosition), render.ApplyShell(MutedText), 0f, Vector2.Zero, render.TransformScale(LabelScale), SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_bodyFont, "DIFFICULTY", render.Transform(layout.DifficultyLabelPosition), render.ApplyShell(MutedText), 0f, Vector2.Zero, render.TransformScale(LabelScale), SpriteEffects.None, 0f);
		}

		private void DrawButtons(WayStationLayout layout, ModalAnimationRenderState render)
		{
			DrawCloseButton(render.Transform(layout.CloseButton), IsHovered(WayStationSceneConstants.CloseButtonName), render);
			if (!layout.SwordButton.IsEmpty) DrawWeaponChoiceButton(render.Transform(layout.SwordButton), _swordArt, "Sword", IsSelected(StartingWeapon.Sword), IsHovered(WayStationSceneConstants.SwordButtonName), render);
			if (!layout.DaggerButton.IsEmpty) DrawWeaponChoiceButton(render.Transform(layout.DaggerButton), _daggerArt, "Dagger", IsSelected(StartingWeapon.Dagger), IsHovered(WayStationSceneConstants.DaggerButtonName), render);
			if (!layout.HammerButton.IsEmpty) DrawWeaponChoiceButton(render.Transform(layout.HammerButton), _hammerArt, "Hammer", IsSelected(StartingWeapon.Hammer), IsHovered(WayStationSceneConstants.HammerButtonName), render);
			if (!layout.EasyButton.IsEmpty) DrawChoiceButton(render.Transform(layout.EasyButton), "Easy", render.TransformScale(ChoiceScale), IsSelected(RunDifficulty.Easy), IsHovered(WayStationSceneConstants.EasyButtonName), render);
			if (!layout.NormalButton.IsEmpty) DrawChoiceButton(render.Transform(layout.NormalButton), "Normal", render.TransformScale(ChoiceScale), IsSelected(RunDifficulty.Normal), IsHovered(WayStationSceneConstants.NormalButtonName), render);
			if (!layout.HardButton.IsEmpty) DrawChoiceButton(render.Transform(layout.HardButton), "Hard", render.TransformScale(ChoiceScale), IsSelected(RunDifficulty.Hard), IsHovered(WayStationSceneConstants.HardButtonName), render);
			DrawProceedButton(render.Transform(layout.DepartButton), IsHovered(WayStationSceneConstants.DepartButtonName), render);
		}

		private void DrawCloseButton(Rectangle rect, bool hovered, ModalAnimationRenderState render)
		{
			var fill = hovered ? SelectedFill : ChoiceFill;
			var border = hovered ? SelectedBorder : Color.White;
			_spriteBatch.Draw(_pixel, rect, render.ApplyShell(fill));
			DrawBorder(rect, render.ApplyShell(border), 2);
			DrawCenteredString(_titleFont, "X", rect, render.ApplyShell(Color.White), render.TransformScale(0.2f));
		}

		private void DrawChoiceButton(Rectangle rect, string label, float scale, bool selected, bool hovered, ModalAnimationRenderState render)
		{
			if (selected)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), render.ApplyShell(SelectedGlow));
			}

			var fill = selected ? SelectedFill : ChoiceFill;
			var border = selected ? SelectedBorder : (hovered ? Color.White : Color.White * 0.5f);
			var text = selected || hovered ? Color.White : BodyText;
			_spriteBatch.Draw(_pixel, rect, render.ApplyShell(fill));
			DrawBorder(rect, render.ApplyShell(border), 2);
			DrawCenteredString(_bodyFont, label, rect, render.ApplyShell(text), scale);
		}

		private void DrawWeaponChoiceButton(Rectangle rect, Texture2D art, string label, bool selected, bool hovered, ModalAnimationRenderState render)
		{
			if (selected)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), render.ApplyShell(SelectedGlow));
			}

			var fill = selected ? SelectedFill : ChoiceFill;
			var border = selected ? SelectedBorder : (hovered ? Color.White : Color.White * 0.5f);
			var text = selected || hovered ? Color.White : BodyText;
			var artTint = selected || hovered ? Color.White : BodyText;

			_spriteBatch.Draw(_pixel, rect, render.ApplyShell(fill));
			DrawBorder(rect, render.ApplyShell(border), 2);

			int padding = System.Math.Max(0, (int)System.Math.Round(WeaponArtPadding * render.ShellScale));
			int labelHeight = System.Math.Max(12, (int)System.Math.Round(WeaponLabelHeight * render.ShellScale));
			var artBounds = new Rectangle(
				rect.X + padding,
				rect.Y + padding,
				System.Math.Max(1, rect.Width - padding * 2),
				System.Math.Max(1, rect.Height - padding * 2 - labelHeight));
			DrawTextureFitted(artBounds, art, render.ApplyShell(artTint));

			var labelRect = new Rectangle(rect.X, rect.Bottom - labelHeight, rect.Width, labelHeight);
			DrawCenteredString(_bodyFont, label, labelRect, render.ApplyShell(text), render.TransformScale(WeaponLabelScale));
		}

		private void DrawProceedButton(Rectangle rect, bool hovered, ModalAnimationRenderState render)
		{
			var fill = hovered ? SelectedFill : ChoiceFill;
			var border = hovered ? SelectedBorder : Color.White;
			if (hovered)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), render.ApplyShell(SelectedGlow));
			}

			_spriteBatch.Draw(_pixel, rect, render.ApplyShell(fill));
			DrawBorder(rect, render.ApplyShell(border), 2);
			DrawCenteredString(_titleFont, "Depart", rect, render.ApplyShell(Color.White), render.TransformScale(ProceedScale));
		}

		private void DrawTextureFitted(Rectangle bounds, Texture2D texture, Color tint)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0 || texture == null) return;
			float scale = System.Math.Min(bounds.Width / (float)texture.Width, bounds.Height / (float)texture.Height);
			int drawW = System.Math.Max(1, (int)System.Math.Round(texture.Width * scale));
			int drawH = System.Math.Max(1, (int)System.Math.Round(texture.Height * scale));
			var dst = new Rectangle(bounds.X + (bounds.Width - drawW) / 2, bounds.Y + (bounds.Height - drawH) / 2, drawW, drawH);
			_spriteBatch.Draw(texture, dst, tint);
		}

		private void DrawGradientRule(Rectangle rect, ModalAnimationRenderState render)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			for (int i = 0; i < rect.Width; i++)
			{
				float t = rect.Width <= 1 ? 1f : i / (float)(rect.Width - 1);
				float alpha = t <= 0.5f ? t * 2f : (1f - t) * 2f;
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X + i, rect.Y, 1, rect.Height), render.ApplyShell(SelectedBorder * alpha));
			}
		}

		private void DrawStringWithShadow(SpriteFont font, string text, Vector2 pos, Color color, float scale)
		{
			_spriteBatch.DrawString(font, text, pos + new Vector2(0, 2), Color.Black * (0.8f * color.A / 255f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawCenteredString(SpriteFont font, string text, Rectangle rect, Color color, float scale)
		{
			var size = Measure(font, text, scale);
			var pos = new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f);
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

		private void EnsureModalRoot()
		{
			var root = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName);
			if (root == null)
			{
				root = EntityManager.CreateEntity(WayStationSceneConstants.ModalRootName);
				EntityManager.AddComponent(root, new Transform { Position = Vector2.Zero, ZOrder = 10000 });
				EntityManager.AddComponent(root, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					IsHidden = true,
					LayerType = UILayerType.Overlay,
					TooltipType = TooltipType.None,
					ShowHoverHighlight = false
				});
				EntityManager.AddComponent(root, new ModalAnimation { InputContextId = WayStationSceneConstants.ModalContextId });
				EntityManager.AddComponent(root, new WayStationClimbModalRoot());
				InputContextService.EnsureContext(EntityManager, root, WayStationSceneConstants.ModalContextId, 100, false);
			}

			InputContextService.EnsureContext(
				EntityManager,
				root,
				WayStationSceneConstants.ModalContextId,
				100,
				root.GetComponent<ModalAnimation>()?.Phase != ModalAnimationPhase.Hidden);
		}

		private void SyncModalPanel(Rectangle bounds, bool visible)
		{
			var panel = EntityManager.GetEntity(WayStationSceneConstants.ModalPanelName);
			if (panel == null)
			{
				panel = EntityManager.CreateEntity(WayStationSceneConstants.ModalPanelName);
				EntityManager.AddComponent(panel, new Transform());
				EntityManager.AddComponent(panel, new UIElement { TooltipType = TooltipType.None, ShowHoverHighlight = false });
				EntityManager.AddComponent(panel, new WayStationClimbModalPanel());
				InputContextService.EnsureMember(EntityManager, panel, WayStationSceneConstants.ModalContextId);
			}

			var transform = panel.GetComponent<Transform>();
			transform.Position = new Vector2(bounds.X, bounds.Y);
			transform.ZOrder = 10001;
			var ui = panel.GetComponent<UIElement>();
			ui.Bounds = bounds;
			ui.IsInteractable = visible;
			ui.IsHidden = !visible;
			ui.LayerType = UILayerType.Overlay;
			InputContextService.EnsureMember(EntityManager, panel, WayStationSceneConstants.ModalContextId);
		}

		private void SyncButton(string name, Rectangle bounds, bool interactable)
		{
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement { TooltipType = TooltipType.None });
				AddModalMarker(entity, name);
			}

			var transform = entity.GetComponent<Transform>();
			transform.Position = new Vector2(bounds.X, bounds.Y);
			transform.ZOrder = 10002;

			var ui = entity.GetComponent<UIElement>();
			ui.Bounds = bounds;
			ui.IsInteractable = interactable;
			ui.IsHidden = !interactable;
			ui.LayerType = UILayerType.Overlay;
			ui.TooltipType = TooltipType.None;
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.ModalContextId);
		}

		private void SyncChoiceButton(
			string name,
			Rectangle bounds,
			ModalAnimationRenderState render,
			bool modalInteractive)
		{
			bool unlocked = !bounds.IsEmpty;
			SyncButton(name, unlocked ? render.Transform(bounds) : Rectangle.Empty, modalInteractive && unlocked);
		}

		private void AddModalMarker(Entity entity, string name)
		{
			if (name == WayStationSceneConstants.CloseButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalCloseButton());
			else if (name == WayStationSceneConstants.DepartButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalDepartButton());
			else if (name == WayStationSceneConstants.SwordButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalWeaponChoice { Weapon = StartingWeapon.Sword });
			else if (name == WayStationSceneConstants.DaggerButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalWeaponChoice { Weapon = StartingWeapon.Dagger });
			else if (name == WayStationSceneConstants.HammerButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalWeaponChoice { Weapon = StartingWeapon.Hammer });
			else if (name == WayStationSceneConstants.EasyButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalDifficultyChoice { Difficulty = RunDifficulty.Easy });
			else if (name == WayStationSceneConstants.NormalButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalDifficultyChoice { Difficulty = RunDifficulty.Normal });
			else if (name == WayStationSceneConstants.HardButtonName) EntityManager.AddComponent(entity, new WayStationClimbModalDifficultyChoice { Difficulty = RunDifficulty.Hard });
		}

		private void OpenModal()
		{
			EnsureModalRoot();
			var animation = GetModalAnimation();
			if (animation == null) return;
			animation.RequestedVisible = true;
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.ClimbMenuEnter, Volume = 0.5f });
		}

		private void CloseModal(bool immediate = false)
		{
			var animation = GetModalAnimation();
			if (animation == null) return;
			animation.RequestedVisible = false;
			if (!immediate) return;
			animation.Phase = ModalAnimationPhase.Hidden;
			animation.ElapsedSeconds = 0f;
			var root = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName);
			var context = root?.GetComponent<InputContext>();
			if (context != null) context.IsActive = false;
			var rootUi = root?.GetComponent<UIElement>();
			if (rootUi != null)
			{
				rootUi.Bounds = Rectangle.Empty;
				rootUi.IsInteractable = false;
				rootUi.IsHidden = true;
				rootUi.IsHovered = false;
				rootUi.IsClicked = false;
			}
		}

		private ModalAnimation GetModalAnimation()
		{
			return EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)?.GetComponent<ModalAnimation>();
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
			yield return WayStationSceneConstants.ModalPanelName;
			yield return WayStationSceneConstants.CloseButtonName;
			yield return WayStationSceneConstants.SwordButtonName;
			yield return WayStationSceneConstants.DaggerButtonName;
			yield return WayStationSceneConstants.HammerButtonName;
			yield return WayStationSceneConstants.EasyButtonName;
			yield return WayStationSceneConstants.NormalButtonName;
			yield return WayStationSceneConstants.HardButtonName;
			yield return WayStationSceneConstants.DepartButtonName;
		}

		private bool WasClicked(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsClicked == true;
		}

		private bool IsHovered(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsHovered == true;
		}

		private bool IsWayStationActive()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.WayStation;
		}

		private static bool IsSelected(StartingWeapon weapon)
		{
			return WayStationRunSetupSingleton.SelectedWeapon == weapon;
		}

		private static bool IsSelected(RunDifficulty difficulty)
		{
			return WayStationRunSetupSingleton.SelectedDifficulty == difficulty;
		}

		private static void SelectWeapon(WayStationMetaSave meta, StartingWeapon weapon)
		{
			if (!ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon)) return;
			WayStationRunSetupSingleton.SelectedWeapon = weapon;
			if (!ClimbUnlockProgressionRules.IsDifficultyUnlocked(meta, weapon, WayStationRunSetupSingleton.SelectedDifficulty))
			{
				WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
			}
		}

		private static void SelectDifficulty(WayStationMetaSave meta, RunDifficulty difficulty)
		{
			if (!ClimbUnlockProgressionRules.IsDifficultyUnlocked(meta, WayStationRunSetupSingleton.SelectedWeapon, difficulty)) return;
			WayStationRunSetupSingleton.SelectedDifficulty = difficulty;
		}

		private static void NormalizeSelection(WayStationMetaSave meta)
		{
			if (!ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, WayStationRunSetupSingleton.SelectedWeapon))
			{
				WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
			}
			if (!ClimbUnlockProgressionRules.IsDifficultyUnlocked(
				meta,
				WayStationRunSetupSingleton.SelectedWeapon,
				WayStationRunSetupSingleton.SelectedDifficulty))
			{
				WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
			}
		}
	}
}
