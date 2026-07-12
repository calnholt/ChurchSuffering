using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.VisualEffects;
using System.Collections.Generic;
using System;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Input;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Skeleton banner that displays current attack name, base damage (sum of on-hit Damage effects),
	/// and a simple list of leaf blocking conditions. Shown when there is a current planned attack.
	/// </summary>
	[DebugTab("Enemy Attack Display")]
	public partial class EnemyAttackDisplaySystem : Core.System
	{
		// Graphics & rendering
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _contentFont = FontSingleton.ContentFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;
		private readonly Texture2D _enemyAttackCornerBlTexture;
		private readonly Texture2D _enemyAttackCornerBrTexture;
		private readonly Texture2D _enemyAttackTopTexture;
		private readonly Texture2D _enemyAttackSkullTexture;
		private readonly Texture2D _panelAuraTexture;

		// Confirm button texture cache
		private Texture2D _cachedConfirmTexture;
		private string _cachedConfirmText;

		// Tooltip state
		private Entity _attackTextTooltipEntity = null;
		private Rectangle _bannerRect = Rectangle.Empty;

		// Animation state
		private int _lastAttackSequence = -1;
		// Impact animation flow (anticipation -> impact -> settle)
		private bool _impactActive = false;
		private bool _impactMomentFired = false;
		private float _impactElapsedSeconds = 0f;
		private float _impactIntensity = 0.25f;
		private float _idleElapsedSeconds = 0f;
		private float _outlineEchoElapsedSeconds = 0f;
		private int _impactDamage = 0;
		private SubPhase _lastPresentationPhase = SubPhase.StartBattle;
		private EnemyAttackEntranceSample _entranceSample;

		// Prevent repeated confirm presses for the same attack context
		private readonly HashSet<int> _confirmedAttackSequences = [];
		private int _pendingConfirmSequence = -1;
		private bool _showBanner = false;

		// Absorb tween (panel -> enemy)
		[DebugEditable(DisplayName = "Absorb Duration (s)", Step = 0.02f, Min = 0.05f, Max = 3f)]
		public float AbsorbDurationSeconds { get; set; } = 0.4f;
		[DebugEditable(DisplayName = "Absorb Target Y Offset", Step = 2, Min = -400, Max = 400)]
		public int AbsorbTargetYOffset { get; set; } = -40;
		private float _absorbElapsedSeconds = 0f;
		private bool _absorbCompleteFired = false;

		// Panel position
		[DebugEditable(DisplayName = "Center Offset X", Step = 2, Min = -1000, Max = 1000)]
		public int OffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Center Offset Y", Step = 2, Min = -400, Max = 400)]
		public int OffsetY { get; set; } = -300;

		// Panel sizing
		[DebugEditable(DisplayName = "Panel Padding", Step = 1, Min = 4, Max = 40)]
		public int PanelPadding { get; set; } = 30;

		[DebugEditable(DisplayName = "Border Thickness", Step = 1, Min = 1, Max = 8)]
		public int BorderThickness { get; set; } = 0;

		[DebugEditable(DisplayName = "Background Alpha", Step = 5, Min = 0, Max = 255)]
		public int BackgroundAlpha { get; set; } = 160;

		[DebugEditable(DisplayName = "Outline Echo Interval (s)", Step = 0.05f, Min = 0.1f, Max = 5f)]
		public float OutlineEchoIntervalSeconds { get; set; } = 1f;

		[DebugEditable(DisplayName = "Outline Echo Duration (s)", Step = 0.05f, Min = 0.05f, Max = 1f)]
		public float OutlineEchoDurationSeconds { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Outline Echo Expansion (px)", Step = 1, Min = 0, Max = 40)]
		public int OutlineEchoExpansionPx { get; set; } = 8;

		[DebugEditable(DisplayName = "Outline Echo Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float OutlineEchoAlpha { get; set; } = 0.22f;

		// Text
		[DebugEditable(DisplayName = "Title Scale", Step = 0.05f, Min = 0.05f, Max = 2.5f)]
		public float TitleScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.01f, Max = 2.5f)]
		public float TextScale { get; set; } = 0.138f;

		[DebugEditable(DisplayName = "Panel Max Width % of Screen", Step = 0.05f, Min = 0.1f, Max = 1f)]
		public float PanelMaxWidthPercent { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Panel Min Width % of Screen", Step = 0.01f, Min = 0f, Max = 1f)]
		public float PanelMinWidthPercent { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Line Spacing Extra", Step = 1, Min = 0, Max = 20)]
		public int LineSpacingExtra { get; set; } = 3;

		[DebugEditable(DisplayName = "Title Spacing Extra", Step = 1, Min = 0, Max = 120)]
		public int TitleSpacingExtra { get; set; } = 80;

		// Decorations
		[DebugEditable(DisplayName = "Corner Ornament Scale", Step = 0.01f, Min = 0.1f, Max = 4f)]
		public float CornerOrnamentScale { get; set; } = 0.24f;

		[DebugEditable(DisplayName = "Corner Left Offset X", Step = 1, Min = -400, Max = 400)]
		public int CornerLeftOffsetX { get; set; } = -5;

		[DebugEditable(DisplayName = "Corner Left Offset Y", Step = 1, Min = -400, Max = 400)]
		public int CornerLeftOffsetY { get; set; } = 5;

		[DebugEditable(DisplayName = "Corner Right Offset X", Step = 1, Min = -400, Max = 400)]
		public int CornerRightOffsetX { get; set; } = 5;

		[DebugEditable(DisplayName = "Corner Right Offset Y", Step = 1, Min = -400, Max = 400)]
		public int CornerRightOffsetY { get; set; } = 5;

		[DebugEditable(DisplayName = "Top Ornament Scale", Step = 0.01f, Min = 0.1f, Max = 4f)]
		public float TopOrnamentScale { get; set; } = 0.37f;

		[DebugEditable(DisplayName = "Top Ornament Offset Y", Step = 1, Min = -400, Max = 400)]
		public int TopOrnamentOffsetY { get; set; } = 22;

		[DebugEditable(DisplayName = "Skull Scale", Step = 0.01f, Min = 0.1f, Max = 4f)]
		public float SkullScale { get; set; } = 0.15f;

		[DebugEditable(DisplayName = "Skull Vertical Offset", Step = 2, Min = -400, Max = 200)]
		public int SkullVerticalOffset { get; set; } = 30;

		// Impact animation tuning
		[DebugEditable(DisplayName = "Overshoot Intensity", Step = 0.05f, Min = 0f, Max = 3f)]
		public float OvershootIntensity { get; set; } = 0.8f;

		[DebugEditable(DisplayName = "Shake Duration (s)", Step = 0.05f, Min = 0f, Max = 1.5f)]
		public float ShakeDurationSeconds { get; set; } = 0.25f;

		[DebugEditable(DisplayName = "Shake Amplitude (px)", Step = 1, Min = 0, Max = 50)]
		public int ShakeAmplitudePx { get; set; } = 9;

		// Impact squash/flash/crater
		[DebugEditable(DisplayName = "Squash Duration (s)", Step = 0.02f, Min = 0.05f, Max = 1f)]
		public float SquashDurationSeconds { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Squash X Factor", Step = 0.05f, Min = 1f, Max = 2.5f)]
		public float SquashXFactor { get; set; } = 1.25f;

		[DebugEditable(DisplayName = "Squash Y Factor", Step = 0.05f, Min = 0.3f, Max = 1f)]
		public float SquashYFactor { get; set; } = 0.8f;

		[DebugEditable(DisplayName = "Impact Flash Duration (s)", Step = 0.02f, Min = 0f, Max = 1f)]
		public float FlashDurationSeconds { get; set; } = 0.12f;

		[DebugEditable(DisplayName = "Impact Flash Max Alpha", Step = 5, Min = 0, Max = 255)]
		public int FlashMaxAlpha { get; set; } = 110;

		[DebugEditable(DisplayName = "Crater Duration (s)", Step = 0.02f, Min = 0f, Max = 1.5f)]
		public float CraterDurationSeconds { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Crater Max Expand (px)", Step = 2, Min = 0, Max = 200)]
		public int CraterMaxExpandPx { get; set; } = 24;

		[DebugEditable(DisplayName = "Crater Max Alpha", Step = 5, Min = 0, Max = 255)]
		public int CraterMaxAlpha { get; set; } = 120;

		[DebugEditable(DisplayName = "Ring Two Delay (s)", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float RingTwoDelaySeconds { get; set; } = 0.05f;

		[DebugEditable(DisplayName = "Panel Aura Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float PanelAuraAlpha { get; set; } = 0.22f;

		[DebugEditable(DisplayName = "Ornament Slide (px)", Step = 1, Min = 0, Max = 100)]
		public int OrnamentSlidePx { get; set; } = 24;

		[DebugEditable(DisplayName = "Confirm Hover Scale", Step = 0.01f, Min = 1f, Max = 1.3f)]
		public float ConfirmHoverScale { get; set; } = 1.04f;

		[DebugEditable(DisplayName = "Absorb Afterimage Count", Step = 1, Min = 0, Max = 8)]
		public int AbsorbAfterimageCount { get; set; } = 3;

		// Impact shockwave
		[DebugEditable(DisplayName = "Impact Shockwave Enabled")]
		public bool ImpactShockwaveEnabled { get; set; } = true;

		[DebugEditable(DisplayName = "Impact Shockwave Origin Width", Step = 2, Min = 1, Max = 800)]
		public int ImpactShockwaveOriginWidthPx { get; set; } = 80;

		[DebugEditable(DisplayName = "Impact Shockwave Origin Height", Step = 2, Min = 1, Max = 800)]
		public int ImpactShockwaveOriginHeightPx { get; set; } = 60;

		[DebugEditable(DisplayName = "Impact Shockwave Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float ImpactShockwaveDurationSeconds { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "Impact Shockwave Amp Multiplier", Step = 0.01f, Min = 0f, Max = 5f)]
		public float ImpactShockwaveAmpMultiplier { get; set; } = 0.12f;

		[DebugEditable(DisplayName = "Impact Shockwave Min Amp", Step = 0.01f, Min = 0f, Max = 10f)]
		public float ImpactShockwaveMinAmp { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Impact Shockwave Max Amp", Step = 0.01f, Min = 0f, Max = 20f)]
		public float ImpactShockwaveMaxAmp { get; set; } = 3f;

		[DebugEditable(DisplayName = "Impact Shockwave Base Radius", Step = 2, Min = 0, Max = 2000)]
		public int ImpactShockwaveBaseRadiusPx { get; set; } = 110;

		[DebugEditable(DisplayName = "Impact Shockwave Radius Per Amp", Step = 2, Min = 0, Max = 1000)]
		public int ImpactShockwaveRadiusPerAmpPx { get; set; } = 55;

		[DebugEditable(DisplayName = "Impact Shockwave Base Ripple Width", Step = 1, Min = 1, Max = 200)]
		public int ImpactShockwaveBaseRippleWidthPx { get; set; } = 8;

		[DebugEditable(DisplayName = "Impact Shockwave Ripple Width Per Amp", Step = 1, Min = 0, Max = 100)]
		public int ImpactShockwaveRippleWidthPerAmpPx { get; set; } = 3;

		[DebugEditable(DisplayName = "Impact Shockwave Base Strength", Step = 0.01f, Min = 0f, Max = 10f)]
		public float ImpactShockwaveBaseStrength { get; set; } = 0.55f;

		[DebugEditable(DisplayName = "Impact Shockwave Base Chromatic Amp", Step = 0.001f, Min = 0f, Max = 1f)]
		public float ImpactShockwaveBaseChromaticAberrationAmp { get; set; } = 0.015f;

		[DebugEditable(DisplayName = "Impact Shockwave Chromatic Freq", Step = 0.01f, Min = 0f, Max = 40f)]
		public float ImpactShockwaveChromaticAberrationFreq { get; set; } = 3.14f;

		[DebugEditable(DisplayName = "Impact Shockwave Base Shading", Step = 0.01f, Min = 0f, Max = 5f)]
		public float ImpactShockwaveBaseShadingIntensity { get; set; } = 0.25f;

		// Confirm button tuning
		[DebugEditable(DisplayName = "Confirm Button Offset Y", Step = 2, Min = -600, Max = 600)]
		public int ConfirmButtonOffsetY { get; set; } = 8;
		[DebugEditable(DisplayName = "Confirm Button Width", Step = 2, Min = 20, Max = 600)]
		public int ConfirmButtonWidth { get; set; } = 154;
		[DebugEditable(DisplayName = "Confirm Button Height", Step = 2, Min = 16, Max = 200)]
		public int ConfirmButtonHeight { get; set; } = 42;
		[DebugEditable(DisplayName = "Confirm Button Text Scale", Step = 0.05f, Min = 0.3f, Max = 3f)]
		public float ConfirmButtonTextScale { get; set; } = 0.175f;
		[DebugEditable(DisplayName = "Confirm Button Z", Step = 10, Min = -100000, Max = 100000)]
		public int ConfirmButtonZ { get; set; } = 20000;

		// Impact particles
		[DebugEditable(DisplayName = "Particle Count Min", Step = 1, Min = 0, Max = 200)]
		public int ParticleCountMin { get; set; } = 24;

		[DebugEditable(DisplayName = "Particle Count Max", Step = 1, Min = 0, Max = 300)]
		public int ParticleCountMax { get; set; } = 64;

		[DebugEditable(DisplayName = "Particle Gravity", Step = 10f, Min = -1000f, Max = 2000f)]
		public float ParticleGravity { get; set; } = 430f;

		[DebugEditable(DisplayName = "Particle Drag", Step = 0.01f, Min = 0.8f, Max = 1f)]
		public float ParticleDrag { get; set; } = 0.985f;

		public EnemyAttackDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
			_enemyAttackCornerBlTexture = TryLoadDecorationTexture("enemy_attack_bl");
			_enemyAttackCornerBrTexture = TryLoadDecorationTexture("enemy_attack_br");
			_enemyAttackTopTexture = TryLoadDecorationTexture("enemy_attack_top");
			_enemyAttackSkullTexture = TryLoadDecorationTexture("enemy_attack_skull");
			_panelAuraTexture = PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, 512, 0.2f, 1f);

			EventManager.Subscribe<ConfirmBlocksRequested>(_ =>
			{
                LoggingService.Append("EnemyAttackDisplaySystem.OnConfirmBlocksRequested", new System.Text.Json.Nodes.JsonObject { ["event"] = "ConfirmBlocksRequested" });
				OnConfirmPressed();
			});
			EventManager.Subscribe<DeleteCachesEvent>(_ =>
			{
				ClearAttackDisplayState();
				HideConfirmButton();
				_cachedConfirmTexture?.Dispose();
				_cachedConfirmTexture = null;
				_cachedConfirmText = null;
			});

			// Clear any transient visuals when leaving Enemy phases
			EventManager.Subscribe<ChangeBattlePhaseEvent>(evt =>
			{
				if (evt.Current != SubPhase.Block && evt.Current != SubPhase.EnemyAttack)
				{
					ClearAttackDisplayState();
				}
				if (evt.Current == SubPhase.Block && evt.Previous != SubPhase.Block)
				{
					_showBanner = false;
					ResetAnchorBounds();
				}
			});

			EventManager.Subscribe<TriggerEnemyAttackDisplayEvent>(_ =>
			{
				_showBanner = true;
				if (EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out Entity _, out AttackIntent _, out var plannedAttack))
				{
					int attackDamage = plannedAttack?.AttackDefinition?.Damage ?? 0;
					StartImpactSequence(attackDamage);
				}
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.EnemyAttackIntro });
			});
		}

		private Texture2D TryLoadDecorationTexture(string assetName)
		{
			return _imageAssets.TryGetTexture(assetName);
		}

		private void CreateConfirmButton()
		{
			var primaryBtn = EntityManager.CreateEntity("UIButton_ConfirmEnemyAttack");
			EntityManager.AddComponent(primaryBtn, new Transform{});
			EntityManager.AddComponent(primaryBtn, new UIElement { IsInteractable = true, EventType = UIElementEventType.ConfirmBlocks });
			EntityManager.AddComponent(primaryBtn, new HotKey { Button = FaceButton.Y });
			EnsureConfirmTexture();
		}

		private void EnsureConfirmTexture()
		{
			const string label = "Confirm";
			if (_cachedConfirmTexture != null && _cachedConfirmText == label) return;
			_cachedConfirmTexture?.Dispose();
			_cachedConfirmTexture = ButtonTextureFactory.Create(_graphicsDevice, label, Color.White, Color.DarkRed);
			_cachedConfirmText = label;
		}

		private void OnConfirmPressed()
		{
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
			if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out _, out var intent, out _))
				return;

			if (!EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
				EntityManager,
				_confirmedAttackSequences))
			{
				if (!BattleInputGate.IsTutorialActionAllowed(EntityManager, TutorialAction.ConfirmBlocks))
				{
					BattleInputGate.TryAllowTutorialAction(EntityManager, TutorialAction.ConfirmBlocks);
				}
				return;
			}

			if (EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(
				EntityManager,
				_confirmedAttackSequences))
			{
				ExecuteConfirm(intent.ActiveAttackSequence);
				return;
			}

			QueueConfirm(intent.ActiveAttackSequence);
		}

		private void QueueConfirm(int attackSequence)
		{
			_pendingConfirmSequence = attackSequence;
			var phase = GetPhaseState();
			if (phase != null) phase.PendingBlockConfirm = true;
			HideConfirmButton();
		}

		private bool TryResolvePendingConfirm(int currentSequence)
		{
			if (_pendingConfirmSequence < 0) return false;
			if (_pendingConfirmSequence != currentSequence)
			{
				ClearPendingConfirm();
				return false;
			}

			if (!EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(
				EntityManager,
				_confirmedAttackSequences))
			{
				return false;
			}

			ExecuteConfirm(_pendingConfirmSequence);
			return true;
		}

		private void ExecuteConfirm(int attackSequence)
		{
			ClearPendingConfirm();
			_confirmedAttackSequences.Add(attackSequence);
			HideConfirmButton();
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyAttack, Previous = SubPhase.Block });
			EventQueue.EnqueueRule(new QueuedDiscardAssignedBlocksEvent(EntityManager));
			EventQueue.EnqueueRule(new QueuedResolveAttackEvent());
			EventQueue.EnqueueRule(new QueuedWaitAbsorbEvent());
			EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out var enemy, out _, out var planned);
			var attack = planned?.AttackDefinition;
			var requests = attack == null
				? Array.Empty<VisualEffectRequested>()
				: VisualEffectRequestFactory.ForEnemyAttackSequence(EntityManager, enemy, attack, attack.AttackEffectSequence);
			var drivingRequest = requests.SingleOrDefault(request => request.DrivesGameplayImpact);
			if (drivingRequest != null)
			{
				foreach (var request in requests)
				{
					EventQueue.EnqueueRule(new QueuedStartVisualEffect(request));
				}
				EventQueue.EnqueueRule(new QueuedWaitVisualEffectImpact(drivingRequest.RequestId));
			}
			else
			{
				LoggingService.Append("EnemyAttackDisplaySystem.ExecuteConfirm", new System.Text.Json.Nodes.JsonObject
				{
					["reason"] = "VisualEffectRequestFailed",
					["attackSequence"] = attackSequence,
					["attackId"] = attack?.Id.ToKey() ?? string.Empty
				});
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<EnemyAttackImpactNow>(
					"Rule.EnemyAttackImpactNow.Emergency",
					new EnemyAttackImpactNow()));
			}
			EventQueue.EnqueueRule(new QueuedAdvanceToNextPlannedAttackEvent(EntityManager));
		}

		private void HideConfirmButton()
		{
			var confirmBtn = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
			if (confirmBtn == null) return;

			var ui = confirmBtn.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.IsInteractable = false;
				ui.Bounds = Rectangle.Empty;
			}

			var hotkey = confirmBtn.GetComponent<HotKey>();
			if (hotkey != null)
			{
				hotkey.IsActive = false;
			}
		}

		private void ClearPendingConfirm()
		{
			_pendingConfirmSequence = -1;
			var phase = GetPhaseState();
			if (phase != null) phase.PendingBlockConfirm = false;
		}

		private void ClearAttackDisplayState()
		{
			_impactActive = false;
			_impactMomentFired = false;
			_impactElapsedSeconds = 0f;
			_outlineEchoElapsedSeconds = 0f;
			_absorbElapsedSeconds = 0f;
			_absorbCompleteFired = false;
			_lastAttackSequence = -1;
			_confirmedAttackSequences.Clear();
			ClearPendingConfirm();
			_particles.Clear();
			_absorbEmbers.Clear();
			_showBanner = false;
			var presentation = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerPresentation>()
				.FirstOrDefault()?.GetComponent<EnemyAttackBannerPresentation>();
			if (presentation != null) presentation.IsVisible = false;
			ResetAnchorBounds();
			if (_attackTextTooltipEntity != null)
			{
				EntityManager.DestroyEntity(_attackTextTooltipEntity.Id);
				_attackTextTooltipEntity = null;
			}
		}

		private void ResetAnchorBounds()
		{
			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchorEntity == null) return;
			var anchorUi = anchorEntity.GetComponent<UIElement>();
			if (anchorUi != null)
				anchorUi.Bounds = new Rectangle(0, 0, 1, 1);
		}

		private PhaseState GetPhaseState()
		{
			return EntityManager.GetEntitiesWithComponent<PhaseState>()
				.FirstOrDefault()
				?.GetComponent<PhaseState>();
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<AttackIntent>();
		}

		private int GetCurrentAttackDamage()
		{
			if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out _, out _, out var plannedAttack))
				return 0;
			return Math.Max(0, plannedAttack?.AttackDefinition?.Damage ?? 0);
		}

		private void StartImpactSequence(int attackDamage)
		{
			_impactActive = true;
			_impactMomentFired = false;
			_impactElapsedSeconds = 0f;
			_outlineEchoElapsedSeconds = 0f;
			_impactDamage = Math.Max(0, attackDamage);
			_impactIntensity = EnemyAttackAnimationService.ComputeImpactIntensity(_impactDamage);
			_entranceSample = EnemyAttackAnimationService.ComputeEntrance(0f, _impactIntensity);
			_particles.Clear();
		}

		private void PublishImpactShockwave(int attackDamage)
		{
			if (!ImpactShockwaveEnabled) return;

			float minAmp = Math.Max(0f, ImpactShockwaveMinAmp);
			float maxAmp = Math.Max(minAmp, ImpactShockwaveMaxAmp);
			float amp = MathHelper.Clamp(attackDamage * Math.Max(0f, ImpactShockwaveAmpMultiplier), minAmp, maxAmp);
			Vector2 center = CalculateImpactShockwaveCenter();

			EventManager.Publish(new RectangularShockwaveEvent
			{
				BoundsCenterPx = center,
				BoundsSizePx = new Vector2(
					Math.Max(1, ImpactShockwaveOriginWidthPx),
					Math.Max(1, ImpactShockwaveOriginHeightPx)),
				DurationSec = Math.Max(0.01f, ImpactShockwaveDurationSeconds),
				MaxRadiusPx = Math.Max(0f, ImpactShockwaveBaseRadiusPx + ImpactShockwaveRadiusPerAmpPx * amp),
				RippleWidthPx = Math.Max(1f, ImpactShockwaveBaseRippleWidthPx + ImpactShockwaveRippleWidthPerAmpPx * amp),
				Strength = Math.Max(0f, ImpactShockwaveBaseStrength * amp),
				ChromaticAberrationAmp = Math.Max(0f, ImpactShockwaveBaseChromaticAberrationAmp * amp),
				ChromaticAberrationFreq = Math.Max(0f, ImpactShockwaveChromaticAberrationFreq),
				ShadingIntensity = Math.Max(0f, ImpactShockwaveBaseShadingIntensity * amp)
			});
		}

		private Vector2 CalculateImpactShockwaveCenter()
		{
			var presentation = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerPresentation>()
				.FirstOrDefault()?.GetComponent<EnemyAttackBannerPresentation>();
			if (presentation != null && presentation.RenderBounds.Width > 0 && presentation.RenderBounds.Height > 0)
			{
				return new Vector2(presentation.RenderBounds.Center.X, presentation.RenderBounds.Center.Y);
			}

			return new Vector2(
				Game1.VirtualWidth / 2f + OffsetX,
				Game1.VirtualHeight / 2f + OffsetY);
		}

		[DebugAction("Replay Impact Animation")]
		public void Debug_ReplayImpactAnimation()
		{
			StartImpactSequence(GetCurrentAttackDamage());
		}

		[DebugAction("Test Impact 3")]
		public void Debug_TestImpact3()
		{
			StartImpactSequence(3);
		}

		[DebugAction("Test Impact 8")]
		public void Debug_TestImpact8()
		{
			StartImpactSequence(8);
		}

		[DebugAction("Test Impact 15")]
		public void Debug_TestImpact15()
		{
			StartImpactSequence(15);
		}

		[DebugAction("Test Impact 25")]
		public void Debug_TestImpact25()
		{
			StartImpactSequence(25);
		}

		[DebugActionInt("Test Impact Damage", Step = 1, Min = 0, Max = 100, Default = 10)]
		public void Debug_TestImpactDamage(int damage)
		{
			StartImpactSequence(Math.Max(0, damage));
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (EntityManager.GetEntity("UIButton_ConfirmEnemyAttack") == null) {
				CreateConfirmButton();
			}
			if (BattleInputGate.ShouldSuppressEnemyAttackDisplay(EntityManager))
			{
				ClearAttackDisplayState();
				HideConfirmButton();
				return;
			}
			var phaseNow = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault().GetComponent<PhaseState>().Sub;
			if (phaseNow == SubPhase.Block) {
				_absorbElapsedSeconds = 0f;
				_absorbCompleteFired = false;
			}
			var intent = entity.GetComponent<AttackIntent>();
			if (intent == null || intent.Planned.Count == 0)
			{
				_impactActive = false;
				_lastAttackSequence = -1;
				ClearPendingConfirm();
				_particles.Clear();
				_absorbEmbers.Clear();
				// Cleanup tooltip entity when no attack is planned
				if (_attackTextTooltipEntity != null)
				{
					EntityManager.DestroyEntity(_attackTextTooltipEntity.Id);
					_attackTextTooltipEntity = null;
				}
				ResetAnchorBounds();
				return;
			}

			var currentSequence = intent.ActiveAttackSequence;
			if (_lastAttackSequence != currentSequence)
			{
				_lastAttackSequence = currentSequence;
				// A new active attack sequence always needs a fresh confirm gate.
				_confirmedAttackSequences.Clear();
				if (_pendingConfirmSequence >= 0 && _pendingConfirmSequence != currentSequence)
				{
					ClearPendingConfirm();
				}
			}

			if (TryResolvePendingConfirm(currentSequence)) return;
			UpdateConfirmAvailability(phaseNow, currentSequence);

			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			_idleElapsedSeconds += dt;
			if (_impactActive)
			{
				float previousElapsed = _impactElapsedSeconds;
				_impactElapsedSeconds += dt;
				_entranceSample = EnemyAttackAnimationService.ComputeEntrance(_impactElapsedSeconds, _impactIntensity);
				if (!_impactMomentFired
					&& previousElapsed < EnemyAttackAnimationService.ImpactMomentSeconds
					&& _impactElapsedSeconds >= EnemyAttackAnimationService.ImpactMomentSeconds)
				{
					_impactMomentFired = true;
					SpawnImpactParticles(_impactIntensity, currentSequence, Math.Max(1, _bannerRect.Width), Math.Max(1, _bannerRect.Height));
					PublishImpactShockwave(_impactDamage);
					EventManager.Publish(new RumbleRequested
					{
						Profile = RumbleProfile.EnemyIntentImpact,
						Scale = _impactIntensity,
						Group = RumbleGroup.Gameplay,
					});
				}
				UpdateImpactParticles(dt);
				if (_impactElapsedSeconds >= EnemyAttackAnimationService.PresentationCompleteSeconds
					&& _particles.Count == 0)
				{
					_impactActive = false;
				}
			}
			if (!_impactActive && phaseNow == SubPhase.Block)
			{
				_outlineEchoElapsedSeconds += dt;
			}
			else
			{
				_outlineEchoElapsedSeconds = 0f;
			}
			// Update absorb tween timer based on battle phase
			if (phaseNow == SubPhase.EnemyAttack)
			{
				if (_lastPresentationPhase != SubPhase.EnemyAttack)
				{
					var enemyTransform = entity.GetComponent<Transform>();
					var target = (enemyTransform?.Position ?? Vector2.Zero) + new Vector2(0f, AbsorbTargetYOffset);
					SpawnAbsorbEmbers(_bannerRect, target, currentSequence);
				}
				_absorbElapsedSeconds += dt;
				if (_absorbElapsedSeconds >= AbsorbDurationSeconds)
				{
					EventManager.Publish(new EnemyAbsorbComplete());
					_absorbCompleteFired = true;
					ResetAnchorBounds();
				}
			}
			_lastPresentationPhase = phaseNow;

			UpdatePresentationState(entity, intent, phaseNow);
		}

		private void UpdateConfirmAvailability(SubPhase phaseNow, int attackSequence)
		{
			var confirmButton = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
			var ui = confirmButton?.GetComponent<UIElement>();
			var hotkey = confirmButton?.GetComponent<HotKey>();
			if (ui == null) return;

			bool pending = _pendingConfirmSequence == attackSequence;
			bool entranceReady = !_impactActive || _impactElapsedSeconds >= 0.30f;
			bool available = entranceReady && !pending && EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
				EntityManager,
				_confirmedAttackSequences);

			ui.IsInteractable = available;
			if (!available) ui.Bounds = Rectangle.Empty;
			if (hotkey != null)
			{
				hotkey.IsActive = available && ui.IsInteractable;
			}
		}

		private EnemyAttackProgress FindEnemyAttackProgress()
		{
			return EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress)
				? progress
				: null;
		}

		private bool IsAnyBlockAssignmentAnimating()
		{
			return EnemyAttackConfirmAvailabilityService.IsAnyBlockAssignmentAnimating(EntityManager);
		}
	}
}
