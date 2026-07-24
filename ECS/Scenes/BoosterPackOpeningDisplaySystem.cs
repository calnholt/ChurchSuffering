using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Objects.Equipment;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems;

[DebugTab("Booster Pack Opening")]
public sealed class BoosterPackOpeningDisplaySystem : Core.System
{
	internal readonly record struct CardFanPlacement(
		CardData.CardColor Color,
		Vector2 Position,
		float Rotation);

	private const string OverlayEntityName = "BoosterPackOpeningOverlay";
	private const string BlockerEntityName = "BoosterPackOpeningBlocker";
	private const string EquipmentTooltipEntityName = "BoosterPack_EquipmentTooltip";
	private const string ContextId = "overlay.booster-pack-opening";
	private const string RumbleChannelId = "booster-pack-opening";
	private const int FixedMaskDiameter = 512;
	private const int ParticleCoreDiameter = 24;
	private const int ShardMaskWidth = 32;
	private const int ShardMaskHeight = 64;

	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;
	private readonly ImageAssetService _imageAssets;
	private readonly IPlayerInputSource _inputSource;
	private readonly Texture2D _pixel;
	private readonly SpriteFont _titleFont;
	private readonly SpriteFont _bodyFont;
	private readonly Random _rng;
	private readonly Dictionary<string, Texture2D> _assetTextures = new();
	private readonly Dictionary<int, Texture2D> _lootTextures = new();

	private Texture2D _booster1;
	private Texture2D _booster2;
	private Texture2D _booster3;
	private Texture2D _boosterLeft;
	private Texture2D _boosterRight;
	private Texture2D _softRadialMask;
	private Texture2D _vignetteMask;
	private Texture2D _rayburstMask;
	private Texture2D _particleCoreMask;
	private Texture2D _shardMask;
	private bool _snapshotTimeFrozen;
	private EquipmentTooltipDisplaySystem _equipmentTooltipDisplaySystem;

	[DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
	public int ZOrder { get; set; } = 61000;

	// Timeline
	[DebugEditable(DisplayName = "Summon Duration Seconds", Step = 0.01f, Min = 0f, Max = 5f)]
	public float SummonDurationSeconds { get; set; } = 0.76f;

	[DebugEditable(DisplayName = "Idle Duration Seconds", Step = 0.01f, Min = 0f, Max = 5f)]
	public float IdleDurationSeconds { get; set; } = 0.52f;

	[DebugEditable(DisplayName = "Charge Duration Seconds", Step = 0.01f, Min = 0f, Max = 5f)]
	public float ChargeDurationSeconds { get; set; } = 0.85f;

	[DebugEditable(DisplayName = "Crack Duration Seconds", Step = 0.01f, Min = 0f, Max = 5f)]
	public float CrackDurationSeconds { get; set; } = 0.65f;

	[DebugEditable(DisplayName = "Rupture Duration Seconds", Step = 0.01f, Min = 0f, Max = 5f)]
	public float RuptureDurationSeconds { get; set; } = 0.76f;

	[DebugEditable(DisplayName = "Showcase Duration Seconds", Step = 0.01f, Min = 0f, Max = 5f)]
	public float ShowcaseDurationSeconds { get; set; } = 1.60f;

	[DebugEditable(DisplayName = "Charge Particle Interval Seconds", Step = 0.01f, Min = 0.01f, Max = 2f)]
	public float ChargeParticleIntervalSeconds { get; set; } = 0.26f;

	[DebugEditable(DisplayName = "Reveal Stagger Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
	public float RevealStaggerSeconds { get; set; } = 0.12f;

	[DebugEditable(DisplayName = "Reveal Travel Seconds", Step = 0.01f, Min = 0.01f, Max = 3f)]
	public float RevealTravelSeconds { get; set; } = 0.72f;

	[DebugEditable(DisplayName = "Sheen Delay Seconds", Step = 0.01f, Min = 0f, Max = 3f)]
	public float SheenDelaySeconds { get; set; } = 0.52f;

	// Stage and pack
	[DebugEditable(DisplayName = "Stage Center X", Step = 1, Min = 0, Max = 1920)]
	public float StageCenterX { get; set; } = 960f;

	[DebugEditable(DisplayName = "Stage Center Y", Step = 1, Min = 0, Max = 1080)]
	public float StageCenterY { get; set; } = 540f;

	[DebugEditable(DisplayName = "Pack Width", Step = 1, Min = 100, Max = 800)]
	public int PackWidth { get; set; } = 370;

	[DebugEditable(DisplayName = "Pack Height", Step = 1, Min = 100, Max = 1000)]
	public int PackHeight { get; set; } = 620;

	[DebugEditable(DisplayName = "Pack Center Y", Step = 1, Min = 0, Max = 1080)]
	public float PackCenterY { get; set; } = 541f;

	[DebugEditable(DisplayName = "Pack Aura Size", Step = 1, Min = 100, Max = 1000)]
	public int PackAuraSize { get; set; } = 590;

	[DebugEditable(DisplayName = "Pack Summon Offset Y", Step = 1, Min = -1000, Max = 0)]
	public float PackSummonOffsetY { get; set; } = -560f;

	[DebugEditable(DisplayName = "Pack Summon Start Scale", Step = 0.01f, Min = 0.1f, Max = 1f)]
	public float PackSummonStartScale { get; set; } = 0.54f;

	[DebugEditable(DisplayName = "Pack Summon Overshoot Y", Step = 1, Min = 0, Max = 120)]
	public float PackSummonOvershootY { get; set; } = 34f;

	[DebugEditable(DisplayName = "Pack Summon Overshoot Scale", Step = 0.01f, Min = 1f, Max = 1.5f)]
	public float PackSummonOvershootScale { get; set; } = 1.08f;

	[DebugEditable(DisplayName = "Pack Idle Float Px", Step = 1, Min = 0, Max = 60)]
	public float PackIdleFloatPx { get; set; } = 14f;

	[DebugEditable(DisplayName = "Pack Charge Shake Px", Step = 1, Min = 0, Max = 40)]
	public float PackChargeShakePx { get; set; } = 5f;

	[DebugEditable(DisplayName = "Pack Crack Shake Px", Step = 1, Min = 0, Max = 60)]
	public float PackCrackShakePx { get; set; } = 10f;

	[DebugEditable(DisplayName = "Rupture Shake Amplitude Px", Step = 1, Min = 0, Max = 60)]
	public float RuptureShakeAmplitudePx { get; set; } = 13f;

	[DebugEditable(DisplayName = "Rupture Shake Duration Seconds", Step = 0.01f, Min = 0f, Max = 2f)]
	public float RuptureShakeDurationSeconds { get; set; } = 0.58f;

	// Lighting
	[DebugEditable(DisplayName = "Base Blackout Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float BaseBlackoutAlpha { get; set; } = 0.68f;

	[DebugEditable(DisplayName = "Charge Blackout Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float ChargeBlackoutAlpha { get; set; } = 0.76f;

	[DebugEditable(DisplayName = "Showcase Blackout Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float ShowcaseBlackoutAlpha { get; set; } = 0.70f;

	[DebugEditable(DisplayName = "Vignette Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float VignetteAlpha { get; set; } = 0.72f;

	[DebugEditable(DisplayName = "Floor Glow Width", Step = 1, Min = 100, Max = 1200)]
	public int FloorGlowWidth { get; set; } = 760;

	[DebugEditable(DisplayName = "Floor Glow Height", Step = 1, Min = 40, Max = 400)]
	public int FloorGlowHeight { get; set; } = 150;

	[DebugEditable(DisplayName = "Floor Glow Y", Step = 1, Min = 0, Max = 1080)]
	public int FloorGlowY { get; set; } = 830;

	[DebugEditable(DisplayName = "Stage Ray Width", Step = 1, Min = 100, Max = 1200)]
	public int StageRayWidth { get; set; } = 640;

	[DebugEditable(DisplayName = "Stage Ray Height", Step = 1, Min = 40, Max = 700)]
	public int StageRayHeight { get; set; } = 218;

	[DebugEditable(DisplayName = "Reward Ray Size", Step = 1, Min = 80, Max = 700)]
	public int RewardRaySize { get; set; } = 320;

	[DebugEditable(DisplayName = "Reward Ray Min Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float RewardRayMinAlpha { get; set; } = 0.14f;

	[DebugEditable(DisplayName = "Reward Ray Max Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float RewardRayMaxAlpha { get; set; } = 0.20f;

	// Loot layout
	[DebugEditable(DisplayName = "Loot Slot Width", Step = 1, Min = 100, Max = 600)]
	public int LootSlotWidth { get; set; } = 360;

	[DebugEditable(DisplayName = "Loot Slot Height", Step = 1, Min = 100, Max = 800)]
	public int LootSlotHeight { get; set; } = 540;

	[DebugEditable(DisplayName = "Loot Gap", Step = 1, Min = 0, Max = 220)]
	public int LootGap { get; set; } = 70;

	[DebugEditable(DisplayName = "Loot Center Y", Step = 1, Min = 0, Max = 1080)]
	public int LootCenterY { get; set; } = 540;

	[DebugEditable(DisplayName = "Reward Arc Height", Step = 1, Min = 0, Max = 500)]
	public float RewardArcHeight { get; set; } = 120f;

	[DebugEditable(DisplayName = "Reward Idle Float Px", Step = 1, Min = 0, Max = 30)]
	public float RewardIdleFloatPx { get; set; } = 4f;

	[DebugEditable(DisplayName = "Reward Idle Period Seconds", Step = 0.01f, Min = 0.1f, Max = 10f)]
	public float RewardIdlePeriodSeconds { get; set; } = 2.6f;

	[DebugEditable(DisplayName = "Card Scale", Step = 0.01f, Min = 0.2f, Max = 2f)]
	public float CardScale { get; set; } = 1.09f;

	[DebugEditable(DisplayName = "Card Fan Horizontal Gap", Step = 1f, Min = 0f, Max = 160f)]
	public float CardFanHorizontalGap { get; set; } = 34f;

	[DebugEditable(DisplayName = "Card Fan Rear Drop", Step = 1f, Min = -100f, Max = 100f)]
	public float CardFanRearDrop { get; set; } = 12f;

	[DebugEditable(DisplayName = "Card Fan Rotation (deg)", Step = 0.5f, Min = 0f, Max = 30f)]
	public float CardFanRotationDegrees { get; set; } = 5f;

	[DebugEditable(DisplayName = "Medal Size", Step = 1, Min = 40, Max = 320)]
	public int MedalSize { get; set; } = 156;

	[DebugEditable(DisplayName = "Equipment Icon Box", Step = 1, Min = 40, Max = 320)]
	public int EquipmentIconBox { get; set; } = 148;

	[DebugEditable(DisplayName = "Equipment Icon Scale", Step = 0.01f, Min = 0.1f, Max = 3f)]
	public float EquipmentIconScale { get; set; } = 1.55f;

	// FX
	[DebugEditable(DisplayName = "Charge Particle Count", Step = 1, Min = 0, Max = 200)]
	public int ChargeParticleCount { get; set; } = 34;

	[DebugEditable(DisplayName = "Charge Repeat Count", Step = 1, Min = 0, Max = 80)]
	public int ChargeRepeatParticleCount { get; set; } = 12;

	[DebugEditable(DisplayName = "Crack Particle Count", Step = 1, Min = 0, Max = 200)]
	public int CrackParticleCount { get; set; } = 24;

	[DebugEditable(DisplayName = "Burst Particle Count", Step = 1, Min = 0, Max = 200)]
	public int BurstParticleCount { get; set; } = 74;

	[DebugEditable(DisplayName = "Showcase Particle Count", Step = 1, Min = 0, Max = 200)]
	public int ShowcaseParticleCount { get; set; } = 42;

	[DebugEditable(DisplayName = "Shard Count", Step = 1, Min = 0, Max = 120)]
	public int ShardCount { get; set; } = 34;

	[DebugEditable(DisplayName = "Streak Width", Step = 0.01f, Min = 0.5f, Max = 30f)]
	public float StreakWidth { get; set; } = 4.5f;

	[DebugEditable(DisplayName = "Streak Length", Step = 1f, Min = 4f, Max = 180f)]
	public float StreakLength { get; set; } = 62f;

	[DebugEditable(DisplayName = "Shard Highlight Scale", Step = 0.01f, Min = 0.1f, Max = 1f)]
	public float ShardHighlightScale { get; set; } = 0.62f;

	[DebugEditable(DisplayName = "Flash Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float FlashAlpha { get; set; } = 0.62f;

	[DebugEditable(DisplayName = "Beam Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
	public float BeamAlpha { get; set; } = 0.54f;

	[DebugEditable(DisplayName = "Shockwave Duration", Step = 0.01f, Min = 0.01f, Max = 3f)]
	public float ShockwaveDurationSec { get; set; } = 0.45f;

	[DebugEditable(DisplayName = "Shockwave Max Radius", Step = 1f, Min = 1f, Max = 1920f)]
	public float ShockwaveMaxRadiusPx { get; set; } = 720f;

	[DebugEditable(DisplayName = "Shockwave Ripple Width", Step = 1f, Min = 1f, Max = 200f)]
	public float ShockwaveRippleWidthPx { get; set; } = 36f;

	[DebugEditable(DisplayName = "Shockwave Strength", Step = 0.001f, Min = 0f, Max = 0.1f)]
	public float ShockwaveStrength { get; set; } = 0.014f;

	[DebugEditable(DisplayName = "Shockwave Chromatic Amp", Step = 0.001f, Min = 0f, Max = 0.1f)]
	public float ShockwaveChromaticAberrationAmp { get; set; } = 0.006f;

	[DebugEditable(DisplayName = "Shockwave Chromatic Freq", Step = 1f, Min = 0f, Max = 100f)]
	public float ShockwaveChromaticAberrationFreq { get; set; } = 22f;

	[DebugEditable(DisplayName = "Shockwave Shading", Step = 0.01f, Min = 0f, Max = 1f)]
	public float ShockwaveShadingIntensity { get; set; } = 0.20f;

	// Title
	[DebugEditable(DisplayName = "Reward Title Y", Step = 1, Min = 0, Max = 300)]
	public int RewardTitleY { get; set; } = 62;

	[DebugEditable(DisplayName = "Reward Kicker Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
	public float RewardKickerScale { get; set; } = 0.09f;

	[DebugEditable(DisplayName = "Reward Headline Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
	public float RewardHeadlineScale { get; set; } = 0.58f;

	[DebugEditable(DisplayName = "Rumble Buildup Low", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleBuildupLow { get; set; } = 0.35f;

	[DebugEditable(DisplayName = "Rumble Buildup High", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleBuildupHigh { get; set; } = 0.55f;

	[DebugEditable(DisplayName = "Rumble Loot Pulse Low", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleLootPulseLow { get; set; } = 0.08f;

	[DebugEditable(DisplayName = "Rumble Loot Pulse High", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleLootPulseHigh { get; set; } = 0.12f;

	[DebugEditable(DisplayName = "Rumble Loot Pulse Duration (s)", Step = 0.01f, Min = 0.01f, Max = 1f)]
	public float RumbleLootPulseDurationSeconds { get; set; } = 0.06f;

	[DebugEditable(DisplayName = "Rumble Buildup Trigger", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleBuildupTrigger { get; set; } = 0.18f;

	[DebugEditable(DisplayName = "Rumble Loot Pulse Trigger", Step = 0.05f, Min = 0f, Max = 1f)]
	public float RumbleLootPulseTrigger { get; set; } = 0.08f;

	public BoosterPackOpeningDisplaySystem(
		EntityManager entityManager,
		GraphicsDevice graphicsDevice,
		SpriteBatch spriteBatch,
		ImageAssetService imageAssets,
		IPlayerInputSource inputSource = null,
		Random random = null)
		: base(entityManager)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		_imageAssets = imageAssets;
		_inputSource = inputSource;
		_rng = random ?? new Random();
		_pixel = _imageAssets.GetPixel(Color.White);
		_titleFont = FontSingleton.TitleFont;
		_bodyFont = FontSingleton.ChakraPetchFont;

		EventManager.Subscribe<ShowBoosterPackOpeningOverlayEvent>(evt => OpenOverlay(evt?.Pack));
		EventManager.Subscribe<CloseBoosterPackOpeningOverlayEvent>(_ => CloseOverlay());
		EventManager.Subscribe<DeleteCachesEvent>(_ => ClearRenderResources());
		EnsureEquipmentTooltipEntity();
		_equipmentTooltipDisplaySystem = new EquipmentTooltipDisplaySystem(
			EntityManager,
			_graphicsDevice,
			_spriteBatch,
			_imageAssets,
			EquipmentTooltipEntityName);
	}

	protected override IEnumerable<Entity> GetRelevantEntities()
	{
		return EntityManager.GetEntitiesWithComponent<BoosterPackOpeningOverlayState>();
	}

	protected override void UpdateEntity(Entity entity, GameTime gameTime)
	{
	}

	public override void Update(GameTime gameTime)
	{
		var state = GetOverlay()?.GetComponent<BoosterPackOpeningOverlayState>();
		if (state?.IsOpen != true)
		{
			StopRumble();
			SetBlockerActive(false);
			SetPreviewEntitiesInactive(state);
			return;
		}
		if (_snapshotTimeFrozen || IsSnapshotScene()) return;

		EnsureBlocker();
		SetBlockerActive(true);
		var timing = BuildTiming();
		state.PreviousElapsedSeconds = state.ElapsedSeconds;
		float delta = (float)gameTime.ElapsedGameTime.TotalSeconds;
		if (!float.IsFinite(delta) || delta < 0f) delta = 0f;
		state.ElapsedSeconds += delta;
		state.Phase = BoosterPackOpeningAnimationService.GetPhase(state.ElapsedSeconds, timing);
		ProcessMilestones(state, timing);
		UpdatePresentationParticles(state);
		UpdateCardSheens(state, timing);
		UpdateRewardInteractions(state, timing);
		SetDismissEnabled(
			state,
			BoosterPackOpeningAnimationService.CanDismiss(state.ElapsedSeconds, timing));
		UpdateRumble(state, timing);
		EnsureEquipmentTooltipEntity();
		_equipmentTooltipDisplaySystem?.Update(gameTime);
	}

	public void Draw()
	{
		var state = GetOverlay()?.GetComponent<BoosterPackOpeningOverlayState>();
		if (state?.IsOpen != true || !HasRenderResources()) return;

		DrawSceneWash(state);
		DrawStageLighting(state);
		DrawPack(state);
		DrawRuptureFx(state);
		DrawParticles(state);
		DrawShards(state);
		DrawLoot(state);
		DrawRewardTitle(state);
		DrawVignette();
		_equipmentTooltipDisplaySystem?.Draw();
	}

	[DebugAction("Play Booster Pack")]
	public void DebugPlayBoosterPack() => OpenOverlay();

	[DebugAction("Close Booster Pack")]
	public void DebugCloseBoosterPack() => CloseOverlay();

	internal void OpenForSnapshot(float elapsedSeconds, BoosterPackSave pack = null)
	{
		OpenOverlay(pack);
		_snapshotTimeFrozen = true;
		var state = GetOverlay()?.GetComponent<BoosterPackOpeningOverlayState>();
		if (state == null) return;
		float elapsed = float.IsFinite(elapsedSeconds) ? Math.Max(0f, elapsedSeconds) : 0f;
		state.PreviousElapsedSeconds = 0f;
		state.ElapsedSeconds = elapsed;
		var timing = BuildTiming();
		state.Phase = BoosterPackOpeningAnimationService.GetPhase(elapsed, timing);
		ProcessMilestones(state, timing);
		UpdatePresentationParticles(state);
		UpdateCardSheens(state, timing);
		UpdateRewardInteractions(state, timing);
		SetDismissEnabled(state, BoosterPackOpeningAnimationService.CanDismiss(elapsed, timing));
	}

	private BoosterPackOpeningTiming BuildTiming()
	{
		return new BoosterPackOpeningTiming(
			SummonDurationSeconds,
			IdleDurationSeconds,
			ChargeDurationSeconds,
			CrackDurationSeconds,
			RuptureDurationSeconds,
			ShowcaseDurationSeconds,
			ChargeParticleIntervalSeconds,
			RevealStaggerSeconds,
			RevealTravelSeconds,
			SheenDelaySeconds,
			RuptureShakeDurationSeconds);
	}

	private BoosterPackRumbleSettings BuildRumbleSettings()
	{
		return new BoosterPackRumbleSettings(
			RumbleBuildupLow,
			RumbleBuildupHigh,
			RumbleLootPulseLow,
			RumbleLootPulseHigh,
			RumbleLootPulseDurationSeconds,
			RumbleBuildupTrigger,
			RumbleLootPulseTrigger);
	}

	private void UpdateRumble(BoosterPackOpeningOverlayState state, BoosterPackOpeningTiming timing)
	{
		if (_inputSource == null) return;
		PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
		if (!frame.IsGamepadConnected || !frame.IsWindowActive)
		{
			StopRumble();
			return;
		}

		var sample = BoosterPackOpeningAnimationService.SampleRumble(
			state.ElapsedSeconds,
			state.Loot.Count,
			timing,
			BuildRumbleSettings());
		_inputSource.SetRumbleChannel(RumbleChannelId, new RumbleMotorState(
			sample.LowFrequency,
			sample.HighFrequency,
			sample.LeftTrigger,
			sample.RightTrigger));
	}

	private void StopRumble()
	{
		_inputSource?.ClearRumbleChannel(RumbleChannelId);
	}

	private void OpenOverlay(BoosterPackSave pack = null)
	{
		_snapshotTimeFrozen = false;
		var overlay = EnsureOverlay();
		var state = overlay.GetComponent<BoosterPackOpeningOverlayState>();
		DestroyPreviewEntities(state);
		state.Particles.Clear();
		state.Shards.Clear();
		_lootTextures.Clear();
		EnsureRenderResources();

		state.IsOpen = true;
		state.CanDismiss = false;
		state.ElapsedSeconds = 0f;
		state.PreviousElapsedSeconds = 0f;
		state.Phase = BoosterPackOpeningPhase.Summon;
		state.IsAuthoritativePack = pack?.rewards?.Count == 3;
		var timing = BuildTiming();
		state.NextChargeParticleSeconds = timing.ChargeStart + Math.Max(0.001f, timing.ChargeParticleInterval);
		state.Loot = CreateLootPreviews(timing, pack);
		EnsureBlocker();
		EnsureEquipmentTooltipEntity();
		SetDismissEnabled(state, false);
		SetBlockerActive(true);
		UpdateRewardInteractions(state, timing);
	}

	private void CloseOverlay()
	{
		var state = GetOverlay()?.GetComponent<BoosterPackOpeningOverlayState>();
		if (state == null)
		{
			StopRumble();
			SetBlockerActive(false);
			return;
		}

		bool wasAuthoritativePack = state.IsAuthoritativePack;
		state.IsOpen = false;
		state.IsAuthoritativePack = false;
		state.CanDismiss = false;
		DestroyPreviewEntities(state);
		state.Loot.Clear();
		state.Particles.Clear();
		state.Shards.Clear();
		_lootTextures.Clear();
		SetDismissEnabled(state, false);
		SetBlockerActive(false);
		StopRumble();
		ResetEquipmentTooltipState();
		EventManager.Publish(new BoosterPackOpeningDismissedEvent { WasAuthoritativePack = wasAuthoritativePack });
	}

	private Entity EnsureOverlay()
	{
		var overlay = EntityManager.GetEntity(OverlayEntityName);
		if (overlay == null)
		{
			overlay = EntityManager.CreateEntity(OverlayEntityName);
			EntityManager.AddComponent(overlay, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
			EntityManager.AddComponent(overlay, new BoosterPackOpeningOverlayState());
			EntityManager.AddComponent(overlay, new DontDestroyOnLoad());
			InputContextService.EnsureContext(EntityManager, overlay, ContextId, 760, true);
		}

		if (overlay.GetComponent<Transform>() is Transform transform) transform.ZOrder = ZOrder;
		var context = InputContextService.EnsureContext(EntityManager, overlay, ContextId, 760, true);
		context.IsActive = overlay.GetComponent<BoosterPackOpeningOverlayState>()?.IsOpen == true;
		return overlay;
	}

	private Entity GetOverlay() => EntityManager.GetEntity(OverlayEntityName);

	private void EnsureEquipmentTooltipEntity()
	{
		var entity = EntityManager.GetEntity(EquipmentTooltipEntityName);
		if (entity == null)
		{
			entity = EntityManager.CreateEntity(EquipmentTooltipEntityName);
			EntityManager.AddComponent(entity, new EquipmentTooltipState());
			EntityManager.AddComponent(entity, new Transform { ZOrder = 10002 });
			EntityManager.AddComponent(entity, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = false,
				IsHidden = true,
				TooltipType = TooltipType.None,
			});
			EntityManager.AddComponent(entity, new DontDestroyOnLoad());
			return;
		}

		if (entity.GetComponent<EquipmentTooltipState>() == null)
		{
			EntityManager.AddComponent(entity, new EquipmentTooltipState());
		}
		if (entity.GetComponent<Transform>() == null)
		{
			EntityManager.AddComponent(entity, new Transform { ZOrder = 10002 });
		}
		if (entity.GetComponent<UIElement>() == null)
		{
			EntityManager.AddComponent(entity, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = false,
				IsHidden = true,
				TooltipType = TooltipType.None,
			});
		}
	}

	private void ResetEquipmentTooltipState()
	{
		var state = EntityManager.GetEntity(EquipmentTooltipEntityName)
			?.GetComponent<EquipmentTooltipState>();
		if (state == null) return;
		state.TargetVisible = false;
		state.EquipmentEntity = null;
	}

	private bool IsSnapshotScene()
	{
		return EntityManager.GetEntitiesWithComponent<SceneState>()
			.FirstOrDefault()
			?.GetComponent<SceneState>()
			?.Current == SceneId.Snapshot;
	}

	private Entity EnsureBlocker()
	{
		var blocker = EntityManager.GetEntity(BlockerEntityName);
		if (blocker == null)
		{
			blocker = EntityManager.CreateEntity(BlockerEntityName);
			EntityManager.AddComponent(blocker, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
			EntityManager.AddComponent(blocker, new UIElement());
			EntityManager.AddComponent(blocker, new DontDestroyOnLoad());
			InputContextService.EnsureMember(EntityManager, blocker, ContextId);
		}

		if (blocker.GetComponent<Transform>() is Transform transform) transform.ZOrder = ZOrder;
		if (blocker.GetComponent<UIElement>() is UIElement ui)
		{
			ui.Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
			ui.IsInteractable = true;
			ui.IsHidden = false;
			ui.TooltipType = TooltipType.None;
			ui.LayerType = UILayerType.Overlay;
			ui.ShowHoverHighlight = false;
			ui.SecondaryEventType = UIElementEventType.None;
		}
		return blocker;
	}

	private void SetBlockerActive(bool active)
	{
		var blocker = EntityManager.GetEntity(BlockerEntityName);
		if (blocker?.GetComponent<UIElement>() is UIElement ui)
		{
			ui.IsHidden = !active;
			ui.IsInteractable = active;
			if (!active)
			{
				ui.EventType = UIElementEventType.None;
				ui.SecondaryEventType = UIElementEventType.None;
				ui.Bounds = Rectangle.Empty;
			}
			else
			{
				ui.Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
			}
		}

		if (GetOverlay()?.GetComponent<InputContext>() is InputContext context)
		{
			context.IsActive = active;
		}
	}

	private List<BoosterPackLootPreview> CreateLootPreviews(BoosterPackOpeningTiming timing, BoosterPackSave pack = null)
	{
		var loot = new List<BoosterPackLootPreview>(3);
		for (int index = 0; index < 3; index++)
		{
			var preview = new BoosterPackLootPreview
			{
				RevealDelaySeconds = index * Math.Max(0f, timing.RevealStagger),
			};
			var reward = pack?.rewards?[index];
			if (reward != null && TryConfigureAuthoritativePreview(preview, reward, index))
			{
				PreparePreviewEntities(preview);
				PreloadLootTexture(preview, index);
				loot.Add(preview);
				continue;
			}
			preview.Kind = (BoosterPackLootKind)_rng.Next(3);

			switch (preview.Kind)
			{
				case BoosterPackLootKind.Card:
					ConfigureCardPreview(preview, index);
					break;
				case BoosterPackLootKind.Medal:
					ConfigureMedalPreview(preview, index);
					break;
				case BoosterPackLootKind.Equipment:
					ConfigureEquipmentPreview(preview, index);
					break;
			}

			PreparePreviewEntities(preview);
			PreloadLootTexture(preview, index);
			loot.Add(preview);
		}
		return loot;
	}

	private bool TryConfigureAuthoritativePreview(BoosterPackLootPreview preview, BoosterPackRewardSave reward, int index)
	{
		if (preview == null || reward == null || string.IsNullOrWhiteSpace(reward.id)) return false;
		if (!Enum.TryParse<BoosterPackLootKind>(reward.kind, true, out var kind)) return false;
		preview.Kind = kind;
		preview.Id = reward.id;
		if (kind == BoosterPackLootKind.Card)
		{
			return ConfigureCardPreviewEntities(preview, index, allowWeapons: false);
		}
		if (kind == BoosterPackLootKind.Medal)
		{
			ConfigureMedalPreview(preview, index);
			return preview.PreviewEntity != null;
		}
		ConfigureEquipmentPreview(preview, index);
		return preview.PreviewEntity != null;
	}

	private void ConfigureCardPreview(BoosterPackLootPreview preview, int index)
	{
		var cards = CardFactory.GetAllCards().Keys.Select(id => id.ToKey()).ToList();
		if (cards.Count == 0) return;
		preview.Id = cards[_rng.Next(cards.Count)];
		ConfigureCardPreviewEntities(preview, index, allowWeapons: true);
	}

	private bool ConfigureCardPreviewEntities(
		BoosterPackLootPreview preview,
		int index,
		bool allowWeapons)
	{
		foreach (var color in new[]
		{
			CardData.CardColor.White,
			CardData.CardColor.Red,
			CardData.CardColor.Black,
		})
		{
			var entity = EntityFactory.CreateCardFromDefinition(
				EntityManager,
				preview.Id,
				color,
				allowWeapons,
				index);
			if (entity == null)
			{
				DestroyPreviewEntities(preview);
				return false;
			}

			if (entity.GetComponent<UIElement>() is UIElement ui)
			{
				ui.EventType = UIElementEventType.None;
				ui.SecondaryEventType = UIElementEventType.None;
				ui.TooltipPosition = TooltipPosition.Above;
				ui.TooltipOffsetPx = 18;
			}
			if (entity.GetComponent<CardTooltip>() is CardTooltip tooltip)
			{
				tooltip.CardColor = color;
			}
			EntityManager.AddComponent(entity, new CardSheen());
			preview.CardPreviewEntities[color] = entity;
		}

		preview.CardColor = CardData.CardColor.White;
		preview.PreviewEntity = preview.CardPreviewEntities[CardData.CardColor.White];
		return true;
	}

	private void ConfigureMedalPreview(BoosterPackLootPreview preview, int index)
	{
		var medalIds = MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()).ToList();
		if (medalIds.Count == 0) return;
		if (string.IsNullOrWhiteSpace(preview.Id)) preview.Id = medalIds[_rng.Next(medalIds.Count)];
		var medal = MedalFactory.Create(preview.Id);
		if (medal == null) return;

		var entity = EntityManager.CreateEntity($"BoosterPackMedalPreview_{index}");
		EntityManager.AddComponent(entity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 10 + index });
		EntityManager.AddComponent(entity, new UIElement
		{
			Bounds = Rectangle.Empty,
			IsInteractable = false,
			IsHidden = true,
			Tooltip = $"{medal.Name}\n\n{medal.Text}",
			TooltipType = TooltipType.Text,
			TooltipPosition = TooltipPosition.Below,
			TooltipOffsetPx = 18,
			EventType = UIElementEventType.None,
			SecondaryEventType = UIElementEventType.None,
			LayerType = UILayerType.Overlay,
			ShowHoverHighlight = false,
		});
		EntityManager.AddComponent(entity, new EquippedMedal { Medal = medal });
		EntityManager.AddComponent(entity, new DontDestroyOnLoad());
		EntityManager.AddComponent(entity, ParallaxLayer.GetUIParallaxLayer());
		InputContextService.EnsureMember(EntityManager, entity, ContextId);
		medal.Initialize(EntityManager, entity);
		preview.PreviewEntity = entity;
	}

	private void ConfigureEquipmentPreview(BoosterPackLootPreview preview, int index)
	{
		var equipmentIds = EquipmentFactory.GetAllEquipment().Keys.Select(id => id.ToKey()).ToList();
		if (equipmentIds.Count == 0) return;
		if (string.IsNullOrWhiteSpace(preview.Id)) preview.Id = equipmentIds[_rng.Next(equipmentIds.Count)];
		var equipment = EquipmentFactory.Create(preview.Id);
		if (equipment == null) return;

		var entity = EntityManager.CreateEntity($"BoosterPackEquipmentPreview_{index}");
		EntityManager.AddComponent(entity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 10 + index });
		EntityManager.AddComponent(entity, new UIElement
		{
			Bounds = Rectangle.Empty,
			IsInteractable = false,
			IsHidden = true,
			Tooltip = string.Empty,
			TooltipType = TooltipType.Equipment,
			EventType = UIElementEventType.None,
			SecondaryEventType = UIElementEventType.None,
			LayerType = UILayerType.Overlay,
			ShowHoverHighlight = false,
		});
		EntityManager.AddComponent(entity, new EquippedEquipment { Equipment = equipment });
		EntityManager.AddComponent(entity, new EquipmentZone { Zone = EquipmentZoneType.Default });
		EntityManager.AddComponent(entity, new DontDestroyOnLoad());
		EntityManager.AddComponent(entity, ParallaxLayer.GetUIParallaxLayer());
		InputContextService.EnsureMember(EntityManager, entity, ContextId);
		equipment.Initialize(EntityManager, entity);
		preview.PreviewEntity = entity;
	}

	private void PreparePreviewEntities(BoosterPackLootPreview preview)
	{
		foreach (var entity in GetPreviewEntities(preview))
		{
			PreparePreviewEntity(entity);
		}
	}

	private void PreparePreviewEntity(Entity entity)
	{
		if (entity == null) return;
		if (entity.GetComponent<Transform>() is Transform transform)
		{
			transform.Position = new Vector2(-5000f, -5000f);
			transform.Scale = Vector2.One;
			transform.Rotation = 0f;
			transform.ZOrder = ZOrder + 12;
		}
		if (entity.GetComponent<UIElement>() is UIElement ui)
		{
			ui.Bounds = Rectangle.Empty;
			ui.IsInteractable = false;
			ui.IsHidden = true;
			ui.EventType = UIElementEventType.None;
			ui.SecondaryEventType = UIElementEventType.None;
			ui.LayerType = UILayerType.Overlay;
			ui.ShowHoverHighlight = false;
		}
		InputContextService.EnsureMember(EntityManager, entity, ContextId);
	}

	private void PreloadLootTexture(BoosterPackLootPreview preview, int index)
	{
		var equipment = preview.PreviewEntity?.GetComponent<EquippedEquipment>()?.Equipment;
		Texture2D texture = preview.Kind switch
		{
			BoosterPackLootKind.Medal => LoadAssetTexture("Medals/" + preview.Id),
			BoosterPackLootKind.Equipment => EquipmentArtService.GetTexture(_imageAssets, equipment),
			_ => null,
		};
		if (texture != null) _lootTextures[index] = texture;
	}

	private void DestroyPreviewEntities(BoosterPackOpeningOverlayState state)
	{
		if (state?.Loot == null) return;
		foreach (var preview in state.Loot)
		{
			DestroyPreviewEntities(preview);
		}
	}

	private void DestroyPreviewEntities(BoosterPackLootPreview preview)
	{
		foreach (var entity in GetPreviewEntities(preview).ToList())
		{
			if (entity.GetComponent<CardSheen>() is CardSheen sheen) sheen.IsActive = false;
			EntityManager.DestroyEntity(entity.Id);
		}
		preview?.CardPreviewEntities?.Clear();
		if (preview != null) preview.PreviewEntity = null;
	}

	private static IEnumerable<Entity> GetPreviewEntities(BoosterPackLootPreview preview)
	{
		if (preview?.CardPreviewEntities?.Count > 0)
		{
			foreach (var entity in preview.CardPreviewEntities.Values)
			{
				if (entity != null) yield return entity;
			}
			yield break;
		}

		if (preview?.PreviewEntity != null) yield return preview.PreviewEntity;
	}

	private void ProcessMilestones(
		BoosterPackOpeningOverlayState state,
		BoosterPackOpeningTiming timing)
	{
		foreach (var milestone in BoosterPackOpeningAnimationService.GetCrossedMilestones(
			state.PreviousElapsedSeconds,
			state.ElapsedSeconds,
			timing))
		{
			ProcessMilestone(state, milestone, timing);
		}
	}

	private void ProcessMilestone(
		BoosterPackOpeningOverlayState state,
		BoosterPackOpeningMilestone milestone,
		BoosterPackOpeningTiming timing)
	{
		switch (milestone.Kind)
		{
			case BoosterPackOpeningMilestoneKind.ChargeStarted:
				SpawnParticles(state, milestone.Seconds, ChargeParticleCount, ParticleGroup.Charge);
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.BoosterPackCharge, Volume = 0.5f });
				break;
			case BoosterPackOpeningMilestoneKind.ChargePulse:
				SpawnParticles(state, milestone.Seconds, ChargeRepeatParticleCount, ParticleGroup.Charge);
				state.NextChargeParticleSeconds = milestone.Seconds + Math.Max(0.001f, timing.ChargeParticleInterval);
				break;
			case BoosterPackOpeningMilestoneKind.CrackStarted:
				SpawnParticles(state, milestone.Seconds, CrackParticleCount, ParticleGroup.Crack);
				break;
			case BoosterPackOpeningMilestoneKind.RuptureStarted:
				SpawnParticles(state, milestone.Seconds, BurstParticleCount, ParticleGroup.Rupture);
				SpawnShards(state, milestone.Seconds, ShardCount);
				EventManager.Publish(new ShockwaveEvent
				{
					CenterPx = new Vector2(StageCenterX, 500f),
					DurationSec = ShockwaveDurationSec,
					MaxRadiusPx = ShockwaveMaxRadiusPx,
					RippleWidthPx = ShockwaveRippleWidthPx,
					Strength = ShockwaveStrength,
					ChromaticAberrationAmp = ShockwaveChromaticAberrationAmp,
					ChromaticAberrationFreq = ShockwaveChromaticAberrationFreq,
					ShadingIntensity = ShockwaveShadingIntensity,
				});
				break;
			case BoosterPackOpeningMilestoneKind.ShowcaseStarted:
				SpawnParticles(state, milestone.Seconds, ShowcaseParticleCount, ParticleGroup.Showcase);
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.BoosterPackReveal, Volume = 0.5f });
				break;
			case BoosterPackOpeningMilestoneKind.ReadyStarted:
				state.CanDismiss = true;
				break;
		}
	}

	private void SpawnParticles(
		BoosterPackOpeningOverlayState state,
		float startSeconds,
		int count,
		ParticleGroup group)
	{
		bool inward = group == ParticleGroup.Charge;
		Vector2 core = new(StageCenterX, group == ParticleGroup.Charge ? 500f : 475f);
		for (int index = 0; index < count; index++)
		{
			float angle = NextFloat() * MathHelper.TwoPi;
			(float minDistance, float maxDistance, float minDuration, float maxDuration) = group switch
			{
				ParticleGroup.Charge => (180f, 420f, 0.65f, 0.95f),
				ParticleGroup.Crack => (90f, 260f, 0.32f, 0.58f),
				ParticleGroup.Rupture => (220f, 700f, 0.72f, 1.35f),
				_ => (140f, 480f, 0.65f, 1.15f),
			};
			float distance = MathHelper.Lerp(minDistance, maxDistance, NextFloat());
			Vector2 radial = new((float)Math.Cos(angle) * distance, (float)Math.Sin(angle) * distance);
			float groupLengthScale = group == ParticleGroup.Crack ? 0.55f : group == ParticleGroup.Rupture ? 1.35f : 1f;
			state.Particles.Add(new BoosterPackParticleFx
			{
				Start = inward ? core + radial : core,
				Delta = inward ? -radial : radial,
				Width = StreakWidth * MathHelper.Lerp(0.65f, 1.4f, NextFloat()),
				Length = StreakLength * groupLengthScale * MathHelper.Lerp(0.72f, 1.28f, NextFloat()),
				Color = PickParticleColor(),
				StartSeconds = startSeconds,
				DurationSeconds = MathHelper.Lerp(minDuration, maxDuration, NextFloat()),
				IsInward = inward,
			});
		}
	}

	private void SpawnShards(BoosterPackOpeningOverlayState state, float startSeconds, int count)
	{
		for (int index = 0; index < count; index++)
		{
			float angle = -MathHelper.Pi * 0.92f
				+ MathHelper.Pi * 1.84f * index / Math.Max(1, count - 1)
				+ (NextFloat() - 0.5f) * 0.28f;
			float distance = MathHelper.Lerp(240f, 760f, NextFloat());
			state.Shards.Add(new BoosterPackShardFx
			{
				Start = new Vector2(StageCenterX, 476f),
				Delta = new Vector2(
					(float)Math.Cos(angle) * distance,
					(float)Math.Sin(angle) * distance + NextFloat() * 110f),
				Width = MathHelper.Lerp(8f, 22f, NextFloat()),
				Height = MathHelper.Lerp(20f, 62f, NextFloat()),
				RotationRadians = MathHelper.ToRadians(
					(NextFloat() > 0.5f ? 1f : -1f) * MathHelper.Lerp(220f, 740f, NextFloat())),
				StartSeconds = startSeconds,
				DurationSeconds = MathHelper.Lerp(0.76f, 1.22f, NextFloat()),
			});
		}
	}

	private static void UpdatePresentationParticles(BoosterPackOpeningOverlayState state)
	{
		float now = state.ElapsedSeconds;
		state.Particles.RemoveAll(p => now > p.StartSeconds + p.DurationSeconds + 0.05f);
		state.Shards.RemoveAll(s => now > s.StartSeconds + s.DurationSeconds + 0.05f);
	}

	private void UpdateCardSheens(
		BoosterPackOpeningOverlayState state,
		BoosterPackOpeningTiming timing)
	{
		for (int index = 0; index < state.Loot.Count; index++)
		{
			bool isActive = BoosterPackOpeningAnimationService.HasSheenStarted(
				state.ElapsedSeconds,
				index,
				timing);
			foreach (var entity in GetPreviewEntities(state.Loot[index]))
			{
				if (entity.GetComponent<CardSheen>() is CardSheen sheen) sheen.IsActive = isActive;
			}
		}
	}

	private void UpdateRewardInteractions(
		BoosterPackOpeningOverlayState state,
		BoosterPackOpeningTiming timing)
	{
		var centers = ComputeLootCenters();
		Vector2 ruptureCenter = new(StageCenterX, 500f);
		for (int index = 0; index < state.Loot.Count; index++)
		{
			var preview = state.Loot[index];
			var entity = preview.PreviewEntity;
			var ui = entity?.GetComponent<UIElement>();
			if (ui == null) continue;

			var sample = GetDisplayedLootSample(
				state.ElapsedSeconds,
				index,
				ruptureCenter,
				centers[index],
				timing);
			bool settled = sample.IsSettled;
			ConfigurePreviewInteraction(
				ui,
				settled,
				state.CanDismiss,
				settled ? GetItemHitbox(preview, sample.Position) : Rectangle.Empty);
			if (entity.GetComponent<Transform>() is Transform transform)
			{
				transform.ZOrder = ZOrder + 20 + index;
			}
		}
	}

	internal static void ConfigurePreviewInteraction(
		UIElement ui,
		bool settled,
		bool canDismiss,
		Rectangle settledBounds)
	{
		if (ui == null) return;
		ui.IsHidden = !settled;
		ui.IsInteractable = settled;
		ui.Bounds = settled ? settledBounds : Rectangle.Empty;
		ui.EventType = settled && canDismiss
			? UIElementEventType.BoosterPackOpeningClose
			: UIElementEventType.None;
		ui.SecondaryEventType = UIElementEventType.None;
	}

	private void SetDismissEnabled(BoosterPackOpeningOverlayState state, bool enabled)
	{
		state.CanDismiss = enabled;
		if (EntityManager.GetEntity(BlockerEntityName)?.GetComponent<UIElement>() is UIElement blockerUi)
		{
			blockerUi.EventType = enabled
				? UIElementEventType.BoosterPackOpeningClose
				: UIElementEventType.None;
			blockerUi.SecondaryEventType = UIElementEventType.None;
		}

		foreach (var preview in state.Loot)
		{
			if (preview.PreviewEntity?.GetComponent<UIElement>() is not UIElement ui) continue;
			ui.EventType = enabled && ui.IsInteractable
				? UIElementEventType.BoosterPackOpeningClose
				: UIElementEventType.None;
			ui.SecondaryEventType = UIElementEventType.None;
		}
	}

	private static void SetPreviewEntitiesInactive(BoosterPackOpeningOverlayState state)
	{
		if (state?.Loot == null) return;
		foreach (var preview in state.Loot)
		{
			foreach (var entity in GetPreviewEntities(preview))
			{
				if (entity.GetComponent<UIElement>() is UIElement ui)
				{
					ui.Bounds = Rectangle.Empty;
					ui.IsHidden = true;
					ui.IsInteractable = false;
					ui.EventType = UIElementEventType.None;
					ui.SecondaryEventType = UIElementEventType.None;
				}
				if (entity.GetComponent<CardSheen>() is CardSheen sheen) sheen.IsActive = false;
			}
		}
	}

	private Rectangle GetItemHitbox(BoosterPackLootPreview preview, Vector2 center)
	{
		return preview.Kind switch
		{
			BoosterPackLootKind.Card => CardGeometryService.GetVisualRect(
				EntityManager,
				GetCardRenderPosition(center, CardScale),
				CardScale),
			BoosterPackLootKind.Medal => CenteredRect(center, MedalSize + 40, MedalSize + 40),
			_ => CenteredRect(
				center,
				(int)(EquipmentIconBox * EquipmentIconScale),
				(int)(EquipmentIconBox * EquipmentIconScale)),
		};
	}

	private void EnsureRenderResources()
	{
		_booster1 ??= LoadAssetTexture("Booster_Pack/booster_1");
		_booster2 ??= LoadAssetTexture("Booster_Pack/booster_2");
		_booster3 ??= LoadAssetTexture("Booster_Pack/booster_3");
		_boosterLeft ??= LoadAssetTexture("Booster_Pack/booster_4_left");
		_boosterRight ??= LoadAssetTexture("Booster_Pack/booster_4_right");
		_softRadialMask ??= PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, FixedMaskDiameter, 0.05f, 1f);
		_vignetteMask ??= PrimitiveTextureFactory.GetInvertedSoftRadialCircle(_graphicsDevice, FixedMaskDiameter, 0.50f, 1f);
		_rayburstMask ??= PrimitiveTextureFactory.GetAntialiasedRadialBurstMask(
			_graphicsDevice,
			FixedMaskDiameter,
			24,
			0.08f,
			0.98f,
			0.46f);
		_particleCoreMask ??= PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, ParticleCoreDiameter / 2);
		_shardMask ??= PrimitiveTextureFactory.GetAntialiasedPolygonMask(
			_graphicsDevice,
			ShardMaskWidth,
			ShardMaskHeight,
			"booster-shard-normalized",
			new[] { new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.76f) });
	}

	private bool HasRenderResources()
	{
		return _softRadialMask != null
			&& _vignetteMask != null
			&& _rayburstMask != null
			&& _particleCoreMask != null
			&& _shardMask != null;
	}

	private Texture2D LoadAssetTexture(string assetName)
	{
		if (string.IsNullOrWhiteSpace(assetName)) return null;
		if (_assetTextures.TryGetValue(assetName, out var cached)) return cached;
		var texture = _imageAssets.TryGetTexture(assetName);
		_assetTextures[assetName] = texture;
		return texture;
	}

	private void ClearRenderResources()
	{
		_assetTextures.Clear();
		_lootTextures.Clear();
		_booster1 = null;
		_booster2 = null;
		_booster3 = null;
		_boosterLeft = null;
		_boosterRight = null;
		_softRadialMask = null;
		_vignetteMask = null;
		_rayburstMask = null;
		_particleCoreMask = null;
		_shardMask = null;
	}

	private void DrawSceneWash(BoosterPackOpeningOverlayState state)
	{
		var timing = BuildTiming();
		float blackout = state.Phase switch
		{
			BoosterPackOpeningPhase.Summon or BoosterPackOpeningPhase.Idle => BaseBlackoutAlpha,
			BoosterPackOpeningPhase.Charge => MathHelper.Lerp(
				BaseBlackoutAlpha,
				ChargeBlackoutAlpha,
				BoosterPackOpeningAnimationService.GetPhaseProgress(state.ElapsedSeconds, state.Phase, timing)),
			BoosterPackOpeningPhase.Crack => ChargeBlackoutAlpha,
			BoosterPackOpeningPhase.Rupture => MathHelper.Lerp(ChargeBlackoutAlpha, ShowcaseBlackoutAlpha, 0.35f),
			_ => ShowcaseBlackoutAlpha,
		};
		_spriteBatch.Draw(
			_pixel,
			new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
			Color.Black * MathHelper.Clamp(blackout, 0f, 1f));

		float bloomAlpha = state.Phase >= BoosterPackOpeningPhase.Showcase ? 0.34f : 0.12f;
		DrawSoftMask(new Rectangle(518, 115, 884, 734), new Color(92, 42, 24) * bloomAlpha);
	}

	private void DrawStageLighting(BoosterPackOpeningOverlayState state)
	{
		float elapsed = state.ElapsedSeconds;
		float breathe = 0.5f + 0.5f * (float)Math.Sin(elapsed * MathHelper.TwoPi / 2.8f);
		var floor = new Rectangle(
			(int)(StageCenterX - FloorGlowWidth / 2f),
			FloorGlowY,
			FloorGlowWidth,
			FloorGlowHeight);
		DrawSoftMask(floor, Blood * MathHelper.Lerp(0.20f, 0.34f, breathe));
		DrawSoftMask(Inflate(floor, -100, -20), Gold * MathHelper.Lerp(0.10f, 0.18f, breathe));

		float stageRayAlpha = state.Phase switch
		{
			BoosterPackOpeningPhase.Rupture => 0.12f,
			BoosterPackOpeningPhase.Showcase or BoosterPackOpeningPhase.Ready => 0.08f,
			_ => 0.25f,
		};
		_spriteBatch.Draw(
			_rayburstMask,
			CenteredRect(new Vector2(StageCenterX, StageCenterY + 44f), StageRayWidth, StageRayHeight),
			Gold * stageRayAlpha);
	}

	private void DrawPack(BoosterPackOpeningOverlayState state)
	{
		var timing = BuildTiming();
		float elapsed = state.ElapsedSeconds;
		if (elapsed >= timing.ShowcaseStart) return;

		Vector2 stageShake = BoosterPackOpeningAnimationService.SampleRuptureShake(
			elapsed,
			timing,
			RuptureShakeAmplitudePx);
		Vector2 center = new(StageCenterX, PackCenterY);
		Vector2 offset = Vector2.Zero;
		float scale = 1f;
		float rotation = 0f;
		float alpha = 1f;
		Texture2D pack = state.Phase >= BoosterPackOpeningPhase.Crack
			? _booster3
			: state.Phase >= BoosterPackOpeningPhase.Charge
				? _booster2
				: _booster1;

		switch (state.Phase)
		{
			case BoosterPackOpeningPhase.Summon:
			{
				float progress = BoosterPackOpeningAnimationService.GetPhaseProgress(elapsed, state.Phase, timing);
				if (progress < 0.72f)
				{
					float key = EaseOutCubic(progress / 0.72f);
					offset.Y = MathHelper.Lerp(PackSummonOffsetY, PackSummonOvershootY, key);
					scale = MathHelper.Lerp(PackSummonStartScale, PackSummonOvershootScale, key);
				}
				else
				{
					float key = EaseOutCubic((progress - 0.72f) / 0.28f);
					offset.Y = MathHelper.Lerp(PackSummonOvershootY, 0f, key);
					scale = MathHelper.Lerp(PackSummonOvershootScale, 1f, key);
				}
				alpha = MathHelper.Clamp(progress / 0.18f, 0f, 1f);
				break;
			}
			case BoosterPackOpeningPhase.Idle:
			{
				float phase = BoosterPackOpeningAnimationService.GetPhaseProgress(elapsed, state.Phase, timing);
				float wave = (float)Math.Sin(phase * MathHelper.TwoPi);
				offset.Y = wave * PackIdleFloatPx * 0.5f;
				rotation = MathHelper.ToRadians(MathHelper.Lerp(-0.5f, 0.7f, (wave + 1f) * 0.5f));
				break;
			}
			case BoosterPackOpeningPhase.Charge:
			{
				float local = elapsed - timing.ChargeStart;
				float wave = (float)Math.Sin(local * MathHelper.TwoPi / 0.13f);
				offset = new Vector2(wave * PackChargeShakePx, -3f + wave * 2f);
				rotation = MathHelper.ToRadians(wave * 1.1f);
				scale = MathHelper.Lerp(1.035f, 1.055f, (wave + 1f) * 0.5f);
				break;
			}
			case BoosterPackOpeningPhase.Crack:
			{
				float local = elapsed - timing.CrackStart;
				float wave = (float)Math.Sin(local * MathHelper.TwoPi / 0.08f);
				offset = new Vector2(wave * PackCrackShakePx, wave * 2f);
				rotation = MathHelper.ToRadians(wave * 1.3f);
				scale = MathHelper.Lerp(1.06f, 1.08f, (wave + 1f) * 0.5f);
				break;
			}
		}

		Vector2 drawCenter = center + offset + stageShake;
		float auraAlpha = GetAuraAlpha(state, timing);
		float auraScale = GetAuraScale(state, timing);
		DrawSoftMask(
			CenteredRect(drawCenter, (int)(PackAuraSize * auraScale), (int)(PackAuraSize * auraScale)),
			Gold * auraAlpha);

		if (state.Phase < BoosterPackOpeningPhase.Rupture)
		{
			DrawTextureCentered(
				pack,
				drawCenter + new Vector2(12f, 24f),
				PackWidth,
				PackHeight,
				Color.Black * (0.32f * alpha),
				rotation,
				scale * 1.02f);
			DrawTextureCentered(pack, drawCenter, PackWidth, PackHeight, Color.White * alpha, rotation, scale);
			if (state.Phase == BoosterPackOpeningPhase.Crack)
			{
				DrawCracks(drawCenter, rotation, scale, 1f);
			}
			return;
		}

		float peel = EaseOutCubic(MathHelper.Clamp((elapsed - timing.RuptureStart) / 0.62f, 0f, 1f));
		DrawSoftMask(
			CenteredRect(new Vector2(StageCenterX, 535f) + stageShake, 220, 520),
			Gold * MathHelper.Lerp(0.86f, 0.18f, peel));
		DrawPackHalf(_boosterLeft, true, peel, stageShake);
		DrawPackHalf(_boosterRight, false, peel, stageShake);
	}

	private void DrawPackHalf(Texture2D texture, bool left, float peel, Vector2 stageShake)
	{
		if (texture == null) return;
		float width = left ? PackWidth * 379f / 786f : PackWidth * 376f / 786f;
		float baseX = left ? StageCenterX - width / 2f : StageCenterX + width / 2f;
		float x = MathHelper.Lerp(0f, left ? -680f : 680f, peel);
		float y = MathHelper.Lerp(0f, 128f, peel);
		float rotation = MathHelper.ToRadians(MathHelper.Lerp(0f, left ? -38f : 38f, peel));
		float alpha = 1f - EaseInQuad(MathHelper.Clamp((peel - 0.45f) / 0.55f, 0f, 1f));
		var destination = new Rectangle(
			(int)Math.Round(baseX + x + stageShake.X),
			(int)Math.Round(PackCenterY + y + stageShake.Y),
			(int)Math.Round(width),
			PackHeight);
		var origin = left
			? new Vector2(texture.Width, texture.Height / 2f)
			: new Vector2(0f, texture.Height / 2f);
		_spriteBatch.Draw(texture, destination, null, Color.White * alpha, rotation, origin, SpriteEffects.None, 0f);
	}

	private void DrawCracks(Vector2 center, float rotation, float scale, float alpha)
	{
		float pulse = 0.72f + 0.28f * (0.5f + 0.5f * (float)Math.Sin(
			GetOverlay().GetComponent<BoosterPackOpeningOverlayState>().ElapsedSeconds * MathHelper.TwoPi / 0.55f));
		var branches = new[]
		{
			(new Vector2(0f, -185f), 370f, 1f),
			(new Vector2(-34f, -95f), 118f, -42f),
			(new Vector2(30f, -70f), 134f, 41f),
			(new Vector2(-22f, 56f), 110f, 52f),
			(new Vector2(28f, 78f), 126f, -38f),
		};
		foreach (var (localStart, length, angleDegrees) in branches)
		{
			Vector2 start = center + Rotate(localStart * scale, rotation);
			float angle = rotation + MathHelper.ToRadians(angleDegrees);
			Vector2 end = start + Rotate(new Vector2(0f, length * scale), angle);
			DrawSegment(start, end, 16f * scale, Blood * (0.45f * pulse * alpha));
			DrawSegment(start, end, 8f * scale, Gold * (0.78f * pulse * alpha));
			DrawSegment(start, end, 3f * scale, GoldHot * (pulse * alpha));
		}
	}

	private void DrawRuptureFx(BoosterPackOpeningOverlayState state)
	{
		var timing = BuildTiming();
		float local = state.ElapsedSeconds - timing.RuptureStart;
		if (local <= 0f || state.ElapsedSeconds >= timing.ShowcaseStart) return;
		Vector2 shake = BoosterPackOpeningAnimationService.SampleRuptureShake(
			state.ElapsedSeconds,
			timing,
			RuptureShakeAmplitudePx);

		float rayProgress = MathHelper.Clamp(local / Math.Max(0.001f, timing.RuptureDuration), 0f, 1f);
		float rayScale = MathHelper.Lerp(0.20f, 1.08f, EaseOutCubic(rayProgress));
		float rayAlpha = (1f - rayProgress) * BeamAlpha;
		int raySize = (int)Math.Round(980f * rayScale);
		_spriteBatch.Draw(
			_rayburstMask,
			CenteredRect(new Vector2(StageCenterX, 464f) + shake, raySize, raySize),
			GoldHot * rayAlpha);

		float flashProgress = MathHelper.Clamp(local / 0.34f, 0f, 1f);
		if (flashProgress < 1f)
		{
			float flashEnvelope = flashProgress < 0.18f
				? flashProgress / 0.18f
				: 1f - (flashProgress - 0.18f) / 0.82f;
			_spriteBatch.Draw(
				_pixel,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				GoldHot * (FlashAlpha * 0.22f * MathHelper.Clamp(flashEnvelope, 0f, 1f)));
			DrawSoftMask(
				CenteredRect(new Vector2(StageCenterX, 500f) + shake, 1180, 1080),
				GoldHot * (FlashAlpha * MathHelper.Clamp(flashEnvelope, 0f, 1f)));
		}

		float flareProgress = MathHelper.Clamp(local / 0.56f, 0f, 1f);
		if (flareProgress > 0f && flareProgress < 1f)
		{
			float flareAlpha = flareProgress < 0.16f
				? flareProgress / 0.16f
				: 1f - (flareProgress - 0.16f) / 0.84f;
			float width = MathHelper.Lerp(9.6f, 192f, flareProgress);
			Vector2 start = new(StageCenterX + shake.X, 150f + shake.Y);
			Vector2 end = new(StageCenterX + shake.X, 910f + shake.Y);
			DrawSegment(start, end, width, GoldHot * flareAlpha);
			DrawSegment(start, end, width * 0.38f, BloodHot * (flareAlpha * 0.58f));
		}
	}

	private void DrawParticles(BoosterPackOpeningOverlayState state)
	{
		foreach (var particle in state.Particles)
		{
			float progress = MathHelper.Clamp(
				(state.ElapsedSeconds - particle.StartSeconds) / Math.Max(0.001f, particle.DurationSeconds),
				0f,
				1f);
			if (progress <= 0f || progress >= 1f) continue;
			float alpha = progress < 0.08f ? progress / 0.08f : 1f - progress;
			float eased = EaseOutCubic(progress);
			Vector2 leading = particle.Start + particle.Delta * eased;
			Vector2 direction = SafeNormalize(particle.Delta);
			float length = particle.Length * MathHelper.Lerp(0.45f, 1f, 1f - progress);
			Vector2 tail = leading - direction * length;
			DrawSegment(tail, leading, particle.Width * 2.2f, particle.Color * (alpha * 0.24f));
			DrawSegment(tail, leading, particle.Width * 0.72f, GoldHot * (alpha * 0.88f));
			int coreSize = Math.Max(2, (int)Math.Round(particle.Width * 2.4f));
			_spriteBatch.Draw(_particleCoreMask, CenteredRect(leading, coreSize, coreSize), GoldHot * alpha);
		}
	}

	private void DrawShards(BoosterPackOpeningOverlayState state)
	{
		foreach (var shard in state.Shards)
		{
			float progress = MathHelper.Clamp(
				(state.ElapsedSeconds - shard.StartSeconds) / Math.Max(0.001f, shard.DurationSeconds),
				0f,
				1f);
			if (progress <= 0f || progress >= 1f) continue;
			float alpha = progress < 0.10f ? progress / 0.10f : 1f - progress;
			Vector2 position = shard.Start + shard.Delta * EaseOutCubic(progress);
			float scale = MathHelper.Lerp(1f, 0.35f, progress);
			Vector2 outerScale = new(
				shard.Width * scale / _shardMask.Width,
				shard.Height * scale / _shardMask.Height);
			float rotation = shard.RotationRadians * progress;
			_spriteBatch.Draw(
				_shardMask,
				position,
				null,
				new Color(92, 14, 25) * alpha,
				rotation,
				new Vector2(_shardMask.Width / 2f, _shardMask.Height / 2f),
				outerScale,
				SpriteEffects.None,
				0f);
			_spriteBatch.Draw(
				_shardMask,
				position,
				null,
				GoldHot * (alpha * 0.82f),
				rotation,
				new Vector2(_shardMask.Width / 2f, _shardMask.Height / 2f),
				outerScale * ShardHighlightScale,
				SpriteEffects.None,
				0f);
		}
	}

	private void DrawLoot(BoosterPackOpeningOverlayState state)
	{
		var timing = BuildTiming();
		if (state.ElapsedSeconds < timing.ShowcaseStart) return;
		Vector2 ruptureCenter = new(StageCenterX, 500f);
		var centers = ComputeLootCenters();
		for (int index = 0; index < state.Loot.Count; index++)
		{
			var sample = GetDisplayedLootSample(
				state.ElapsedSeconds,
				index,
				ruptureCenter,
				centers[index],
				timing);
			if (sample.Progress <= 0f) continue;
			DrawLootPlate(sample.Position, sample, state.ElapsedSeconds, index);
			DrawLootItem(state.Loot[index], index, sample);
		}
	}

	private void DrawLootPlate(
		Vector2 center,
		BoosterPackLootAnimationSample sample,
		float elapsedSeconds,
		int slotIndex)
	{
		float pulse = sample.IsSettled
			? 0.5f + 0.5f * (float)Math.Sin(
				elapsedSeconds * MathHelper.TwoPi / Math.Max(0.1f, RewardIdlePeriodSeconds) + slotIndex * 0.8f)
			: sample.Progress;
		float alpha = sample.IsSettled
			? MathHelper.Lerp(RewardRayMinAlpha, RewardRayMaxAlpha, pulse)
			: RewardRayMaxAlpha * sample.Alpha;
		float rayScale = sample.IsSettled ? 1f : MathHelper.Lerp(0.34f, 1f, EaseOutCubic(sample.Progress));
		int size = (int)Math.Round(RewardRaySize * rayScale);
		_spriteBatch.Draw(_rayburstMask, CenteredRect(center, size, size), Gold * alpha);
		DrawSoftMask(
			CenteredRect(center, (int)(RewardRaySize * 0.88f), (int)(RewardRaySize * 0.88f)),
			GoldHot * (0.18f * sample.Alpha));
	}

	private void DrawLootItem(
		BoosterPackLootPreview preview,
		int slotIndex,
		BoosterPackLootAnimationSample sample)
	{
		switch (preview.Kind)
		{
			case BoosterPackLootKind.Card:
				DrawCardPreview(preview, sample);
				break;
			case BoosterPackLootKind.Medal:
				DrawMedalPreview(slotIndex, sample);
				break;
			case BoosterPackLootKind.Equipment:
				DrawEquipmentPreview(slotIndex, sample);
				break;
		}
	}

	private void DrawCardPreview(
		BoosterPackLootPreview preview,
		BoosterPackLootAnimationSample sample)
	{
		float renderScale = CardScale * sample.Scale;
		var placements = ComputeCardFanPlacements(
			sample.Position,
			sample.Scale,
			sample.Rotation,
			CardFanHorizontalGap,
			CardFanRearDrop,
			MathHelper.ToRadians(CardFanRotationDegrees));
		Vector2 position = GetCardRenderPosition(placements[^1].Position, renderScale);
		var rect = CardGeometryService.GetVisualRect(EntityManager, position, renderScale);
		DrawSoftMask(Inflate(rect, 44, 44), Blood * (0.28f * sample.Alpha));
		DrawSoftMask(Inflate(rect, 8, 8), GoldHot * (0.16f * sample.Alpha));

		foreach (var placement in placements)
		{
			if (!preview.CardPreviewEntities.TryGetValue(placement.Color, out var entity)) continue;
			EventManager.Publish(new CardRenderScaledEvent
			{
				Card = entity,
				Position = GetCardRenderPosition(placement.Position, renderScale),
				Scale = renderScale,
				Alpha = sample.Alpha,
				Rotation = placement.Rotation,
			});
		}
	}

	internal static CardFanPlacement[] ComputeCardFanPlacements(
		Vector2 center,
		float revealScale,
		float groupRotation,
		float horizontalGap,
		float rearDrop,
		float fanRotation)
	{
		Vector2 redOffset = RotateOffset(
			new Vector2(-horizontalGap, rearDrop) * revealScale,
			groupRotation);
		Vector2 blackOffset = RotateOffset(
			new Vector2(horizontalGap, rearDrop) * revealScale,
			groupRotation);
		return new[]
		{
			new CardFanPlacement(CardData.CardColor.Black, center + blackOffset, groupRotation + fanRotation),
			new CardFanPlacement(CardData.CardColor.Red, center + redOffset, groupRotation - fanRotation),
			new CardFanPlacement(CardData.CardColor.White, center, groupRotation),
		};
	}

	private static Vector2 RotateOffset(Vector2 offset, float rotation)
	{
		float cosine = MathF.Cos(rotation);
		float sine = MathF.Sin(rotation);
		return new Vector2(
			offset.X * cosine - offset.Y * sine,
			offset.X * sine + offset.Y * cosine);
	}

	private void DrawMedalPreview(int slotIndex, BoosterPackLootAnimationSample sample)
	{
		if (!_lootTextures.TryGetValue(slotIndex, out var texture) || texture == null) return;
		int size = (int)Math.Round(MedalSize * sample.Scale);
		DrawSoftMask(CenteredRect(sample.Position, size + 86, size + 86), Gold * (0.24f * sample.Alpha));
		_spriteBatch.Draw(
			texture,
			sample.Position,
			null,
			Color.White * sample.Alpha,
			sample.Rotation,
			new Vector2(texture.Width / 2f, texture.Height / 2f),
			size / (float)Math.Max(texture.Width, texture.Height),
			SpriteEffects.None,
			0f);
	}

	private void DrawEquipmentPreview(int slotIndex, BoosterPackLootAnimationSample sample)
	{
		if (!_lootTextures.TryGetValue(slotIndex, out var texture) || texture == null) return;
		int box = (int)Math.Round(EquipmentIconBox * EquipmentIconScale * sample.Scale);
		DrawSoftMask(CenteredRect(sample.Position, box + 72, box + 72), Blue * (0.20f * sample.Alpha));
		float uniformScale = box / (float)Math.Max(texture.Width, texture.Height);
		_spriteBatch.Draw(
			texture,
			sample.Position,
			null,
			Color.White * sample.Alpha,
			sample.Rotation,
			new Vector2(texture.Width / 2f, texture.Height / 2f),
			uniformScale,
			SpriteEffects.None,
			0f);
	}

	private void DrawRewardTitle(BoosterPackOpeningOverlayState state)
	{
		var timing = BuildTiming();
		if (state.ElapsedSeconds < timing.ShowcaseStart) return;
		float progress = EaseOutBack(MathHelper.Clamp((state.ElapsedSeconds - timing.ShowcaseStart) / 0.68f, 0f, 1f));
		float alpha = MathHelper.Clamp((state.ElapsedSeconds - timing.ShowcaseStart) / 0.32f, 0f, 1f);
		float y = RewardTitleY + MathHelper.Lerp(-18f, 0f, progress);
		DrawCenteredString(
			_bodyFont,
			"PACK OPENED",
			new Vector2(StageCenterX, y),
			GoldHot * (0.72f * alpha),
			RewardKickerScale * MathHelper.Lerp(0.9f, 1f, progress));
		DrawCenteredString(
			_titleFont,
			"HOLY SPOILS",
			new Vector2(StageCenterX, y + 28f),
			new Color(255, 247, 204) * alpha,
			RewardHeadlineScale * MathHelper.Lerp(0.9f, 1f, progress));
	}

	private void DrawVignette()
	{
		_spriteBatch.Draw(
			_vignetteMask,
			new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
			Color.Black * VignetteAlpha);
	}

	private BoosterPackLootAnimationSample GetDisplayedLootSample(
		float elapsedSeconds,
		int slotIndex,
		Vector2 ruptureCenter,
		Vector2 finalCenter,
		BoosterPackOpeningTiming timing)
	{
		var sample = BoosterPackOpeningAnimationService.SampleLoot(
			elapsedSeconds,
			slotIndex,
			ruptureCenter,
			finalCenter,
			timing,
			RewardArcHeight);
		if (!sample.IsSettled) return sample;

		float period = Math.Max(0.1f, RewardIdlePeriodSeconds);
		float phase = elapsedSeconds * MathHelper.TwoPi / period + slotIndex * 0.84f;
		return sample with
		{
			Position = sample.Position + new Vector2(0f, (float)Math.Sin(phase) * RewardIdleFloatPx),
		};
	}

	private Vector2[] ComputeLootCenters()
	{
		float totalWidth = LootSlotWidth * 3f + LootGap * 2f;
		float firstX = StageCenterX - totalWidth / 2f + LootSlotWidth / 2f;
		return new[]
		{
			new Vector2(firstX, LootCenterY),
			new Vector2(firstX + LootSlotWidth + LootGap, LootCenterY),
			new Vector2(firstX + (LootSlotWidth + LootGap) * 2f, LootCenterY),
		};
	}

	private Vector2 GetCardRenderPosition(Vector2 desiredVisualCenter, float renderScale)
	{
		var settings = CardGeometryService.GetSettings(EntityManager);
		int offsetY = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;
		return new Vector2(desiredVisualCenter.X, desiredVisualCenter.Y + offsetY * renderScale);
	}

	private float GetAuraAlpha(
		BoosterPackOpeningOverlayState state,
		BoosterPackOpeningTiming timing)
	{
		if (state.Phase is BoosterPackOpeningPhase.Charge or BoosterPackOpeningPhase.Crack)
		{
			float local = state.ElapsedSeconds - timing.ChargeStart;
			float pulse = 0.5f + 0.5f * (float)Math.Sin(local * MathHelper.TwoPi / 0.5f);
			return MathHelper.Lerp(0.40f, 0.90f, pulse);
		}
		return 0.45f;
	}

	private float GetAuraScale(
		BoosterPackOpeningOverlayState state,
		BoosterPackOpeningTiming timing)
	{
		if (state.Phase is not (BoosterPackOpeningPhase.Charge or BoosterPackOpeningPhase.Crack)) return 1f;
		float local = state.ElapsedSeconds - timing.ChargeStart;
		float pulse = 0.5f + 0.5f * (float)Math.Sin(local * MathHelper.TwoPi / 0.5f);
		return MathHelper.Lerp(0.72f, 1.04f, pulse);
	}

	private void DrawSoftMask(Rectangle destination, Color color)
	{
		if (destination.Width <= 0 || destination.Height <= 0 || _softRadialMask == null) return;
		_spriteBatch.Draw(_softRadialMask, destination, color);
	}

	private void DrawTextureCentered(
		Texture2D texture,
		Vector2 center,
		int width,
		int height,
		Color color,
		float rotation,
		float scale)
	{
		if (texture == null) return;
		var destination = new Rectangle(
			(int)Math.Round(center.X),
			(int)Math.Round(center.Y),
			(int)Math.Round(width * scale),
			(int)Math.Round(height * scale));
		_spriteBatch.Draw(
			texture,
			destination,
			null,
			color,
			rotation,
			new Vector2(texture.Width / 2f, texture.Height / 2f),
			SpriteEffects.None,
			0f);
	}

	private void DrawSegment(Vector2 start, Vector2 end, float thickness, Color color)
	{
		Vector2 delta = end - start;
		float length = delta.Length();
		if (length <= 0.001f || thickness <= 0f) return;
		_spriteBatch.Draw(
			_pixel,
			start,
			null,
			color,
			(float)Math.Atan2(delta.Y, delta.X),
			new Vector2(0f, 0.5f),
			new Vector2(length, thickness),
			SpriteEffects.None,
			0f);
	}

	private void DrawCenteredString(
		SpriteFont font,
		string text,
		Vector2 centerTop,
		Color color,
		float scale)
	{
		if (font == null || string.IsNullOrEmpty(text)) return;
		Vector2 size = font.MeasureString(text) * scale;
		_spriteBatch.DrawString(
			font,
			text,
			new Vector2(centerTop.X - size.X / 2f, centerTop.Y),
			color,
			0f,
			Vector2.Zero,
			scale,
			SpriteEffects.None,
			0f);
	}

	private Color PickParticleColor()
	{
		float roll = NextFloat();
		if (roll < 0.66f) return GoldHot;
		if (roll < 0.85f) return Blue;
		return BloodHot;
	}

	private float NextFloat() => (float)_rng.NextDouble();

	private static Vector2 SafeNormalize(Vector2 value)
	{
		return value.LengthSquared() <= 0.0001f ? Vector2.UnitY : Vector2.Normalize(value);
	}

	private static Vector2 Rotate(Vector2 value, float rotation)
	{
		float sine = (float)Math.Sin(rotation);
		float cosine = (float)Math.Cos(rotation);
		return new Vector2(
			value.X * cosine - value.Y * sine,
			value.X * sine + value.Y * cosine);
	}

	private static Rectangle CenteredRect(Vector2 center, int width, int height)
	{
		return new Rectangle(
			(int)Math.Round(center.X - width / 2f),
			(int)Math.Round(center.Y - height / 2f),
			width,
			height);
	}

	private static Rectangle Inflate(Rectangle rect, int x, int y)
	{
		rect.Inflate(x, y);
		return rect;
	}

	private static float EaseOutCubic(float value)
	{
		float inverse = 1f - MathHelper.Clamp(value, 0f, 1f);
		return 1f - inverse * inverse * inverse;
	}

	private static float EaseInQuad(float value)
	{
		value = MathHelper.Clamp(value, 0f, 1f);
		return value * value;
	}

	private static float EaseOutBack(float value)
	{
		value = MathHelper.Clamp(value, 0f, 1f);
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		return 1f + c3 * (float)Math.Pow(value - 1f, 3f) + c1 * (float)Math.Pow(value - 1f, 2f);
	}

	private static readonly Color Blood = new(197, 31, 51);
	private static readonly Color BloodHot = new(255, 64, 86);
	private static readonly Color Gold = new(233, 199, 85);
	private static readonly Color GoldHot = new(255, 240, 164);
	private static readonly Color Blue = new(101, 209, 255);

	private enum ParticleGroup
	{
		Charge,
		Crack,
		Rupture,
		Showcase,
	}
}
