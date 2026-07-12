using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Pause Menu")]
	public class PauseMenuDisplaySystem : Core.System
	{
		private const string ContextId = "overlay.pause";
		private const string RootName = "PauseMenu_Overlay";
		private const string BlockerName = "PauseMenu_Blocker";
		private const string AbandonName = "PauseMenu_AbandonButton";
		private const string SkipTutorialName = "PauseMenu_SkipTutorialButton";
		private const string MusicSliderName = "PauseMenu_MusicSlider";
		private const string SfxSliderName = "PauseMenu_SfxSlider";
		private const string RumbleToggleName = "PauseMenu_RumbleToggle";

		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;

		private Entity _rootEntity;
		private Entity _blockerEntity;
		private Entity _abandonButtonEntity;
		private Entity _skipTutorialButtonEntity;
		private Entity _musicSliderEntity;
		private Entity _sfxSliderEntity;
		private Entity _rumbleToggleEntity;

		private static readonly Color DimColor = Color.Black;
		private static readonly Color RailFill = new Color(8, 8, 8) * 0.92f;
		private static readonly Color White = Color.White;
		private static readonly Color WarmWhite = new Color(240, 236, 230);
		private static readonly Color MutedWhite = new Color(200, 192, 184);
		private static readonly Color RailAccent = new Color(255, 77, 98);
		private static readonly Color RailAccentGlow = new Color(255, 55, 80);
		private static readonly Color ButtonFill = new Color(30, 30, 30);
		private static readonly Color ButtonFillHover = new Color(42, 42, 42);
		private static readonly Color ButtonBorder = new Color(255, 255, 255) * 0.5f;
		private static readonly Color ButtonBorderHover = Color.White;

		[DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
		public int ZOrder { get; set; } = 62000;

		[DebugEditable(DisplayName = "Dim Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float DimAlpha { get; set; } = 0.65f;

		[DebugEditable(DisplayName = "Left Falloff Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float LeftFalloffAlpha { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Fade In Sec", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float FadeInSec { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Fade Out Sec", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float FadeOutSec { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Rail Width", Step = 1, Min = 100, Max = 1000)]
		public int RailWidth { get; set; } = 480;

		[DebugEditable(DisplayName = "Rail Pad Left", Step = 1, Min = 0, Max = 200)]
		public int RailPadLeft { get; set; } = 64;

		[DebugEditable(DisplayName = "Rail Pad Top", Step = 1, Min = 0, Max = 200)]
		public int RailPadTop { get; set; } = 72;

		[DebugEditable(DisplayName = "Rail Pad Bottom", Step = 1, Min = 0, Max = 200)]
		public int RailPadBottom { get; set; } = 48;

		[DebugEditable(DisplayName = "Content Width", Step = 1, Min = 100, Max = 600)]
		public int ContentWidth { get; set; } = 340;

		[DebugEditable(DisplayName = "Accent Width", Step = 1, Min = 1, Max = 20)]
		public int AccentWidth { get; set; } = 3;

		[DebugEditable(DisplayName = "Accent Top Bottom", Step = 1, Min = 0, Max = 300)]
		public int AccentTopBottom { get; set; } = 80;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.05f, Max = 1.5f)]
		public float TitleScale { get; set; } = 0.41f;

		[DebugEditable(DisplayName = "Subtitle Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float SubtitleScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Button Text Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float ButtonTextScale { get; set; } = 0.13f;

		[DebugEditable(DisplayName = "Hint Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float HintScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Title Y", Step = 1, Min = 0, Max = 400)]
		public int TitleY { get; set; } = 72;

		[DebugEditable(DisplayName = "Subtitle Y", Step = 1, Min = 0, Max = 400)]
		public int SubtitleY { get; set; } = 136;

		[DebugEditable(DisplayName = "Music Row Y", Step = 1, Min = 0, Max = 800)]
		public int MusicRowY { get; set; } = 200;

		[DebugEditable(DisplayName = "SFX Row Y", Step = 1, Min = 0, Max = 800)]
		public int SfxRowY { get; set; } = 352;

		[DebugEditable(DisplayName = "Rumble Row Y", Step = 1, Min = 0, Max = 900)]
		public int RumbleRowY { get; set; } = 500;

		[DebugEditable(DisplayName = "Toggle Width", Step = 1, Min = 40, Max = 240)]
		public int ToggleWidth { get; set; } = 92;

		[DebugEditable(DisplayName = "Toggle Height", Step = 1, Min = 20, Max = 100)]
		public int ToggleHeight { get; set; } = 42;

		[DebugEditable(DisplayName = "Slider Row Height", Step = 1, Min = 20, Max = 160)]
		public int SliderRowHeight { get; set; } = 80;

		[DebugEditable(DisplayName = "Track Offset Y", Step = 1, Min = 0, Max = 120)]
		public int TrackOffsetY { get; set; } = 58;

		[DebugEditable(DisplayName = "Track Height", Step = 1, Min = 1, Max = 20)]
		public int TrackHeight { get; set; } = 5;

		[DebugEditable(DisplayName = "Button Height", Step = 1, Min = 20, Max = 120)]
		public int ButtonHeight { get; set; } = 52;

		[DebugEditable(DisplayName = "Button Bottom", Step = 1, Min = 0, Max = 200)]
		public int ButtonBottom { get; set; } = 48;

		[DebugEditable(DisplayName = "Button Border", Step = 1, Min = 0, Max = 12)]
		public int ButtonBorderThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "Hint Right", Step = 1, Min = 0, Max = 300)]
		public int HintRight { get; set; } = 48;

		[DebugEditable(DisplayName = "Hint Bottom", Step = 1, Min = 0, Max = 200)]
		public int HintBottom { get; set; } = 36;

		public PauseMenuDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<DeleteCachesEvent>(_ => DismissOverlay());
			EventManager.Subscribe<RunEndSequenceRequested>(_ => DismissOverlay());
			EventManager.Subscribe<GuidedTutorialSkipRequested>(_ => DismissOverlay());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			var overlay = EnsureOverlay();

			if (!Game1.WindowIsActive || StateSingleton.IsActive)
			{
				DismissOverlay();
				return;
			}

			bool canOpenHere = scene != null
				&& scene.Current != SceneId.TitleMenu
				&& scene.Current != SceneId.None;

			PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
			bool togglePressed = input.WasPressed(PlayerButton.Escape) || input.WasPressed(PlayerButton.Back);

			if (overlay.Phase == PauseMenuPhase.Hidden)
			{
				SyncEntitiesActive(false, scene);
				if (canOpenHere && togglePressed)
				{
					OpenOverlay(overlay);
				}
				return;
			}

			if (!canOpenHere)
			{
				DismissOverlay();
				return;
			}

			UpdateAnimation(overlay, gameTime);
			UpdateEntityLayout(overlay);
			SyncEntitiesActive(true, scene);

			if (togglePressed)
			{
				BeginClose(overlay);
				return;
			}

			var blockerUi = _blockerEntity?.GetComponent<UIElement>();
			if (blockerUi?.IsClicked == true)
			{
				blockerUi.IsClicked = false;
				if (input.PointerPosition.X >= RailWidth)
				{
					BeginClose(overlay);
				}
			}
		}

		private PauseMenuOverlay EnsureOverlay()
		{
			EnsureEntities();
			return _rootEntity.GetComponent<PauseMenuOverlay>();
		}

		private void OpenOverlay(PauseMenuOverlay overlay)
		{
			overlay.Phase = PauseMenuPhase.FadingIn;
			overlay.Progress01 = 0f;
			ResetSlider(_musicSliderEntity, "Music", PauseMenuSliderSetting.MusicVolume, SaveCache.GetMusicVolumeLevel());
			ResetSlider(_sfxSliderEntity, "SFX", PauseMenuSliderSetting.SfxVolume, SaveCache.GetSfxVolumeLevel());
			UpdateEntityLayout(overlay);
			SyncEntitiesActive(true, GetCurrentScene());
		}

		internal void OpenForSnapshot()
		{
			var overlay = EnsureOverlay();
			overlay.Phase = PauseMenuPhase.Visible;
			overlay.Progress01 = 1f;
			ResetSlider(_musicSliderEntity, "Music", PauseMenuSliderSetting.MusicVolume, SaveCache.GetMusicVolumeLevel());
			ResetSlider(_sfxSliderEntity, "SFX", PauseMenuSliderSetting.SfxVolume, SaveCache.GetSfxVolumeLevel());
			UpdateEntityLayout(overlay);
			SyncEntitiesActive(true, GetCurrentScene());
		}

		private void BeginClose(PauseMenuOverlay overlay)
		{
			if (overlay.Phase == PauseMenuPhase.Hidden || overlay.Phase == PauseMenuPhase.FadingOut) return;
			overlay.Phase = PauseMenuPhase.FadingOut;
		}

		private void DismissOverlay()
		{
			var overlay = _rootEntity?.GetComponent<PauseMenuOverlay>();
			if (overlay != null)
			{
				overlay.Phase = PauseMenuPhase.Hidden;
				overlay.Progress01 = 0f;
			}
			SyncEntitiesActive(false, GetCurrentScene());
		}

		private void UpdateAnimation(PauseMenuOverlay overlay, GameTime gameTime)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			switch (overlay.Phase)
			{
				case PauseMenuPhase.FadingIn:
				{
					overlay.Progress01 += dt / Math.Max(0.001f, FadeInSec);
					if (overlay.Progress01 >= 1f)
					{
						overlay.Progress01 = 1f;
						overlay.Phase = PauseMenuPhase.Visible;
					}
					break;
				}
				case PauseMenuPhase.FadingOut:
				{
					overlay.Progress01 -= dt / Math.Max(0.001f, FadeOutSec);
					if (overlay.Progress01 <= 0f)
					{
						overlay.Progress01 = 0f;
						overlay.Phase = PauseMenuPhase.Hidden;
						SyncEntitiesActive(false, GetCurrentScene());
					}
					break;
				}
			}
		}

		private void EnsureEntities()
		{
			if (_rootEntity == null || EntityManager.GetEntity(RootName) == null)
			{
				_rootEntity = EntityManager.GetEntity(RootName) ?? EntityManager.CreateEntity(RootName);
				EnsureComponent(_rootEntity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
				EnsureComponent(_rootEntity, new PauseMenuOverlay());
				EnsureDontDestroy(_rootEntity);
			}

			if (_blockerEntity == null || EntityManager.GetEntity(BlockerName) == null)
			{
				_blockerEntity = EntityManager.GetEntity(BlockerName) ?? EntityManager.CreateEntity(BlockerName);
				EnsureComponent(_blockerEntity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
				EnsureComponent(_blockerEntity, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					LayerType = UILayerType.Overlay,
					EventType = UIElementEventType.None,
					ShowHoverHighlight = false,
					IsHidden = true,
				});
				InputContextService.EnsureContext(EntityManager, _blockerEntity, ContextId, 900, false);
				EnsureDontDestroy(_blockerEntity);
			}

			_musicSliderEntity = EnsureSliderEntity(
				_musicSliderEntity,
				MusicSliderName,
				"Music",
				PauseMenuSliderSetting.MusicVolume,
				SaveCache.GetMusicVolumeLevel());
			_sfxSliderEntity = EnsureSliderEntity(
				_sfxSliderEntity,
				SfxSliderName,
				"SFX",
				PauseMenuSliderSetting.SfxVolume,
				SaveCache.GetSfxVolumeLevel());

			if (_rumbleToggleEntity == null || EntityManager.GetEntity(RumbleToggleName) == null)
			{
				_rumbleToggleEntity = EntityManager.GetEntity(RumbleToggleName) ?? EntityManager.CreateEntity(RumbleToggleName);
				EnsureComponent(_rumbleToggleEntity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 2 });
				EnsureComponent(_rumbleToggleEntity, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					LayerType = UILayerType.Overlay,
					EventType = UIElementEventType.ToggleRumble,
					ShowHoverHighlight = false,
					IsHidden = true,
				});
				EnsureComponent(_rumbleToggleEntity, new PauseMenuToggle { Label = "Rumble" });
				InputContextService.EnsureMember(EntityManager, _rumbleToggleEntity, ContextId);
				EnsureDontDestroy(_rumbleToggleEntity);
			}

			if (_abandonButtonEntity == null || EntityManager.GetEntity(AbandonName) == null)
			{
				_abandonButtonEntity = EntityManager.GetEntity(AbandonName) ?? EntityManager.CreateEntity(AbandonName);
				EnsureComponent(_abandonButtonEntity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 2 });
				EnsureComponent(_abandonButtonEntity, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					LayerType = UILayerType.Overlay,
					EventType = UIElementEventType.AbandonQuest,
					ShowHoverHighlight = false,
					IsHidden = true,
				});
				InputContextService.EnsureMember(EntityManager, _abandonButtonEntity, ContextId);
				EnsureDontDestroy(_abandonButtonEntity);
			}

			if (_skipTutorialButtonEntity == null || EntityManager.GetEntity(SkipTutorialName) == null)
			{
				_skipTutorialButtonEntity = EntityManager.GetEntity(SkipTutorialName) ?? EntityManager.CreateEntity(SkipTutorialName);
				EnsureComponent(_skipTutorialButtonEntity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 2 });
				EnsureComponent(_skipTutorialButtonEntity, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					LayerType = UILayerType.Overlay,
					EventType = UIElementEventType.SkipTutorial,
					ShowHoverHighlight = false,
					IsHidden = true,
				});
				InputContextService.EnsureMember(EntityManager, _skipTutorialButtonEntity, ContextId);
				EnsureDontDestroy(_skipTutorialButtonEntity);
			}
		}

		private Entity EnsureSliderEntity(Entity current, string name, string label, PauseMenuSliderSetting setting, int value)
		{
			if (current != null && EntityManager.GetEntity(name) != null) return current;
			var entity = EntityManager.GetEntity(name) ?? EntityManager.CreateEntity(name);
			EnsureComponent(entity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 2 });
			EnsureComponent(entity, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = false,
				LayerType = UILayerType.Overlay,
				EventType = UIElementEventType.None,
				ShowHoverHighlight = false,
				IsHidden = true,
			});
			EnsureComponent(entity, new PauseMenuSlider { Label = label, Setting = setting, Value = value, Min = 0, Max = 100 });
			InputContextService.EnsureMember(EntityManager, entity, ContextId);
			EnsureDontDestroy(entity);
			return entity;
		}

		private void EnsureComponent<T>(Entity entity, T component) where T : class, IComponent
		{
			if (entity.GetComponent<T>() == null)
			{
				EntityManager.AddComponent(entity, component);
			}
		}

		private void EnsureDontDestroy(Entity entity)
		{
			if (entity.GetComponent<DontDestroyOnLoad>() == null)
			{
				EntityManager.AddComponent(entity, new DontDestroyOnLoad());
			}
		}

		private void ResetSlider(Entity entity, string label, PauseMenuSliderSetting setting, int value)
		{
			var slider = entity?.GetComponent<PauseMenuSlider>();
			if (slider == null) return;
			slider.Label = label;
			slider.Setting = setting;
			slider.Value = value;
			slider.Min = 0;
			slider.Max = 100;
			slider.IsDragging = false;
		}

		private void SyncEntitiesActive(bool active, SceneState scene)
		{
			bool hidden = !active
				|| _rootEntity?.GetComponent<PauseMenuOverlay>()?.Phase == PauseMenuPhase.Hidden;
			var context = _blockerEntity?.GetComponent<InputContext>();
			if (context != null) context.IsActive = !hidden;

			SetUiActive(_blockerEntity, !hidden, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight));
			SetUiActive(_musicSliderEntity, !hidden, _musicSliderEntity?.GetComponent<PauseMenuSlider>()?.RowBounds ?? Rectangle.Empty);
			SetUiActive(_sfxSliderEntity, !hidden, _sfxSliderEntity?.GetComponent<PauseMenuSlider>()?.RowBounds ?? Rectangle.Empty);
			SetUiActive(_rumbleToggleEntity, !hidden, _rumbleToggleEntity?.GetComponent<PauseMenuToggle>()?.RowBounds ?? Rectangle.Empty);

			bool showAbandon = !hidden
				&& SaveCache.IsRunActive()
				&& !GuidedTutorialService.IsActive(EntityManager);
			bool showSkipTutorial = !hidden
				&& GuidedTutorialService.IsActive(EntityManager);
			var layout = ComputeLayout();
			SetUiActive(_abandonButtonEntity, showAbandon, layout.AbandonButton);
			SetUiActive(_skipTutorialButtonEntity, showSkipTutorial, layout.AbandonButton);
		}

		private static void SetUiActive(Entity entity, bool active, Rectangle bounds)
		{
			var ui = entity?.GetComponent<UIElement>();
			if (ui == null) return;
			ui.IsInteractable = active;
			ui.IsHidden = !active;
			ui.Bounds = active ? bounds : Rectangle.Empty;
			if (!active)
			{
				ui.IsHovered = false;
				ui.IsClicked = false;
			}
		}

		private void UpdateEntityLayout(PauseMenuOverlay overlay)
		{
			var layout = ComputeLayout();
			int railX = CalculateRailX(overlay);
			layout = layout.Offset(railX, 0);

			UpdateTransform(_rootEntity, ZOrder, Vector2.Zero);
			UpdateTransform(_blockerEntity, ZOrder, Vector2.Zero);
			UpdateTransform(_musicSliderEntity, ZOrder + 2, new Vector2(layout.MusicRow.X, layout.MusicRow.Y));
			UpdateTransform(_sfxSliderEntity, ZOrder + 2, new Vector2(layout.SfxRow.X, layout.SfxRow.Y));
			UpdateTransform(_rumbleToggleEntity, ZOrder + 2, new Vector2(layout.RumbleRow.X, layout.RumbleRow.Y));
			UpdateTransform(_abandonButtonEntity, ZOrder + 2, new Vector2(layout.AbandonButton.X, layout.AbandonButton.Y));
			UpdateTransform(_skipTutorialButtonEntity, ZOrder + 2, new Vector2(layout.AbandonButton.X, layout.AbandonButton.Y));

			UpdateSliderLayout(_musicSliderEntity, layout.MusicRow, layout.MusicTrack);
			UpdateSliderLayout(_sfxSliderEntity, layout.SfxRow, layout.SfxTrack);
			UpdateToggleLayout(_rumbleToggleEntity, layout.RumbleRow, layout.RumbleToggle);

			var abandonUi = _abandonButtonEntity?.GetComponent<UIElement>();
			if (abandonUi?.IsInteractable == true) abandonUi.Bounds = layout.AbandonButton;

			var skipTutorialUi = _skipTutorialButtonEntity?.GetComponent<UIElement>();
			if (skipTutorialUi?.IsInteractable == true) skipTutorialUi.Bounds = layout.AbandonButton;
		}

		private static void UpdateToggleLayout(Entity entity, Rectangle row, Rectangle toggleBounds)
		{
			var toggle = entity?.GetComponent<PauseMenuToggle>();
			var ui = entity?.GetComponent<UIElement>();
			if (toggle == null) return;
			toggle.RowBounds = row;
			toggle.ToggleBounds = toggleBounds;
			if (ui?.IsInteractable == true) ui.Bounds = row;
		}

		private void UpdateSliderLayout(Entity entity, Rectangle row, Rectangle track)
		{
			var slider = entity?.GetComponent<PauseMenuSlider>();
			var ui = entity?.GetComponent<UIElement>();
			if (slider == null) return;
			slider.RowBounds = row;
			slider.TrackBounds = track;
			if (ui != null && ui.IsInteractable) ui.Bounds = row;
		}

		private static void UpdateTransform(Entity entity, int zOrder, Vector2 position)
		{
			var transform = entity?.GetComponent<Transform>();
			if (transform == null) return;
			transform.ZOrder = zOrder;
			transform.Position = position;
		}

		public void Draw()
		{
			var overlay = _rootEntity?.GetComponent<PauseMenuOverlay>();
			if (overlay == null || overlay.Phase == PauseMenuPhase.Hidden || overlay.Progress01 <= 0f) return;

			var layout = ComputeLayout();
			int railX = CalculateRailX(overlay);
			float alpha = EaseOut(MathHelper.Clamp(overlay.Progress01, 0f, 1f));
			var drawLayout = layout.Offset(railX, 0);

			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), DimColor * (DimAlpha * alpha));
			DrawLeftFalloff(alpha);
			_spriteBatch.Draw(_pixel, drawLayout.Rail, RailFill * alpha);
			DrawAccent(drawLayout.Accent, alpha);
			DrawHeader(drawLayout, alpha);
			DrawRumbleToggle(drawLayout, alpha);
			DrawFooterButtons(drawLayout, alpha);
			DrawResumeHint(alpha);
		}

		private void DrawRumbleToggle(PauseMenuLayout layout, float alpha)
		{
			var ui = _rumbleToggleEntity?.GetComponent<UIElement>();
			if (ui == null || ui.IsHidden) return;
			bool enabled = SaveCache.GetRumbleEnabled();
			_spriteBatch.DrawString(
				_bodyFont,
				"Rumble",
				new Vector2(layout.RumbleRow.X, layout.RumbleRow.Y),
				MutedWhite * alpha,
				0f,
				Vector2.Zero,
				SubtitleScale,
				SpriteEffects.None,
				0f);

			Color fill = enabled ? RailAccent : ButtonFill;
			Color border = ui.IsHovered ? ButtonBorderHover : ButtonBorder;
			_spriteBatch.Draw(_pixel, layout.RumbleToggle, fill * alpha);
			DrawBorder(layout.RumbleToggle, border * alpha, ButtonBorderThickness);
			string value = enabled ? "ON" : "OFF";
			Vector2 size = _bodyFont.MeasureString(value) * ButtonTextScale;
			var position = new Vector2(
				layout.RumbleToggle.Center.X - size.X / 2f,
				layout.RumbleToggle.Center.Y - size.Y / 2f);
			_spriteBatch.DrawString(_bodyFont, value, position, WarmWhite * alpha, 0f, Vector2.Zero, ButtonTextScale, SpriteEffects.None, 0f);
		}

		private void DrawLeftFalloff(float alpha)
		{
			int width = Math.Min(Game1.VirtualWidth, RailWidth * 3);
			int steps = 32;
			float stepW = width / (float)steps;
			for (int i = 0; i < steps; i++)
			{
				float t = i / (float)(steps - 1);
				float stripAlpha = (1f - t) * LeftFalloffAlpha * alpha;
				int x = (int)MathF.Round(i * stepW);
				int nextX = i == steps - 1 ? width : (int)MathF.Round((i + 1) * stepW);
				_spriteBatch.Draw(_pixel, new Rectangle(x, 0, Math.Max(1, nextX - x), Game1.VirtualHeight), Color.Black * stripAlpha);
			}
		}

		private void DrawAccent(Rectangle accent, float alpha)
		{
			if (accent.Width <= 0 || accent.Height <= 0) return;
			_spriteBatch.Draw(_pixel, new Rectangle(accent.X - 8, accent.Y, accent.Width + 16, accent.Height), RailAccentGlow * (0.16f * alpha));
			_spriteBatch.Draw(_pixel, new Rectangle(accent.X - 3, accent.Y, accent.Width + 6, accent.Height), RailAccentGlow * (0.25f * alpha));
			_spriteBatch.Draw(_pixel, accent, RailAccent * alpha);
		}

		private void DrawHeader(PauseMenuLayout layout, float alpha)
		{
			var titlePos = new Vector2(layout.ContentX, layout.TitleY);
			_spriteBatch.DrawString(_titleFont, "Paused", titlePos + new Vector2(0, 4), Color.Black * (0.9f * alpha), 0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_titleFont, "Paused", titlePos, White * alpha, 0f, Vector2.Zero, TitleScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_bodyFont, "Audio and haptics", new Vector2(layout.ContentX, layout.SubtitleY), MutedWhite * alpha, 0f, Vector2.Zero, SubtitleScale, SpriteEffects.None, 0f);
		}

		private void DrawFooterButtons(PauseMenuLayout layout, float alpha)
		{
			var abandonUi = _abandonButtonEntity?.GetComponent<UIElement>();
			if (abandonUi != null && !abandonUi.IsHidden)
			{
				DrawFooterButton(abandonUi, layout.AbandonButton, "Abandon Climb", alpha);
				return;
			}

			var skipTutorialUi = _skipTutorialButtonEntity?.GetComponent<UIElement>();
			if (skipTutorialUi != null && !skipTutorialUi.IsHidden)
				DrawFooterButton(skipTutorialUi, layout.AbandonButton, "Skip Tutorial", alpha);
		}

		private void DrawFooterButton(UIElement ui, Rectangle bounds, string text, float alpha)
		{
			Color fill = ui.IsHovered ? ButtonFillHover : ButtonFill;
			Color border = ui.IsHovered ? ButtonBorderHover : ButtonBorder;
			_spriteBatch.Draw(_pixel, bounds, fill * alpha);
			DrawBorder(bounds, border * alpha, ButtonBorderThickness);

			Vector2 size = _bodyFont.MeasureString(text) * ButtonTextScale;
			var pos = new Vector2(
				bounds.X + (bounds.Width - size.X) / 2f,
				bounds.Y + (bounds.Height - size.Y) / 2f);
			_spriteBatch.DrawString(_bodyFont, text, pos, WarmWhite * alpha, 0f, Vector2.Zero, ButtonTextScale, SpriteEffects.None, 0f);
		}

		private void DrawResumeHint(float alpha)
		{
			string text = "Press Esc to resume";
			Vector2 size = _bodyFont.MeasureString(text) * HintScale;
			var pos = new Vector2(
				Game1.VirtualWidth - HintRight - size.X,
				Game1.VirtualHeight - HintBottom - size.Y);
			_spriteBatch.DrawString(_bodyFont, text, pos, Color.White * (0.35f * alpha), 0f, Vector2.Zero, HintScale, SpriteEffects.None, 0f);
		}

		private void DrawBorder(Rectangle rect, Color color, int thickness)
		{
			if (thickness <= 0) return;
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private int CalculateRailX(PauseMenuOverlay overlay)
		{
			float eased = EaseOut(MathHelper.Clamp(overlay.Progress01, 0f, 1f));
			return (int)MathF.Round(-RailWidth + RailWidth * eased);
		}

		private PauseMenuLayout ComputeLayout()
		{
			int buttonY = Game1.VirtualHeight - ButtonBottom - ButtonHeight;
			int contentX = RailPadLeft;
			return new PauseMenuLayout
			{
				ContentX = contentX,
				TitleY = TitleY,
				SubtitleY = SubtitleY,
				Rail = new Rectangle(0, 0, RailWidth, Game1.VirtualHeight),
				Accent = new Rectangle(RailWidth - AccentWidth, AccentTopBottom, AccentWidth, Math.Max(0, Game1.VirtualHeight - AccentTopBottom * 2)),
				MusicRow = new Rectangle(contentX, MusicRowY, ContentWidth, SliderRowHeight),
				SfxRow = new Rectangle(contentX, SfxRowY, ContentWidth, SliderRowHeight),
				MusicTrack = new Rectangle(contentX, MusicRowY + TrackOffsetY, ContentWidth, TrackHeight),
				SfxTrack = new Rectangle(contentX, SfxRowY + TrackOffsetY, ContentWidth, TrackHeight),
				RumbleRow = new Rectangle(contentX, RumbleRowY, ContentWidth, SliderRowHeight),
				RumbleToggle = new Rectangle(contentX + ContentWidth - ToggleWidth, RumbleRowY, ToggleWidth, ToggleHeight),
				AbandonButton = new Rectangle(contentX, buttonY, ContentWidth, ButtonHeight),
			};
		}

		private SceneState GetCurrentScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
		}

		private static float EaseOut(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return 1f - MathF.Pow(1f - t, 3f);
		}

		private struct PauseMenuLayout
		{
			public int ContentX;
			public int TitleY;
			public int SubtitleY;
			public Rectangle Rail;
			public Rectangle Accent;
			public Rectangle MusicRow;
			public Rectangle SfxRow;
			public Rectangle MusicTrack;
			public Rectangle SfxTrack;
			public Rectangle RumbleRow;
			public Rectangle RumbleToggle;
			public Rectangle AbandonButton;

			public PauseMenuLayout Offset(int x, int y)
			{
				return new PauseMenuLayout
				{
					ContentX = ContentX + x,
					TitleY = TitleY + y,
					SubtitleY = SubtitleY + y,
					Rail = OffsetRect(Rail, x, y),
					Accent = OffsetRect(Accent, x, y),
					MusicRow = OffsetRect(MusicRow, x, y),
					SfxRow = OffsetRect(SfxRow, x, y),
					MusicTrack = OffsetRect(MusicTrack, x, y),
					SfxTrack = OffsetRect(SfxTrack, x, y),
					RumbleRow = OffsetRect(RumbleRow, x, y),
					RumbleToggle = OffsetRect(RumbleToggle, x, y),
					AbandonButton = OffsetRect(AbandonButton, x, y),
				};
			}

			private static Rectangle OffsetRect(Rectangle rect, int x, int y)
			{
				return new Rectangle(rect.X + x, rect.Y + y, rect.Width, rect.Height);
			}
		}
	}
}
