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
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _contentFont = FontSingleton.ContentFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;
		private readonly Texture2D _enemyAttackCornerBlTexture;
		private readonly Texture2D _enemyAttackCornerBrTexture;
		private readonly Texture2D _enemyAttackTopTexture;
		private readonly Texture2D _enemyAttackSkullTexture;

		// Confirm button texture cache
		private Texture2D _cachedConfirmTexture;
		private string _cachedConfirmText;

		// Tooltip state
		private Entity _attackTextTooltipEntity = null;
		private Rectangle _bannerRect = Rectangle.Empty;

		// Animation state
		private int _lastAttackSequence = -1;
		private float _shakeElapsedSeconds = 0f;

		// Impact animation flow (spawn centered -> impact)
		private bool _impactActive = false;
		private float _squashElapsedSeconds = 0f;
		private float _flashElapsedSeconds = 0f;
		private float _craterElapsedSeconds = 0f;

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
		public int FlashMaxAlpha { get; set; } = 180;

		[DebugEditable(DisplayName = "Crater Duration (s)", Step = 0.02f, Min = 0f, Max = 1.5f)]
		public float CraterDurationSeconds { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Crater Max Expand (px)", Step = 2, Min = 0, Max = 200)]
		public int CraterMaxExpandPx { get; set; } = 24;

		[DebugEditable(DisplayName = "Crater Max Alpha", Step = 5, Min = 0, Max = 255)]
		public int CraterMaxAlpha { get; set; } = 120;

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

		// Debris
		[DebugEditable(DisplayName = "Debris Count", Step = 1, Min = 0, Max = 100)]
		public int DebrisCount { get; set; } = 100;

		[DebugEditable(DisplayName = "Debris Speed Min", Step = 5, Min = 0, Max = 600)]
		public int DebrisSpeedMin { get; set; } = 210;

		[DebugEditable(DisplayName = "Debris Speed Max", Step = 5, Min = 0, Max = 800)]
		public int DebrisSpeedMax { get; set; } = 420;

		[DebugEditable(DisplayName = "Debris Lifetime (s)", Step = 0.05f, Min = 0f, Max = 2f)]
		public float DebrisLifetimeSeconds { get; set; } = 0.8f;

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

			EventManager.Subscribe<ConfirmBlocksRequested>(_ =>
			{
                LoggingService.Append("EnemyAttackDisplaySystem.OnConfirmBlocksRequested", new System.Text.Json.Nodes.JsonObject { ["event"] = "ConfirmBlocksRequested" });
				OnConfirmPressed();
			});
			EventManager.Subscribe<DeleteCachesEvent>(_ =>
			{
				ClearAttackDisplayState();
				HideConfirmButton();
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
			var parallaxLayer = ParallaxLayer.GetUIParallaxLayer();
			parallaxLayer.MultiplierX = 0.045f;
			parallaxLayer.MultiplierY = 0.045f;
			EntityManager.AddComponent(primaryBtn, parallaxLayer);
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
			var recipe = attack?.AttackEffectRecipe ?? VisualEffectPresets.EnemyAttackLunge();
			var request = attack == null
				? null
				: VisualEffectRequestFactory.ForEnemyAttack(EntityManager, enemy, attack, recipe);
			if (request != null)
			{
				EventQueue.EnqueueRule(new QueuedStartVisualEffect(request));
				EventQueue.EnqueueRule(new QueuedWaitVisualEffectImpact(request.RequestId));
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
			_absorbElapsedSeconds = 0f;
			_absorbCompleteFired = false;
			_lastAttackSequence = -1;
			_confirmedAttackSequences.Clear();
			ClearPendingConfirm();
			_debris.Clear();
			_showBanner = false;
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
			_squashElapsedSeconds = 0f;
			_flashElapsedSeconds = 0f;
			_craterElapsedSeconds = 0f;
			_shakeElapsedSeconds = 0f;
			_debris.Clear();
			SpawnDebris();
			PublishImpactShockwave(Math.Max(0, attackDamage));
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
			EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out var enemy, out _, out var plannedAttack);
			var phase = GetPhaseState();
			var def = plannedAttack?.AttackDefinition;
			if (enemy != null && def != null && phase != null && _contentFont != null && _bodyFont != null)
			{
				Rectangle impactRect = CalculateBannerRect(enemy, phase.Sub, def);
				if (impactRect.Width > 0 && impactRect.Height > 0)
				{
					return new Vector2(
						impactRect.X + impactRect.Width / 2f,
						impactRect.Y + impactRect.Height / 2f);
				}
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
				_debris.Clear();
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
			if (_impactActive)
			{
				_squashElapsedSeconds += dt;
				_flashElapsedSeconds += dt;
				_craterElapsedSeconds += dt;
				_shakeElapsedSeconds += dt;
				UpdateDebris(dt);
			}
			// Update absorb tween timer based on battle phase
			if (phaseNow == SubPhase.EnemyAttack)
			{
				_absorbElapsedSeconds += dt;
				if (_absorbElapsedSeconds >= AbsorbDurationSeconds)
				{
					EventManager.Publish(new EnemyAbsorbComplete());
					_absorbCompleteFired = true;
					ResetAnchorBounds();
				}
			}

			// Calculate and store banner rect for TutorialDisplaySystem to query
			// Note: anchor Bounds are written in Draw (UpdateAnchorEntity) with parallax-adjusted
			// values — do NOT overwrite them here, or downstream systems lose parallax.
			if (_showBanner && _contentFont != null && _bodyFont != null)
			{
				var pa = intent.Planned[0];
				var def = pa.AttackDefinition;
				if (def != null)
				{
					_bannerRect = CalculateBannerRect(entity, phaseNow, def);
				}
			}

			// Ensure banner anchor entity exists (unconditional so BuildDrawContext finds it)
			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchorEntity == null)
			{
				anchorEntity = EntityManager.CreateEntity("EnemyAttackBannerAnchor");
				EntityManager.AddComponent(anchorEntity, new EnemyAttackBannerAnchor());
				EntityManager.AddComponent(anchorEntity, new Transform());
				var parallaxLayer = ParallaxLayer.GetUIParallaxLayer();
				parallaxLayer.MultiplierX = 0.045f;
				parallaxLayer.MultiplierY = 0.045f;
				EntityManager.AddComponent(anchorEntity, parallaxLayer);
				EntityManager.AddComponent(anchorEntity, new UIElement { Bounds = new Rectangle(0, 0, 1, 1), IsInteractable = false });
			}

			// Only write Positions when banner rect is valid (banner is visible)
			if (_bannerRect != Rectangle.Empty)
			{
				var anchorTransform = anchorEntity.GetComponent<Transform>();
				if (anchorTransform != null)
				{
					var centerBase = new Vector2(Game1.VirtualWidth / 2f + OffsetX, Game1.VirtualHeight / 2f + OffsetY);
					anchorTransform.Position = centerBase;
				}

				// Write confirm button Position so parallax can adjust it
				var confirmBtn = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
				if (confirmBtn != null)
				{
					var btnTransform = confirmBtn.GetComponent<Transform>();
					if (btnTransform != null)
					{
						btnTransform.Position = new Vector2(
							_bannerRect.X + _bannerRect.Width / 2f - ConfirmButtonWidth / 2f,
							_bannerRect.Bottom + ConfirmButtonOffsetY
						);
						btnTransform.ZOrder = ConfirmButtonZ;
					}
				}
			}
		}

		private void UpdateConfirmAvailability(SubPhase phaseNow, int attackSequence)
		{
			var confirmButton = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
			var ui = confirmButton?.GetComponent<UIElement>();
			var hotkey = confirmButton?.GetComponent<HotKey>();
			if (ui == null) return;

			bool pending = _pendingConfirmSequence == attackSequence;
			bool available = !pending && EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
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
