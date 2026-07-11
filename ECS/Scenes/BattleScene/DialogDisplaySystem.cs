using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Dialog;
using System.Collections.Generic;
using Crusaders30XX.ECS.Utils;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Scenes.BattleScene;
using Crusaders30XX.ECS.Utils.RichText;
using Crusaders30XX.ECS.Rendering;
using System;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("Dialog Overlay")]
    public class DialogDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private readonly SpriteFont _titleFont;
        private readonly SpriteFont _bodyFont;
        private readonly Texture2D _pixel;

        private Texture2D _skipButtonTexture;
        private Texture2D _roundedCardTexture;
        private Texture2D _railGradientTexture;
        private Texture2D _bottomBarGradientTexture;
        private int _cardCachedW, _cardCachedH, _cardCachedR;
        private int _railGradientTextureWidth;
        private int _bottomBarGradientTextureWidth;

        // Debug-editable layout — phases
        [DebugEditable(DisplayName = "Intro Duration (s)", Step = 0.05f, Min = 0.1f, Max = 5f)]
        public float IntroDurationSec { get; set; } = 0.62f;

        [DebugEditable(DisplayName = "Outro Duration (s)", Step = 0.05f, Min = 0.1f, Max = 5f)]
        public float OutroDurationSec { get; set; } = 0.52f;

        // Rail
        [DebugEditable(DisplayName = "Rail Width %", Step = 0.01f, Min = 0.1f, Max = 0.7f)]
        public float RailWidthPercent { get; set; } = 0.38f;

        [DebugEditable(DisplayName = "Rail Accent W (px)", Step = 1, Min = 1, Max = 12)]
        public int RailAccentWidthPx { get; set; } = 3;

        [DebugEditable(DisplayName = "Rail Grad Steps", Step = 1, Min = 2, Max = 40)]
        public int RailGradientSteps { get; set; } = 10;

        // Portrait
        [DebugEditable(DisplayName = "Portrait Left %", Step = 0.01f, Min = 0.05f, Max = 0.45f)]
        public float PortraitLeftPercent { get; set; } = 0.19f;

        [DebugEditable(DisplayName = "Portrait Slot W", Step = 1, Min = 100, Max = 800)]
        public int PortraitSlotWidth { get; set; } = 420;

        [DebugEditable(DisplayName = "Portrait Slot H", Step = 1, Min = 100, Max = 900)]
        public int PortraitSlotHeight { get; set; } = 520;

        [DebugEditable(DisplayName = "Portrait Bottom Ofs", Step = 1, Min = 0, Max = 400)]
        public int PortraitSlotBottomOffset { get; set; } = 120;

        [DebugEditable(DisplayName = "Portrait Exit Dur (s)", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float PortraitExitDurationSec { get; set; } = 0.22f;

        [DebugEditable(DisplayName = "Portrait Exit Slide", Step = 1, Min = 0, Max = 300)]
        public int PortraitExitSlidePx { get; set; } = 110;

        [DebugEditable(DisplayName = "Portrait Enter Dur (s)", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float PortraitEnterDurationSec { get; set; } = 0.38f;

        [DebugEditable(DisplayName = "Portrait Enter Slide", Step = 1, Min = 0, Max = 500)]
        public int PortraitEnterSlidePx { get; set; } = 180;

        [DebugEditable(DisplayName = "Portrait Shadow OfsY", Step = 1, Min = 0, Max = 60)]
        public int PortraitShadowOffsetY { get; set; } = 12;

        [DebugEditable(DisplayName = "Portrait Shadow Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
        public float PortraitShadowAlpha { get; set; } = 0.55f;

        // Stage
        [DebugEditable(DisplayName = "Stage Bottom Ofs", Step = 1, Min = 0, Max = 300)]
        public int StageBottomOffset { get; set; } = 72;

        [DebugEditable(DisplayName = "Stage Height", Step = 1, Min = 80, Max = 500)]
        public int StageHeight { get; set; } = 248;

        [DebugEditable(DisplayName = "Stage Max Width", Step = 1, Min = 200, Max = 1920)]
        public int StageMaxWidth { get; set; } = 960;

        [DebugEditable(DisplayName = "Stage Pad Left", Step = 1, Min = 0, Max = 200)]
        public int StagePaddingLeft { get; set; } = 48;

        [DebugEditable(DisplayName = "Stage Pad Right", Step = 1, Min = 0, Max = 200)]
        public int StagePaddingRight { get; set; } = 64;

        [DebugEditable(DisplayName = "Speaker Line H", Step = 1, Min = 20, Max = 120)]
        public int SpeakerLineHeight { get; set; } = 50;

        [DebugEditable(DisplayName = "Speaker Line Gap", Step = 1, Min = 0, Max = 60)]
        public int SpeakerLineGap { get; set; } = 14;

        [DebugEditable(DisplayName = "Speaker Dash W", Step = 1, Min = 8, Max = 120)]
        public int SpeakerDashWidth { get; set; } = 48;

        [DebugEditable(DisplayName = "Speaker Dash H", Step = 1, Min = 1, Max = 8)]
        public int SpeakerDashHeight { get; set; } = 2;

        [DebugEditable(DisplayName = "Speaker Name Gap", Step = 1, Min = 0, Max = 60)]
        public int SpeakerNameGap { get; set; } = 14;

        [DebugEditable(DisplayName = "Text BG Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float TextBgAlpha { get; set; } = 0.55f;

        // Typography
        [DebugEditable(DisplayName = "Speaker Name Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float SpeakerNameScale { get; set; } = 0.25f;

        [DebugEditable(DisplayName = "Body Text Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
        public float BodyScale { get; set; } = 0.21875f;

        [DebugEditable(DisplayName = "Prompt Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float PromptScale { get; set; } = 0.10156f;

        [DebugEditable(DisplayName = "Prompt Pulse Min A", Step = 0.05f, Min = 0f, Max = 1f)]
        public float PromptPulseMinAlpha { get; set; } = 0.35f;

        [DebugEditable(DisplayName = "Prompt Pulse Max A", Step = 0.05f, Min = 0f, Max = 1f)]
        public float PromptPulseMaxAlpha { get; set; } = 0.75f;

        [DebugEditable(DisplayName = "Prompt Pulse Period", Step = 0.1f, Min = 0.2f, Max = 5f)]
        public float PromptPulsePeriodSec { get; set; } = 1.6f;

        [DebugEditable(DisplayName = "Idle Title Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float IdleTitleScale { get; set; } = 0.21875f;

        [DebugEditable(DisplayName = "Idle Body Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float IdleBodyScale { get; set; } = 0.109375f;

        // Bottom bar
        [DebugEditable(DisplayName = "Bottom Bar H", Step = 1, Min = 1, Max = 16)]
        public int BottomBarHeight { get; set; } = 4;

        [DebugEditable(DisplayName = "Bottom Bar Steps", Step = 1, Min = 2, Max = 40)]
        public int BottomBarGradientSteps { get; set; } = 10;

        // Skip button
        [DebugEditable(DisplayName = "Skip Btn Margin", Step = 1, Min = 0, Max = 200)]
        public int SkipBtnMargin { get; set; } = 16;

        [DebugEditable(DisplayName = "Skip Btn Text Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
        public float SkipBtnTextScale { get; set; } = 0.140625f;

        [DebugEditable(DisplayName = "Skip Btn Border Px", Step = 1, Min = 0, Max = 6)]
        public int SkipBtnBorderPx { get; set; } = 2;

        // Idle overlay
        [DebugEditable(DisplayName = "Idle Overlay Alpha", Step = 0.05f, Min = 0f, Max = 1f)]
        public float IdleOverlayAlpha { get; set; } = 0.25f;

        [DebugEditable(DisplayName = "Idle Card Pad X", Step = 1, Min = 0, Max = 120)]
        public int IdleCardPaddingX { get; set; } = 40;

        [DebugEditable(DisplayName = "Idle Card Pad Y", Step = 1, Min = 0, Max = 80)]
        public int IdleCardPaddingY { get; set; } = 28;

        [DebugEditable(DisplayName = "Idle Card Radius", Step = 1, Min = 0, Max = 40)]
        public int IdleCardBorderRadius { get; set; } = 8;

        [DebugEditable(DisplayName = "Idle Card BG Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float IdleCardBgAlpha { get; set; } = 0.72f;

        // Click prompt
        [DebugEditable(DisplayName = "Prompt Bottom", Step = 1, Min = 0, Max = 200)]
        public int ClickPromptBottom { get; set; } = 18;

        [DebugEditable(DisplayName = "Prompt Right", Step = 1, Min = 0, Max = 200)]
        public int ClickPromptRight { get; set; } = 28;

        // General
        [DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
        public int ZOrder { get; set; } = 50000;

        [DebugEditable(DisplayName = "Chars / Second", Step = 1f, Min = 1f, Max = 120f)]
        public float CharsPerSecond { get; set; } = 80f;

        // Rich text/effects
        [DebugEditable(DisplayName = "Enable Effects")] public bool EnableEffects { get; set; } = true;
        [DebugEditable(DisplayName = "Jitter Amp", Step = 0.5f, Min = 0f, Max = 20f)] public float JitterAmp { get; set; } = 2f;
        [DebugEditable(DisplayName = "Jitter Freq", Step = 0.5f, Min = 0f, Max = 30f)] public float JitterFreq { get; set; } = 12f;
        [DebugEditable(DisplayName = "Shake Amp", Step = 0.5f, Min = 0f, Max = 30f)] public float ShakeAmp { get; set; } = 4f;
        [DebugEditable(DisplayName = "Shake Freq", Step = 0.5f, Min = 0f, Max = 30f)] public float ShakeFreq { get; set; } = 6f;
        [DebugEditable(DisplayName = "Nod Amp", Step = 0.5f, Min = 0f, Max = 30f)] public float NodAmp { get; set; } = 3f;
        [DebugEditable(DisplayName = "Nod Freq", Step = 0.5f, Min = 0f, Max = 30f)] public float NodFreq { get; set; } = 2.5f;
        [DebugEditable(DisplayName = "Ripple Amp", Step = 0.5f, Min = 0f, Max = 30f)] public float RippleAmp { get; set; } = 3f;
        [DebugEditable(DisplayName = "Ripple Wavelength", Step = 1f, Min = 10f, Max = 200f)] public float RippleWavelength { get; set; } = 40f;
        [DebugEditable(DisplayName = "Ripple Speed", Step = 0.1f, Min = 0f, Max = 10f)] public float RippleSpeed { get; set; } = 1.2f;
        [DebugEditable(DisplayName = "Big Scale", Step = 0.05f, Min = 0.5f, Max = 4f)] public float BigScale { get; set; } = 1.5f;
        [DebugEditable(DisplayName = "Small Scale", Step = 0.05f, Min = 0.1f, Max = 1f)] public float SmallScale { get; set; } = 0.75f;
        [DebugEditable(DisplayName = "Pop Duration", Step = 0.05f, Min = 0.05f, Max = 1.5f)] public float PopDuration { get; set; } = 0.25f;
        [DebugEditable(DisplayName = "Pop Scale", Step = 0.05f, Min = 1f, Max = 3f)] public float PopScale { get; set; } = 1.35f;
        [DebugEditable(DisplayName = "Bloom Radius", Step = 1f, Min = 0f, Max = 20f)] public float BloomRadius { get; set; } = 2f;
        [DebugEditable(DisplayName = "Bloom Intensity", Step = 0.05f, Min = 0f, Max = 1f)] public float BloomIntensity { get; set; } = 0.35f;
        [DebugEditable(DisplayName = "Bloom Passes", Step = 1f, Min = 0f, Max = 8f)] public int BloomPasses { get; set; } = 0;
        [DebugEditable(DisplayName = "Fast Speed x", Step = 0.1f, Min = 0.1f, Max = 5f)] public float FastSpeedFactor { get; set; } = 2f;
        [DebugEditable(DisplayName = "Slow Speed x", Step = 0.1f, Min = 0.1f, Max = 5f)] public float SlowSpeedFactor { get; set; } = 0.5f;

        // Colors
        private static readonly Color RailAccentRed = new Color(196, 30, 58);
        private static readonly Color BottomBarRed = new Color(196, 30, 58);
        private static readonly Color SpeakerDashRed = new Color(196, 30, 58);
        private static readonly Color SkipBtnBorderRed = new Color(139, 0, 0);
        private static readonly Color SkipBtnHover = new Color(255, 245, 245);
        private static readonly Color SpeakerNameColor = Color.White;
        private static readonly Color SpeakerNameSwapColor = new Color(196, 30, 58);
        private static readonly Color BodyTextColor = new Color(240, 236, 230);
        private static readonly Color BodyTextShadowColor = Color.Black * 0.6f;
        private static readonly Color ClickPromptColor = Color.White * 0.45f;
        private static readonly Color IdleCardBgColor = new Color(10, 10, 10) * 0.72f;
        private static readonly Color IdleCardBorderColor = Color.White * 0.12f;
        private static readonly Color IdleTitleColor = Color.White;
        private static readonly Color IdleBodyColor = new Color(200, 192, 184);

        // Typewriter state
        private int _cachedLineIndex = -1;
        private string _cachedFilteredMessage = string.Empty;
        private float _revealProgressSec = 0f;
        private int _revealedChars = 0;
        private bool _lineComplete = false;
        private bool _wasTypingLastFrame = false;

        // Rich text
        private FlattenedRichText _flat;
        private LaidOutText _layout;
        private List<float> _glyphRevealTimes = new List<float>();
        private float _effectsTimeSec = 0f;

        // Phase animation state
        private float _phaseElapsedSec = 0f;

        // Portrait swap state
        private Texture2D _portraitCurrent;
        private Texture2D _portraitEntering;
        private float _portraitSwapTimer = 0f;

        // Previous actor for change detection
        private string _previousActor = string.Empty;

        private struct CinematicLayout
        {
            public Rectangle Rail;
            public Rectangle RailAccent;
            public Rectangle PortraitSlot;
            public Rectangle Stage;
            public Rectangle SpeakerDash;
            public Vector2 SpeakerNamePos;
            public Rectangle BodyTextArea;
            public Rectangle BottomBar;
            public Rectangle SkipButton;
            public Vector2 ClickPromptPos;
        }

        private enum PortraitSwapState { None, Exiting, Entering }
        private PortraitSwapState _swapState = PortraitSwapState.None;

        private DialogTextEffectSettings BuildSettings()
        {
            return new DialogTextEffectSettings
            {
                EnableEffects = EnableEffects,
                JitterAmplitudePx = JitterAmp,
                JitterFrequencyHz = JitterFreq,
                ShakeAmplitudePx = ShakeAmp,
                ShakeFrequencyHz = ShakeFreq,
                NodAmplitudePx = NodAmp,
                NodFrequencyHz = NodFreq,
                RippleAmplitudePx = RippleAmp,
                RippleWavelengthPx = RippleWavelength,
                RippleSpeedHz = RippleSpeed,
                BigScale = BigScale,
                SmallScale = SmallScale,
                PopDurationSec = PopDuration,
                PopScaleOnReveal = PopScale,
                BloomRadiusPx = BloomRadius,
                BloomIntensity01 = BloomIntensity,
                BloomPasses = BloomPasses,
                FastSpeedFactor = FastSpeedFactor,
                SlowSpeedFactor = SlowSpeedFactor,
            };
        }

        private static bool HasVisualEffect(List<EffectInstance> effects)
        {
            if (effects == null) return false;
            for (int k = 0; k < effects.Count; k++)
            {
                var t = effects[k].Type;
                if (t == TextEffectType.Jitter || t == TextEffectType.Shake || t == TextEffectType.Nod || t == TextEffectType.Ripple || t == TextEffectType.Big || t == TextEffectType.Small || t == TextEffectType.Explode || t == TextEffectType.Bloom)
                    return true;
            }
            return false;
        }

        private static bool HasBloom(List<EffectInstance> effects)
        {
            if (effects == null) return false;
            for (int k = 0; k < effects.Count; k++) if (effects[k].Type == TextEffectType.Bloom) return true;
            return false;
        }

        public bool IsOverlayActive
        {
            get
            {
                var e = EntityManager.GetEntitiesWithComponent<DialogOverlayState>().FirstOrDefault();
                var st = e?.GetComponent<DialogOverlayState>();
                return st?.IsActive ?? false;
            }
        }

        public DialogDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets) : base(em)
        {
            _graphicsDevice = gd;
            _spriteBatch = sb;
            _imageAssets = imageAssets;
            _titleFont = FontSingleton.TitleFont;
            _bodyFont = FontSingleton.ChakraPetchFont;
            _pixel = _imageAssets.GetPixel(Color.White);

            EventManager.Subscribe<QuestSelected>(OnQuestSelected);
            EventManager.Subscribe<TransitionCompleteEvent>(OnTransitionComplete);
            EventManager.Subscribe<DialogueSequenceRequested>(OnDialogueSequenceRequested);
            EventManager.Subscribe<DialogSkipRequested>(OnDialogSkipRequested);
            EventManager.Subscribe<DialogEnded>(_ => ClearPendingDialog());
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            EnsureOverlayEntity();
            var overlayEntity = EntityManager.GetEntity("DialogOverlay");
            var ui = overlayEntity?.GetComponent<UIElement>();
            var state = overlayEntity?.GetComponent<DialogOverlayState>();
            if (ui == null || state == null) return;

            InputContextService.EnsureContext(
                EntityManager,
                overlayEntity,
                "overlay.dialog",
                800,
                state.IsActive);
            ui.LayerType = UILayerType.Overlay;
            ui.ShowHoverHighlight = false;

            bool drawActive = state.IsActive;
            ui.IsInteractable = drawActive;
            ui.Bounds = drawActive
                ? new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight)
                : new Rectangle(0, 0, 0, 0);

            if (!drawActive)
            {
                ui.IsClicked = false;
                ui.IsHovered = false;
                var endBtn = EntityManager.GetEntity("DialogEndButton");
                var endUi = endBtn?.GetComponent<UIElement>();
                if (endUi != null) endUi.IsInteractable = false;
                return;
            }

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            switch (state.Phase)
            {
                case DialogPhase.Idle:
                    UpdateIdlePhase(ui, state, dt);
                    break;
                case DialogPhase.Intro:
                    UpdateIntroPhase(ui, state, dt);
                    break;
                case DialogPhase.Active:
                    UpdateActivePhase(ui, state, dt);
                    break;
                case DialogPhase.Outro:
                    UpdateOutroPhase(state, dt);
                    break;
            }
        }

        private void UpdateIdlePhase(UIElement ui, DialogOverlayState state, float dt)
        {
            if (ui.IsClicked)
            {
                ui.IsClicked = false;
                PlayIntro(state);
            }
        }

        private void UpdateIntroPhase(UIElement ui, DialogOverlayState state, float dt)
        {
            _phaseElapsedSec += dt;

            EnsureEndButtonEntity();

            if (ui.IsClicked)
            {
                ui.IsClicked = false;
                FinishIntro(state);
                return;
            }

            if (_phaseElapsedSec >= IntroDurationSec)
            {
                FinishIntro(state);
            }
        }

        private void UpdateActivePhase(UIElement ui, DialogOverlayState state, float dt)
        {
            UpdateTypewriter(state, dt);
            UpdatePortraitSwap(dt);

            bool isTyping = state.Phase == DialogPhase.Active
                && !_lineComplete
                && !string.IsNullOrEmpty(_cachedFilteredMessage);
            if (isTyping && !_wasTypingLastFrame)
            {
                EventManager.Publish(new PlaySfxEvent
                {
                    Track = SfxTrack.ActiveDialogue,
                    Loop = true,
                    Volume = 0.4f,
                });
            }
            else if (!isTyping && _wasTypingLastFrame)
            {
                EventManager.Publish(new StopSfxEvent { Track = SfxTrack.ActiveDialogue });
            }
            _wasTypingLastFrame = isTyping;

            EnsureEndButtonEntity();

            if (ui.IsClicked)
            {
                ui.IsClicked = false;
                if (!_lineComplete)
                {
                    _revealedChars = _cachedFilteredMessage.Length;
                    _lineComplete = true;
                }
                else
                {
                    state.Index++;
                    if (state.Index >= (state.Lines?.Count ?? 0))
                    {
                        PlayOutro(state);
                    }
                    else
                    {
                        AdvanceToLine(state);
                    }
                }
            }
        }

        private void UpdateOutroPhase(DialogOverlayState state, float dt)
        {
            _phaseElapsedSec += dt;
            _portraitSwapTimer += dt;

            if (_phaseElapsedSec >= OutroDurationSec)
            {
                FinishOutro(state);
            }
        }

        private void UpdateTypewriter(DialogOverlayState state, float dt)
        {
            if (_cachedLineIndex != state.Index)
            {
                ResetTypewriterForCurrentLine(state);
            }

            _effectsTimeSec += dt;

            if (!_lineComplete && CharsPerSecond > 0f)
            {
                _revealProgressSec += dt;
                while (_revealProgressSec > 0f && _flat != null && _layout != null && _revealedChars < _layout.GlyphLayouts.Count)
                {
                    float speed = 1f;
                    if (_flat != null && _revealedChars < _flat.Glyphs.Count)
                    {
                        speed = System.Math.Max(0.01f, _flat.Glyphs[_revealedChars].SpeedFactor);
                    }
                    float perCharTime = 1f / (CharsPerSecond * speed);
                    if (_revealProgressSec >= perCharTime)
                    {
                        _revealProgressSec -= perCharTime;
                        _revealedChars++;
                        _glyphRevealTimes.Add(0f);
                    }
                    else
                    {
                        break;
                    }
                }
                _lineComplete = _flat != null && _layout != null && _revealedChars >= _layout.GlyphLayouts.Count;
            }

            for (int i = 0; i < _glyphRevealTimes.Count; i++)
            {
                _glyphRevealTimes[i] += dt;
            }
        }

        private void UpdatePortraitSwap(float dt)
        {
            if (_swapState == PortraitSwapState.None) return;

            _portraitSwapTimer += dt;

            if (_swapState == PortraitSwapState.Exiting && _portraitSwapTimer >= PortraitExitDurationSec)
            {
                _portraitCurrent = _portraitEntering;
                _portraitEntering = null;
                _swapState = PortraitSwapState.Entering;
                _portraitSwapTimer = 0f;
            }

            if (_swapState == PortraitSwapState.Entering && _portraitSwapTimer >= PortraitEnterDurationSec)
            {
                _swapState = PortraitSwapState.None;
                _portraitSwapTimer = 0f;
            }
        }

        private void AdvanceToLine(DialogOverlayState state)
        {
            var line = (state.Index >= 0 && state.Index < (state.Lines?.Count ?? 0)) ? state.Lines[state.Index] : null;
            string actor = line?.actor ?? string.Empty;

            if (!string.IsNullOrEmpty(actor) && actor != _previousActor)
            {
                BeginPortraitSwap(actor);
                _previousActor = actor;
            }

            ResetTypewriterForCurrentLine(state);
        }

        private void BeginPortraitSwap(string actor)
        {
            Texture2D newTex = ResolvePortrait(actor);
            if (_swapState == PortraitSwapState.Exiting || _swapState == PortraitSwapState.Entering)
            {
                _portraitEntering = null;
            }
            if (_portraitCurrent == null)
            {
                _portraitCurrent = newTex;
                _swapState = PortraitSwapState.None;
                return;
            }
            _portraitEntering = newTex;
            _swapState = PortraitSwapState.Exiting;
            _portraitSwapTimer = 0f;
        }

        private void PlayIntro(DialogOverlayState state)
        {
            state.Phase = DialogPhase.Intro;
            _phaseElapsedSec = 0f;
            _previousActor = string.Empty;
            _swapState = PortraitSwapState.None;
            _portraitCurrent = null;
            _portraitEntering = null;
            _lineComplete = false;

            var line0 = (state.Lines?.Count > 0) ? state.Lines[0] : null;
            if (line0 != null)
            {
                _portraitCurrent = ResolvePortrait(line0.actor ?? string.Empty);
                _previousActor = line0.actor ?? string.Empty;
            }
            ResetTypewriterForCurrentLine(state);
        }

        private void PlayOutro(DialogOverlayState state)
        {
            state.Phase = DialogPhase.Outro;
            _phaseElapsedSec = 0f;
            _portraitSwapTimer = 0f;
            ClearTypewriterState();
        }

        private void FinishIntro(DialogOverlayState state)
        {
            state.Phase = DialogPhase.Active;
            _phaseElapsedSec = 0f;
        }

        private void FinishOutro(DialogOverlayState state)
        {
            state.Phase = DialogPhase.Idle;
            state.IsActive = false;
            _portraitCurrent = null;
            _portraitEntering = null;
            _swapState = PortraitSwapState.None;
            _previousActor = string.Empty;
            ClearTypewriterState();
            CompleteDialog(state);
        }

        private void ClearTypewriterState()
        {
            _cachedFilteredMessage = string.Empty;
            _cachedLineIndex = -1;
            _layout = null;
            _flat = null;
            _revealProgressSec = 0f;
            _revealedChars = 0;
            _glyphRevealTimes.Clear();
            _lineComplete = true;
            _effectsTimeSec = 0f;
            _wasTypingLastFrame = false;
            EventManager.Publish(new StopSfxEvent { Track = SfxTrack.ActiveDialogue });
        }

        public void Open(DialogDefinition def)
        {
            if (def == null || def.lines == null || def.lines.Count == 0) return;
            EnsureOverlayEntity();
            var e = EntityManager.GetEntity("DialogOverlay");
            var st = e.GetComponent<DialogOverlayState>();
            st.Lines = def.lines;
            st.Index = 0;
            st.IsActive = true;
            st.IsCorrelatedSequence = false;
            st.BackgroundOnly = false;
            st.DefinitionId = def.id ?? string.Empty;
            st.SegmentId = string.Empty;
            st.RequestId = Guid.Empty;
            PlayIntro(st);
        }

        private void OnDialogueSequenceRequested(DialogueSequenceRequested request)
        {
            if (request == null || request.RequestId == Guid.Empty) return;
            if (!DialogDefinitionCache.TryGet(request.DefinitionId, out var definition) || definition == null) return;

            var lines = definition.ResolveSegment(request.SegmentId);
            if (lines == null || lines.Count == 0) return;

            EnsureOverlayEntity();
            var state = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (state == null) return;

            state.Lines = lines.ToList();
            state.Index = 0;
            state.IsActive = true;
            state.IsCorrelatedSequence = true;
            state.BackgroundOnly = request.BackgroundOnly;
            state.DefinitionId = request.DefinitionId ?? string.Empty;
            state.SegmentId = request.SegmentId ?? string.Empty;
            state.RequestId = request.RequestId;
            PlayIntro(state);
        }

        private static void CompleteDialog(DialogOverlayState state)
        {
            if (state != null && state.IsCorrelatedSequence)
            {
                EventManager.Publish(new DialogueSequenceCompleted
                {
                    DefinitionId = state.DefinitionId,
                    SegmentId = state.SegmentId,
                    RequestId = state.RequestId,
                });
                state.IsCorrelatedSequence = false;
                state.BackgroundOnly = false;
                state.DefinitionId = string.Empty;
                state.SegmentId = string.Empty;
                state.RequestId = Guid.Empty;
                return;
            }

            EventManager.Publish(new DialogEnded());
        }

        private void OnDialogSkipRequested(DialogSkipRequested evt)
        {
            var state = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (state == null || !state.IsActive) return;
            if (state.Phase != DialogPhase.Intro && state.Phase != DialogPhase.Active) return;

            PlayOutro(state);
        }

        public void Draw()
        {
            var e = EntityManager.GetEntity("DialogOverlay");
            var st = e?.GetComponent<DialogOverlayState>();
            if (st == null || !st.IsActive) return;
            int vw = Game1.VirtualWidth;
            int vh = Game1.VirtualHeight;
            var layout = ComputeLayout(vw, vh);

            var line = (st.Index >= 0 && st.Index < (st.Lines?.Count ?? 0)) ? st.Lines[st.Index] : null;
            string actor = line?.actor ?? string.Empty;
            string message = line?.message ?? string.Empty;

            // 1. Idle overlay dim (idle phase only)
            if (st.Phase == DialogPhase.Idle)
            {
                DrawIdleOverlay(layout);
                return; // nothing else to draw in idle
            }

            // Animation progress
            float railProgress = RailProgress();
            float railAccentProgress = RailAccentProgress();
            float portraitOpacity = PortraitOpacity();
            float stageOpacity = StageOpacity();
            float stageSlide = StageTranslateX();
            float bottomBarProgress = BottomBarProgress();
            float speakerDashProgress = SpeakerDashProgress();
            float skipOpacity = SkipButtonOpacity();
            float skipSlideY = SkipButtonSlideY();

            // 2. Rail gradient clip-reveal
            DrawRail(layout, railProgress);

            // 3. Rail accent (scaleY)
            if (railAccentProgress > 0f)
            {
                int accentDrawH = (int)(layout.RailAccent.Height * railAccentProgress);
                int accentY = layout.RailAccent.Y + (layout.RailAccent.Height - accentDrawH) / 2;
                if (accentDrawH > 0)
                    _spriteBatch.Draw(_pixel, new Rectangle(layout.RailAccent.X, accentY, layout.RailAccent.Width, accentDrawH), RailAccentRed);
            }

            // 4. Bottom bar (scaleX from left)
            if (bottomBarProgress > 0f)
            {
                int barW = (int)System.Math.Ceiling(layout.BottomBar.Width * System.Math.Clamp(bottomBarProgress, 0f, 1f));
                DrawHorizontalGradientStrip(layout.BottomBar.X, layout.BottomBar.Y, layout.BottomBar.Width, barW, layout.BottomBar.Height, BottomBarRed, BottomBarGradientSteps);
            }

            // 5. Portrait
            DrawPortrait(layout, portraitOpacity, stageSlide, actor);

            // 6. Stage (opacity + slide)
            if (stageOpacity > 0f)
            {
                int stageDrawY = layout.Stage.Y;
                int stageDrawX = layout.Stage.X + (int)stageSlide;

                // Faint black background behind text
                if (TextBgAlpha > 0f)
                    _spriteBatch.Draw(_pixel,
                        new Rectangle(stageDrawX, stageDrawY, layout.Stage.Width, layout.Stage.Height),
                        Color.Black * TextBgAlpha * stageOpacity);

                // 6a. Speaker dash (scaleX)
                if (speakerDashProgress > 0f)
                {
                    int dashW = (int)(layout.SpeakerDash.Width * speakerDashProgress);
                    if (dashW > 0)
                        _spriteBatch.Draw(_pixel,
                            new Rectangle(stageDrawX + layout.SpeakerDash.X - layout.Stage.X, layout.SpeakerDash.Y, dashW, layout.SpeakerDash.Height),
                            SpeakerDashRed * stageOpacity);
                }

                // 6b. Speaker name
                DrawSpeakerName(layout, stageDrawX, stageDrawY, stageOpacity, actor);

                // 6c. Body text
                DrawBodyText(layout, stageDrawX, stageDrawY, stageOpacity, message);
            }

            // 7. Click prompt
            if (st.Phase == DialogPhase.Active && _lineComplete)
            {
                DrawClickPrompt(layout);
            }

            // 8. Skip button
            if (st.Phase != DialogPhase.Idle)
            {
                DrawSkipButton(layout, skipOpacity, skipSlideY);
            }
        }

        private CinematicLayout ComputeLayout(int vw, int vh)
        {
            int railW = (int)System.Math.Round(vw * System.Math.Clamp(RailWidthPercent, 0.1f, 0.7f));
            int stageX = railW + StagePaddingLeft;
            int stageY = vh - StageBottomOffset - StageHeight;
            int stageW = System.Math.Min(StageMaxWidth, vw - railW - StagePaddingLeft - StagePaddingRight);

            int dashX = stageX;
            int dashY = stageY + (SpeakerLineHeight - SpeakerDashHeight) / 2;

            float nameMeasuredW = _titleFont.MeasureString("A").X * SpeakerNameScale;
            int nameX = dashX + SpeakerDashWidth + SpeakerNameGap;
            float nameY = stageY + (SpeakerLineHeight - _titleFont.MeasureString("A").Y * SpeakerNameScale) / 2f;

            int bodyX = stageX;
            int bodyY = stageY + SpeakerLineHeight + SpeakerLineGap;
            int bodyH = StageHeight - SpeakerLineHeight - SpeakerLineGap;

            int portraitX = (int)System.Math.Round(vw * System.Math.Clamp(PortraitLeftPercent, 0.05f, 0.45f) - PortraitSlotWidth / 2f);
            int portraitY = vh - PortraitSlotBottomOffset - PortraitSlotHeight;

            int bottomBarX = railW;
            int bottomBarY = vh - BottomBarHeight;
            int bottomBarW = vw - railW;

            int skipW = _skipButtonTexture?.Width ?? 68;
            int skipH = _skipButtonTexture?.Height ?? 37;
            int skipX = vw - SkipBtnMargin - skipW;
            int skipY = SkipBtnMargin;

            string promptText = "CLICK TO CONTINUE";
            var promptSize = _bodyFont.MeasureString(promptText) * PromptScale;
            int promptX = vw - ClickPromptRight - (int)promptSize.X;
            int promptY = vh - ClickPromptBottom - (int)promptSize.Y;

            return new CinematicLayout
            {
                Rail = new Rectangle(0, 0, railW, vh),
                RailAccent = new Rectangle(railW - RailAccentWidthPx, 0, RailAccentWidthPx, vh),
                PortraitSlot = new Rectangle(portraitX, portraitY, PortraitSlotWidth, PortraitSlotHeight),
                Stage = new Rectangle(stageX, stageY, stageW, StageHeight),
                SpeakerDash = new Rectangle(dashX, dashY, SpeakerDashWidth, SpeakerDashHeight),
                SpeakerNamePos = new Vector2(nameX, nameY),
                BodyTextArea = new Rectangle(bodyX, bodyY, stageW, bodyH),
                BottomBar = new Rectangle(bottomBarX, bottomBarY, bottomBarW, BottomBarHeight),
                SkipButton = new Rectangle(skipX, skipY, skipW, skipH),
                ClickPromptPos = new Vector2(promptX, promptY),
            };
        }

        // Animation progress helpers
        private float RailProgress()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.6f);
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / System.Math.Max(0.001f, OutroDurationSec * (0.5f / 0.52f)));
                return 1f - EaseIn(t);
            }
            return 0f;
        }

        private float RailAccentProgress()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Max(0f, System.Math.Min(1f, (_phaseElapsedSec - 0.25f) / 0.55f));
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.4f);
                return 1f - EaseIn(t);
            }
            return 0f;
        }

        private float PortraitOpacity()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Max(0f, System.Math.Min(1f, (_phaseElapsedSec - 0.2f) / 0.35f));
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.35f);
                return 1f - EaseIn(t);
            }
            return 0f;
        }

        private float StageOpacity()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Max(0f, System.Math.Min(1f, (_phaseElapsedSec - 0.22f) / 0.5f));
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.35f);
                return 1f - EaseOut(t);
            }
            return 0f;
        }

        private float StageTranslateX()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 40f;
            if (st.Phase == DialogPhase.Active) return 0f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Max(0f, System.Math.Min(1f, (_phaseElapsedSec - 0.22f) / 0.5f));
                return (1f - t) * 40f;
            }
            if (st.Phase == DialogPhase.Outro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.35f);
                return t * 30f;
            }
            return 40f;
        }

        private float BottomBarProgress()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Max(0f, System.Math.Min(1f, (_phaseElapsedSec - 0.35f) / 0.55f));
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.3f);
                return 1f - EaseOut(t);
            }
            return 0f;
        }

        private float SpeakerDashProgress()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Max(0f, System.Math.Min(1f, (_phaseElapsedSec - 0.22f) / 0.35f));
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro) return 0f;
            return 0f;
        }

        private float SkipButtonOpacity()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return 0f;
            if (st.Phase == DialogPhase.Idle) return 0f;
            if (st.Phase == DialogPhase.Active) return 1f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.35f);
                return EaseOut(t);
            }
            if (st.Phase == DialogPhase.Outro) return 0f;
            return 0f;
        }

        private float SkipButtonSlideY()
        {
            var st = EntityManager.GetEntity("DialogOverlay")?.GetComponent<DialogOverlayState>();
            if (st == null) return -12f;
            if (st.Phase == DialogPhase.Idle) return -12f;
            if (st.Phase == DialogPhase.Active) return 0f;
            if (st.Phase == DialogPhase.Intro)
            {
                float t = System.Math.Min(1f, _phaseElapsedSec / 0.35f);
                return (1f - t) * -12f;
            }
            if (st.Phase == DialogPhase.Outro) return -12f;
            return -12f;
        }

        private float PromptAlpha()
        {
            float t = (float)(System.DateTime.Now.Ticks / 10000000.0) % PromptPulsePeriodSec / PromptPulsePeriodSec;
            float sin = (float)System.Math.Sin(t * System.Math.PI * 2) * 0.5f + 0.5f;
            return System.Math.Max(0f, PromptPulseMinAlpha + sin * (PromptPulseMaxAlpha - PromptPulseMinAlpha));
        }

        private static float EaseOut(float t) => t >= 1f ? 1f : 1f - (float)System.Math.Pow(1f - t, 3f);
        private static float EaseIn(float t) => t >= 1f ? 1f : (float)System.Math.Pow(t, 3f);

        // Draw helpers
        private void DrawIdleOverlay(CinematicLayout layout)
        {
            int vw = Game1.VirtualWidth;
            int vh = Game1.VirtualHeight;

            // Fullscreen dim
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, vw, vh), Color.Black * IdleOverlayAlpha);

            // Idle card
            string title = "Click to begin";
            string body = "Cinematic rail expands from the left";
            var titleSize = _titleFont.MeasureString(title) * IdleTitleScale;
            var bodySize = _bodyFont.MeasureString(body) * IdleBodyScale;
            float totalW = System.Math.Max(titleSize.X, bodySize.X) + IdleCardPaddingX * 2;
            float totalH = titleSize.Y + bodySize.Y + 8 + IdleCardPaddingY * 2;
            int cardX = (vw - (int)totalW) / 2;
            int cardY = (vh - (int)totalH) / 2;
            var cardRect = new Rectangle(cardX, cardY, (int)totalW, (int)totalH);

            // Card bg - rounded rect
            bool rebuild = _roundedCardTexture == null || _cardCachedW != cardRect.Width || _cardCachedH != cardRect.Height || _cardCachedR != IdleCardBorderRadius;
            if (rebuild)
            {
                _roundedCardTexture?.Dispose();
                _roundedCardTexture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, cardRect.Width, cardRect.Height, IdleCardBorderRadius);
                _cardCachedW = cardRect.Width; _cardCachedH = cardRect.Height; _cardCachedR = IdleCardBorderRadius;
            }
            _spriteBatch.Draw(_roundedCardTexture, cardRect, IdleCardBgColor);

            // Card border (1px white 12% alpha inset)
            DrawBorder(cardRect, IdleCardBorderColor, 1);

            // Card text
            float titleX = cardX + (totalW - titleSize.X) / 2f;
            float titleY = cardY + IdleCardPaddingY;
            _spriteBatch.DrawString(_titleFont, title, new Vector2(titleX, titleY), IdleTitleColor, 0f, Vector2.Zero, IdleTitleScale, SpriteEffects.None, 0f);

            float bodyX = cardX + (totalW - bodySize.X) / 2f;
            float bodyY = titleY + titleSize.Y + 8;
            _spriteBatch.DrawString(_bodyFont, body, new Vector2(bodyX, bodyY), IdleBodyColor, 0f, Vector2.Zero, IdleBodyScale, SpriteEffects.None, 0f);
        }

        private void DrawBorder(Rectangle rect, Color color, int thickness)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }

        private void DrawRail(CinematicLayout layout, float progress)
        {
            int visibleW = (int)System.Math.Ceiling(layout.Rail.Width * System.Math.Clamp(progress, 0f, 1f));
            if (visibleW <= 0) return;

            EnsureRailGradientTexture(layout.Rail.Width);
            visibleW = System.Math.Min(visibleW, _railGradientTextureWidth);
            _spriteBatch.Draw(
                _railGradientTexture,
                new Rectangle(layout.Rail.X, layout.Rail.Y, visibleW, layout.Rail.Height),
                new Rectangle(0, 0, _railGradientTextureWidth, 1),
                Color.White);
        }

        private void DrawHorizontalGradientStrip(int x, int y, int fullWidth, int visibleWidth, int height, Color baseColor, int steps)
        {
            if (fullWidth <= 0 || visibleWidth <= 0 || height <= 0) return;

            EnsureBottomBarGradientTexture(fullWidth, baseColor);
            int drawW = System.Math.Min(visibleWidth, fullWidth);
            _spriteBatch.Draw(
                _bottomBarGradientTexture,
                new Rectangle(x, y, drawW, height),
                new Rectangle(0, 0, _bottomBarGradientTextureWidth, 1),
                Color.White);
        }

        private void EnsureRailGradientTexture(int width)
        {
            width = System.Math.Max(1, width);
            if (_railGradientTexture != null && _railGradientTextureWidth == width) return;

            _railGradientTexture?.Dispose();
            _railGradientTexture = new Texture2D(_graphicsDevice, width, 1, false, SurfaceFormat.Color);
            _railGradientTextureWidth = width;

            var data = new Color[width];
            for (int x = 0; x < width; x++)
            {
                float t = width <= 1 ? 0f : x / (float)(width - 1);
                float alpha;
                if (t <= 0.85f)
                {
                    float localT = t / 0.85f;
                    alpha = 0.88f + localT * (0.55f - 0.88f);
                }
                else
                {
                    float localT = (t - 0.85f) / 0.15f;
                    alpha = 0.55f * (1f - localT);
                }
                data[x] = Color.Black * alpha;
            }
            _railGradientTexture.SetData(data);
        }

        private void EnsureBottomBarGradientTexture(int width, Color baseColor)
        {
            width = System.Math.Max(1, width);
            if (_bottomBarGradientTexture != null && _bottomBarGradientTextureWidth == width) return;

            _bottomBarGradientTexture?.Dispose();
            _bottomBarGradientTexture = new Texture2D(_graphicsDevice, width, 1, false, SurfaceFormat.Color);
            _bottomBarGradientTextureWidth = width;

            var data = new Color[width];
            for (int x = 0; x < width; x++)
            {
                float t = width <= 1 ? 0f : x / (float)(width - 1);
                float alpha = 1f - t;
                data[x] = baseColor * alpha;
            }
            _bottomBarGradientTexture.SetData(data);
        }

        private void DrawPortrait(CinematicLayout layout, float slotOpacity, float stageSlide, string actor)
        {
            if (slotOpacity <= 0f) return;

            // Portrait slot bounds
            var slot = layout.PortraitSlot;

            // During swap, also check for current/entering portraits
            if (_swapState == PortraitSwapState.Exiting && _portraitCurrent != null)
            {
                float t = System.Math.Min(1f, _portraitSwapTimer / System.Math.Max(0.001f, PortraitExitDurationSec));
                float slide = EaseIn(t) * PortraitExitSlidePx;
                float alpha = (1f - t) * slotOpacity;
                DrawSinglePortrait(_portraitCurrent, slot, alpha, -slide);
            }
            else if (_swapState == PortraitSwapState.Entering)
            {
                if (_portraitCurrent != null)
                {
                    float enterT = System.Math.Min(1f, _portraitSwapTimer / System.Math.Max(0.001f, PortraitEnterDurationSec));
                    float enterSlide = (1f - EaseOut(enterT)) * PortraitEnterSlidePx;
                    float enterAlpha = EaseOut(enterT) * slotOpacity;
                    DrawSinglePortrait(_portraitCurrent, slot, enterAlpha, -enterSlide);
                }
            }
            else if (_portraitCurrent != null)
            {
                DrawSinglePortrait(_portraitCurrent, slot, slotOpacity, 0f);
            }
        }

        private void DrawSinglePortrait(Texture2D tex, Rectangle slot, float alpha, float slideX)
        {
            if (tex == null || alpha <= 0f) return;

            float scale = System.Math.Min(slot.Width / (float)tex.Width, slot.Height / (float)tex.Height);
            int drawW = System.Math.Max(1, (int)System.Math.Round(tex.Width * scale));
            int drawH = System.Math.Max(1, (int)System.Math.Round(tex.Height * scale));
            int drawX = slot.X + (slot.Width - drawW) / 2 + (int)slideX;
            int drawY = slot.Y + slot.Height - drawH;

            // Drop shadow
            if (PortraitShadowAlpha > 0f && PortraitShadowOffsetY > 0)
            {
                int shadowY = drawY + PortraitShadowOffsetY;
                _spriteBatch.Draw(tex, new Rectangle(drawX, shadowY, drawW, drawH), Color.Black * PortraitShadowAlpha * alpha);
            }

            _spriteBatch.Draw(tex, new Rectangle(drawX, drawY, drawW, drawH), Color.White * alpha);
        }

        private void DrawSpeakerName(CinematicLayout layout, int stageDrawX, int stageDrawY, float opacity, string actor)
        {
            if (string.IsNullOrEmpty(actor) || opacity <= 0f) return;

            string safeActor = TextUtils.FilterUnsupportedGlyphs(_titleFont, actor ?? string.Empty);
            var namePos = layout.SpeakerNamePos;

            // Determine name color based on swap state
            Color nameColor = SpeakerNameColor * opacity;
            if (_swapState == PortraitSwapState.Exiting)
            {
                nameColor = Color.Lerp(SpeakerNameColor, SpeakerNameSwapColor, 0.5f) * opacity;
            }
            else if (_swapState == PortraitSwapState.Entering)
            {
                nameColor = Color.Lerp(SpeakerNameSwapColor, SpeakerNameColor, System.Math.Min(1f, _portraitSwapTimer / 0.18f)) * opacity;
            }

            var drawPos = new Vector2(stageDrawX + namePos.X - layout.Stage.X, namePos.Y);
            _spriteBatch.DrawString(_titleFont, safeActor, drawPos, nameColor, 0f, Vector2.Zero, SpeakerNameScale, SpriteEffects.None, 0f);
        }

        private void DrawBodyText(CinematicLayout layout, int stageDrawX, int stageDrawY, float opacity, string message)
        {
            if (_layout == null)
            {
                int bodyX = stageDrawX + layout.BodyTextArea.X - layout.Stage.X;
                _layout = RichTextLayout.Layout(_bodyFont, _cachedFilteredMessage ?? string.Empty, BodyScale, layout.BodyTextArea.Width, bodyX, layout.BodyTextArea.Y, 0);
            }

            int toDraw = System.Math.Min(_revealedChars, _layout.GlyphLayouts.Count);
            if (toDraw <= 0) return;

            var settings = BuildSettings();
            int i = 0;
            while (i < toDraw)
            {
                var gl = _layout.GlyphLayouts[i];
                var g = _flat.Glyphs[i];
                bool hasVisual = HasVisualEffect(g.Effects);
                if (!hasVisual)
                {
                    float lineY = gl.BasePosition.Y;
                    int j = i;
                    var buff = new System.Text.StringBuilder();
                    while (j < toDraw)
                    {
                        var glj = _layout.GlyphLayouts[j];
                        var gj = _flat.Glyphs[j];
                        if (System.Math.Abs(glj.BasePosition.Y - lineY) > 0.1f) break;
                        if (HasVisualEffect(gj.Effects)) break;
                        buff.Append(glj.Character);
                        j++;
                    }
                    if (buff.Length > 0)
                    {
                        var drawColor = BodyTextColor * opacity;

                        // Text shadow pass
                        if (BodyTextShadowColor.A > 0)
                        {
                            var shadowPos = gl.BasePosition + new Vector2(0, 2);
                            _spriteBatch.DrawString(_bodyFont, buff.ToString(), shadowPos, BodyTextShadowColor * opacity, 0f, Vector2.Zero, BodyScale, SpriteEffects.None, 0f);
                        }

                        _spriteBatch.DrawString(_bodyFont, buff.ToString(), gl.BasePosition, drawColor, 0f, Vector2.Zero, BodyScale, SpriteEffects.None, 0f);
                        i = j;
                        continue;
                    }
                }

                float since = i < _glyphRevealTimes.Count ? _glyphRevealTimes[i] : 999f;
                var xf = TextEffectApplier.ComposeTransforms(g.Effects, settings, _effectsTimeSec, i, since);
                if (EnableEffects && BloomPasses > 0 && HasBloom(g.Effects))
                {
                    for (int p = 0; p < BloomPasses; p++)
                    {
                        var offset = new Vector2((p - BloomPasses / 2f) * BloomRadius * 0.15f);
                        _spriteBatch.DrawString(_bodyFont, gl.Character.ToString(), gl.BasePosition + xf.Offset + offset, Color.White * BloomIntensity * opacity, 0f, Vector2.Zero, BodyScale * xf.Scale, SpriteEffects.None, 0f);
                    }
                }
                _spriteBatch.DrawString(_bodyFont, gl.Character.ToString(), gl.BasePosition + xf.Offset, Color.White * xf.Alpha * opacity, 0f, Vector2.Zero, BodyScale * xf.Scale, SpriteEffects.None, 0f);
                i++;
            }
        }

        private void DrawClickPrompt(CinematicLayout layout)
        {
            float alpha = PromptAlpha();
            if (alpha <= 0f) return;

            string text = "CLICK TO CONTINUE";
            _spriteBatch.DrawString(_bodyFont, text, layout.ClickPromptPos, ClickPromptColor * alpha, 0f, Vector2.Zero, PromptScale, SpriteEffects.None, 0f);
        }

        private void DrawSkipButton(CinematicLayout layout, float opacity, float slideY)
        {
            if (_skipButtonTexture == null || opacity <= 0f) return;

            int borderPx = SkipBtnBorderPx;
            int drawX = layout.SkipButton.X;
            int drawY = (int)(layout.SkipButton.Y + slideY);
            int drawW = layout.SkipButton.Width;
            int drawH = layout.SkipButton.Height;

            // Border rect (slightly larger)
            if (borderPx > 0)
            {
                _spriteBatch.Draw(_pixel, new Rectangle(drawX - borderPx, drawY - borderPx, drawW + borderPx * 2, drawH + borderPx * 2), SkipBtnBorderRed * opacity);
            }

            // Button fill
            var btnEnt = EntityManager.GetEntity("DialogEndButton");
            var endUi = btnEnt?.GetComponent<UIElement>();
            Color fillColor = (endUi != null && endUi.IsHovered) ? SkipBtnHover * opacity : Color.White * opacity;
            var drawRect = new Rectangle(drawX, drawY, drawW, drawH);
            _spriteBatch.Draw(_skipButtonTexture, drawRect, fillColor);

            if (endUi != null)
            {
                endUi.Bounds = new Rectangle(drawX - borderPx, drawY - borderPx, drawW + borderPx * 2, drawH + borderPx * 2);
            }
        }

        private Texture2D ResolvePortrait(string actor)
        {
            string assetName = ResolvePortraitAssetName(actor);
            if (string.IsNullOrEmpty(assetName)) return null;
            return _imageAssets.TryGetTexture(assetName);
        }

        internal static string ResolvePortraitAssetName(string actor)
        {
            if (string.IsNullOrWhiteSpace(actor)) return string.Empty;
            return actor.Trim().ToLowerInvariant() switch
            {
                "angel" or "remiel" => "guardian_angel",
                "crusader" => CrusaderPortraitAssets.DialogPortraitAsset,
                "horde" => "Horde",
                "skeleton" => "Skeleton",
                "sand_corpse" => "Sand_Corpse",
                "fallen shepherd" => "Fallen_Shepherd",
                "nun" => "character/nun",
                "reverent crusader" => "character/reverent_crusader",
                "revered crusader" => "character/revered_crusader",
                "smith" => "character/smith",
                "keeper" => "waystation/the-keeper",
                "elias" => "waystation/elias",
                "old confessor" => "waystation/old-confessor",
                "mara" => "waystation/mara",
                "rook" => "waystation/rook",
                _ => string.Empty,
            };
        }

        private void EnsureOverlayEntity()
        {
            var e = EntityManager.GetEntity("DialogOverlay");
            if (e == null)
            {
                e = EntityManager.CreateEntity("DialogOverlay");
                var t = new Transform { Position = Vector2.Zero, ZOrder = ZOrder };
                var ui = new UIElement
                {
                    Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
                    IsInteractable = true,
                    LayerType = UILayerType.Overlay,
                    ShowHoverHighlight = false,
                };
                EntityManager.AddComponent(e, t);
                EntityManager.AddComponent(e, ui);
                EntityManager.AddComponent(e, new DialogOverlayState());
                InputContextService.EnsureContext(
                    EntityManager,
                    e,
                    "overlay.dialog",
                    800,
                    false);
                EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
                EntityManager.AddComponent(e, new DontDestroyOnLoad());
            }
            else
            {
                var t = e.GetComponent<Transform>();
                if (t != null) t.ZOrder = ZOrder;
            }
        }

        private void EnsureEndButtonEntity()
        {
            _skipButtonTexture ??= ButtonTextureFactory.Create(_graphicsDevice, "Skip", Color.White, SkipBtnBorderRed, SkipBtnTextScale, cornerRadius: 4);

            int btnW = _skipButtonTexture.Width;
            int btnH = _skipButtonTexture.Height;
            int borderPx = SkipBtnBorderPx;

            var ent = EntityManager.GetEntity("DialogEndButton");
            if (ent == null)
            {
                ent = EntityManager.CreateEntity("DialogEndButton");
                int vw = Game1.VirtualWidth;
                int x = vw - SkipBtnMargin - btnW - borderPx * 2;
                int y = SkipBtnMargin;
                EntityManager.AddComponent(ent, new Transform { Position = new Vector2(x, y), ZOrder = ZOrder + 1 });
                EntityManager.AddComponent(ent, new UIElement { Bounds = new Rectangle(x, y, btnW + borderPx * 2, btnH + borderPx * 2), IsInteractable = true, LayerType = UILayerType.Overlay, EventType = UIElementEventType.SkipDialog });
                EntityManager.AddComponent(ent, new HotKey { Button = FaceButton.Start, RequiresHold = true });
                InputContextService.EnsureMember(
                    EntityManager,
                    ent,
                    "overlay.dialog");
                EntityManager.AddComponent(ent, ParallaxLayer.GetUIParallaxLayer());
            }
            else
            {
                int vw = Game1.VirtualWidth;
                int x = vw - SkipBtnMargin - btnW - borderPx * 2;
                int y = SkipBtnMargin;
                var t = ent.GetComponent<Transform>();
                if (t != null)
                {
                    t.ZOrder = ZOrder + 1;
                    t.Position = new Vector2(x, y);
                }
                var ui = ent.GetComponent<UIElement>();
                if (ui != null)
                {
                    ui.LayerType = UILayerType.Overlay;
                    ui.IsInteractable = true;
                    ui.EventType = UIElementEventType.SkipDialog;
                }
            }
        }

        private void ResetTypewriterForCurrentLine(DialogOverlayState st)
        {
            _cachedLineIndex = st.Index;
            var raw = (st.Index >= 0 && st.Index < (st.Lines?.Count ?? 0)) ? st.Lines[st.Index].message : string.Empty;
            var doc = RichTextParser.Parse(raw ?? string.Empty);
            var settings = BuildSettings();
            _flat = RichTextFlattener.FlattenAndFilter(doc, _bodyFont, settings);
            _cachedFilteredMessage = _flat.FilteredPlain;
            _layout = null;
            _revealProgressSec = 0f;
            _revealedChars = 0;
            _glyphRevealTimes.Clear();
            _lineComplete = string.IsNullOrEmpty(_cachedFilteredMessage);
            _effectsTimeSec = 0f;
        }

        private void OnQuestSelected(QuestSelected evt)
        {
            if (evt == null) return;
            string id = evt.QuestId;
            var qeEntity = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
            if (qeEntity == null) return;
            bool isGate = string.Equals(id, "fallen_shepherd", StringComparison.OrdinalIgnoreCase);
            string dialogId = isGate ? "fallen_shepherd" : id;
            if (DialogDefinitionCache.TryGet(dialogId, out var _))
            {
                var existing = qeEntity.GetComponent<PendingQuestDialog>();
                if (existing == null)
                {
                    EntityManager.AddComponent(qeEntity, new PendingQuestDialog
                    {
                        DialogId = dialogId,
                        SegmentId = isGate ? "intro" : string.Empty,
                        RequestId = isGate ? Guid.NewGuid() : Guid.Empty,
                        WillShowDialog = true,
                    });
                }
                else
                {
                    existing.DialogId = dialogId;
                    existing.SegmentId = isGate ? "intro" : string.Empty;
                    existing.RequestId = isGate ? Guid.NewGuid() : Guid.Empty;
                    existing.WillShowDialog = true;
                }
            }
            else
            {
                var existing = qeEntity.GetComponent<PendingQuestDialog>();
                if (existing != null) { existing.DialogId = string.Empty; existing.WillShowDialog = false; }
            }
        }

        private void OnTransitionComplete(TransitionCompleteEvent evt)
        {
            if (evt == null || evt.Scene != SceneId.Battle) return;
            var currentScene = EntityManager.GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>()
                ?.Current;
            if (currentScene != SceneId.Battle) return;
            var qeEntity = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
            var pending = qeEntity?.GetComponent<PendingQuestDialog>();
            if (pending == null) return;
            if (string.IsNullOrWhiteSpace(pending.DialogId)) { EntityManager.RemoveComponent<PendingQuestDialog>(qeEntity); return; }
            if (DialogDefinitionCache.TryGet(pending.DialogId, out var def) && def != null)
            {
                if (!pending.WillShowDialog) return;
                if (pending.RequestId != Guid.Empty)
                {
                    EventManager.Publish(new DialogueSequenceRequested
                    {
                        DefinitionId = pending.DialogId,
                        SegmentId = pending.SegmentId,
                        RequestId = pending.RequestId,
                    });
                }
                else
                {
                    Open(def);
                }
            }
            else
            {
                EntityManager.RemoveComponent<PendingQuestDialog>(qeEntity);
            }
        }

        private void ClearPendingDialog()
        {
            var qeEntity = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
            if (qeEntity == null) return;
            var pending = qeEntity.GetComponent<PendingQuestDialog>();
            if (pending != null) pending.WillShowDialog = false;
        }
    }
}
