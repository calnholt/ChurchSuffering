using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
 
using System;
using Crusaders30XX.ECS.Scenes.BattleScene;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Tutorials;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Battle Scene System")]
	public class BattleSceneSystem : Core.System
	{
		private readonly SystemManager _systemManager;
		private readonly World _world;
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ContentManager _content;
		private bool _loadedSystems = false;
		private bool _loadedEntities = false;

		// Battle systems (logic and draw). Only present while in Battle
	

	private HandDisplaySystem _handDisplaySystem;
	private CardHoverDetectionSystem _cardHoverDetectionSystem;
	private CardVisualEffectsSuppressionSystem _cardVisualEffectsSuppressionSystem;
		private BattleBackgroundSystem _battleBackgroundSystem;
		private DrawPileDisplaySystem _drawPileDisplaySystem;
		private DiscardPileDisplaySystem _discardPileDisplaySystem;
		private DrawPileColorCountDisplaySystem _drawPileColorCountDisplaySystem;
		private PlayerDisplaySystem _playerDisplaySystem;
		private PlayerWispParticleSystem _playerWispParticleSystem;
		private PlayerTemperanceActivationDisplaySystem _playerTemperanceActivationDisplaySystem;
		private ModularEffectCoordinatorSystem _modularEffectCoordinatorSystem;
		private ModularEffectActorPresentationSystem _modularEffectActorPresentationSystem;
		private ModularEffectScreenDisplaySystem _modularEffectScreenDisplaySystem;
		private ModularEffectPrimitiveDisplaySystem _modularEffectPrimitiveDisplaySystem;
		private ModularEffectParticleDisplaySystem _modularEffectParticleDisplaySystem;
		private CardModularEffectsDebugSystem _cardModularEffectsDebugSystem;
		private EquipmentModularEffectsDebugSystem _equipmentModularEffectsDebugSystem;
		private MedalModularEffectsDebugSystem _medalModularEffectsDebugSystem;
		private EnemyAttackModularEffectsDebugSystem _enemyAttackModularEffectsDebugSystem;
		private CathedralLightingSystem _cathedralLightingSystem;
		private DesertBackgroundEffectSystem _desertBackgroundEffectSystem;
		private DesertStormDisplaySystem _desertStormDisplaySystem;
		private VolcanoEmbersDisplaySystem _volcanoEmbersDisplaySystem;
		private SnowstormDisplaySystem _snowstormDisplaySystem;
		private PurpleZapsDisplaySystem _purpleZapsDisplaySystem;
		private JungleBackgroundDisplaySystem _jungleBackgroundDisplaySystem;
		private PlayerHudLayoutSystem _playerHudLayoutSystem;
		private PlayerHudFeedbackSystem _playerHudFeedbackSystem;
		private PlayerHudRootDisplaySystem _playerHudRootDisplaySystem;
		private PlayerHudHealthDisplaySystem _playerHudHealthDisplaySystem;
		private PlayerHudCourageDisplaySystem _playerHudCourageDisplaySystem;
		private PlayerHudTemperanceDisplaySystem _playerHudTemperanceDisplaySystem;
		private PlayerHudActionPointDisplaySystem _playerHudActionPointDisplaySystem;
		private PlayerHudPledgeDisplaySystem _playerHudPledgeDisplaySystem;
		private CourageManagerSystem _courageManagerSystem;
		private ActionPointManagementSystem _actionPointManagementSystem;
		private TemperanceManagerSystem _temperanceManagerSystem;
		#pragma warning disable CS0618 // Intentional until the plunder gauge owns its rendering.
		private HPDisplaySystem _hpDisplaySystem;
		#pragma warning restore CS0618
		private AppliedPassivesDisplaySystem _appliedPassivesDisplaySystem;
		private PoisonSystem _poisonSystem;
		private PassiveMeterRenderSystem _passiveMeterRenderSystem;
		private CardGeometrySettingsDebugSystem _cardGeometrySettingsDebugSystem;
		private HpManagementSystem _hpManagementSystem;
		private BattlePhaseDisplaySystem _battlePhaseDisplaySystem;
		private TestFightHpDisplaySystem _testFightHpDisplaySystem;
		private EnemyDisplaySystem _enemyDisplaySystem;
		private GuardianAngelDisplaySystem _guardianAngelDisplaySystem;
		private EnemyIntentPipsSystem _enemyIntentPipsSystem;
		private EnemyAttackDisplaySystem _enemyAttackDisplaySystem;
		private EnemyDamageMeterDisplaySystem _enemyDamageMeterDisplaySystem;
		private AmbushDisplaySystem _ambushDisplaySystem;
		// private QueuedEventsDisplaySystem _queuedEventsDisplaySystem;
		private DamageModificationDisplaySystem _damageModificationDisplaySystem;
		private SplashEffectAnimationDisplaySystem _attackAnimationDisplaySystem;
		private CardPlayedAnimationSystem _cardPlayedAnimationSystem;
		private PixelBurstDisplaySystem _pixelBurstDisplaySystem;
		private EnemyDefeatFlowSystem _enemyDefeatFlowSystem;
		private EnemyPhaseFlowSystem _enemyPhaseFlowSystem;
		private CardMoveDisplaySystem _cardMoveDisplaySystem;
		private AssignedBlockCardsDisplaySystem _assignedBlockCardsDisplaySystem;
		private ExhaustOnBlockDisplaySystem _exhaustOnBlockDisplaySystem;
		private EnemyIntentPlanningSystem _enemyIntentPlanningSystem;
		private EnemyAttackProgressManagementSystem _enemyAttackProgressManagementSystem;
		private MarkedForSpecificDiscardSystem _markedForSpecificDiscardSystem;
		private MarkedForEndOfTurnSystem _markedForEndOfTurnSystem;
		private MarkedForExhaustSystem _markedForExhaustSystem;
		private StunnedOverlaySystem _stunnedOverlaySystem;
		private AttackResolutionSystem _attackResolutionSystem;
		private HandBlockInteractionSystem _handBlockInteractionSystem;
		private CardZoneSystem _cardZoneSystem;
		private AssignedBlocksToDiscardSystem _assignedBlocksToDiscardSystem;
		private EnemyDamageManagerSystem _enemyDamageManagerSystem;
		private EventQueueSystem _eventQueueSystem;
		private CardPlaySystem _cardPlaySystem;
		private EndTurnDisplaySystem _endTurnDisplaySystem;
		private DrawHandSystem _battlePhaseDrawSystem;
		private DeckEmptyDeathCheckSystem _deckEmptyDeathCheckSystem;
		private PhaseCoordinatorSystem _phaseCoordinatorSystem;
		private PhaseChangeEventSystem _phaseChangeEventSystem;
		private PayCostOverlaySystem _payCostOverlaySystem;
		private CantPlayCardMessageSystem _cantPlayCardMessageSystem;
		private TutorialRetryDisplaySystem _tutorialRetryDisplaySystem;
		private TestFightFlowSystem _testFightFlowSystem;
		private WeaponManagementSystem _weaponManagementSystem;
		private EquipmentManagerSystem _equipmentManagerSystem;
		private MedalManagerSystem _medalManagerSystem;
		private MedalDisplaySystem _medalDisplaySystem;
		private EquippedWeaponDisplaySystem _equippedWeaponDisplaySystem;
		private EquipmentDisplaySystem _equipmentDisplaySystem;
		private EquipmentTooltipDisplaySystem _equipmentTooltipDisplaySystem;
		private HighlightSettingsSystem _equipmentHighlightSettingsDebugSystem;
		private EquipmentBlockInteractionSystem _equipmentBlockInteractionSystem;
		private ReplacementEffectSystem _replacementEffectSystem;
		private AppliedPassivesManagementSystem _appliedPassivesManagementSystem;
		private AnathemaManagementSystem _anathemaManagementSystem;
		private BrittleManagementSystem _brittleManagementSystem;
		private ScorchedManagementSystem _scorchedManagementSystem;
		private ThornedManagementSystem _thornedManagementSystem;
		private BleedManagementSystem _bleedManagementSystem;
		private BattleStateInfoManagementSystem _battleStateInfoManagementSystem;
		private DiscardSpecificCardHighlightSystem _discardSpecificCardHighlightSystem;
		private MillCardSystem _millCardSystem;
		private IntimidateManagementSystem _intimidateManagementSystem;
		private ShackleManagementSystem _shackleManagementSystem;
		private IntimidateDisplaySystem _intimidateDisplaySystem;
		private FrozenCardDisplaySystem _frozenCardDisplaySystem;
		private RecoilManagementSystem _recoilManagementSystem;
		private RecoilDisplaySystem _recoilDisplaySystem;
		private SealManagementSystem _sealManagementSystem;
		private VigorManagementSystem _vigorManagementSystem;
		private ShackleDisplaySystem _shackleDisplaySystem;
		private UIElementHighlightSystem _uiElementHighlightSystem;
		private TribulationManagerSystem _tribulationManagerSystem;
		private QuestTribulationDisplaySystem _questTribulationDisplaySystem;
		private MustBeBlockedSystem _mustBeBlockedSystem;
		private ActiveCharacterIndicatorDisplaySystem _activeCharacterIndicatorDisplaySystem;
		private TutorialManager _tutorialManager;
		private TutorialDisplaySystem _tutorialDisplaySystem;
		private GuidedTutorialDirectorSystem _guidedTutorialDirectorSystem;
		
		// Pledge system
		private PledgeManagementSystem _pledgeManagementSystem;
		private PledgeDisplaySystem _pledgeDisplaySystem;

		// Plunder system (Wyvern)
		private PlunderManagementSystem _plunderManagementSystem;
		private PlunderDisplaySystem _plunderDisplaySystem;
		private PlunderSnatchDisplaySystem _plunderSnatchDisplaySystem;

		// Marksman system (Sniper)
		private MarkManagementSystem _markManagementSystem;
		private MarkDisplaySystem _markDisplaySystem;

		// Can-play card highlight
		private CanPlayCardHighlightSystem _canPlayCardHighlightSystem;
		private CanPlayHighlightSettingsSystem _canPlayHighlightSettingsSystem;

		// Bloodshot effect system and render targets for background compositing
		private BloodshotDisplaySystem _bloodshotDisplaySystem;
		private RenderTarget2D _bgRt;
		private RenderTarget2D _bgTemp;
		private RasterizerState _rasterizerState;


		public BattleSceneSystem(EntityManager em, SystemManager sm, World world, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content) : base(em)
		{
			_systemManager = sm;
			_world = world;
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			EventManager.Subscribe<StartBattleRequested>(_ =>
			{
				LoggingService.Append("BattleSceneSystem.OnStartBattleRequested", new System.Text.Json.Nodes.JsonObject { ["event"] = "StartBattleRequested" });
				InitBattle();
			});
			EventManager.Subscribe<LoadSceneEvent>(_ => {
				LoggingService.Append("BattleSceneSystem.OnLoadSceneEvent", new System.Text.Json.Nodes.JsonObject { ["event"] = "LoadSceneEvent", ["scene"] = _.Scene.ToString() });
				if (_.Scene != SceneId.Battle) 
				{
					return;
				}
				var queued = EntityManager.GetEntity("QueuedEvents").GetComponent<QueuedEvents>();
				bool guidedTutorial = GuidedTutorialService.IsActive(EntityManager);
				var musicTrack = MusicTrack.DesertBattle;
				if (!guidedTutorial && queued?.IsClimbEncounter == true)
				{
					musicTrack = BattleLocationAssetService.GetMusicTrack(queued.BattleLocation ?? BattleLocation.Desert);
				}
				else if (!guidedTutorial)
				{
					LocationDefinitionCache.TryGet(queued.LocationId, out var def);
					musicTrack = def?.pointsOfInterest[queued.QuestIndex].musicTrack ?? MusicTrack.DesertBattle;
				}
				EventManager.Publish(new ChangeMusicTrack { Track = musicTrack });
				if (!_loadedSystems)
				{
					AddBattleSystems();
				}
				EventManager.Publish(new ChangeBattleLocationEvent
				{
					Location = ResolveBattleLocationForLoad(queued, guidedTutorial),
				});
				if (!_loadedEntities)
				{
					CreateBattleSceneEntities();
				}
				// Initialize the battle scene before pending dialog, but wait for dialog completion before starting battle rules.
				bool willShowDialog = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault()?.GetComponent<PendingQuestDialog>()?.WillShowDialog ?? false;
				InitBattle();
				// EnqueueBattleRules(false);
			});
			EventManager.Subscribe<DialogEnded>(_ =>
			{
				LoggingService.Append("BattleSceneSystem.OnDialogEnded", new System.Text.Json.Nodes.JsonObject { ["event"] = "DialogEnded" });
				var pending = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault()
					?.GetComponent<PendingQuestDialog>();
				if (pending == null || !pending.WillShowDialog || pending.RequestId != Guid.Empty) return;
				if (!_loadedEntities) 
				{
					CreateBattleSceneEntities();
				}
				EnsureBattleInitialized();
				TimerScheduler.Schedule(0.3f, () => {
					EventManager.Publish(new ShowStartOfBattleAnimationEvent());
				});
				// EnqueueBattleRules(true);
			});
			EventManager.Subscribe<DialogueSequenceCompleted>(completed =>
			{
				var pending = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault()
					?.GetComponent<PendingQuestDialog>();
				if (pending == null
					|| !pending.WillShowDialog
					|| pending.RequestId == Guid.Empty
					|| completed.RequestId != pending.RequestId)
				{
					return;
				}

				pending.WillShowDialog = false;
				if (!_loadedEntities)
				{
					CreateBattleSceneEntities();
				}
				EnsureBattleInitialized();
				TimerScheduler.Schedule(0.3f, () =>
				{
					EventManager.Publish(new ShowStartOfBattleAnimationEvent());
				});
			});
			EventManager.Subscribe<DeleteCachesEvent>(_ => {
				if (_.Scene == SceneId.Battle) return;
				LoggingService.Append("BattleSceneSystem.OnDeleteCachesEvent", new System.Text.Json.Nodes.JsonObject { ["event"] = "DeleteCachesEvent", ["scene"] = _.Scene.ToString() });
				_loadedEntities = false;
				// EventQueue.Clear();
				// RemoveBattleSystems();
			});
			EventManager.Subscribe<TransitionCompleteEvent>(_ => {
				if (_.Scene != SceneId.Battle) return;
				bool willShowDialog = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault()?.GetComponent<PendingQuestDialog>()?.WillShowDialog ?? false;
				if (!willShowDialog)
				{
					TimerScheduler.Schedule(0.3f, () => {
						EventManager.Publish(new ShowStartOfBattleAnimationEvent());
					});
				}
			});
			EventManager.Subscribe<BattlePhaseAnimationCompleteEvent>(_ => {
				if (_.SubPhase == SubPhase.StartBattle)
				{
					EnqueueBattleRules(true);
				}
			});
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			// no-op per-frame
		}

		public void Draw()
		{
			// End the current SpriteBatch to draw backgrounds to a separate render target
			_spriteBatch.End();
			
			// Save the current render target (Game1's _sceneRt)
			var originalRenderTargets = _graphicsDevice.GetRenderTargets();
			RenderTarget2D originalRt = originalRenderTargets.Length > 0 ? originalRenderTargets[0].RenderTarget as RenderTarget2D : null;
			
			// Draw backgrounds to _bgRt
			_graphicsDevice.SetRenderTarget(_bgRt);
			_graphicsDevice.Clear(Color.Transparent);
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, _rasterizerState);
			FrameProfiler.Measure("BattleBackgroundSystem.Draw", _battleBackgroundSystem.Draw);
			FrameProfiler.Measure("CathedralLightingSystem.Draw", _cathedralLightingSystem.Draw);
			bool hasDesertStorm = _desertStormDisplaySystem?.CanComposite == true;
			bool hasVolcanoEmbers = _volcanoEmbersDisplaySystem?.CanComposite == true;
			bool hasSnowstorm = _snowstormDisplaySystem?.CanComposite == true;
			bool hasPurpleZaps = _purpleZapsDisplaySystem?.CanComposite == true;
			bool hasJungleBackground = _jungleBackgroundDisplaySystem?.CanComposite == true;
			if (!hasDesertStorm)
			{
				FrameProfiler.Measure("DesertBackgroundEffectSystem.Draw", _desertBackgroundEffectSystem.Draw);
			}
			_spriteBatch.End();

			Texture2D backgroundSource = _bgRt;
			if (hasDesertStorm)
			{
				FrameProfiler.Measure(
					"DesertStormDisplaySystem.Composite",
					() => _desertStormDisplaySystem.Composite(_bgRt, _bgTemp)
				);
				backgroundSource = _bgTemp;
			}
			else if (hasVolcanoEmbers)
			{
				FrameProfiler.Measure(
					"VolcanoEmbersDisplaySystem.Composite",
					() => _volcanoEmbersDisplaySystem.Composite(_bgRt, _bgTemp)
				);
				backgroundSource = _bgTemp;
			}
			else if (hasSnowstorm)
			{
				FrameProfiler.Measure(
					"SnowstormDisplaySystem.Composite",
					() => _snowstormDisplaySystem.Composite(_bgRt, _bgTemp)
				);
				backgroundSource = _bgTemp;
			}
			else if (hasPurpleZaps)
			{
				FrameProfiler.Measure(
					"PurpleZapsDisplaySystem.Composite",
					() => _purpleZapsDisplaySystem.Composite(_bgRt, _bgTemp)
				);
				backgroundSource = _bgTemp;
			}
			else if (hasJungleBackground)
			{
				FrameProfiler.Measure(
					"JungleBackgroundDisplaySystem.Composite",
					() => _jungleBackgroundDisplaySystem.Composite(_bgRt, _bgTemp)
				);
				backgroundSource = _bgTemp;
			}
			
			// Apply bloodshot effect to backgrounds if active
			bool hasBloodshot = ShaderRuntimeOptions.ShadersEnabled &&
				_bloodshotDisplaySystem != null && _bloodshotDisplaySystem.IsActive();
			if (hasBloodshot)
			{
				RenderTarget2D bloodshotTemp = ReferenceEquals(backgroundSource, _bgRt) ? _bgTemp : _bgRt;
				FrameProfiler.Measure(
					"BloodshotDisplaySystem.Composite",
					() => _bloodshotDisplaySystem.Composite(backgroundSource, bloodshotTemp, originalRt)
				);
			}
			else
			{
				_graphicsDevice.SetRenderTarget(originalRt);
				_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
				_spriteBatch.Draw(backgroundSource, _graphicsDevice.Viewport.Bounds, Color.White);
				_spriteBatch.End();
			}
			
			// Resume SpriteBatch for remaining UI drawing
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, _rasterizerState);
			
			// If there will be dialog to show for the quest, skip drawing battle UI (overlay is drawn globally)
			bool willShowDialog = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault()?.GetComponent<PendingQuestDialog>()?.WillShowDialog ?? false;
			bool encounterDialogueActive = EntityManager.GetEntitiesWithComponent<DialogOverlayState>()
				.FirstOrDefault()?.GetComponent<DialogOverlayState>()?.IsActive ?? false;
			if (willShowDialog || encounterDialogueActive) return;
			if (RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(EntityManager)) return;
			FrameProfiler.Measure("PlayerDisplaySystem.Draw", _playerDisplaySystem.Draw);
			FrameProfiler.Measure("GuardianAngelDisplaySystem.Draw", _guardianAngelDisplaySystem.Draw);
			FrameProfiler.Measure("EnemyDisplaySystem.Draw", _enemyDisplaySystem.Draw);
			FrameProfiler.Measure("PixelBurstDisplaySystem.Draw", _pixelBurstDisplaySystem.Draw);
			FrameProfiler.Measure("ModularEffectPrimitiveDisplaySystem.Draw", _modularEffectPrimitiveDisplaySystem.Draw);
			FrameProfiler.Measure("ModularEffectParticleDisplaySystem.Draw", _modularEffectParticleDisplaySystem.Draw);
			FrameProfiler.Measure("ModularEffectScreenDisplaySystem.Draw", _modularEffectScreenDisplaySystem.Draw);
			FrameProfiler.Measure("PlunderDisplaySystem.Draw", _plunderDisplaySystem.Draw);
			FrameProfiler.Measure("PlunderSnatchDisplaySystem.Draw", _plunderSnatchDisplaySystem.Draw);
			FrameProfiler.Measure("ActiveCharacterIndicatorDisplaySystem.Draw", _activeCharacterIndicatorDisplaySystem.Draw);
			FrameProfiler.Measure("EnemyIntentPipsSystem.Draw", _enemyIntentPipsSystem.Draw);
			FrameProfiler.Measure("AmbushDisplaySystem.Draw", _ambushDisplaySystem.Draw);
			bool guidedTutorial = GuidedTutorialService.IsActive(EntityManager);
			// if (!guidedTutorial) FrameProfiler.Measure("QueuedEventsDisplaySystem.Draw", _queuedEventsDisplaySystem.Draw);
			FrameProfiler.Measure("AttackAnimationDisplaySystem.Draw", _attackAnimationDisplaySystem.Draw);
			FrameProfiler.Measure("StunnedOverlaySystem.Draw", _stunnedOverlaySystem.Draw);
			FrameProfiler.Measure("UIElementHighlightSystem.Draw", _uiElementHighlightSystem.Draw);
			FrameProfiler.Measure("AssignedBlockCardsDisplaySystem.Draw", _assignedBlockCardsDisplaySystem.Draw);
			FrameProfiler.Measure("ExhaustOnBlockDisplaySystem.Draw", _exhaustOnBlockDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerWispParticleSystem.Draw", _playerWispParticleSystem.Draw);
			FrameProfiler.Measure("PlayerTemperanceActivationDisplaySystem.Draw", _playerTemperanceActivationDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerHudRootDisplaySystem.Draw", _playerHudRootDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerHudHealthDisplaySystem.Draw", _playerHudHealthDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerHudCourageDisplaySystem.Draw", _playerHudCourageDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerHudTemperanceDisplaySystem.Draw", _playerHudTemperanceDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerHudActionPointDisplaySystem.Draw", _playerHudActionPointDisplaySystem.Draw);
			FrameProfiler.Measure("PlayerHudPledgeDisplaySystem.Draw", _playerHudPledgeDisplaySystem.Draw);
			FrameProfiler.Measure("QuestTribulationDisplaySystem.Draw", _questTribulationDisplaySystem.Draw);
			FrameProfiler.Measure("LegacyPlunderHpDisplaySystem.Draw", _hpDisplaySystem.Draw);
			FrameProfiler.Measure("AppliedPassivesDisplaySystem.Draw", _appliedPassivesDisplaySystem.Draw);
			FrameProfiler.Measure("PassiveMeterRenderSystem.Draw", _passiveMeterRenderSystem.Draw);
			FrameProfiler.Measure("PayCostOverlaySystem.DrawBackdrop", _payCostOverlaySystem.DrawBackdrop);
			FrameProfiler.Measure("EnemyAttackDisplaySystem.Draw", _enemyAttackDisplaySystem.Draw);
			FrameProfiler.Measure("EnemyDamageMeterDisplaySystem.Draw", _enemyDamageMeterDisplaySystem.Draw);
			FrameProfiler.Measure("EndTurnDisplaySystem.Draw", _endTurnDisplaySystem.Draw);
			FrameProfiler.Measure("HandDisplaySystem.DrawHand", _handDisplaySystem.DrawHand);
			FrameProfiler.Measure("CardMoveDisplaySystem.DrawAlpha", _cardMoveDisplaySystem.DrawAlpha);
			FrameProfiler.Measure("CardPlayedAnimationSystem.Draw", _cardPlayedAnimationSystem.Draw);
			FrameProfiler.Measure("EquipmentDisplaySystem.Draw", _equipmentDisplaySystem.Draw);
			FrameProfiler.Measure("EquipmentTooltipDisplaySystem.Draw", _equipmentTooltipDisplaySystem.Draw);
			var guidedState = GuidedTutorialService.GetState(EntityManager);
			bool showDrawPile = !guidedTutorial || (guidedState?.Section == 8);
			if (guidedState == null || guidedState.Section >= 5)
				FrameProfiler.Measure("EquippedWeaponDisplaySystem.Draw", _equippedWeaponDisplaySystem.Draw);
			FrameProfiler.Measure("MedalDisplaySystem.Draw", _medalDisplaySystem.Draw);
			if (showDrawPile) FrameProfiler.Measure("DrawPileDisplaySystem.Draw", _drawPileDisplaySystem.Draw);
			if (showDrawPile) FrameProfiler.Measure("DrawPileColorCountDisplaySystem.Draw", _drawPileColorCountDisplaySystem.Draw);
			if (!guidedTutorial) FrameProfiler.Measure("DiscardPileDisplaySystem.Draw", _discardPileDisplaySystem.Draw);
			FrameProfiler.Measure("MillCardSystem.Draw", _millCardSystem.Draw);
			FrameProfiler.Measure("PayCostOverlaySystem.DrawForeground", _payCostOverlaySystem.DrawForeground);
			FrameProfiler.Measure("CantPlayCardMessageSystem.Draw", _cantPlayCardMessageSystem.Draw);
			FrameProfiler.Measure("DiscardSpecificCardHighlightSystem.Draw", _discardSpecificCardHighlightSystem.Draw);
			FrameProfiler.Measure("IntimidateDisplaySystem.Draw", _intimidateDisplaySystem.Draw);
			FrameProfiler.Measure("BattlePhaseDisplaySystem.Draw", _battlePhaseDisplaySystem.Draw);
			FrameProfiler.Measure("TestFightHpDisplaySystem.Draw", _testFightHpDisplaySystem.Draw);
			FrameProfiler.Measure("DamageModificationDisplaySystem.Draw", _damageModificationDisplaySystem.Draw);
		if (_tutorialRetryDisplaySystem != null) FrameProfiler.Measure("TutorialRetryDisplaySystem.Draw", _tutorialRetryDisplaySystem.Draw);
		if (_tutorialDisplaySystem != null) FrameProfiler.Measure("TutorialDisplaySystem.Draw", _tutorialDisplaySystem.Draw);
		}

		private void CreateBattleSceneEntities() {
			var sceneEntity = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault();
			if (sceneEntity == null)
			{
				sceneEntity = EntityManager.CreateEntity("SceneState");
				EntityManager.AddComponent(sceneEntity, new SceneState { Current = SceneId.Internal_QueueEventsMenu });
			}
			EntityFactory.CreateGameState(_world);
			bool guidedTutorial = GuidedTutorialService.IsActive(EntityManager);
			var deckEntity = TestFightRuntime.IsActive
				? EntityManager.GetEntity("Deck")
				: guidedTutorial
				? EntityManager.GetEntitiesWithComponent<StockHand>().FirstOrDefault()
				: RunDeckService.EnsureRunDeck(EntityManager);
			var player = TestFightRuntime.IsActive
				? EntityManager.GetEntity("Player")
				: guidedTutorial
				? EntityManager.GetEntity("Player")
				: RunPlayerService.EnsureRunPlayer(_world);
			var playerComp = player?.GetComponent<Player>();
			if (playerComp != null)
			{
				playerComp.DeckEntity = deckEntity;
			}
			// Create tribulations for the current quest
			var queuedEntity = EntityManager.GetEntity("QueuedEvents");
			var queued = queuedEntity.GetComponent<QueuedEvents>();
			if (!guidedTutorial && !string.IsNullOrEmpty(queued.LocationId))
			{
				TribulationQuestService.CreateTribulationsForQuest(EntityManager, queued.LocationId, queued.QuestIndex);
			}
			_loadedEntities = true;
		}

		public void InitBattle() 
		{
			bool guidedTutorial = GuidedTutorialService.IsActive(EntityManager);
			if (guidedTutorial)
				GuidedTutorialService.PrepareStockHand(EntityManager);
			else if (TestFightRuntime.IsActive)
			{
				TestFightSetupService.RegenerateDeck(EntityManager);
				EventManager.Publish(new ResetDeckEvent { });
			}
			else
			{
				RunDeckService.EnsureRunDeck(EntityManager);
				EventManager.Publish(new ResetDeckEvent { });
			}
			var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
			var queuedEntity = EntityManager.GetEntity("QueuedEvents");
			var queued = queuedEntity.GetComponent<QueuedEvents>();
			bool shouldApplyClimbBattlePackage = IsFirstQueuedClimbEncounter(queued);
			EventManager.Publish(new SetActionPointsEvent { Amount = 0 });
			var player = EntityManager.GetEntity("Player");
			if (player == null)
			{
				LoggingService.Append("BattleSceneSystem.InitBattle", new System.Text.Json.Nodes.JsonObject { ["error"] = "Player entity missing" });
				return;
			}
			var battleStateInfo = player.GetComponent<BattleStateInfo>();
			battleStateInfo.EquipmentTriggeredThisBattle.Clear();
			var playerPassives = player.GetComponent<AppliedPassives>();

			var allCards = EntityManager.GetEntitiesWithComponent<CardData>();
			foreach (var c in allCards)
			{
				if (c.HasComponent<Pledge>())
				{
					EventManager.Publish(new RemovePledgeFromCardRequested { Card = c });
				}
			}

			if (playerPassives == null)
			{
				_world.AddComponent(player, new AppliedPassives());
			}
			EntityManager.DestroyEntity("Enemy");
			LoggingService.Append("BattleSceneSystem.InitBattle", new System.Text.Json.Nodes.JsonObject { ["eventsCount"] = queued.Events.Count, ["currentIndex"] = queued.CurrentIndex });
			Entity nextEnemy;
			if (guidedTutorial)
			{
				var tutorial = GuidedTutorialService.GetState(EntityManager);
				var sectionDef = GuidedTutorialDefinitions.GetSection(tutorial.Section);
				EventManager.Publish(new SetCourageEvent { Amount = tutorial.BaselineCourage });
				EventManager.Publish(new SetTemperanceEvent { Amount = tutorial.BaselineTemperance });
				player.GetComponent<Intellect>().Value = sectionDef.Turns[0].StockHand.Count;
				player.GetComponent<MaxHandSize>().Value = sectionDef.Turns[0].StockHand.Count;
				StateSingleton.IsPledgeEnabled = tutorial.Section >= 8;
				tutorial.IsRestart = false;
				queued.CurrentIndex = 0;
				nextEnemy = EntityFactory.CreateEnemyFromId(_world, "gleeber", EntityManager);
			}
			else
			{
				EventManager.Publish(new SetCourageEvent { Amount = 0 });
				StateSingleton.IsPledgeEnabled = true;
				var nextEvent = queued.Events[++queued.CurrentIndex];
				nextEnemy = EntityFactory.CreateEnemyFromId(_world, nextEvent.EventId, EntityManager, nextEvent.Difficulty);
			}
			TestFightSetupService.ApplyEnemyHpDelta(nextEnemy);
			if (!guidedTutorial)
				EventManager.Publish(new ResetDeckEvent { });
			var phaseState = EntityManager.GetEntity("PhaseState").GetComponent<PhaseState>();
			phaseState.Main = MainPhase.StartBattle;
			phaseState.Sub = SubPhase.StartBattle;
			phaseState.TurnNumber = 1;
			phaseState.DefeatPresentationActive = false;
			if (guidedTutorial)
			{
				var tutorial = GuidedTutorialService.GetState(EntityManager);
				var hp = player.GetComponent<HP>();
				if (hp != null)
				{
					hp.Max = 25;
					hp.UnscarredMax = 25;
					hp.Current = tutorial.PlayerHp;
				}
			}
			else
			{
				EventManager.Publish(new ApplyBattleMaxHpEvent
				{
					Target = player,
					ScarPenalty = GetScarStacks(player)
				});
				EventManager.Publish(new FullyHealEvent { Target = player });
			}
			ApplyPendingClimbBattlePackage(shouldApplyClimbBattlePackage, player);
			foreach (var e in EntityManager.GetEntitiesWithComponent<CardTooltip>()) {
				var cardData = e.GetComponent<CardData>();
				var card = CardFactory.Create(cardData.Card.CardId);
				if (card != null && string.IsNullOrEmpty(card.CardTooltip))
				{
					e.RemoveComponent<CardTooltip>();
				}
			}
		}

		internal static bool IsFirstQueuedClimbEncounter(QueuedEvents queued)
		{
			return queued?.IsClimbEncounter == true && queued.CurrentIndex == -1;
		}

		internal static BattleLocation ResolveBattleLocationForLoad(QueuedEvents queued, bool guidedTutorial)
		{
			if (guidedTutorial) return BattleLocation.Desert;
			return queued?.BattleLocation ?? BattleLocation.Desert;
		}

		internal static bool ApplyPendingClimbBattlePackage(bool shouldApply, Entity player)
		{
			if (!shouldApply || player == null) return false;

			var climb = SaveCache.GetClimbState();
			var bonus = climb?.nextBattleBonus;
			var penalty = climb?.nextBattlePenalty;
			int courage = Math.Max(0, bonus?.courage ?? 0);
			int temperance = Math.Max(0, bonus?.temperance ?? 0);
			int vigor = Math.Max(0, bonus?.vigor ?? 0);
			int burn = Math.Max(0, penalty?.burn ?? 0);
			int fear = Math.Max(0, penalty?.fear ?? 0);
			if (courage == 0 && temperance == 0 && vigor == 0 && burn == 0 && fear == 0)
			{
				return false;
			}

			if (courage > 0)
			{
				EventManager.Publish(new ModifyCourageRequestEvent
				{
					Delta = courage,
					Reason = "Climb next-battle bonus",
					Type = ModifyCourageType.Gain,
				});
			}
			if (temperance > 0)
			{
				EventManager.Publish(new ModifyTemperanceEvent { Delta = temperance });
			}
			PublishPassiveGrant(player, AppliedPassiveType.Vigor, vigor);
			PublishPassiveGrant(player, AppliedPassiveType.Burn, burn);
			PublishPassiveGrant(player, AppliedPassiveType.Fear, fear);

			climb.nextBattleBonus = new ClimbNextBattleBonusSave();
			climb.nextBattlePenalty = new ClimbNextBattlePenaltySave();
			SaveCache.SaveClimbState(climb);
			return true;
		}

		private static void PublishPassiveGrant(Entity player, AppliedPassiveType type, int amount)
		{
			if (amount <= 0) return;
			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = player,
				Type = type,
				Delta = amount,
			});
		}

		private static int GetScarStacks(Entity player)
		{
			var passives = player?.GetComponent<AppliedPassives>()?.Passives;
			if (passives == null) return 0;
			return passives.TryGetValue(AppliedPassiveType.Scar, out var stacks)
				? Math.Max(0, stacks)
				: 0;
		}

		private void EnsureBattleInitialized()
		{
			var queued = EntityManager.GetEntity("QueuedEvents")?.GetComponent<QueuedEvents>();
			if (queued != null && queued.CurrentIndex >= 0) return;
			InitBattle();
		}

		public void EnqueueBattleRules(bool isFollowingDialog) 
		{
			EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
				"Rule.ChangePhase.EnemyStart",
				new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle, Previous = SubPhase.StartBattle }
			));				
			EventQueueBridge.EnqueueTriggerAction("BattleSceneSystem.StartBattle", () => {
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.EnemyStart",
					new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart }
				));
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.PreBlock",
					new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock }
				));
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.Block",
					new ChangeBattlePhaseEvent { Current = SubPhase.Block }
				));
			}, 0f);
		}
		
		[DebugAction("Next Battle")]
		public void Debug_NextBattle() 
		{
			EventManager.Publish(new StartBattleRequested {  });
		}
		private void AddBattleSystems()
		{
			if (_loadedSystems) return;
			_loadedSystems = true;
			LoggingService.Append("BattleSceneSystem.AddBattleSystems", new System.Text.Json.Nodes.JsonObject { ["action"] = "Adding battle systems" });
		_battleBackgroundSystem = new BattleBackgroundSystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
		_handDisplaySystem = new HandDisplaySystem(_world.EntityManager, _graphicsDevice);
		_cardHoverDetectionSystem = new CardHoverDetectionSystem(_world.EntityManager);
		_cardVisualEffectsSuppressionSystem = new CardVisualEffectsSuppressionSystem(_world.EntityManager);
			_cardZoneSystem = new CardZoneSystem(_world.EntityManager);
			_drawPileDisplaySystem = new DrawPileDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_discardPileDisplaySystem = new DiscardPileDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_drawPileColorCountDisplaySystem = new DrawPileColorCountDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_millCardSystem = new MillCardSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerDisplaySystem = new PlayerDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_cathedralLightingSystem = new CathedralLightingSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_desertBackgroundEffectSystem = new DesertBackgroundEffectSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_desertStormDisplaySystem = new DesertStormDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_volcanoEmbersDisplaySystem = new VolcanoEmbersDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_snowstormDisplaySystem = new SnowstormDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_purpleZapsDisplaySystem = new PurpleZapsDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_jungleBackgroundDisplaySystem = new JungleBackgroundDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_playerWispParticleSystem = new PlayerWispParticleSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerTemperanceActivationDisplaySystem = new PlayerTemperanceActivationDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_modularEffectCoordinatorSystem = new ModularEffectCoordinatorSystem(_world.EntityManager);
			_modularEffectActorPresentationSystem = new ModularEffectActorPresentationSystem(_world.EntityManager);
			_modularEffectScreenDisplaySystem = new ModularEffectScreenDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_modularEffectPrimitiveDisplaySystem = new ModularEffectPrimitiveDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_modularEffectParticleDisplaySystem = new ModularEffectParticleDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_cardModularEffectsDebugSystem = new CardModularEffectsDebugSystem(_world.EntityManager);
			_equipmentModularEffectsDebugSystem = new EquipmentModularEffectsDebugSystem(_world.EntityManager);
			_medalModularEffectsDebugSystem = new MedalModularEffectsDebugSystem(_world.EntityManager);
			_enemyAttackModularEffectsDebugSystem = new EnemyAttackModularEffectsDebugSystem(_world.EntityManager);
			_playerHudLayoutSystem = new PlayerHudLayoutSystem(_world.EntityManager);
			_playerHudFeedbackSystem = new PlayerHudFeedbackSystem(_world.EntityManager);
			_playerHudRootDisplaySystem = new PlayerHudRootDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerHudHealthDisplaySystem = new PlayerHudHealthDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerHudCourageDisplaySystem = new PlayerHudCourageDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerHudTemperanceDisplaySystem = new PlayerHudTemperanceDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerHudActionPointDisplaySystem = new PlayerHudActionPointDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_playerHudPledgeDisplaySystem = new PlayerHudPledgeDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			// _threatDisplaySystem = new ThreatDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_cardMoveDisplaySystem = new CardMoveDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_courageManagerSystem = new CourageManagerSystem(_world.EntityManager);
			_actionPointManagementSystem = new ActionPointManagementSystem(_world.EntityManager);
			_temperanceManagerSystem = new TemperanceManagerSystem(_world.EntityManager);
			// _threatManagementSystem = new ThreatManagementSystem(_world.EntityManager);
			#pragma warning disable CS0618 // Intentional until the plunder gauge owns its rendering.
			_hpDisplaySystem = new HPDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			#pragma warning restore CS0618
			_appliedPassivesDisplaySystem = new AppliedPassivesDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_poisonSystem = new PoisonSystem(_world.EntityManager);
			_passiveMeterRenderSystem = new PassiveMeterRenderSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_cardGeometrySettingsDebugSystem = new CardGeometrySettingsDebugSystem(_world.EntityManager);
			_hpManagementSystem = new HpManagementSystem(_world.EntityManager);
			_pledgeManagementSystem = new PledgeManagementSystem(_world.EntityManager);
			_eventQueueSystem = new EventQueueSystem(_world.EntityManager);
			_battlePhaseDisplaySystem = new BattlePhaseDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_testFightHpDisplaySystem = new TestFightHpDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_enemyDisplaySystem = new EnemyDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_guardianAngelDisplaySystem = new GuardianAngelDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_enemyIntentPipsSystem = new EnemyIntentPipsSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_enemyAttackDisplaySystem = new EnemyAttackDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_enemyDamageMeterDisplaySystem = new EnemyDamageMeterDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_ambushDisplaySystem = new AmbushDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			// _queuedEventsDisplaySystem = new QueuedEventsDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_damageModificationDisplaySystem = new DamageModificationDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_attackAnimationDisplaySystem = new SplashEffectAnimationDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_cardPlayedAnimationSystem = new CardPlayedAnimationSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_pixelBurstDisplaySystem = new PixelBurstDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_enemyDefeatFlowSystem = new EnemyDefeatFlowSystem(_world.EntityManager, _content);
			_enemyPhaseFlowSystem = new EnemyPhaseFlowSystem(_world.EntityManager);
			_endTurnDisplaySystem = new EndTurnDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_assignedBlockCardsDisplaySystem = new AssignedBlockCardsDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_exhaustOnBlockDisplaySystem = new ExhaustOnBlockDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_payCostOverlaySystem = new PayCostOverlaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_cantPlayCardMessageSystem = new CantPlayCardMessageSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_tutorialRetryDisplaySystem = new TutorialRetryDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_testFightFlowSystem = new TestFightFlowSystem(_world.EntityManager);
			_enemyIntentPlanningSystem = new EnemyIntentPlanningSystem(_world.EntityManager);
			_enemyAttackProgressManagementSystem = new EnemyAttackProgressManagementSystem(_world.EntityManager);
			_markedForSpecificDiscardSystem = new MarkedForSpecificDiscardSystem(_world.EntityManager);
			_markedForEndOfTurnSystem = new MarkedForEndOfTurnSystem(_world.EntityManager);
			_markedForExhaustSystem = new MarkedForExhaustSystem(_world.EntityManager);
			_stunnedOverlaySystem = new StunnedOverlaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_attackResolutionSystem = new AttackResolutionSystem(_world.EntityManager);
			_handBlockInteractionSystem = new HandBlockInteractionSystem(_world.EntityManager);
			_assignedBlocksToDiscardSystem = new AssignedBlocksToDiscardSystem(_world.EntityManager, _graphicsDevice);
			_enemyDamageManagerSystem = new EnemyDamageManagerSystem(_world.EntityManager);
			_cardPlaySystem = new CardPlaySystem(_world.EntityManager);
			_guidedTutorialDirectorSystem = new GuidedTutorialDirectorSystem(_world.EntityManager);
			_battlePhaseDrawSystem = new DrawHandSystem(_world.EntityManager);
			_deckEmptyDeathCheckSystem = new DeckEmptyDeathCheckSystem(_world.EntityManager);
			_phaseCoordinatorSystem = new PhaseCoordinatorSystem(_world.EntityManager);
			_phaseChangeEventSystem = new PhaseChangeEventSystem(_world.EntityManager);
			_weaponManagementSystem = new WeaponManagementSystem(_world.EntityManager);
			_equipmentManagerSystem = new EquipmentManagerSystem(_world.EntityManager);
			_medalManagerSystem = new MedalManagerSystem(_world.EntityManager);
			_tribulationManagerSystem = new TribulationManagerSystem(_world.EntityManager);
			_equipmentDisplaySystem = new EquipmentDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_equipmentTooltipDisplaySystem = new EquipmentTooltipDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_equippedWeaponDisplaySystem = new EquippedWeaponDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_medalDisplaySystem = new MedalDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_questTribulationDisplaySystem = new QuestTribulationDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_equipmentHighlightSettingsDebugSystem = new HighlightSettingsSystem(_world.EntityManager);
			_equipmentBlockInteractionSystem = new EquipmentBlockInteractionSystem(_world.EntityManager);
			_replacementEffectSystem = new ReplacementEffectSystem(_world.EntityManager);
			_appliedPassivesManagementSystem = new AppliedPassivesManagementSystem(_world.EntityManager);
			_anathemaManagementSystem = new AnathemaManagementSystem(_world.EntityManager);
			_brittleManagementSystem = new BrittleManagementSystem(_world.EntityManager);
			_scorchedManagementSystem = new ScorchedManagementSystem(_world.EntityManager);
			_thornedManagementSystem = new ThornedManagementSystem(_world.EntityManager);
			_bleedManagementSystem = new BleedManagementSystem(_world.EntityManager);
			_battleStateInfoManagementSystem = new BattleStateInfoManagementSystem(_world.EntityManager);
			_discardSpecificCardHighlightSystem = new DiscardSpecificCardHighlightSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_intimidateManagementSystem = new IntimidateManagementSystem(_world.EntityManager);
			_shackleManagementSystem = new ShackleManagementSystem(_world.EntityManager);
			if (!ShaderRuntimeOptions.ShadersEnabled)
			{
				var frostTexture = _content.Load<Texture2D>("frost");
				_frozenCardDisplaySystem = new FrozenCardDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, frostTexture);
			}
			_recoilManagementSystem = new RecoilManagementSystem(_world.EntityManager);
			_recoilDisplaySystem = new RecoilDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_sealManagementSystem = new SealManagementSystem(_world.EntityManager);
			_vigorManagementSystem = new VigorManagementSystem(_world.EntityManager);
			var shackleTexture = _content.Load<Texture2D>("shackles");
			_shackleDisplaySystem = new ShackleDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, shackleTexture);
			_intimidateDisplaySystem = new IntimidateDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_uiElementHighlightSystem = new UIElementHighlightSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_mustBeBlockedSystem = new MustBeBlockedSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_activeCharacterIndicatorDisplaySystem = new ActiveCharacterIndicatorDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);

			// Plunder system (Wyvern)
			_plunderManagementSystem = new PlunderManagementSystem(_world.EntityManager, _graphicsDevice);
			_plunderDisplaySystem = new PlunderDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_plunderSnatchDisplaySystem = new PlunderSnatchDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);

			// Marksman system (Sniper)
			_markManagementSystem = new MarkManagementSystem(_world.EntityManager);
			var markTexture = _content.Load<Texture2D>("mark");
			_markDisplaySystem = new MarkDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, markTexture);

			// Can-play card highlight
			_canPlayCardHighlightSystem = new CanPlayCardHighlightSystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_canPlayHighlightSettingsSystem = new CanPlayHighlightSettingsSystem(_world.EntityManager);

			// Bloodshot effect system and render targets for background compositing
			_bloodshotDisplaySystem = new BloodshotDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_bgRt = new RenderTarget2D(_graphicsDevice, Game1.VirtualWidth, Game1.VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);
			_bgTemp = new RenderTarget2D(_graphicsDevice, Game1.VirtualWidth, Game1.VirtualHeight, false, SurfaceFormat.Color, DepthFormat.None);
			_rasterizerState = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
			
		// Register
		_world.AddSystem(_handDisplaySystem);
		_world.AddSystem(_cardHoverDetectionSystem);
		_world.AddSystem(_cardVisualEffectsSuppressionSystem);
		_world.AddSystem(_cardZoneSystem);
			_world.AddSystem(_handBlockInteractionSystem);
			_world.AddSystem(_pledgeManagementSystem);
			_world.AddSystem(_eventQueueSystem);
			_world.AddSystem(_drawPileDisplaySystem);
			_world.AddSystem(_discardPileDisplaySystem);
			_world.AddSystem(_drawPileColorCountDisplaySystem);
			_world.AddSystem(_millCardSystem);
			_world.AddSystem(_playerDisplaySystem);
			_world.AddSystem(_guardianAngelDisplaySystem);
			_world.AddSystem(_cathedralLightingSystem);
			_world.AddSystem(_desertBackgroundEffectSystem);
			_world.AddSystem(_desertStormDisplaySystem);
			_world.AddSystem(_volcanoEmbersDisplaySystem);
			_world.AddSystem(_snowstormDisplaySystem);
			_world.AddSystem(_purpleZapsDisplaySystem);
			_world.AddSystem(_jungleBackgroundDisplaySystem);
			_world.AddSystem(_playerWispParticleSystem);
			_world.AddSystem(_modularEffectCoordinatorSystem, SystemUpdatePhase.Presentation);
			_world.AddSystem(_modularEffectActorPresentationSystem, SystemUpdatePhase.Presentation);
			_world.AddSystem(_modularEffectScreenDisplaySystem, SystemUpdatePhase.Presentation);
			_world.AddSystem(_modularEffectPrimitiveDisplaySystem, SystemUpdatePhase.Presentation);
			_world.AddSystem(_modularEffectParticleDisplaySystem, SystemUpdatePhase.Presentation);
			_world.AddSystem(_cardModularEffectsDebugSystem);
			_world.AddSystem(_equipmentModularEffectsDebugSystem);
			_world.AddSystem(_medalModularEffectsDebugSystem);
			_world.AddSystem(_enemyAttackModularEffectsDebugSystem);
			_world.AddSystem(_playerTemperanceActivationDisplaySystem);
			_world.AddSystem(_playerHudLayoutSystem);
			_world.AddSystem(_playerHudFeedbackSystem);
			_world.AddSystem(_playerHudRootDisplaySystem);
			_world.AddSystem(_playerHudHealthDisplaySystem);
			_world.AddSystem(_playerHudCourageDisplaySystem);
			_world.AddSystem(_playerHudTemperanceDisplaySystem);
			_world.AddSystem(_playerHudActionPointDisplaySystem);
			_world.AddSystem(_playerHudPledgeDisplaySystem);
			_world.AddSystem(_courageManagerSystem);
			_world.AddSystem(_temperanceManagerSystem);
			_world.AddSystem(_actionPointManagementSystem);
			_world.AddSystem(_battleBackgroundSystem);
			_world.AddSystem(_hpDisplaySystem);
			_world.AddSystem(_appliedPassivesDisplaySystem);
			_world.AddSystem(_cardGeometrySettingsDebugSystem);
			_world.AddSystem(_hpManagementSystem);
			_world.AddSystem(_enemyDefeatFlowSystem);
			_world.AddSystem(_enemyPhaseFlowSystem);
			_world.AddSystem(_pixelBurstDisplaySystem);
			_world.AddSystem(_battlePhaseDisplaySystem);
			_world.AddSystem(_testFightHpDisplaySystem);
			_world.AddSystem(_enemyDisplaySystem);
			_world.AddSystem(_enemyIntentPipsSystem);
			_world.AddSystem(_poisonSystem);
			_world.AddSystem(_passiveMeterRenderSystem);
			_world.AddSystem(_enemyIntentPlanningSystem);
			_world.AddSystem(_enemyAttackProgressManagementSystem);
			_world.AddSystem(_markedForSpecificDiscardSystem);
			_world.AddSystem(_markedForEndOfTurnSystem);
			_world.AddSystem(_markedForExhaustSystem);
			_world.AddSystem(_stunnedOverlaySystem);
			_world.AddSystem(_attackResolutionSystem);
			_world.AddSystem(_enemyAttackDisplaySystem);
			_world.AddSystem(_enemyDamageMeterDisplaySystem);
			_world.AddSystem(_ambushDisplaySystem);
			// _world.AddSystem(_queuedEventsDisplaySystem);
			_world.AddSystem(_damageModificationDisplaySystem);
			_world.AddSystem(_attackAnimationDisplaySystem);
			_world.AddSystem(_cardPlayedAnimationSystem);
			_world.AddSystem(_cardMoveDisplaySystem);
			_world.AddSystem(_endTurnDisplaySystem);
			_world.AddSystem(_assignedBlockCardsDisplaySystem);
			_world.AddSystem(_exhaustOnBlockDisplaySystem);
			_world.AddSystem(_assignedBlocksToDiscardSystem);
			_world.AddSystem(_enemyDamageManagerSystem);
			_world.AddSystem(_cardPlaySystem);
			_world.AddSystem(_guidedTutorialDirectorSystem);
			_world.AddSystem(_battlePhaseDrawSystem);
			_world.AddSystem(_deckEmptyDeathCheckSystem);
			_world.AddSystem(_phaseCoordinatorSystem);
			_world.AddSystem(_phaseChangeEventSystem);
			_world.AddSystem(_weaponManagementSystem);
			_world.AddSystem(_equipmentManagerSystem);
			_world.AddSystem(_medalManagerSystem);
			_world.AddSystem(_tribulationManagerSystem);
			_world.AddSystem(_equipmentDisplaySystem);
			_world.AddSystem(_equipmentTooltipDisplaySystem);
			_world.AddSystem(_equippedWeaponDisplaySystem);
			_world.AddSystem(_medalDisplaySystem);
			_world.AddSystem(_questTribulationDisplaySystem);
			_world.AddSystem(_equipmentHighlightSettingsDebugSystem);
			_world.AddSystem(_equipmentBlockInteractionSystem);
			_world.AddSystem(_replacementEffectSystem);
			_world.AddSystem(_appliedPassivesManagementSystem);
			_world.AddSystem(_anathemaManagementSystem);
			_world.AddSystem(_brittleManagementSystem);
			_world.AddSystem(_scorchedManagementSystem);
			_world.AddSystem(_thornedManagementSystem);
			_world.AddSystem(_vigorManagementSystem);
			_world.AddSystem(_bleedManagementSystem);
			_world.AddSystem(_battleStateInfoManagementSystem);
			_world.AddSystem(_payCostOverlaySystem);
			_world.AddSystem(_cantPlayCardMessageSystem);
			_world.AddSystem(_testFightFlowSystem);
			_world.AddSystem(_discardSpecificCardHighlightSystem);
			_world.AddSystem(_intimidateManagementSystem);
			_world.AddSystem(_shackleManagementSystem);
			_world.AddSystem(_intimidateDisplaySystem);
			if (_frozenCardDisplaySystem != null)
			{
				_world.AddSystem(_frozenCardDisplaySystem);
			}
			_world.AddSystem(_recoilManagementSystem);
			_world.AddSystem(_recoilDisplaySystem);
			_world.AddSystem(_sealManagementSystem);
			_world.AddSystem(_shackleDisplaySystem);
			_world.AddSystem(_plunderManagementSystem);
			_world.AddSystem(_plunderDisplaySystem);
			_world.AddSystem(_plunderSnatchDisplaySystem);
			_world.AddSystem(_markManagementSystem);
			_world.AddSystem(_markDisplaySystem);
			_world.AddSystem(_mustBeBlockedSystem);
			_world.AddSystem(_activeCharacterIndicatorDisplaySystem);
			_world.AddSystem(_bloodshotDisplaySystem);
			_world.AddSystem(_canPlayCardHighlightSystem);
			_world.AddSystem(_canPlayHighlightSettingsSystem);

			// Tutorial system
			if (!TutorialLaunchOptions.SkipTutorials)
			{
				_tutorialManager = new TutorialManager(_world.EntityManager);
				_tutorialDisplaySystem = new TutorialDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content, _tutorialManager);
				_world.AddSystem(_tutorialManager);
				_world.AddSystem(_tutorialDisplaySystem);
				_world.AddSystem(_tutorialRetryDisplaySystem);
			}

			// Pledge system
			_pledgeDisplaySystem = new PledgeDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
			_world.AddSystem(_pledgeDisplaySystem);
		}

		public void DrawAdditive()
		{
			if (RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(EntityManager)) return;
			_cardMoveDisplaySystem?.DrawAdditive();
		}

	}
}
