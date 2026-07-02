using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Utils;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Narrative Event Modal")]
	public class NarrativeEventModalDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;
		private readonly HorizontalGradientRuleCache _gradientRuleCache;
		private const string ContextId = "overlay.narrative-event";

		private EventBase _activeEvent;
		private NarrativeModalContent _suppliedContent;
		private int _snapshotVisibleOptionCap;
		private bool _forceSnapshotDraw;
		private NarrativeEventLayout _layout;
		private CachedTextMetrics _textMetrics;
		private readonly List<(int OptionIndex, string Text)> _visibleOptions = new();

		private bool _layoutValid;
		private bool _textMetricsValid;
		private bool _drawOnLocationOrSnapshot;
		private int _cachedVw;
		private int _cachedVh;
		private int _cachedVisibleCount;
		private int _pendingCloseExitSequence = -1;

		private struct NarrativeEventLayout
		{
			public Rectangle Modal;
			public Rectangle Content;
			public Rectangle Body;
			public Rectangle BodyInner;
			public Rectangle Footer;
			public int RuleY;
			public Rectangle[] OptionButtons;
		}

		private struct CachedTextMetrics
		{
			public Vector2 TitlePos;
			public Vector2 TitleSize;
			public List<string> BodyLines;
			public List<Vector2> BodyLinePositions;
			public List<(string Label, Vector2 Pos, float Scale)> OptionLabels;
		}

		[DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
		public int ZOrder { get; set; } = 52010;

		[DebugEditable(DisplayName = "Modal Width", Step = 10, Min = 200, Max = 1600)]
		public int ModalWidth { get; set; } = 920;
		[DebugEditable(DisplayName = "Modal Height", Step = 10, Min = 200, Max = 1200)]
		public int ModalHeight { get; set; } = 520;
		[DebugEditable(DisplayName = "Border Thickness", Step = 1, Min = 1, Max = 8)]
		public int BorderThickness { get; set; } = 2;
		[DebugEditable(DisplayName = "Dim Alpha", Step = 5, Min = 0, Max = 255)]
		public int DimAlpha { get; set; } = 140;
		[DebugEditable(DisplayName = "Drop Shadow Offset Y", Step = 1, Min = 0, Max = 40)]
		public int DropShadowOffsetY { get; set; } = 16;

		[DebugEditable(DisplayName = "Body Padding Top", Step = 2, Min = 0, Max = 120)]
		public int BodyPaddingTop { get; set; } = 40;
		[DebugEditable(DisplayName = "Body Padding X", Step = 2, Min = 0, Max = 80)]
		public int BodyPaddingX { get; set; } = 40;
		[DebugEditable(DisplayName = "Body Padding Bottom", Step = 2, Min = 0, Max = 120)]
		public int BodyPaddingBottom { get; set; } = 28;
		[DebugEditable(DisplayName = "Body Stack Gap", Step = 2, Min = 0, Max = 80)]
		public int BodyStackGap { get; set; } = 20;

		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float TitleScale { get; set; } = 0.281f;
		[DebugEditable(DisplayName = "Body Text Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float BodyTextScale { get; set; } = 0.172f;
		[DebugEditable(DisplayName = "Title Offset Y", Step = 1, Min = -80, Max = 80)]
		public int TitleOffsetY { get; set; } = 0;
		[DebugEditable(DisplayName = "Body Offset Y", Step = 1, Min = -80, Max = 80)]
		public int BodyOffsetY { get; set; } = 0;

		[DebugEditable(DisplayName = "Red Rule Width", Step = 2, Min = 20, Max = 200)]
		public int RedRuleWidth { get; set; } = 80;
		[DebugEditable(DisplayName = "Red Rule Height", Step = 1, Min = 1, Max = 12)]
		public int RedRuleHeight { get; set; } = 3;

		[DebugEditable(DisplayName = "Footer Padding", Step = 2, Min = 0, Max = 60)]
		public int FooterPadding { get; set; } = 20;
		[DebugEditable(DisplayName = "Option Gap", Step = 2, Min = 0, Max = 60)]
		public int OptionGap { get; set; } = 20;
		[DebugEditable(DisplayName = "Option Min Height", Step = 2, Min = 30, Max = 120)]
		public int OptionMinHeight { get; set; } = 64;
		[DebugEditable(DisplayName = "Option Text Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float OptionTextScale { get; set; } = 0.133f;

		public NarrativeEventModalDisplaySystem(EntityManager entityManager, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets) : base(entityManager)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_pixel = imageAssets.GetPixel(Color.White);
			_gradientRuleCache = new HorizontalGradientRuleCache(gd);

			EventManager.Subscribe<ShowNarrativeEventOverlay>(OnShowNarrativeEventOverlay);
			EventManager.Subscribe<NarrativeEventOverlayClosedEvent>(OnNarrativeEventOverlayClosed);
			EventManager.Subscribe<DeleteCachesEvent>(_ =>
			{
				InvalidateCaches();
				_gradientRuleCache.DisposeAll();
			});
		}

		private void OnShowNarrativeEventOverlay(ShowNarrativeEventOverlay e)
		{
			if (e == null || IsOverlayOpen(EntityManager)) return;
			if (e.Content != null)
			{
				OpenSupplied(e.ResolutionContextId, e.Content);
				return;
			}
			OpenLegacy(e.RunMapEventId, e.EventTypeId, snapshotVisibleOptionCap: 0);
		}

		private void OnNarrativeEventOverlayClosed(NarrativeEventOverlayClosedEvent e)
		{
			RequestCloseAnimation();
		}

		public void OpenForSnapshot(string eventTypeId, int visibleOptionCount = 0)
		{
			_forceSnapshotDraw = true;
			OpenLegacy(string.Empty, eventTypeId, visibleOptionCount);
		}

		private void OpenLegacy(string runMapEventId, string eventTypeId, int snapshotVisibleOptionCap)
		{
			if (string.IsNullOrWhiteSpace(eventTypeId)) return;

			var narrativeEvent = EventFactory.Create(eventTypeId);
			if (narrativeEvent == null)
			{
				System.Diagnostics.Debug.WriteLine($"[NarrativeEventModalDisplaySystem] Unknown event type: {eventTypeId}");
				return;
			}

			EnsureOverlayEntity();
			var st = EntityManager.GetEntity("NarrativeEventOverlay").GetComponent<NarrativeEventOverlayState>();
			st.RunMapEventId = runMapEventId ?? string.Empty;
			st.EventTypeId = eventTypeId;
			st.ResolutionContextId = string.Empty;
			st.IsOpen = true;
			RequestOpenAnimation();

			_activeEvent = narrativeEvent;
			_suppliedContent = null;
			_activeEvent.Initialize(EntityManager);
			_snapshotVisibleOptionCap = snapshotVisibleOptionCap;
			InvalidateCaches();
		}

		private void OpenSupplied(string resolutionContextId, NarrativeModalContent content)
		{
			if (content == null || string.IsNullOrWhiteSpace(resolutionContextId)) return;

			EnsureOverlayEntity();
			var state = EntityManager.GetEntity("NarrativeEventOverlay").GetComponent<NarrativeEventOverlayState>();
			state.RunMapEventId = string.Empty;
			state.EventTypeId = string.Empty;
			state.ResolutionContextId = resolutionContextId;
			state.IsOpen = true;
			RequestOpenAnimation();

			_activeEvent = null;
			_suppliedContent = new NarrativeModalContent
			{
				Title = content.Title ?? string.Empty,
				Body = content.Body ?? string.Empty,
				ConfirmLabel = content.ConfirmLabel ?? string.Empty,
			};
			_snapshotVisibleOptionCap = 0;
			InvalidateCaches();
		}

		public static bool IsOverlayOpen(EntityManager entityManager)
		{
			var st = entityManager.GetEntity("NarrativeEventOverlay")?.GetComponent<NarrativeEventOverlayState>();
			return st != null && st.IsOpen;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity sceneEntity, GameTime gameTime)
		{
			var overlayEntity = EntityManager.GetEntity("NarrativeEventOverlay");
			if (overlayEntity == null) return;
			var state = overlayEntity.GetComponent<NarrativeEventOverlayState>();
			if (state == null) return;
			InputContextService.EnsureContext(
				EntityManager,
				overlayEntity,
				ContextId,
				730,
				state.IsOpen);
			CompletePendingCloseIfReady(overlayEntity);
			if (!state.IsOpen)
			{
				if (!_forceSnapshotDraw)
					StateSingleton.PreventClicking = false;
				return;
			}

			var scene = sceneEntity.GetComponent<SceneState>();
			if (!_forceSnapshotDraw)
				StateSingleton.PreventClicking = scene != null && (scene.Current == SceneId.Location || scene.Current == SceneId.Climb);

			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			int visibleCount = CountVisibleOptions();
			EnsureLayout(vw, vh, visibleCount, scene);

			var overlayT = overlayEntity.GetComponent<Transform>();
			if (overlayT != null) overlayT.ZOrder = ZOrder;

			SyncOptionButtons(state, visibleCount);
		}

		private void SyncOptionButtons(NarrativeEventOverlayState state, int visibleCount)
		{
			for (int slot = 0; slot < 3; slot++)
			{
				int buttonNum = slot + 1;
				var btn = EnsureOptionButton(buttonNum);
				var btnUi = btn?.GetComponent<UIElement>();
				if (btnUi == null) continue;

				if (slot < visibleCount && slot < _visibleOptions.Count)
				{
					var entry = _visibleOptions[slot];
					btnUi.Bounds = _layout.OptionButtons != null && slot < _layout.OptionButtons.Length
						? _layout.OptionButtons[slot]
						: Rectangle.Empty;
					btnUi.IsInteractable = IsModalAnimationInteractive();
					btnUi.IsHidden = false;

					if (btnUi.IsClicked)
					{
						btnUi.IsClicked = false;
						ResolveOption(state, entry.OptionIndex);
						return;
					}
				}
				else
				{
					btnUi.Bounds = Rectangle.Empty;
					btnUi.IsInteractable = false;
					btnUi.IsHidden = true;
				}

				var btnT = btn.GetComponent<Transform>();
				if (btnT != null) btnT.ZOrder = ZOrder + 2;
			}
		}

		private void ResolveOption(NarrativeEventOverlayState state, int optionIndex)
		{
			if (!state.IsOpen) return;
			if (_suppliedContent != null)
			{
				var request = new NarrativeModalChoiceRequested
				{
					ResolutionContextId = state.ResolutionContextId,
					ChoiceIndex = optionIndex,
				};
				EventManager.Publish(request);
				if (request.Handled) RequestCloseAnimation();
				return;
			}

			if (_activeEvent == null) return;

			switch (optionIndex)
			{
				case 1: _activeEvent.OnOption1(EntityManager); break;
				case 2: _activeEvent.OnOption2(EntityManager); break;
				case 3: _activeEvent.OnOption3(EntityManager); break;
			}

			if (!string.IsNullOrWhiteSpace(state.RunMapEventId))
				SaveCache.TryCompleteRunMapEvent(state.RunMapEventId);

			EventManager.Publish(new NarrativeEventOverlayClosedEvent
			{
				RunMapEventId = state.RunMapEventId,
				EventTypeId = state.EventTypeId,
				OptionIndex = optionIndex
			});
		}

		public void Draw()
		{
			if (_titleFont == null || !IsOverlayOpen(EntityManager)) return;
			var overlayEntity = EntityManager.GetEntity("NarrativeEventOverlay");

			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			int visibleCount = CountVisibleOptions();

			if (!_layoutValid || !_textMetricsValid)
			{
				var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
				EnsureLayout(vw, vh, visibleCount, scene);
			}

			if (!_drawOnLocationOrSnapshot && !_forceSnapshotDraw) return;

			var render = ModalAnimationRenderState.From(overlayEntity?.GetComponent<ModalAnimation>(), _layout.Modal);
			if (!render.ShouldDraw) return;

			ModalOverlayChrome.DrawDim(_spriteBatch, _pixel, vw, vh, (int)System.Math.Round(DimAlpha * render.DimAlphaMultiplier));
			ModalOverlayChrome.DrawDropShadow(_spriteBatch, _pixel, _layout.Modal, DropShadowOffsetY, render.ApplyShadow(ModalOverlayPalette.DropShadow));
			DrawModalRegions(render);

			DrawBodyColumn(render);
			DrawOptionButtons(render);
		}

		private void DrawModalRegions(ModalAnimationRenderState render)
		{
			_spriteBatch.Draw(_pixel, render.Transform(_layout.Modal), render.ApplyShell(ModalOverlayPalette.ModalFill));
			if (_layout.Footer.Width > 0 && _layout.Footer.Height > 0)
			{
				_spriteBatch.Draw(_pixel, render.Transform(_layout.Footer), render.ApplyShell(ModalOverlayPalette.FooterFill));
				_spriteBatch.Draw(_pixel, render.Transform(new Rectangle(_layout.Footer.X, _layout.Footer.Y, _layout.Footer.Width, 1)), render.ApplyShell(ModalOverlayPalette.FooterBorderTop));
			}
			ModalOverlayChrome.DrawInsetHighlight(_spriteBatch, _pixel, render.Transform(_layout.Content));
			ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, render.Transform(_layout.Modal), render.ApplyShell(ModalOverlayPalette.PanelBorder), BorderThickness);
		}

		private void DrawBodyColumn(ModalAnimationRenderState render)
		{
			var m = _textMetrics;
			if (_activeEvent == null && _suppliedContent == null) return;

			string safeTitle = TextUtils.FilterUnsupportedGlyphs(_titleFont, GetTitle());
			_spriteBatch.DrawString(_titleFont, safeTitle, render.Transform(m.TitlePos), render.ApplyShell(ModalOverlayPalette.TitleColor),
				0f, Vector2.Zero, render.TransformScale(TitleScale), SpriteEffects.None, 0f);

			Vector2 ruleCenter = render.Transform(new Vector2(_layout.BodyInner.Center.X, _layout.RuleY));
			_gradientRuleCache.DrawRule(
				_spriteBatch,
				(int)System.Math.Round(ruleCenter.X),
				(int)System.Math.Round(ruleCenter.Y),
				(int)System.Math.Round(RedRuleWidth * render.ShellScale),
				(int)System.Math.Round(RedRuleHeight * render.ShellScale),
				render.ApplyShell(Color.White));

			if (_bodyFont == null || m.BodyLines == null) return;
			for (int i = 0; i < m.BodyLines.Count; i++)
			{
				_spriteBatch.DrawString(_bodyFont, m.BodyLines[i], render.Transform(m.BodyLinePositions[i]), render.ApplyShell(ModalOverlayPalette.BodyTextColor),
					0f, Vector2.Zero, render.TransformScale(BodyTextScale), SpriteEffects.None, 0f);
			}
		}

		private void DrawOptionButtons(ModalAnimationRenderState render)
		{
			for (int i = 0; i < _visibleOptions.Count; i++)
			{
				var entry = _visibleOptions[i];
				int buttonNum = i + 1;
				var btn = EntityManager.GetEntity($"NarrativeEventOptionButton{buttonNum}");
				bool hovered = btn?.GetComponent<UIElement>()?.IsHovered ?? false;

				var rect = _layout.OptionButtons != null && i < _layout.OptionButtons.Length
					? _layout.OptionButtons[i]
					: Rectangle.Empty;

				string label = i < _textMetrics.OptionLabels.Count ? _textMetrics.OptionLabels[i].Label : entry.Text;
				Vector2 pos = i < _textMetrics.OptionLabels.Count ? _textMetrics.OptionLabels[i].Pos : Vector2.Zero;
				float scale = i < _textMetrics.OptionLabels.Count ? _textMetrics.OptionLabels[i].Scale : OptionTextScale;

				ModalOverlayChrome.DrawActionButton(
					_spriteBatch,
					_pixel,
					render.Transform(rect),
					hovered,
					BorderThickness,
					_bodyFont,
					label,
					render.Transform(pos),
					render.TransformScale(scale),
					render.ApplyShell(Color.White));
			}
		}

		private void EnsureLayout(int vw, int vh, int visibleCount, SceneState scene)
		{
			if (_layoutValid && _textMetricsValid && vw == _cachedVw && vh == _cachedVh && visibleCount == _cachedVisibleCount)
			{
				return;
			}

			_cachedVw = vw;
			_cachedVh = vh;
			_cachedVisibleCount = visibleCount;
			_drawOnLocationOrSnapshot = _forceSnapshotDraw
				|| (scene != null && (scene.Current == SceneId.Location || scene.Current == SceneId.Climb || scene.Current == SceneId.Snapshot));

			int footerH = ComputeFooterHeight(visibleCount);
			var shell = ModalShellLayout.ComputeCentered(vw, vh, ModalWidth, ModalHeight, BorderThickness, footerH);

			var bodyInner = new Rectangle(
				shell.Body.X + BodyPaddingX,
				shell.Body.Y + BodyPaddingTop,
				System.Math.Max(1, shell.Body.Width - BodyPaddingX * 2),
				System.Math.Max(1, shell.Body.Height - BodyPaddingTop - BodyPaddingBottom));

			BuildVisibleOptionList();

			_layout = new NarrativeEventLayout
			{
				Modal = shell.Modal,
				Content = shell.Content,
				Body = shell.Body,
				BodyInner = bodyInner,
				Footer = shell.Footer
			};

			RebuildTextMetrics();
			_layoutValid = true;
			_textMetricsValid = true;
		}

		private void RebuildTextMetrics()
		{
			var metrics = new CachedTextMetrics
			{
				BodyLines = new List<string>(),
				BodyLinePositions = new List<Vector2>(),
				OptionLabels = new List<(string, Vector2, float)>()
			};

			string title = TextUtils.FilterUnsupportedGlyphs(_titleFont, GetTitle());
			metrics.TitleSize = _titleFont.MeasureString(title) * TitleScale;
			metrics.TitlePos = new Vector2(
				_layout.BodyInner.Center.X - metrics.TitleSize.X / 2f,
				_layout.BodyInner.Y + TitleOffsetY);

			float cursorY = metrics.TitlePos.Y + metrics.TitleSize.Y + BodyStackGap;
			_layout.RuleY = (int)cursorY;
			cursorY += RedRuleHeight + BodyStackGap + BodyOffsetY;

			if (_bodyFont != null && (_activeEvent != null || _suppliedContent != null))
			{
				string body = TextUtils.FilterUnsupportedGlyphs(_bodyFont, GetBody());
				metrics.BodyLines = TextUtils.WrapText(_bodyFont, body, BodyTextScale, _layout.BodyInner.Width);
				foreach (var line in metrics.BodyLines)
				{
					metrics.BodyLinePositions.Add(new Vector2(_layout.BodyInner.X, cursorY));
					cursorY += _bodyFont.MeasureString(line).Y * BodyTextScale;
				}
			}

			int footerPad = FooterPadding;
			int optionH = System.Math.Max(30, OptionMinHeight);
			int optionW = System.Math.Max(1, _layout.Footer.Width - footerPad * 2);
			int optionX = _layout.Footer.X + footerPad;
			var optionButtons = new Rectangle[3];

			for (int i = 0; i < _visibleOptions.Count; i++)
			{
				var entry = _visibleOptions[i];
				int y = _layout.Footer.Y + footerPad + i * (optionH + OptionGap);
				var rect = new Rectangle(optionX, y, optionW, optionH);
				optionButtons[i] = rect;

				string label = ClipOptionLabel(entry.Text, optionW);
				var labelSize = _bodyFont.MeasureString(label) * OptionTextScale;
				var pos = new Vector2(
					rect.Center.X - labelSize.X / 2f,
					rect.Center.Y - labelSize.Y / 2f);
				metrics.OptionLabels.Add((label, pos, OptionTextScale));
			}

			_layout.OptionButtons = optionButtons;
			_textMetrics = metrics;
		}

		private void BuildVisibleOptionList()
		{
			_visibleOptions.Clear();
			if (_suppliedContent != null)
			{
				_visibleOptions.Add((0, _suppliedContent.ConfirmLabel));
				return;
			}
			if (_activeEvent == null) return;

			if (!string.IsNullOrWhiteSpace(_activeEvent.Option1Text)) _visibleOptions.Add((1, _activeEvent.Option1Text));
			if (!string.IsNullOrWhiteSpace(_activeEvent.Option2Text)) _visibleOptions.Add((2, _activeEvent.Option2Text));
			if (!string.IsNullOrWhiteSpace(_activeEvent.Option3Text)) _visibleOptions.Add((3, _activeEvent.Option3Text));

			if (_snapshotVisibleOptionCap > 0 && _visibleOptions.Count > _snapshotVisibleOptionCap)
				_visibleOptions.RemoveRange(_snapshotVisibleOptionCap, _visibleOptions.Count - _snapshotVisibleOptionCap);
		}

		private int CountVisibleOptions()
		{
			if (_suppliedContent != null) return 1;
			if (_activeEvent == null) return 0;
			int count = 0;
			if (!string.IsNullOrWhiteSpace(_activeEvent.Option1Text)) count++;
			if (!string.IsNullOrWhiteSpace(_activeEvent.Option2Text)) count++;
			if (!string.IsNullOrWhiteSpace(_activeEvent.Option3Text)) count++;
			if (_snapshotVisibleOptionCap > 0) count = System.Math.Min(count, _snapshotVisibleOptionCap);
			return count;
		}

		private string GetTitle() => _suppliedContent?.Title ?? _activeEvent?.Title ?? string.Empty;

		private string GetBody() => _suppliedContent?.Body ?? _activeEvent?.EventText ?? string.Empty;

		private int ComputeFooterHeight(int visibleCount)
		{
			int pad = FooterPadding;
			int optionH = System.Math.Max(30, OptionMinHeight);
			if (visibleCount <= 0) return pad * 2 + optionH;
			return pad * 2 + visibleCount * optionH + System.Math.Max(0, visibleCount - 1) * OptionGap;
		}

		private string ClipOptionLabel(string text, int maxWidthPx)
		{
			if (_bodyFont == null || string.IsNullOrEmpty(text)) return string.Empty;
			string safe = TextUtils.FilterUnsupportedGlyphs(_bodyFont, text);
			if (_bodyFont.MeasureString(safe).X * OptionTextScale <= maxWidthPx) return safe;

			string ellipsis = "...";
			for (int len = safe.Length; len > 0; len--)
			{
				string candidate = safe.Substring(0, len) + ellipsis;
				if (_bodyFont.MeasureString(candidate).X * OptionTextScale <= maxWidthPx)
					return candidate;
			}
			return ellipsis;
		}

		private ModalAnimation EnsureModalAnimation()
		{
			var overlay = EntityManager.GetEntity("NarrativeEventOverlay");
			if (overlay == null) return null;
			var animation = overlay.GetComponent<ModalAnimation>();
			if (animation == null)
			{
				animation = new ModalAnimation { InputContextId = ContextId };
				EntityManager.AddComponent(overlay, animation);
			}
			animation.InputContextId = ContextId;
			return animation;
		}

		private void RequestOpenAnimation()
		{
			var animation = EnsureModalAnimation();
			if (animation == null) return;
			animation.RequestedVisible = true;
			if (_forceSnapshotDraw || IsSnapshotScene())
			{
				animation.Phase = ModalAnimationPhase.Visible;
				animation.ElapsedSeconds = 0f;
			}
			_pendingCloseExitSequence = -1;
		}

		private bool IsSnapshotScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Snapshot;
		}

		private void RequestCloseAnimation()
		{
			var overlay = EntityManager.GetEntity("NarrativeEventOverlay");
			var state = overlay?.GetComponent<NarrativeEventOverlayState>();
			if (state == null) return;
			var animation = EnsureModalAnimation();
			if (animation == null || animation.Phase == ModalAnimationPhase.Hidden)
			{
				CloseOverlay();
				return;
			}

			DisableOptionButtons();
			animation.RequestedVisible = false;
			_pendingCloseExitSequence = animation.Phase == ModalAnimationPhase.Exiting
				? animation.ExitSequence
				: animation.ExitSequence + 1;
		}

		private void CompletePendingCloseIfReady(Entity overlayEntity)
		{
			if (_pendingCloseExitSequence < 0) return;
			var animation = overlayEntity?.GetComponent<ModalAnimation>();
			if (animation == null || animation.CompletedExitSequence < _pendingCloseExitSequence) return;

			_pendingCloseExitSequence = -1;
			CloseOverlay();
		}

		private bool IsModalAnimationInteractive()
		{
			var animation = EntityManager.GetEntity("NarrativeEventOverlay")?.GetComponent<ModalAnimation>();
			return animation == null || animation.Phase == ModalAnimationPhase.Visible;
		}

		private void DisableOptionButtons()
		{
			for (int i = 1; i <= 3; i++)
			{
				var btnUi = EntityManager.GetEntity($"NarrativeEventOptionButton{i}")?.GetComponent<UIElement>();
				if (btnUi == null) continue;
				btnUi.IsInteractable = false;
				btnUi.IsClicked = false;
			}
		}

		private void CloseOverlay()
		{
			var overlayEntity = EntityManager.GetEntity("NarrativeEventOverlay");
			var st = overlayEntity?.GetComponent<NarrativeEventOverlayState>();
			if (st == null) return;
			var animation = overlayEntity.GetComponent<ModalAnimation>();
			if (animation != null)
			{
				animation.RequestedVisible = false;
				animation.Phase = ModalAnimationPhase.Hidden;
				animation.ElapsedSeconds = 0f;
			}
			_pendingCloseExitSequence = -1;

			st.IsOpen = false;
			st.RunMapEventId = string.Empty;
			st.EventTypeId = string.Empty;
			st.ResolutionContextId = string.Empty;
			_activeEvent = null;
			_suppliedContent = null;
			_snapshotVisibleOptionCap = 0;
			_forceSnapshotDraw = false;
			StateSingleton.PreventClicking = false;
			var context = overlayEntity.GetComponent<InputContext>();
			if (context != null)
			{
				context.IsActive = false;
			}

			for (int i = 1; i <= 3; i++)
			{
				var btnUi = EntityManager.GetEntity($"NarrativeEventOptionButton{i}")?.GetComponent<UIElement>();
				if (btnUi != null)
				{
					btnUi.Bounds = Rectangle.Empty;
					btnUi.IsInteractable = false;
					btnUi.IsHidden = true;
				}
			}
			_visibleOptions.Clear();

			InvalidateCaches();
		}

		private void InvalidateCaches()
		{
			_layoutValid = false;
			_textMetricsValid = false;
		}

		private void EnsureOverlayEntity()
		{
			var e = EntityManager.GetEntity("NarrativeEventOverlay");
			if (e == null)
			{
				e = EntityManager.CreateEntity("NarrativeEventOverlay");
				EntityManager.AddComponent(e, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
				EntityManager.AddComponent(e, new NarrativeEventOverlayState());
				InputContextService.EnsureContext(
					EntityManager,
					e,
					ContextId,
					730,
					false);
				EntityManager.AddComponent(e, new ModalAnimation { InputContextId = ContextId });
				EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
				EntityManager.AddComponent(e, new DontDestroyOnLoad());
			}
			else
			{
				if (e.GetComponent<UIElement>() != null)
				{
					EntityManager.RemoveComponent<UIElement>(e);
				}
				var t = e.GetComponent<Transform>();
				if (t != null) t.ZOrder = ZOrder;
				EnsureModalAnimation();
			}
		}

		private Entity EnsureOptionButton(int index)
		{
			string name = $"NarrativeEventOptionButton{index}";
			var ent = EntityManager.GetEntity(name);
			if (ent == null)
			{
				ent = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(ent, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 2 });
				EntityManager.AddComponent(ent, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					IsHidden = true,
					LayerType = UILayerType.Overlay
				});
				EntityManager.AddComponent(ent, ParallaxLayer.GetUIParallaxLayer());
				InputContextService.EnsureMember(
					EntityManager,
					ent,
					ContextId);
				EntityManager.AddComponent(ent, new DontDestroyOnLoad());
			}
			return ent;
		}
	}
}
