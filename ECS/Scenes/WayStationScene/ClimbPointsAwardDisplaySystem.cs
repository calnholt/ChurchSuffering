using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems;

[DebugTab("Climb Points Award")]
public sealed class ClimbPointsAwardDisplaySystem : Core.System, IDebugInspectableChildren
{
	private const string OverlayEntityName = "ClimbPointsAwardOverlay";
	private const string BlockerEntityName = "ClimbPointsAwardBlocker";
	private const string ContextId = "overlay.climb-points-award";
	private const string RumbleChannelId = "climb-points-award";
	private const int ZOrder = 60500;
	private const float SparkDurationSeconds = 0.900f;
	private const float ShockwaveDurationSeconds = 0.780f;

	private static readonly string[] Glyphs = BuildGlyphCache();
	private static readonly string[] PointLabels = BuildPointLabelCache();
	private static readonly SparkDefinition[] Sparks = BuildSparkDefinitions();

	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;
	private readonly Texture2D _background;
	private readonly Texture2D _pixel;
	private readonly SpriteFont _titleFont;
	private readonly SpriteFont _bodyFont;
	private readonly float[] _titleGlyphWidths;
	private readonly float[] _bodyGlyphWidths;
	private readonly float[] _tierRequirementScales;
	private readonly IPlayerInputSource _inputSource;

	private Texture2D _vignetteMask;
	private Texture2D _radianceMask;
	private Texture2D _ringMask;
	private Texture2D _diamondMask;
	private Entity _overlayEntity;
	private Entity _blockerEntity;

	[DebugEditable(DisplayName = "Scene Dim Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float SceneDimAlpha { get; set; } = 1f;

	[DebugEditable(DisplayName = "Particle Alpha", Step = 0.01f, Min = 0f, Max = 2f)]
	public float ParticleAlpha { get; set; } = 1f;

	[DebugEditable(DisplayName = "Route Card Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float RouteCardAlpha { get; set; } = 0.94f;

	[DebugEditable(DisplayName = "Rumble Buildup Low", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleBuildupLow { get; set; } = 0.28f;

	[DebugEditable(DisplayName = "Rumble Buildup High", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleBuildupHigh { get; set; } = 0.42f;

	[DebugEditable(DisplayName = "Rumble Tier Pulse Low", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleTierPulseLow { get; set; } = 0.10f;

	[DebugEditable(DisplayName = "Rumble Tier Pulse High", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleTierPulseHigh { get; set; } = 0.16f;

	[DebugEditable(DisplayName = "Rumble Tier Pulse Duration (s)", Step = 0.01f, Min = 0.01f, Max = 1f)]
	public float RumbleTierPulseDurationSeconds { get; set; } = 0.060f;

	[DebugEditable(DisplayName = "Rumble Empty Pulse Low", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleEmptyPulseLow { get; set; } = 0.06f;

	[DebugEditable(DisplayName = "Rumble Empty Pulse High", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleEmptyPulseHigh { get; set; } = 0.10f;

	[DebugEditable(DisplayName = "Rumble Empty Pulse Duration (s)", Step = 0.01f, Min = 0.01f, Max = 1f)]
	public float RumbleEmptyPulseDurationSeconds { get; set; } = 0.080f;

	[DebugEditable(DisplayName = "Rumble Buildup Trigger", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleBuildupTrigger { get; set; } = 0.14f;

	[DebugEditable(DisplayName = "Rumble Tier Pulse Trigger", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleTierPulseTrigger { get; set; } = 0.06f;

	[DebugEditable(DisplayName = "Rumble Empty Pulse Trigger", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleEmptyPulseTrigger { get; set; } = 0.04f;

	public ClimbPointsAwardDisplaySystem(
		EntityManager entityManager,
		GraphicsDevice graphicsDevice,
		SpriteBatch spriteBatch,
		ImageAssetService imageAssets,
		IPlayerInputSource inputSource = null)
		: base(entityManager)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		_inputSource = inputSource;
		_background = imageAssets.GetRequiredTexture("waystation");
		_pixel = imageAssets.GetPixel(Color.White);
		_titleFont = FontSingleton.TitleFont;
		_bodyFont = FontSingleton.ChakraPetchFont;
		_titleGlyphWidths = BuildGlyphWidths(_titleFont);
		_bodyGlyphWidths = BuildGlyphWidths(_bodyFont);
		_tierRequirementScales = new float[ClimbPointsAwardAnimationService.Tiers.Length];
		for (int index = 0; index < _tierRequirementScales.Length; index++)
		{
			_tierRequirementScales[index] = FitSpacedStringScale(
				_bodyFont,
				ClimbPointsAwardAnimationService.Tiers[index].Requirement,
				0.078f,
				2f,
				270f);
		}

		EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		EventManager.Subscribe<TransitionCompleteEvent>(OnTransitionComplete);
		EventManager.Subscribe<CursorStateEvent>(OnCursorState);
	}

	protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

	public override void Update(GameTime gameTime)
	{
		var state = GetState();
		if (state?.IsOpen != true)
		{
			StopRumble();
			SetInputActive(false);
			return;
		}

		SetInputActive(true);
		float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
		if (!float.IsFinite(delta) || delta < 0f) delta = 0f;

		switch (state.Phase)
		{
			case ClimbPointsAwardOverlayPhase.Playing:
			{
				state.PreviousElapsedSeconds = state.ElapsedSeconds;
				state.ElapsedSeconds += delta;
				int earned = ClimbPointsAwardAnimationService.GetEarnedTierCount(GetScenario(state));
				UpdateRumble(state, earned);
				if (state.ElapsedSeconds >= ClimbPointsAwardAnimationService.GetReadySeconds(earned))
					state.Phase = ClimbPointsAwardOverlayPhase.Ready;
				break;
			}
			case ClimbPointsAwardOverlayPhase.Ready:
				StopRumble();
				state.ElapsedSeconds += delta;
				break;
			case ClimbPointsAwardOverlayPhase.Exiting:
				StopRumble();
				state.ElapsedSeconds += delta;
				state.ExitElapsedSeconds += delta;
				if (state.ExitElapsedSeconds >= ClimbPointsAwardAnimationService.ExitFadeSeconds)
					FinishDismissal(state);
				break;
			default:
				StopRumble();
				break;
		}
	}

	public void Draw()
	{
		var state = GetState();
		if (state?.IsOpen != true || state.Phase == ClimbPointsAwardOverlayPhase.AwaitingTransition) return;

		EnsureRenderResources();
		float overlayAlpha = state.Phase == ClimbPointsAwardOverlayPhase.Exiting
			? 1f - ClimbPointsAwardAnimationService.Clamp01(
				state.ExitElapsedSeconds / ClimbPointsAwardAnimationService.ExitFadeSeconds)
			: 1f;
		var scenario = GetScenario(state);
		int earnedCount = ClimbPointsAwardAnimationService.GetEarnedTierCount(scenario);

		DrawBackdrop(state.ElapsedSeconds, overlayAlpha);
		DrawFrame(state.ElapsedSeconds, overlayAlpha);
		DrawIntro(scenario, state.ElapsedSeconds, overlayAlpha);
		DrawRoute(scenario, earnedCount, state.ElapsedSeconds, overlayAlpha);
		DrawCrest(state, earnedCount, overlayAlpha);
		DrawReadyHint(state, earnedCount, overlayAlpha);
	}

	[DebugAction("Preview Time 0 (+0)")]
	public void DebugPreviewTime0() => OpenDebugPreview(0, false, false);

	[DebugAction("Preview Time 12 (+1)")]
	public void DebugPreviewTime12() => OpenDebugPreview(12, false, false);

	[DebugAction("Preview Time 20 (+4)")]
	public void DebugPreviewTime20() => OpenDebugPreview(20, false, false);

	[DebugAction("Preview Time 26 (+9)")]
	public void DebugPreviewTime26() => OpenDebugPreview(26, false, false);

	[DebugAction("Preview Victory (+12)")]
	public void DebugPreviewVictory() => OpenDebugPreview(32, true, false);

	[DebugAction("Preview Abandoned (+0)")]
	public void DebugPreviewAbandoned() => OpenDebugPreview(18, false, true);

	internal bool OpenForSnapshot(int timeReached, bool completedFinalBoss, bool abandoned, float elapsedSeconds)
	{
		if (GetState()?.IsAuthoritative == true) return false;
		Open(false, timeReached, completedFinalBoss, abandoned);
		var state = GetState();
		state.Phase = ClimbPointsAwardOverlayPhase.Playing;
		state.ElapsedSeconds = Math.Max(0f, elapsedSeconds);
		int earned = ClimbPointsAwardAnimationService.GetEarnedTierCount(GetScenario(state));
		if (state.ElapsedSeconds >= ClimbPointsAwardAnimationService.GetReadySeconds(earned))
			state.Phase = ClimbPointsAwardOverlayPhase.Ready;
		return true;
	}

	private void OnLoadScene(LoadSceneEvent evt)
	{
		if (evt?.Scene != SceneId.WayStation) return;
		var pending = SaveCache.GetCollection().pendingClimbPointAward;
		if (pending == null) return;
		Open(true, pending.timeReached, pending.completedFinalBoss, pending.abandoned, pending.pointsAwarded);
	}

	private void OnTransitionComplete(TransitionCompleteEvent evt)
	{
		if (evt?.Scene != SceneId.WayStation) return;
		var state = GetState();
		if (state?.Phase != ClimbPointsAwardOverlayPhase.AwaitingTransition) return;
		state.Phase = ClimbPointsAwardOverlayPhase.Playing;
		state.ElapsedSeconds = 0f;
	}

	private void OnCursorState(CursorStateEvent evt)
	{
		var state = GetState();
		if (state?.CanDismiss != true || evt?.IsAPressedEdge != true) return;
		if (!ReferenceEquals(evt.TopEntity, _blockerEntity)) return;
		state.Phase = ClimbPointsAwardOverlayPhase.Exiting;
		state.ExitElapsedSeconds = 0f;
	}

	private void OpenDebugPreview(int timeReached, bool completedFinalBoss, bool abandoned)
	{
		var existing = GetState();
		if (existing?.IsAuthoritative == true) return;
		if (SaveCache.GetCollection().pendingClimbPointAward != null) return;
		Open(false, timeReached, completedFinalBoss, abandoned);
		var state = GetState();
		state.Phase = ClimbPointsAwardOverlayPhase.Playing;
	}

	private void Open(
		bool authoritative,
		int timeReached,
		bool completedFinalBoss,
		bool abandoned,
		int? pointsAwarded = null)
	{
		var state = EnsureOverlay();
		state.IsAuthoritative = authoritative;
		state.TimeReached = Math.Max(0, timeReached);
		state.CompletedFinalBoss = completedFinalBoss;
		state.Abandoned = abandoned;
		state.PointsAwarded = pointsAwarded ?? CollectionProgressionRules.CalculateClimbPoints(
			state.TimeReached,
			completedFinalBoss,
			abandoned);
		state.ElapsedSeconds = 0f;
		state.PreviousElapsedSeconds = 0f;
		state.ExitElapsedSeconds = 0f;
		state.RumbleFinaleFlags = ClimbPointsAwardRumbleFinaleFlags.None;
		state.Phase = authoritative
			? ClimbPointsAwardOverlayPhase.AwaitingTransition
			: ClimbPointsAwardOverlayPhase.Playing;
		EnsureBlocker();
		SetInputActive(true);
	}

	private void FinishDismissal(ClimbPointsAwardOverlayState state)
	{
		bool authoritative = state.IsAuthoritative;
		StopRumble();
		state.Phase = ClimbPointsAwardOverlayPhase.Hidden;
		state.IsAuthoritative = false;
		state.ElapsedSeconds = 0f;
		state.PreviousElapsedSeconds = 0f;
		state.ExitElapsedSeconds = 0f;
		state.RumbleFinaleFlags = ClimbPointsAwardRumbleFinaleFlags.None;
		SetInputActive(false);
		if (authoritative)
		{
			EventManager.Publish(new ClimbPointsAwardOverlayDismissedEvent
			{
				WasAuthoritative = true,
			});
		}
	}

	private ClimbPointsAwardOverlayState EnsureOverlay()
	{
		_overlayEntity = EntityManager.GetEntity(OverlayEntityName) ?? EntityManager.CreateEntity(OverlayEntityName);
		var state = _overlayEntity.GetComponent<ClimbPointsAwardOverlayState>();
		if (state == null)
		{
			state = new ClimbPointsAwardOverlayState();
			EntityManager.AddComponent(_overlayEntity, state);
		}
		if (_overlayEntity.GetComponent<Transform>() == null)
			EntityManager.AddComponent(_overlayEntity, new Transform { ZOrder = ZOrder });
		if (_overlayEntity.GetComponent<DontDestroyOnLoad>() == null)
			EntityManager.AddComponent(_overlayEntity, new DontDestroyOnLoad());
		var context = InputContextService.EnsureContext(EntityManager, _overlayEntity, ContextId, 760, state.IsOpen);
		context.IsActive = state.IsOpen;
		return state;
	}

	private Entity EnsureBlocker()
	{
		_blockerEntity = EntityManager.GetEntity(BlockerEntityName) ?? EntityManager.CreateEntity(BlockerEntityName);
		if (_blockerEntity.GetComponent<Transform>() is not Transform transform)
		{
			transform = new Transform();
			EntityManager.AddComponent(_blockerEntity, transform);
		}
		transform.Position = Vector2.Zero;
		transform.ZOrder = ZOrder;

		if (_blockerEntity.GetComponent<UIElement>() is not UIElement ui)
		{
			ui = new UIElement();
			EntityManager.AddComponent(_blockerEntity, ui);
		}
		ui.Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
		ui.IsInteractable = true;
		ui.IsHidden = false;
		ui.IsPreventDefaultClick = true;
		ui.LayerType = UILayerType.Overlay;
		ui.ShowHoverHighlight = false;
		ui.EventType = UIElementEventType.None;
		ui.SecondaryEventType = UIElementEventType.None;
		InputContextService.EnsureMember(EntityManager, _blockerEntity, ContextId);
		if (_blockerEntity.GetComponent<DontDestroyOnLoad>() == null)
			EntityManager.AddComponent(_blockerEntity, new DontDestroyOnLoad());
		return _blockerEntity;
	}

	private void SetInputActive(bool active)
	{
		var state = GetState();
		if (_overlayEntity?.GetComponent<InputContext>() is InputContext context)
			context.IsActive = active && state?.IsOpen == true;

		if (_blockerEntity?.GetComponent<UIElement>() is not UIElement ui) return;
		ui.IsInteractable = active;
		ui.IsHidden = !active;
		ui.Bounds = active
			? new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight)
			: Rectangle.Empty;
		if (!active)
		{
			ui.IsHovered = false;
			ui.IsClicked = false;
		}
	}

	private ClimbPointsAwardOverlayState GetState()
	{
		_overlayEntity ??= EntityManager.GetEntity(OverlayEntityName);
		return _overlayEntity?.GetComponent<ClimbPointsAwardOverlayState>();
	}

	private static ClimbPointsAwardScenario GetScenario(ClimbPointsAwardOverlayState state) =>
		ClimbPointsAwardAnimationService.CreateScenario(
			state.TimeReached,
			state.CompletedFinalBoss,
			state.Abandoned);

	private ClimbPointsAwardRumbleSettings BuildRumbleSettings() =>
		new(
			RumbleBuildupLow,
			RumbleBuildupHigh,
			RumbleTierPulseLow,
			RumbleTierPulseHigh,
			RumbleTierPulseDurationSeconds,
			RumbleEmptyPulseLow,
			RumbleEmptyPulseHigh,
			RumbleEmptyPulseDurationSeconds,
			RumbleBuildupTrigger,
			RumbleTierPulseTrigger,
			RumbleEmptyPulseTrigger);

	private void UpdateRumble(ClimbPointsAwardOverlayState state, int earnedTierCount)
	{
		if (_inputSource == null) return;

		PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
		if (!frame.IsGamepadConnected || !frame.IsWindowActive)
		{
			StopRumble();
			return;
		}

		ClimbPointsAwardRumbleSample sample = ClimbPointsAwardAnimationService.SampleRumble(
			state.ElapsedSeconds,
			earnedTierCount,
			BuildRumbleSettings());
		_inputSource.SetRumbleChannel(RumbleChannelId, new RumbleMotorState(
			sample.LowFrequency,
			sample.HighFrequency,
			sample.LeftTrigger,
			sample.RightTrigger));

		float progressCap = ClimbPointsAwardAnimationService.GetProgressCap(earnedTierCount);
		foreach (var milestone in ClimbPointsAwardAnimationService.GetCrossedFinaleMilestones(
			state.PreviousElapsedSeconds,
			state.ElapsedSeconds,
			earnedTierCount))
		{
			ClimbPointsAwardRumbleFinaleFlags flag = milestone.Kind switch
			{
				ClimbPointsAwardRumbleMilestoneKind.CrestReveal => ClimbPointsAwardRumbleFinaleFlags.CrestReveal,
				ClimbPointsAwardRumbleMilestoneKind.CountUpComplete => ClimbPointsAwardRumbleFinaleFlags.CountUpComplete,
				_ => ClimbPointsAwardRumbleFinaleFlags.None,
			};
			if (flag == ClimbPointsAwardRumbleFinaleFlags.None
				|| state.RumbleFinaleFlags.HasFlag(flag))
			{
				continue;
			}

			state.RumbleFinaleFlags |= flag;
			RumbleProfile profile = ClimbPointsAwardAnimationService.GetFinaleRumbleProfile(
				milestone.Kind,
				progressCap);
			float scale = ClimbPointsAwardAnimationService.GetFinaleRumbleScale(
				milestone.Kind,
				progressCap);
			if (profile == RumbleProfile.None || scale <= 0f) continue;

			EventManager.Publish(new RumbleRequested
			{
				Profile = profile,
				Scale = scale,
				Group = RumbleGroup.Default,
			});
		}
	}

	private void StopRumble()
	{
		_inputSource?.ClearRumbleChannel(RumbleChannelId);
	}

	private void EnsureRenderResources()
	{
		_vignetteMask ??= PrimitiveTextureFactory.GetInvertedSoftRadialCircle(_graphicsDevice, 1024, 0.25f, 1f);
		_radianceMask ??= PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, 512, 0f, 1f);
		_ringMask ??= PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, 256, 256, 3f);
		_diamondMask ??= PrimitiveTextureFactory.GetAntialiasedPolygonMask(
			_graphicsDevice,
			256,
			256,
			"climb-points-award-diamond",
			[
				new Vector2(0.5f, 0f),
				new Vector2(1f, 0.5f),
				new Vector2(0.5f, 1f),
				new Vector2(0f, 0.5f),
			]);
	}

	private void DrawBackdrop(float elapsed, float alpha)
	{
		float settle = ClimbPointsAwardAnimationService.EaseRise(elapsed / 0.9f);
		float zoom = MathHelper.Lerp(1.015f, 1f, settle);
		Rectangle source = WayStationMapSourceService.ComputeCenteredCoverSource(
			_background.Width,
			_background.Height,
			Game1.VirtualWidth,
			Game1.VirtualHeight);
		int sourceWidth = Math.Max(1, (int)MathF.Round(source.Width / zoom));
		int sourceHeight = Math.Max(1, (int)MathF.Round(source.Height / zoom));
		var zoomedSource = new Rectangle(
			source.Center.X - sourceWidth / 2,
			source.Center.Y - sourceHeight / 2,
			sourceWidth,
			sourceHeight);
		_spriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), zoomedSource, Color.White * alpha);

		float dim = ClimbPointsAwardAnimationService.Clamp01(elapsed / 0.32f) * SceneDimAlpha * alpha;
		_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (0.32f * dim));
		const int horizontalStrips = 48;
		for (int strip = 0; strip < horizontalStrips; strip++)
		{
			float x01 = (strip + 0.5f) / horizontalStrips;
			float centerDistance = MathF.Abs(x01 - 0.5f) * 2f;
			float stripAlpha = MathHelper.Lerp(0.62f, 0.84f, centerDistance) * dim;
			int left = strip * Game1.VirtualWidth / horizontalStrips;
			int right = (strip + 1) * Game1.VirtualWidth / horizontalStrips;
			_spriteBatch.Draw(_pixel, new Rectangle(left, 0, Math.Max(1, right - left), Game1.VirtualHeight), new Color(4, 1, 2) * stripAlpha);
		}

		float vignette = ClimbPointsAwardAnimationService.Clamp01(elapsed / 0.4f) * alpha;
		_spriteBatch.Draw(_vignetteMask, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (0.62f * vignette));
		DrawVerticalEdgeShade(vignette);
		float radiance = ClimbPointsAwardAnimationService.Clamp01(elapsed / 0.7f) * alpha;
		_spriteBatch.Draw(_radianceMask, new Rectangle(610, -15, 700, 700), new Color(232, 196, 93) * (0.17f * radiance));
	}

	private void DrawVerticalEdgeShade(float alpha)
	{
		const int strips = 24;
		int stripHeight = Game1.VirtualHeight / strips;
		for (int strip = 0; strip < strips; strip++)
		{
			float y01 = (strip + 0.5f) / strips;
			float top = 1f - MathHelper.Clamp(y01 / 0.24f, 0f, 1f);
			float bottom = MathHelper.Clamp((y01 - 0.72f) / 0.28f, 0f, 1f);
			float shade = Math.Max(top * 0.9f, bottom * 0.88f) * alpha;
			if (shade <= 0f) continue;
			_spriteBatch.Draw(_pixel, new Rectangle(0, strip * stripHeight, Game1.VirtualWidth, stripHeight + 1), Color.Black * shade);
		}
	}

	private void DrawFrame(float elapsed, float alpha)
	{
		float progress = ClimbPointsAwardAnimationService.EaseRise((elapsed - 0.08f) / 0.7f);
		if (progress <= 0f) return;
		int fullWidth = Game1.VirtualWidth - 236;
		int width = (int)MathF.Round(fullWidth * MathHelper.Lerp(0.2f, 1f, progress));
		DrawHorizontalGradientRule(Game1.VirtualWidth / 2, 67, width, new Color(232, 196, 93), 0.58f * progress * alpha);
		DrawHorizontalGradientRule(Game1.VirtualWidth / 2, Game1.VirtualHeight - 67, width, new Color(232, 196, 93), 0.58f * progress * alpha);
	}

	private void DrawIntro(ClimbPointsAwardScenario scenario, float elapsed, float alpha)
	{
		float kickerProgress = ClimbPointsAwardAnimationService.Clamp01((elapsed - 0.15f) / 0.45f);
		float kickerY = MathHelper.Lerp(84f, 92f, kickerProgress);
		DrawCenteredSpacedString(_bodyFont, scenario.Kicker, kickerY, 0.102f, 6f, new Color(232, 196, 93) * (0.8f * kickerProgress * alpha));

		float titleProgress = ClimbPointsAwardAnimationService.EaseSlam((elapsed - 0.21f) / 0.6f);
		if (titleProgress <= 0f) return;
		float scale = MathHelper.Lerp(0.447f, 0.414f, titleProgress);
		DrawCenteredSpacedStringWithShadow(
			_titleFont,
			scenario.Title,
			124f,
			scale,
			5f,
			Color.White * (ClimbPointsAwardAnimationService.Clamp01(titleProgress) * alpha));
	}

	private void DrawRoute(ClimbPointsAwardScenario scenario, int earnedCount, float elapsed, float alpha)
	{
		const float routeTop = 445f;
		const float routeHeight = 460f;
		const float centerX = 960f;
		DrawLine(new Vector2(centerX, routeTop + 2), new Vector2(centerX, routeTop + routeHeight - 4), new Color(183, 173, 163) * (0.09f * alpha), 2f);
		DrawDiamond(new Vector2(centerX, routeTop - 2), 9f, Color.Transparent, new Color(232, 196, 93) * (0.34f * alpha), 1f);
		DrawDiamond(new Vector2(centerX, routeTop + routeHeight + 3), 9f, Color.Transparent, new Color(232, 196, 93) * (0.34f * alpha), 1f);

		float fillHeight = CalculateRouteFillHeight(earnedCount, elapsed);
		if (fillHeight > 0f)
		{
			DrawVerticalGradient(
				new Rectangle(959, (int)MathF.Round(routeTop + routeHeight - 4 - fillHeight), 3, (int)MathF.Ceiling(fillHeight)),
				new Color(232, 196, 93),
				new Color(141, 20, 40),
				16,
				alpha);
		}

		if (earnedCount == 0)
		{
			DrawEmptyState(scenario, elapsed, alpha);
			return;
		}

		int earnedIndex = 0;
		for (int tierIndex = 0; tierIndex < ClimbPointsAwardAnimationService.Tiers.Length; tierIndex++)
		{
			var tier = ClimbPointsAwardAnimationService.Tiers[tierIndex];
			if (!ClimbPointsAwardAnimationService.IsTierEarned(tier, scenario)) continue;
			DrawRouteRow(tier, tierIndex, earnedCount, earnedIndex, elapsed, alpha);
			earnedIndex++;
		}
	}

	private float CalculateRouteFillHeight(int earnedCount, float elapsed)
	{
		float height = 0f;
		for (int index = 0; index < earnedCount; index++)
		{
			float reveal = ClimbPointsAwardAnimationService.GetRouteFillStartSeconds(index);
			if (elapsed < reveal) break;
			float previous = index == 0
				? 0f
				: ClimbPointsAwardAnimationService.GetRouteFillHeight(earnedCount, index - 1);
			float target = ClimbPointsAwardAnimationService.GetRouteFillHeight(earnedCount, index);
			float progress = ClimbPointsAwardAnimationService.EaseRise((elapsed - reveal) / 0.39f);
			height = MathHelper.Lerp(previous, target, progress);
			if (progress < 1f) break;
		}
		return height;
	}

	private void DrawRouteRow(
		ClimbPointsAwardTier tier,
		int tierIndex,
		int earnedCount,
		int earnedIndex,
		float elapsed,
		float alpha)
	{
		float reveal = ClimbPointsAwardAnimationService.GetTierRevealSeconds(earnedIndex);
		float rowProgress = ClimbPointsAwardAnimationService.EaseRise((elapsed - reveal) / 0.56f);
		if (rowProgress <= 0f) return;

		float bottom = ClimbPointsAwardAnimationService.GetRouteRowBottom(earnedCount, earnedIndex);
		float rowTop = 445f + 460f - bottom - 92f + MathHelper.Lerp(26f, 0f, rowProgress);
		float rowAlpha = ClimbPointsAwardAnimationService.Clamp01(rowProgress) * alpha;
		bool left = earnedIndex % 2 == 0;

		float cardProgress = ClimbPointsAwardAnimationService.EaseRise((elapsed - reveal - 0.04f) / 0.62f);
		float cardX = left ? 518f : 1012f;
		cardX += MathHelper.Lerp(left ? -24f : 24f, 0f, cardProgress);
		var card = new Rectangle((int)MathF.Round(cardX), (int)MathF.Round(rowTop + 4f), 390, 78);
		DrawRouteCard(card, left, tier, tierIndex, rowAlpha);

		float nodeProgress = ClimbPointsAwardAnimationService.EaseSlam((elapsed - reveal - 0.08f) / 0.52f);
		if (nodeProgress > 0f)
		{
			float nodeScale = nodeProgress < 0.62f
				? MathHelper.Lerp(0.15f, 1.28f, ClimbPointsAwardAnimationService.EaseSlam(nodeProgress / 0.62f))
				: MathHelper.Lerp(1.28f, 1f, (nodeProgress - 0.62f) / 0.38f);
			Vector2 center = new(960f, rowTop + 45.5f);
			_spriteBatch.Draw(_radianceMask, CenteredRect(center, 76f * nodeScale), new Color(232, 196, 93) * (0.34f * rowAlpha));
			DrawDiamond(center, 27f * nodeScale, new Color(36, 7, 13) * rowAlpha, new Color(232, 196, 93) * rowAlpha, 2f);
			DrawDiamond(center, 12f * nodeScale, new Color(255, 241, 172) * rowAlpha, Color.Transparent, 0f);
		}

		float ringProgress = ClimbPointsAwardAnimationService.Clamp01((elapsed - reveal - 0.09f) / 0.58f);
		if (ringProgress > 0f && ringProgress < 1f)
		{
			float size = 50f * MathHelper.Lerp(0.3f, 2f, ringProgress);
			_spriteBatch.Draw(_ringMask, CenteredRect(new Vector2(960f, rowTop + 46f), size), new Color(232, 196, 93) * (0.75f * (1f - ringProgress) * alpha));
		}
	}

	private void DrawRouteCard(Rectangle card, bool left, ClimbPointsAwardTier tier, int tierIndex, float alpha)
	{
		DrawHorizontalGradient(card, new Color(13, 10, 11), new Color(31, 7, 12), 20, RouteCardAlpha * alpha);
		DrawBorder(card, new Color(183, 173, 163) * (0.24f * alpha), 1);
		var accent = left
			? new Rectangle(card.Right - 2, card.Y, 2, card.Height)
			: new Rectangle(card.X, card.Y, 2, card.Height);
		_spriteBatch.Draw(_pixel, accent, new Color(141, 20, 40) * alpha);

		Vector2 connectorStart = left
			? new Vector2(card.Right, card.Center.Y)
			: new Vector2(card.Left, card.Center.Y);
		Vector2 connectorEnd = new(960f, card.Center.Y);
		DrawLine(connectorStart, connectorEnd, new Color(232, 196, 93) * (0.32f * alpha), 1f);

		float pointsScale = 0.328f;
		string points = PointLabels[Math.Clamp(tier.Points, 0, PointLabels.Length - 1)];
		const float requirementSpacing = 2f;
		float requirementScale = _tierRequirementScales[tierIndex];
		if (left)
		{
			DrawRightAlignedString(_titleFont, tier.Name, card.Right - 82f, card.Y + 9f, 0.195f, new Color(242, 237, 227) * alpha);
			DrawRightAlignedSpacedString(_bodyFont, tier.Requirement, card.Right - 82f, card.Y + 43f, requirementScale, requirementSpacing, new Color(183, 173, 163) * alpha);
			DrawCenteredString(_titleFont, points, new Rectangle(card.Right - 72, card.Y, 62, card.Height), new Color(255, 241, 172) * alpha, pointsScale);
		}
		else
		{
			DrawString(_titleFont, tier.Name, new Vector2(card.X + 82f, card.Y + 9f), 0.195f, new Color(242, 237, 227) * alpha);
			DrawSpacedString(_bodyFont, tier.Requirement, new Vector2(card.X + 82f, card.Y + 43f), requirementScale, requirementSpacing, new Color(183, 173, 163) * alpha);
			DrawCenteredString(_titleFont, points, new Rectangle(card.X + 10, card.Y, 62, card.Height), new Color(255, 241, 172) * alpha, pointsScale);
		}
	}

	private void DrawEmptyState(ClimbPointsAwardScenario scenario, float elapsed, float alpha)
	{
		float progress = ClimbPointsAwardAnimationService.Clamp01((elapsed - ClimbPointsAwardAnimationService.TierStartSeconds) / 0.5f);
		if (progress <= 0f) return;
		float y = MathHelper.Lerp(572f, 560f, progress);
		var panel = new Rectangle(650, (int)MathF.Round(y), 620, 96);
		_spriteBatch.Draw(_pixel, panel, new Color(7, 7, 8) * (0.62f * progress * alpha));
		_spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Y, panel.Width, 1), new Color(183, 173, 163) * (0.22f * progress * alpha));
		_spriteBatch.Draw(_pixel, new Rectangle(panel.X, panel.Bottom - 1, panel.Width, 1), new Color(183, 173, 163) * (0.22f * progress * alpha));
		DrawCenteredSpacedString(_titleFont, scenario.EmptyTitle, panel.Y + 18f, 0.227f, 2f, new Color(242, 237, 227) * (progress * alpha));
		DrawCenteredSpacedString(_bodyFont, scenario.EmptyDetail, panel.Y + 59f, 0.102f, 3f, new Color(183, 173, 163) * (progress * alpha));
	}

	private void DrawCrest(ClimbPointsAwardOverlayState state, int earnedCount, float alpha)
	{
		float totalReveal = ClimbPointsAwardAnimationService.GetTotalRevealSeconds(earnedCount);
		float elapsed = state.ElapsedSeconds;
		bool revealed = elapsed >= totalReveal;
		float progress = revealed
			? ClimbPointsAwardAnimationService.Clamp01((elapsed - totalReveal) / ClimbPointsAwardAnimationService.TotalAnimationSeconds)
			: 0f;
		float crestAlpha = revealed ? ClimbPointsAwardAnimationService.Clamp01(progress / 0.18f) : 0.22f;
		float scale;
		float rotation;
		if (!revealed)
		{
			scale = 0.84f;
			rotation = 0f;
		}
		else if (progress < 0.48f)
		{
			float phase = ClimbPointsAwardAnimationService.EaseSlam(progress / 0.48f);
			scale = MathHelper.Lerp(0.58f, 1.10f, phase);
			rotation = MathHelper.ToRadians(MathHelper.Lerp(-3f, 1f, phase));
		}
		else
		{
			float phase = (progress - 0.48f) / 0.52f;
			scale = MathHelper.Lerp(1.10f, 1f, phase);
			rotation = MathHelper.ToRadians(MathHelper.Lerp(1f, 0f, phase));
		}

		Vector2 center = new(960f, 305f);
		DrawShockwave(center, elapsed - totalReveal, alpha);
		DrawSparks(center, elapsed - totalReveal, alpha);
		DrawCrestGeometry(center, scale, rotation, crestAlpha * alpha);

		int shownTotal = revealed
			? (int)MathF.Round(state.PointsAwarded * ClimbPointsAwardAnimationService.EaseOutCubic((elapsed - totalReveal) / ClimbPointsAwardAnimationService.CountUpSeconds))
			: 0;
		DrawCrestCopy(center, shownTotal, scale, rotation, crestAlpha * alpha);
	}

	private void DrawCrestGeometry(Vector2 center, float scale, float rotation, float alpha)
	{
		_spriteBatch.Draw(_radianceMask, CenteredRect(center, 320f * scale), new Color(232, 196, 93) * (0.18f * alpha));
		DrawDiamond(center, 184f * scale, Color.Transparent, new Color(212, 43, 69) * (0.36f * alpha), 1f * scale, rotation);
		DrawDiamond(center, 160f * scale, new Color(20, 5, 8) * (0.96f * alpha), new Color(232, 196, 93) * (0.88f * alpha), 1f * scale, rotation);
		DrawDiamond(center, 136f * scale, Color.Transparent, new Color(232, 196, 93) * (0.42f * alpha), 1f * scale, rotation);

		float wingY = center.Y;
		DrawLine(new Vector2(center.X - 218f * scale, wingY), new Vector2(center.X - 80f * scale, wingY), new Color(232, 196, 93) * alpha, 1f * scale);
		DrawLine(new Vector2(center.X + 80f * scale, wingY), new Vector2(center.X + 218f * scale, wingY), new Color(232, 196, 93) * alpha, 1f * scale);
		DrawDiamond(new Vector2(center.X - 80f * scale, wingY), 11f * scale, Color.Transparent, new Color(232, 196, 93) * alpha, 1f, rotation);
		DrawDiamond(new Vector2(center.X + 80f * scale, wingY), 11f * scale, Color.Transparent, new Color(232, 196, 93) * alpha, 1f, rotation);
	}

	private void DrawCrestCopy(Vector2 center, int total, float scale, float rotation, float alpha)
	{
		string number = PointLabels[Math.Clamp(total, 0, PointLabels.Length - 1)];
		float numberScale = 0.64f * scale;
		Vector2 numberSize = _titleFont.MeasureString(number) * numberScale;
		Vector2 numberPosition = RotatePoint(new Vector2(center.X - numberSize.X / 2f, center.Y - 51f * scale), center, rotation);
		_spriteBatch.DrawString(_titleFont, number, numberPosition + new Vector2(0f, 4f * scale), new Color(77, 4, 14) * (0.62f * alpha), rotation, Vector2.Zero, numberScale, SpriteEffects.None, 0f);
		_spriteBatch.DrawString(_titleFont, number, numberPosition, new Color(255, 241, 172) * alpha, rotation, Vector2.Zero, numberScale, SpriteEffects.None, 0f);

		const string label = "CLIMB POINTS AWARDED";
		float labelScale = 0.086f * scale;
		float labelWidth = MeasureSpacedString(_bodyFont, label, labelScale, 3f * scale);
		Vector2 labelPosition = RotatePoint(new Vector2(center.X - labelWidth / 2f, center.Y + 53f * scale), center, rotation);
		DrawSpacedString(_bodyFont, label, labelPosition, labelScale, 3f * scale, new Color(242, 237, 227) * alpha, rotation, center);
	}

	private void DrawShockwave(Vector2 center, float localElapsed, float alpha)
	{
		float progress = ClimbPointsAwardAnimationService.Clamp01(localElapsed / ShockwaveDurationSeconds);
		if (localElapsed < 0f || progress >= 1f) return;
		float size = 220f * MathHelper.Lerp(0.35f, 3.35f, progress);
		_spriteBatch.Draw(_ringMask, CenteredRect(center, size), new Color(255, 241, 172) * (0.65f * 0.9f * (1f - progress) * alpha));
	}

	private void DrawSparks(Vector2 center, float localElapsed, float alpha)
	{
		for (int index = 0; index < Sparks.Length; index++)
		{
			SparkDefinition spark = Sparks[index];
			float progress = ClimbPointsAwardAnimationService.Clamp01((localElapsed - spark.DelaySeconds) / SparkDurationSeconds);
			if (localElapsed < spark.DelaySeconds || progress >= 1f) continue;
			float eased = ClimbPointsAwardAnimationService.CubicBezier(progress, 0.1f, 0.7f, 0.2f, 1f);
			Vector2 direction = new(MathF.Sin(spark.AngleRadians), MathF.Cos(spark.AngleRadians));
			Vector2 start = center + direction * (spark.Distance * eased);
			float length = 18f * MathHelper.Lerp(0.5f, 1.3f, progress);
			DrawLine(start, start + direction * length, new Color(255, 241, 172) * ((1f - progress) * ParticleAlpha * alpha), 3f);
		}
	}

	private void DrawReadyHint(ClimbPointsAwardOverlayState state, int earnedCount, float alpha)
	{
		float readyAt = ClimbPointsAwardAnimationService.GetReadySeconds(earnedCount);
		float progress = ClimbPointsAwardAnimationService.Clamp01((state.ElapsedSeconds - readyAt) / 0.35f);
		if (progress <= 0f) return;
		DrawCenteredSpacedString(
			_bodyFont,
			"CLICK ANYWHERE TO CONTINUE",
			982f,
			0.078f,
			3f,
			new Color(242, 237, 227) * (0.44f * progress * alpha));
	}

	private void DrawHorizontalGradientRule(int centerX, int y, int width, Color color, float alpha)
	{
		const int strips = 48;
		int left = centerX - width / 2;
		for (int strip = 0; strip < strips; strip++)
		{
			float t = (strip + 0.5f) / strips;
			float edgeFade = 1f - MathF.Abs(t - 0.5f) * 2f;
			int x0 = left + strip * width / strips;
			int x1 = left + (strip + 1) * width / strips;
			_spriteBatch.Draw(_pixel, new Rectangle(x0, y, Math.Max(1, x1 - x0), 1), color * (alpha * edgeFade));
		}
	}

	private void DrawHorizontalGradient(Rectangle rect, Color left, Color right, int strips, float alpha)
	{
		for (int strip = 0; strip < strips; strip++)
		{
			float t = strips <= 1 ? 0f : strip / (float)(strips - 1);
			int x0 = rect.X + strip * rect.Width / strips;
			int x1 = rect.X + (strip + 1) * rect.Width / strips;
			_spriteBatch.Draw(_pixel, new Rectangle(x0, rect.Y, Math.Max(1, x1 - x0), rect.Height), Color.Lerp(left, right, t) * alpha);
		}
	}

	private void DrawVerticalGradient(Rectangle rect, Color top, Color bottom, int strips, float alpha)
	{
		for (int strip = 0; strip < strips; strip++)
		{
			float t = strips <= 1 ? 0f : strip / (float)(strips - 1);
			int y0 = rect.Y + strip * rect.Height / strips;
			int y1 = rect.Y + (strip + 1) * rect.Height / strips;
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)), Color.Lerp(top, bottom, t) * alpha);
		}
	}

	private void DrawBorder(Rectangle rect, Color color, int thickness)
	{
		int size = Math.Max(1, thickness);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, size), color);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - size, rect.Width, size), color);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, size, rect.Height), color);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - size, rect.Y, size, rect.Height), color);
	}

	private void DrawDiamond(Vector2 center, float size, Color fill, Color border, float thickness, float rotation = 0f)
	{
		if (size <= 0f) return;
		if (fill.A > 0)
			_spriteBatch.Draw(_diamondMask, CenteredRect(center, size), fill);
		if (border.A == 0 || thickness <= 0f) return;
		Vector2 top = new(center.X, center.Y - size / 2f);
		Vector2 right = new(center.X + size / 2f, center.Y);
		Vector2 bottom = new(center.X, center.Y + size / 2f);
		Vector2 left = new(center.X - size / 2f, center.Y);
		if (MathF.Abs(rotation) > 0.0001f)
		{
			top = RotatePoint(top, center, rotation);
			right = RotatePoint(right, center, rotation);
			bottom = RotatePoint(bottom, center, rotation);
			left = RotatePoint(left, center, rotation);
		}
		DrawLine(top, right, border, thickness);
		DrawLine(right, bottom, border, thickness);
		DrawLine(bottom, left, border, thickness);
		DrawLine(left, top, border, thickness);
	}

	private void DrawLine(Vector2 start, Vector2 end, Color color, float thickness)
	{
		Vector2 delta = end - start;
		float length = delta.Length();
		if (length <= 0.001f || color.A == 0) return;
		float rotation = MathF.Atan2(delta.Y, delta.X);
		_spriteBatch.Draw(_pixel, start, null, color, rotation, new Vector2(0f, 0.5f), new Vector2(length, Math.Max(0.5f, thickness)), SpriteEffects.None, 0f);
	}

	private void DrawCenteredSpacedString(SpriteFont font, string text, float y, float scale, float spacing, Color color)
	{
		float width = MeasureSpacedString(font, text, scale, spacing);
		DrawSpacedString(font, text, new Vector2((Game1.VirtualWidth - width) / 2f, y), scale, spacing, color);
	}

	private void DrawCenteredSpacedStringWithShadow(SpriteFont font, string text, float y, float scale, float spacing, Color color)
	{
		float width = MeasureSpacedString(font, text, scale, spacing);
		Vector2 position = new((Game1.VirtualWidth - width) / 2f, y);
		DrawSpacedString(font, text, position + new Vector2(0f, 5f), scale, spacing, Color.Black * (0.9f * (color.A / 255f)));
		DrawSpacedString(font, text, position, scale, spacing, color);
	}

	private void DrawRightAlignedSpacedString(SpriteFont font, string text, float right, float y, float scale, float spacing, Color color)
	{
		float width = MeasureSpacedString(font, text, scale, spacing);
		DrawSpacedString(font, text, new Vector2(right - width, y), scale, spacing, color);
	}

	private void DrawRightAlignedString(SpriteFont font, string text, float right, float y, float scale, Color color)
	{
		Vector2 size = font.MeasureString(text) * scale;
		DrawString(font, text, new Vector2(right - size.X, y), scale, color);
	}

	private void DrawCenteredString(SpriteFont font, string text, Rectangle rect, Color color, float scale)
	{
		Vector2 size = font.MeasureString(text) * scale;
		Vector2 position = new(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f);
		DrawString(font, text, position, scale, color);
	}

	private void DrawString(SpriteFont font, string text, Vector2 position, float scale, Color color)
	{
		_spriteBatch.DrawString(font, text, position, Color.Black * (0.45f * (color.A / 255f)), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		_spriteBatch.DrawString(font, text, position - new Vector2(0f, 2f), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
	}

	private void DrawSpacedString(
		SpriteFont font,
		string text,
		Vector2 position,
		float scale,
		float spacing,
		Color color,
		float rotation = 0f,
		Vector2 rotationOrigin = default)
	{
		Vector2 cursor = position;
		for (int index = 0; index < text.Length; index++)
		{
			char character = text[index];
			string glyph = character is >= ' ' and <= '~' ? Glyphs[character - ' '] : "?";
			Vector2 drawPosition = MathF.Abs(rotation) > 0.0001f
				? RotatePoint(cursor, rotationOrigin, rotation)
				: cursor;
			_spriteBatch.DrawString(font, glyph, drawPosition, color, rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
			cursor.X += GetGlyphWidth(font, character) * scale + spacing;
		}
	}

	private float MeasureSpacedString(SpriteFont font, string text, float scale, float spacing)
	{
		if (string.IsNullOrEmpty(text)) return 0f;
		float width = 0f;
		for (int index = 0; index < text.Length; index++)
		{
			char character = text[index];
			string glyph = character is >= ' ' and <= '~' ? Glyphs[character - ' '] : "?";
			width += GetGlyphWidth(font, character) * scale;
			if (index < text.Length - 1) width += spacing;
		}
		return width;
	}

	private float FitSpacedStringScale(
		SpriteFont font,
		string text,
		float preferredScale,
		float spacing,
		float maxWidth)
	{
		if (string.IsNullOrEmpty(text) || maxWidth <= 0f) return preferredScale;
		float glyphWidth = 0f;
		for (int index = 0; index < text.Length; index++)
		{
			char character = text[index];
			glyphWidth += GetGlyphWidth(font, character);
		}
		float trackingWidth = Math.Max(0, text.Length - 1) * spacing;
		if (glyphWidth <= 0f || trackingWidth >= maxWidth) return preferredScale;
		return Math.Min(preferredScale, (maxWidth - trackingWidth) / glyphWidth);
	}

	private float GetGlyphWidth(SpriteFont font, char character)
	{
		int index = character is >= ' ' and <= '~' ? character - ' ' : '?' - ' ';
		return ReferenceEquals(font, _titleFont) ? _titleGlyphWidths[index] : _bodyGlyphWidths[index];
	}

	private static Rectangle CenteredRect(Vector2 center, float size)
	{
		int rounded = Math.Max(1, (int)MathF.Round(size));
		return new Rectangle(
			(int)MathF.Round(center.X - rounded / 2f),
			(int)MathF.Round(center.Y - rounded / 2f),
			rounded,
			rounded);
	}

	private static Vector2 RotatePoint(Vector2 point, Vector2 origin, float rotation)
	{
		if (MathF.Abs(rotation) <= 0.0001f) return point;
		Vector2 delta = point - origin;
		float cosine = MathF.Cos(rotation);
		float sine = MathF.Sin(rotation);
		return origin + new Vector2(
			delta.X * cosine - delta.Y * sine,
			delta.X * sine + delta.Y * cosine);
	}

	private static string[] BuildGlyphCache()
	{
		var glyphs = new string[95];
		for (int index = 0; index < glyphs.Length; index++) glyphs[index] = ((char)(' ' + index)).ToString();
		return glyphs;
	}

	private static float[] BuildGlyphWidths(SpriteFont font)
	{
		var widths = new float[Glyphs.Length];
		for (int index = 0; index < widths.Length; index++) widths[index] = font.MeasureString(Glyphs[index]).X;
		return widths;
	}

	private static string[] BuildPointLabelCache()
	{
		var labels = new string[100];
		for (int index = 0; index < labels.Length; index++) labels[index] = $"+{index}";
		return labels;
	}

	private static SparkDefinition[] BuildSparkDefinitions()
	{
		var definitions = new SparkDefinition[18];
		for (int index = 0; index < definitions.Length; index++)
		{
			float angle = (360f / definitions.Length) * index + (index % 2 == 1 ? 4f : -3f);
			definitions[index] = new SparkDefinition(
				MathHelper.ToRadians(angle),
				145f + index % 5 * 24f,
				index % 3 * 0.022f);
		}
		return definitions;
	}

	private readonly record struct SparkDefinition(float AngleRadians, float Distance, float DelaySeconds);

	IEnumerable<object> IDebugInspectableChildren.GetDebugInspectableChildren()
	{
		yield return ClimbPointsAwardAnimationTimingSettings.Instance;
	}
}
