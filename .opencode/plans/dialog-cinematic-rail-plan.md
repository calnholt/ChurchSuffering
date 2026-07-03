# Dialog Cinematic Rail Plan

## Context

- **Mockup:** `mockups/dialog-overlay-v1.html` — "Cinematic Rail" overlay
- **Target system:** `ECS/Scenes/BattleScene/DialogDisplaySystem.cs` (Archetype **D — Scene HUD**, screen overlay subset)
- **What changes:** Full visual redesign from bottom panel to fullscreen cinematic layout with phase state machine (idle → intro → active → outro)
- **What stays:** Rich text effects, typewriter reveal, portrait asset resolution, event subscriptions (`OnQuestSelected`, `OnTransitionComplete`, `OnDialogueSequenceRequested`), data model (`DialogLine`, `DialogDefinition`, `DialogOverlayState`), `BackgroundOnly` climb corridor behavior
- **Key new element:** Left gradient rail (38% width, clip-reveal animation), free-floating portrait (centered in left zone), right-stage text panel, bottom accent bar, idle "Click to begin" overlay, animated character-portrait swap

Reference patterns: `GameOverOverlayDisplaySystem.cs` (fade timers, fullscreen overlay), `PledgeDisplaySystem.cs` (animation keyframes per entity), `RoundedRectTextureFactory.cs` (gradient texture generation pattern).

## Files to Modify

| File | Change |
|------|--------|
| `ECS/Components/Scenes.cs` | Add `DialogPhase` enum + `Phase` field to `DialogOverlayState` |
| `ECS/Scenes/BattleScene/DialogDisplaySystem.cs` | Major rewrite: phases, animations, layout, draw pipeline |
| `ECS/Rendering/PrimitiveTextureFactory.cs` | Add `CreateHorizontalGradientTexture` (optional cached helper) |

## DialogPhase State Machine

```
idle ──(click)──→ intro ──(auto 620ms)──→ active ──(last line done)──→ outro ──(auto 520ms)──→ idle
                     ↑ click skip           │    ↑ click skip             │
                     └──→ outro             │    └──→ outro              │
                                            │                            │
                         click (while typing) → complete typewriter      │
                         click (line complete) → advance line or outro   │
```

Phases in `DialogOverlayState`:
- `Idle` — dim overlay with "Click to begin" card; overlay captures click
- `Intro` — rail reveals, portrait fades in, stage slides in, bottom bar extends; click → finish intro early
- `Active` — typewriter + click-to-advance; click skip → outro
- `Outro` — rail closes, all elements fade/shrink; click ignored

## Implementation Steps

### 1. Extend `DialogOverlayState` component (`ECS/Components/Scenes.cs`)

Add before the existing class:

```csharp
public enum DialogPhase { Idle, Intro, Active, Outro }
```

Add to `DialogOverlayState`:
```csharp
public DialogPhase Phase { get; set; } = DialogPhase.Idle;
```

### 2. Replace constructor font field

Change from single `ContentFont` to dual-font:
```csharp
private readonly SpriteFont _titleFont = FontSingleton.TitleFont;      // New Rocker 128px
private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;  // Chakra Petch 128px
```

Remove `_font` field; all text calls use `_titleFont` (speaker name, skip button) or `_bodyFont` (body, prompt).

### 3. Add animation state fields

```csharp
// Phase transition timing
private float _phaseElapsedSec = 0f;

// Portrait swap (two active portraits during transition)
private Texture2D _portraitCurrent;     // currently displayed
private Texture2D _portraitEntering;    // incoming during swap
private float _portraitSwapElapsedSec = 0f;
private enum PortraitSwapState { None, Exiting, Entering }
private PortraitSwapState _portraitSwap = PortraitSwapState.None;

// Rail gradient cache
private Texture2D _railGradient;
private int _railGradientWidth;
private Texture2D _bottomBarGradient;
private int _bottomBarGradientWidth;
```

### 4. Rewrite `UpdateEntity` — phase machine

Keep existing entity setup and event flow but replace the click logic with phase-based branching:

```
if phase == Idle:
  - overlay click → playIntro()
if phase == Intro:
  - tick _phaseElapsedSec
  - if _phaseElapsedSec >= IntroDuration or click → finishIntro() → set Active
  - skip click → playOutro()
if phase == Active:
  - tick typewriter (existing logic)
  - tick portrait swap animation if active
  - click on incomplete line → complete it
  - click on complete line → advance or playOutro()
if phase == Outro:
  - tick _phaseElapsedSec
  - if _phaseElapsedSec >= OutroDuration → finishOutro() → set Idle, reset
```

`playIntro()`: set Phase=Intro, reset _phaseElapsedSec, load Line[0] portrait, reset typewriter.
`playOutro()`: set Phase=Outro, reset _phaseElapsedSec, clear typewriter.
`finishIntro()`: Phase=Active, start typewriter for Line[0].
`finishOutro()`: Phase=Idle, reset all line state, set IsActive=false for end button.

### 5. Add layout struct and compute helper

```csharp
private struct CinematicLayout
{
    public Rectangle Rail;            // left 38%, full height
    public Rectangle RailAccent;      // 3px red edge on rail right
    public Rectangle PortraitSlot;    // 420x520, left 19% center, bottom-anchored
    public Rectangle Stage;           // right of rail, bottom 72px offset
    public Rectangle SpeakerDash;     // 48x2 red rule
    public Vector2 SpeakerNamePos;    // right of dash, vertically centered
    public Rectangle BodyTextArea;    // max 960w x 184h
    public Rectangle BottomBar;       // 4px red gradient, right of rail, bottom 0
    public Rectangle SkipButton;      // top-right 16px margin
    public Vector2 ClickPromptPos;    // bottom-right 28/18 offset
    public Rectangle IdleOverlay;     // fullscreen dim
    public Rectangle IdleCard;        // centered card
}

private CinematicLayout ComputeLayout(int vw, int vh);
```

### 6. Draw pipeline (back-to-front, each gated by phase)

```
 1. Idle overlay dim (fullscreen, idle phase only)
 2. Idle card bg (rounded rect) + border (idle phase only)
 3. Idle card text (idle phase only)
 4. Rail gradient clip-reveal (intro/active/outro)
 5. Rail accent (scaleY, intro/active/outro)
 6. Bottom bar gradient (scaleX, intro/active/outro)
 7. Portrait exit image (slide left, if swap active)
 8. Portrait current / entering image (opacity + slide)
 9. Speaker dash (scaleX)
10. Speaker name (swap animation: opacity + slide + color)
11. Body text (typewriter + rich text effects)
12. Click prompt "CLICK TO CONTINUE" (pulsing opacity, active phase, line complete)
13. Skip button border + fill + text (hover highlight)
```

### 7. Rail gradient texture

Draw N vertical `_pixel` strips with lerped alpha to approximate the CSS gradient:
`rgba(0,0,0,0.88)` at 0% → `rgba(0,0,0,0.55)` at 85% → `transparent` at 100%.
With `RailGradientSteps = 10`, each strip ~73px — imperceptible stepping.

Alternative: generate a cached `Texture2D` via `CreateHorizontalGradientTexture` in `PrimitiveTextureFactory.cs` (device-keyed by width + gradient stops). Plan recommends the strip approach for simplicity.

### 8. Portrait swap animation

When line advances and actor changes:
1. Mark current `_portraitCurrent` for exit: `_portraitSwap = Exiting`
2. Tick `_portraitSwapElapsedSec`
3. After `PortraitExitDurationSec` (0.22s): load new actor texture into `_portraitEntering`, set `_portraitSwap = Entering`, reset timer
4. After `PortraitEnterDurationSec` (0.38s): `_portraitCurrent = _portraitEntering`, `_portraitEntering = null`, `_portraitSwap = None`

During swap:
- Exit image: opacity 1→0, translateX 0→-110px
- Enter image: opacity 0→1, translateX -180px→0

### 9. Body text repositioning

Current `RichTextLayout.Layout` call moves from bottom-panel-relative to stage-relative:
- `textX` = BodyTextArea.X
- `textY` = BodyTextArea.Y
- `textW` = BodyTextArea.Width
- Keep existing `_layout`, `_flat`, `_revealedChars`, `_glyphRevealTimes`

### 10. Skip button restyle

Replace current `ButtonTextureFactory.Create(gd, "Skip", Color.White, Color.DarkRed)` with:
```csharp
ButtonTextureFactory.Create(gd, "Skip", Color.White, Red3, textScale: SkipBtnTextScale, cornerRadius: 4)
```
Draw 2px `Red3` border rect 2px larger than button, then draw button texture inset.

### 11. Remove deprecated elements

Delete:
- `_rounded` texture + `_cachedW`, `_cachedH`, `_cachedR` cache (no bottom panel)
- `PanelHeightPercent`, `PanelPadding`, `CornerRadius`, `PanelAlpha` debug properties
- `NameplateHeight`, `NameplateScale`, `PortraitWidthPercent` debug properties
- Private `_font` field (replaced by `_titleFont` + `_bodyFont`)

### 12. Overlay entity handling

`DialogOverlay` entity: fullscreen bounds in all phases.
`DialogEndButton` ("Skip") entity: reposition to top-right, interactable only during Active phase.
No separate entity for click prompt — drawn as text in `Draw()`.

### 13. BackgroundOnly handling

Add early return in `Draw()`:
```csharp
if (st.BackgroundOnly) return;
```
Preserves climb-scene corridor behavior where only the background renders.

## DebugEditable Properties

All on `DialogDisplaySystem`. `Step = 0.01f` for float scales. Remove deprecated properties listed in step 11.

| Group | Property | Default | Step |
|-------|----------|---------|------|
| **Phases** | `IntroDurationSec` | 0.62f | 0.05f |
| **Phases** | `OutroDurationSec` | 0.52f | 0.05f |
| **Rail** | `RailWidthPercent` | 0.38f | 0.01f |
| **Rail** | `RailAccentWidthPx` | 3 | 1 |
| **Rail** | `RailGradientSteps` | 10 | 1 |
| **Portrait** | `PortraitLeftPercent` | 0.19f | 0.01f |
| **Portrait** | `PortraitSlotWidth` | 420 | 1 |
| **Portrait** | `PortraitSlotHeight` | 520 | 1 |
| **Portrait** | `PortraitSlotBottomOffset` | 120 | 1 |
| **Portrait** | `PortraitExitDurationSec` | 0.22f | 0.01f |
| **Portrait** | `PortraitExitSlidePx` | 110 | 1 |
| **Portrait** | `PortraitEnterDurationSec` | 0.38f | 0.01f |
| **Portrait** | `PortraitEnterSlidePx` | 180 | 1 |
| **Stage** | `StageBottomOffset` | 72 | 1 |
| **Stage** | `StageHeight` | 248 | 1 |
| **Stage** | `StageMaxWidth` | 960 | 1 |
| **Stage** | `StagePaddingLeft` | 48 | 1 |
| **Stage** | `StagePaddingRight` | 64 | 1 |
| **Stage** | `SpeakerLineHeight` | 50 | 1 |
| **Stage** | `SpeakerLineGap` | 14 | 1 |
| **Stage** | `SpeakerDashWidth` | 48 | 1 |
| **Stage** | `SpeakerDashHeight` | 2 | 1 |
| **Stage** | `SpeakerNameGap` | 14 | 1 |
| **Typography** | `SpeakerNameScale` | 0.25f | 0.01f |
| **Typography** | `BodyTextScale` | 0.21875f | 0.01f |
| **Typography** | `PromptScale` | 0.10156f | 0.01f |
| **Typography** | `PromptPulseMinAlpha` | 0.35f | 0.05f |
| **Typography** | `PromptPulseMaxAlpha` | 0.75f | 0.05f |
| **Typography** | `PromptPulsePeriodSec` | 1.6f | 0.1f |
| **Typography** | `IdleTitleScale` | 0.21875f | 0.01f |
| **Typography** | `IdleBodyScale` | 0.109375f | 0.01f |
| **Bottom bar** | `BottomBarHeight` | 4 | 1 |
| **Bottom bar** | `BottomBarGradientSteps` | 10 | 1 |
| **Skip btn** | `SkipBtnMargin` | 16 | 1 |
| **Skip btn** | `SkipBtnTextScale` | 0.140625f | 0.01f |
| **Skip btn** | `SkipBtnBorderPx` | 2 | 1 |
| **Idle** | `IdleOverlayAlpha` | 0.25f | 0.05f |
| **Idle** | `IdleCardPaddingX` | 40 | 1 |
| **Idle** | `IdleCardPaddingY` | 28 | 1 |
| **Idle** | `IdleCardBorderRadius` | 8 | 1 |
| **Idle** | `IdleCardBgAlpha` | 0.72f | 0.01f |
| **Prompt** | `ClickPromptBottom` | 18 | 1 |
| **Prompt** | `ClickPromptRight` | 28 | 1 |
| **Rich text** | *(all existing effects props)* | (unchanged) | — |
| **General** | `ZOrder` | 50000 | 10 |
| **General** | `CharsPerSecond` | 80 | 1 |

## Color Palette

| Role | CSS | MonoGame |
|------|-----|----------|
| Rail start | `rgba(0,0,0,0.88)` | `new Color(0, 0, 0) * 0.88f` |
| Rail mid (85%) | `rgba(0,0,0,0.55)` | `new Color(0, 0, 0) * 0.55f` |
| Rail end (100%) | `transparent` | `Color.Transparent` |
| Rail accent red | `#c41e3a` | `new Color(196, 30, 58)` |
| Bottom bar red | `#c41e3a` | `new Color(196, 30, 58)` |
| Speaker dash red | `#c41e3a` | `new Color(196, 30, 58)` |
| Skip btn bg | `#ffffff` | `Color.White` |
| Skip btn border/text | `#8b0000` | `new Color(139, 0, 0)` |
| Skip btn hover | `#fff5f5` | `new Color(255, 245, 245)` |
| Speaker name | `#ffffff` | `Color.White` |
| Speaker name swap | `#c41e3a` | `new Color(196, 30, 58)` |
| Body text | `#f0ece6` | `new Color(240, 236, 230)` |
| Body text shadow | `rgba(0,0,0,0.6)` | `Color.Black * 0.6f` |
| Click prompt | `rgba(255,255,255,0.45)` | `Color.White * 0.45f` |
| Idle overlay bg | `rgba(0,0,0,0.25)` | `new Color(0, 0, 0) * 0.25f` |
| Idle card bg | `rgba(10,10,10,0.72)` | `new Color(10, 10, 10) * 0.72f` |
| Idle card border | `rgba(255,255,255,0.12)` | `Color.White * 0.12f` |
| Idle card title | `#ffffff` | `Color.White` |
| Idle card body | `#c8c0b8` | `new Color(200, 192, 184)` |

## Pixel Position Reference (1920×1080)

| Element | X | Y | W | H | Notes |
|---------|---|---|---|---|-------|
| Rail | 0 | 0 | 730 | 1080 | 38% of vw |
| Rail accent | 727 | 0 | 3 | 1080 | Right edge of rail |
| Portrait slot | 155 | 440 | 420 | 520 | x = 19% - w/2, y = 1080 - 120 - h |
| Stage (outer) | 778 | 760 | 1078 | 248 | x = rail.W + 48, y = 1080 - 72 - 248 |
| Speaker dash | 778 | 784 | 48 | 2 | y = stage.y + (50-2)/2 |
| Speaker name | 840 | ~773 | — | — | x = dash.x + 48 + 14 |
| Body text area | 778 | 824 | 960 | 184 | y = stage.y + 50 + 14 |
| Bottom bar | 730 | 1076 | 1190 | 4 | y = 1080 - 4 |
| Skip button | 1836 | 16 | ~68 | ~37 | right 16, top 16 |
| Click prompt | ~1792 | ~1044 | — | — | right 28, bottom 18 |
| Idle overlay | 0 | 0 | 1920 | 1080 | Fullscreen dim |
| Idle card | centered | centered | ~400 | ~120 | pad 40x28 |

### Font Scale Reference

| Element | SpriteFont | Native Size | CSS Size | Scale |
|---------|-----------|-------------|----------|-------|
| Speaker name | NewRocker (TitleFont) | 128px | 32px | 0.25 |
| Skip button | NewRocker (TitleFont) | 128px | 18px | 0.140625 |
| Idle card title | NewRocker (TitleFont) | 128px | 28px | 0.21875 |
| Body text | ChakraPetch (ChakraPetchFont) | 128px | 28px | 0.21875 |
| Idle card body | ChakraPetch (ChakraPetchFont) | 128px | 14px | 0.109375 |
| Click prompt | ChakraPetch (ChakraPetchFont) | 128px | 13px | 0.1015625 |

## Layout Modes

| Mode | Condition | Behavior |
|------|-----------|----------|
| **Idle** | `Phase == Idle` | Fullscreen dim + centered card; skip button hidden; overlay click → playIntro |
| **Intro** | `Phase == Intro` | Rail reveals, portrait fades in, stage slides in, bottom bar extends; skip button appears; click → finish intro or skip |
| **Active** | `Phase == Active` | All elements visible; typewriter runs; click-to-advance; skip button; portrait swap on actor change |
| **Outro** | `Phase == Outro` | All elements reverse-animate; skip button visible but non-interactive; auto-finish → Idle |
| **BackgroundOnly** | `BackgroundOnly == true` | Draw() returns early; no overlay rendered (climb corridors) |

## Helper Method Signatures

```csharp
// Layout
private CinematicLayout ComputeLayout(int vw, int vh);

// Phase transitions
private void PlayIntro(DialogOverlayState st);
private void PlayOutro(DialogOverlayState st);
private void FinishIntro(DialogOverlayState st);
private void FinishOutro(DialogOverlayState st);

// Animation progress (0→1 based on phase+elapsed)
private float RailClipProgress();
private float PortraitOpacity();
private float StageOpacity();
private float StageTranslateX();
private float BottomBarScaleX();
private float RailAccentScaleY();
private float SkipButtonOpacity();
private float SkipButtonTranslateY();
private float SpeakerDashScaleX();

// Pulsing
private float PromptAlpha();

// Speaker name color (lerp during swap)
private Color SpeakerNameColor(string name);

// Portrait swap
private void BeginPortraitSwap(string newActor);
private void UpdatePortraitSwap(float dt);

// Draw helpers
private void DrawIdleOverlay(CinematicLayout layout);
private void DrawRail(CinematicLayout layout);
private void DrawSpeakerLine(CinematicLayout layout, string actorName);
private void DrawClickPrompt(CinematicLayout layout);
private void DrawSkipButton(CinematicLayout layout);
private void DrawPortraitImage(CinematicLayout layout, string actor);
```

## Verification Checklist

- [ ] Dialog triggers on quest start (existing flow via `OnTransitionComplete`)
- [ ] Intro animation plays: rail reveals → portrait fades → stage slides → bottom bar
- [ ] Typewriter runs after intro completes (line 0)
- [ ] First click completes typewriter (instantly shows full line)
- [ ] Second click advances to next line
- [ ] Actor change triggers portrait swap animation + name swap
- [ ] Same actor line advances without swap animation
- [ ] Last line advance triggers outro animation
- [ ] Outro animation plays: rail closes → portrait fades → stage exits → bottom bar shrinks
- [ ] System returns to idle after outro
- [ ] Skip button visible and clickable during active/intro/outro
- [ ] Skip button triggers outro immediately
- [ ] Click prompt "CLICK TO CONTINUE" pulsing visible when line complete
- [ ] `BackgroundOnly == true` renders nothing (climb corridors)
- [ ] Existing dialog JSON data loads correctly (no model change)
- [ ] Rich text effects (jitter, shake, bloom) still render on body text
- [ ] All DebugEditable properties tunable in debug tab
- [ ] `dotnet build` succeeds
- [ ] No `MouseState` or `GamePad` usage (UIElement + CursorEvents only)

## Fidelity Notes

- **Rail gradient**: CSS exact linear-gradient. Drawn as N vertical `_pixel` strips with lerped alpha. `RailGradientSteps = 10` → ~73px per strip, imperceptible stepping.
- **Bottom bar gradient**: Same strip approach, `#c41e3a` to transparent over ~1190px.
- **Portrait drop-shadow**: CSS `filter: drop-shadow(0 12px 28px rgba(0,0,0,0.55))` approximated by black-tinted copy drawn 12px down (no GPU blur pass).
- **Letter-spacing**: CSS `letter-spacing: 1px` on speaker name and skip button — no SpriteFont equivalent, skip.
- **Idle card `backdrop-filter: blur(4px)`**: No GPU blur pass — accept unfiltered background.
- **CSS `cubic-bezier` easing**: Approximate with `MathHelper.SmoothStep` or `MathHelper.Lerp`; exact curves (ease-out: 0.22,1,0.36,1, ease-in: 0.55,0,1,0.45) replaced with built-in easing.
- **Body text shadow**: `text-shadow: 0 2px 12px rgba(0,0,0,0.6)` — draw body text black at 0.6 alpha offset by (0, 2), then normal draw on top.
- **Font hinting**: New Rocker at scale 0.25, Chakra Petch at scale 0.218 may differ from browser rasterization — tune via DebugEditable.
- **Click prompt pulsing**: CSS `@keyframes pulsePrompt` approximated with `MathF.Sin` oscillation.
- **Vignette on battle background**: `.bg-vignette` is scene background, not dialog responsibility. Existing battle scene handles background dim.
- **Speaker name swap**: CSS transitions opacity/transform/color simultaneously. Approximate with three parallel lerps over 0.22s.
