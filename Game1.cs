using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Core;
// duplicate removed
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Events;
using System;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.Diagnostics.Snapshots;
using Crusaders30XX.ECS.Components;
using System.Linq;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Data.Achievements;
using System.IO;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Ids;
using System.Collections.Generic;

namespace Crusaders30XX;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private RasterizerState _spriteRasterizer;
    private ImageAssetService _imageAssets;
    private DebugMenuSystem _debugMenuSystem;
    private EntityListOverlaySystem _entityListOverlaySystem;
    private TransitionDisplaySystem _transitionDisplaySystem;
    private SceneLoadingCoordinatorSystem _sceneLoadingCoordinatorSystem;
    private CardDisplaySystem _cardDisplaySystem;
    private SealDisplaySystem _sealDisplaySystem;
    private PlayerInputSystem _playerInputSystem;
    private ControllerRumbleSystem _controllerRumbleSystem;
    private UIInteractionSystem _uiInteractionSystem;
	private CardApplicationManagementSystem _cardApplicationManagementSystem;
	private DualColorManagementSystem _dualColorManagementSystem;
	private CursedManagementSystem _cursedManagementSystem;
	private HexManagementSystem _hexManagementSystem;
	private DeckManagementSystem _deckManagementSystem;

    private DrippingBloodDisplaySystem _drippingBloodDisplaySystem;
    private TitleMenuDisplaySystem _titleMenuDisplaySystem;
    private WayStationBackgroundDisplaySystem _wayStationBackgroundDisplaySystem;
    private IncenseDisplaySystem _incenseDisplaySystem;
    private WayStationPoiDisplaySystem _wayStationPoiDisplaySystem;
    private WayStationDialogueSystem _wayStationDialogueSystem;
    private WayStationClimbSettingsModalSystem _wayStationClimbSettingsModalSystem;
    private WayStationSaintsMedalsModalSystem _wayStationSaintsMedalsModalSystem;
	private ClimbPointsAwardDisplaySystem _climbPointsAwardDisplaySystem;
    private BattleSceneSystem _battleSceneSystem;
    private ClimbSceneSystem _climbSceneSystem;
    private AchievementSceneSystem _achievementSceneSystem;
    private TooltipTextDisplaySystem _tooltipTextDisplaySystem;
    private POITitleTooltipDisplaySystem _poiTitleTooltipDisplaySystem;
    private HintTooltipDisplaySystem _hintTooltipDisplaySystem;
    private CardTooltipDisplaySystem _cardTooltipDisplaySystem;
    private ProfilerSystem _profilerSystem;
    private PositionTweenSystem _positionTweenSystem;
    private ParallaxLayerSystem _parallaxLayerSystem;
    private UIElementHighlightSystem _uiElementHighlightSystem;
    private CursorDisplaySystem _cursorDisplaySystem;
    private CursorTrailDisplaySystem _cursorTrailDisplaySystem;
    private HotKeySystem _hotKeySystem;
    private HotKeyProgressRingSystem _hotKeyProgressRingSystem;
    private UIElementBorderDebugSystem _uiElementBorderDebugSystem;
    private DialogDisplaySystem _dialogDisplaySystem;
    private DebugCommandSystem _debugCommandSystem;
    private MedalEquipDebugSystem _medalEquipDebugSystem;
    private EquipmentEquipDebugSystem _equipmentEquipDebugSystem;
    private LocationNameDisplaySystem _locationNameDisplaySystem;
    private RewardModalDisplaySystem _rewardModalDisplaySystem;
    private NarrativeEventModalDisplaySystem _narrativeEventModalDisplaySystem;
    private CardListModalSystem _cardListModalSystem;
    private BoosterPackOpeningDisplaySystem _boosterPackOpeningDisplaySystem;
    private PauseMenuDisplaySystem _pauseMenuDisplaySystem;
    private PauseMenuSliderDisplaySystem _pauseMenuSliderDisplaySystem;
    private GameOverOverlayDisplaySystem _gameOverOverlayDisplaySystem;
    private DisplaySnapshotHost _snapshotHost;
    private readonly DisplaySnapshotLaunchOptions _snapshotOptions;
    private readonly TestFightLaunchOptions _testFightOptions;
    private readonly CardListProfileLaunchOptions _cardListProfileOptions;
#if DEBUG
    private int _cardListProfileRenderedFrames;
    private int _cardListProfileSettleFrames;
    private int _cardListProfileMaxVisible;
#endif
#if DEBUG
    private bool _writePerfReportOnExit;
#endif
    
    // ECS System
    private World _world;

    // Shockwave integration
    private ShockwaveDisplaySystem _shockwaveSystem;
    private RectangularShockwaveDisplaySystem _rectangularShockwaveSystem;
    private AlertDisplaySystem _alertDisplaySystem;
    private RenderTarget2D _sceneRt;
    private RenderTarget2D _ppA;
    private RenderTarget2D _ppB;

	// Alpha-weighted additive blending so source alpha modulates brightness
	private static readonly BlendState AdditiveAlphaOne = new BlendState
	{
		ColorSourceBlend = Blend.SourceAlpha,
		ColorDestinationBlend = Blend.One,
		ColorBlendFunction = BlendFunction.Add,
		AlphaSourceBlend = Blend.One,
		AlphaDestinationBlend = Blend.One,
		AlphaBlendFunction = BlendFunction.Add
	};

    public static bool WindowIsActive { get; private set; } = true;
    public const int VirtualWidth = DisplayMetrics.LogicalWidth;
    public const int VirtualHeight = DisplayMetrics.LogicalHeight;
    public static DisplayMetrics Display { get; private set; } = DisplayMetrics.Calculate(VirtualWidth, VirtualHeight);
    public static Rectangle RenderDestination => Display.RenderDestination;
    
    public Game1(
        DisplaySnapshotLaunchOptions snapshotOptions = null,
        TestFightLaunchOptions testFightOptions = null,
        CardListProfileLaunchOptions cardListProfileOptions = null)
    {
        _snapshotOptions = snapshotOptions;
        _testFightOptions = testFightOptions;
        _cardListProfileOptions = cardListProfileOptions;
        if (_testFightOptions != null)
        {
            TestFightRuntime.Configure(_testFightOptions);
        }
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        
        // Initial setup - will be adjusted by CalculateRenderDestination
        _graphics.PreferredBackBufferWidth = VirtualWidth;
        _graphics.PreferredBackBufferHeight = VirtualHeight;
#if DEBUG
        if (_cardListProfileOptions != null)
        {
            _graphics.SynchronizeWithVerticalRetrace = false;
            IsFixedTimeStep = false;
        }
#endif
        
        _graphics.ApplyChanges();
        IsMouseVisible = false;

        Window.ClientSizeChanged += (sender, args) =>
        {
            if (_graphics.PreferredBackBufferWidth != Window.ClientBounds.Width ||
                _graphics.PreferredBackBufferHeight != Window.ClientBounds.Height)
            {
                _graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
                _graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
                _graphics.ApplyChanges();
                
                CalculateRenderDestination();
            }
        };
    }

    protected override void Initialize()
    {
        LoggingService.Initialize();
        CardUsageTelemetryRuntime.Initialize(
            _snapshotOptions == null && _testFightOptions == null && _cardListProfileOptions == null);
        CalculateRenderDestination();
        // Initialize ECS World
        _world = new World();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        GpuProfiler.Initialize(GraphicsDevice);
        GraphicsDevice.DeviceReset += (_, _) => GpuProfiler.Reset();
        _spriteRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        _imageAssets = new ImageAssetService(Content, GraphicsDevice);
        EventManager.Subscribe<DeleteCachesEvent>(_ => _imageAssets.ClearTransientCaches());

        // Initialize FontSingleton with both fonts
        FontSingleton.Initialize(Content);

        // Initialize Achievement system
        if (_testFightOptions == null && _cardListProfileOptions == null)
        {
            AchievementManager.Initialize(_world.EntityManager);
        }

        // Seed a SceneState entity
        var sceneEntity = _world.EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault();
        if (sceneEntity == null)
        {
            sceneEntity = _world.CreateEntity("SceneState");
            _world.AddComponent(sceneEntity, new SceneState { Current = SceneId.TitleMenu });
        }
        EntityFactory.CreateCardGeometrySettings(_world);
        // Add parent scene systems only
        _drippingBloodDisplaySystem = new DrippingBloodDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, Content);
        _titleMenuDisplaySystem = new TitleMenuDisplaySystem(_world, _spriteBatch);
        _wayStationBackgroundDisplaySystem = new WayStationBackgroundDisplaySystem(_world.EntityManager, _spriteBatch, _imageAssets);
        _incenseDisplaySystem = new IncenseDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, Content);
        _wayStationPoiDisplaySystem = new WayStationPoiDisplaySystem(_world.EntityManager, _spriteBatch, _imageAssets);
        _wayStationDialogueSystem = new WayStationDialogueSystem(_world.EntityManager, _spriteBatch, _imageAssets);
        _wayStationClimbSettingsModalSystem = new WayStationClimbSettingsModalSystem(_world, _spriteBatch, _imageAssets);
        _wayStationSaintsMedalsModalSystem = new WayStationSaintsMedalsModalSystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        _battleSceneSystem = new BattleSceneSystem(_world.EntityManager, _world.SystemManager, _world, GraphicsDevice, _spriteBatch, Content, _imageAssets);
        _climbSceneSystem = new ClimbSceneSystem(_world.EntityManager, _world.SystemManager, _world, GraphicsDevice, _spriteBatch, Content, _imageAssets);
        _achievementSceneSystem = new AchievementSceneSystem(_world.EntityManager, _world.SystemManager, _world, GraphicsDevice, _spriteBatch, Content);
		_sceneLoadingCoordinatorSystem = new SceneLoadingCoordinatorSystem(_world.EntityManager, Content, _imageAssets);
        _debugMenuSystem = new DebugMenuSystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _world.SystemManager);
        _entityListOverlaySystem = new EntityListOverlaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _transitionDisplaySystem = new TransitionDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        ICardOverlayPass[] cardOverlayPasses = CardOverlayPassCatalog.Create(
            _world.EntityManager,
            Content);
        var cardRenderPipeline = new CardRenderPipeline(
            _world.EntityManager,
            GraphicsDevice,
            _spriteBatch,
            cardOverlayPasses);
        _cardDisplaySystem = new CardDisplaySystem(
            _world.EntityManager,
            GraphicsDevice,
            _spriteBatch,
            _imageAssets,
            cardRenderPipeline);
        var sealTexture = _imageAssets.GetRequiredTexture("seal");
        _sealDisplaySystem = new SealDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, sealTexture);
        _dialogDisplaySystem = new DialogDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        var playerInputAdapter = new MonoGamePlayerInputAdapter();
		_climbPointsAwardDisplaySystem = new ClimbPointsAwardDisplaySystem(
			_world.EntityManager,
			GraphicsDevice,
			_spriteBatch,
			_imageAssets,
			playerInputAdapter);
        _playerInputSystem = new PlayerInputSystem(
            _world.EntityManager,
            playerInputAdapter);
        _controllerRumbleSystem = new ControllerRumbleSystem(
            _world.EntityManager,
			playerInputAdapter,
			SaveCache.GetRumbleEnabled());
        _uiInteractionSystem = new UIInteractionSystem(_world.EntityManager);
        _pauseMenuDisplaySystem = new PauseMenuDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _pauseMenuSliderDisplaySystem = new PauseMenuSliderDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _gameOverOverlayDisplaySystem = new GameOverOverlayDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _tooltipTextDisplaySystem = new TooltipTextDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _poiTitleTooltipDisplaySystem = new POITitleTooltipDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _hintTooltipDisplaySystem = new HintTooltipDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        _cardTooltipDisplaySystem = new CardTooltipDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _locationNameDisplaySystem = new LocationNameDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
		_cardApplicationManagementSystem = new CardApplicationManagementSystem(_world.EntityManager);
		_dualColorManagementSystem = new DualColorManagementSystem(_world.EntityManager);
		_cursedManagementSystem = new CursedManagementSystem(_world.EntityManager);
		_hexManagementSystem = new HexManagementSystem(_world.EntityManager);
		_deckManagementSystem = new DeckManagementSystem(_world.EntityManager);
        _profilerSystem = new ProfilerSystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _cursorDisplaySystem = new CursorDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        _cursorTrailDisplaySystem = new CursorTrailDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, Content);
        _hotKeySystem = new HotKeySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _hotKeyProgressRingSystem = new HotKeyProgressRingSystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _world.SystemManager);
        _positionTweenSystem = new PositionTweenSystem(_world.EntityManager);
        _parallaxLayerSystem = new ParallaxLayerSystem(_world.EntityManager, GraphicsDevice);
        _uiElementBorderDebugSystem = new UIElementBorderDebugSystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _uiElementHighlightSystem = new UIElementHighlightSystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _debugCommandSystem = new DebugCommandSystem(_world.EntityManager);
        _medalEquipDebugSystem = new MedalEquipDebugSystem(_world.EntityManager);
        _equipmentEquipDebugSystem = new EquipmentEquipDebugSystem(_world.EntityManager);
        _world.AddSystem(_drippingBloodDisplaySystem);
        _world.AddSystem(_titleMenuDisplaySystem);
        _world.AddSystem(_wayStationBackgroundDisplaySystem);
        _world.AddSystem(_incenseDisplaySystem);
        _world.AddSystem(_wayStationPoiDisplaySystem);
        _world.AddSystem(_wayStationDialogueSystem);
        _world.AddSystem(_wayStationClimbSettingsModalSystem);
        _world.AddSystem(_wayStationSaintsMedalsModalSystem);
		_world.AddSystem(_climbPointsAwardDisplaySystem);
        _world.AddSystem(_battleSceneSystem);
        _world.AddSystem(_climbSceneSystem);
        _world.AddSystem(_achievementSceneSystem);
		_world.AddSystem(_sceneLoadingCoordinatorSystem);
        _world.AddSystem(new TimerSchedulerSystem(_world.EntityManager));
        if (CardUsageTelemetryRuntime.Store != null)
        {
            _world.AddSystem(new CardUsageTrackingSystem(
                _world.EntityManager,
                CardUsageTelemetryRuntime.Store));
        }
		_world.AddSystem(_cardApplicationManagementSystem);
		_world.AddSystem(_dualColorManagementSystem);
		_world.AddSystem(_cursedManagementSystem);
		_world.AddSystem(_hexManagementSystem);
		_world.AddSystem(_deckManagementSystem);
        _world.AddSystem(_debugMenuSystem);
        _world.AddSystem(_entityListOverlaySystem);
        _world.AddSystem(_transitionDisplaySystem);
        _world.AddSystem(_cardDisplaySystem);
        _world.AddSystem(_sealDisplaySystem);
        _world.AddSystem(_dialogDisplaySystem);
        _world.AddSystem(new ModalAnimationSystem(_world.EntityManager), SystemUpdatePhase.Input);
        _world.AddSystem(_playerInputSystem, SystemUpdatePhase.Input);
        _world.AddSystem(_controllerRumbleSystem, SystemUpdatePhase.Input);
        _world.AddSystem(_uiInteractionSystem, SystemUpdatePhase.Interaction);
        _world.AddSystem(_pauseMenuDisplaySystem);
        _world.AddSystem(_pauseMenuSliderDisplaySystem);
        _world.AddSystem(_gameOverOverlayDisplaySystem);
        _world.AddSystem(_tooltipTextDisplaySystem);
        _world.AddSystem(_poiTitleTooltipDisplaySystem);
        _world.AddSystem(_hintTooltipDisplaySystem);
        _world.AddSystem(_cardTooltipDisplaySystem);
        _world.AddSystem(_profilerSystem);
        _world.AddSystem(_locationNameDisplaySystem);
        // _world.AddSystem(_worldMapSystem);
        _world.AddSystem(_cursorDisplaySystem, SystemUpdatePhase.Presentation);
        _world.AddSystem(_cursorTrailDisplaySystem, SystemUpdatePhase.Presentation);
        _world.AddSystem(_hotKeySystem);
        _world.AddSystem(_hotKeyProgressRingSystem);
        _world.AddLateSystem(_positionTweenSystem);
        _world.AddLateSystem(_parallaxLayerSystem);
        _world.AddSystem(_uiElementBorderDebugSystem);
        _world.AddSystem(_debugCommandSystem);
        _world.AddSystem(_medalEquipDebugSystem);
        _world.AddSystem(_equipmentEquipDebugSystem);
        _rewardModalDisplaySystem = new RewardModalDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        _world.AddSystem(_rewardModalDisplaySystem);
        _narrativeEventModalDisplaySystem = new NarrativeEventModalDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        _world.AddSystem(_narrativeEventModalDisplaySystem);
        _cardListModalSystem = new CardListModalSystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets);
        _world.AddSystem(_cardListModalSystem);
        _world.AddSystem(new CollectionProgressionSystem(_world.EntityManager));
        _boosterPackOpeningDisplaySystem = new BoosterPackOpeningDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, _imageAssets, playerInputAdapter);
        _world.AddSystem(_boosterPackOpeningDisplaySystem);
        _world.AddSystem(new RunDeckLifecycleSystem(_world.EntityManager));
        _world.AddSystem(new ClimbEventSystem(_world.EntityManager));
        _world.AddSystem(new ClimbEncounterSystem(_world.EntityManager));
        // Global music manager
        _world.AddSystem(new MusicManagerSystem(_world.EntityManager, Content));
        // Global sound effect manager
        _world.AddSystem(new SoundEffectManagerSystem(_world.EntityManager, Content));
        _shockwaveSystem = new ShockwaveDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, Content);
        _world.AddSystem(_shockwaveSystem);
        _rectangularShockwaveSystem = new RectangularShockwaveDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch, Content);
        _world.AddSystem(_rectangularShockwaveSystem);
        _world.AddSystem(new CursorEmptySelectDisplaySystem(_world.EntityManager, GraphicsDevice));
        _world.AddSystem(new UISelectDisplaySystem(_world.EntityManager, GraphicsDevice));
        _world.AddSystem(new JigglePulseDisplaySystem(_world.EntityManager));
        _alertDisplaySystem = new AlertDisplaySystem(_world.EntityManager, GraphicsDevice, _spriteBatch);
        _world.AddSystem(_alertDisplaySystem);
        EventManager.Subscribe<PlayerCommandEvent>(OnPlayerCommand);

        // Mark persistent entities
        _world.AddComponent(sceneEntity, new DontDestroyOnLoad());
        var cvsEntity = _world.EntityManager.GetEntity("CardGeometrySettings");
        if (cvsEntity != null)
        {
            _world.AddComponent(cvsEntity, new DontDestroyOnLoad());
        }
        // Allocate render targets
        AllocateRenderTargets();

        _snapshotHost = DisplaySnapshotHost.TryCreate(_snapshotOptions, this, GraphicsDevice, Content);
        _snapshotHost?.OnGameReady(_world, sceneEntity, _spriteBatch);

        if (_testFightOptions != null)
        {
            TestFightSetupService.PrepareWorld(_world);
            EventManager.Publish(new ShowTransition
            {
                Scene = SceneId.Battle,
                SkipWipe = true,
            });
        }

#if DEBUG
        if (_cardListProfileOptions != null)
        {
            PrepareCardListProfile();
        }
#endif
    }
    
    protected override void Update(GameTime gameTime)
    {
        bool skipProfile = _snapshotHost?.IsActive == true || IsCardListProfileSettling();
        FrameProfiler.BeginUpdateIteration(skipProfile);

#if DEBUG
        UpdateCardListProfileScroll();
#endif

#if DEBUG
        FrameProfiler.MeasureInclusive("Game1.Update", () => RunUpdate(gameTime));
#else
        RunUpdate(gameTime);
#endif
    }

    private void RunUpdate(GameTime gameTime)
    {
        LoggingService.Tick();
        WindowIsActive = IsActive;
        IsMouseVisible = false;

        _world.Update(gameTime);

#if DEBUG
        var sceneEntity = _world.EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault();
        var sceneId = sceneEntity?.GetComponent<SceneState>()?.Current ?? SceneId.None;
        FrameProfiler.SetActiveScene(sceneId);
#endif

        base.Update(gameTime);
    }

    private void OnPlayerCommand(PlayerCommandEvent command)
    {
        if (command == null) return;
        if (command.Command == PlayerCommand.QuitApplication)
        {
#if DEBUG
            _writePerfReportOnExit = true;
#endif
            Exit();
            return;
        }

        if (_snapshotHost?.IsActive == true) return;
        switch (command.Command)
        {
            case PlayerCommand.ToggleFullScreen:
                ToggleFullScreen();
                break;
            case PlayerCommand.ToggleDebugMenu:
                ToggleDebugMenu();
                break;
            case PlayerCommand.ToggleEntityList:
                ToggleEntityListOverlay();
                break;
            case PlayerCommand.DealDebugDamage:
                _debugCommandSystem.Debug_PlayerDealDamage(999);
                break;
            case PlayerCommand.ToggleProfiler:
                ToggleProfiler();
                break;
        }
    }

    private void ToggleProfiler()
    {
        var entity = _world.EntityManager
            .GetEntitiesWithComponent<ProfilerOverlay>()
            .FirstOrDefault();
        if (entity == null)
        {
            entity = _world.EntityManager.CreateEntity("ProfilerOverlay");
            _world.EntityManager.AddComponent(entity, new ProfilerOverlay { IsOpen = true });
            return;
        }

        var profiler = entity.GetComponent<ProfilerOverlay>();
        profiler.IsOpen = !profiler.IsOpen;
    }

    public RasterizerState GetRasterizerState() => _spriteRasterizer;

    protected override void Draw(GameTime gameTime)
    {
#if DEBUG
        FrameProfiler.BeginRenderFrame(_snapshotHost?.IsActive == true || IsCardListProfileSettling());
        try
        {
            FrameProfiler.MeasureInclusive("Game1.Draw.Commands", () => DrawGame(gameTime));
        }
        finally
        {
            FrameProfiler.EndRenderFrame();
            AdvanceCardListProfile();
        }
#else
        DrawGame(gameTime);
#endif
    }

    private void DrawGame(GameTime gameTime)
    {
#if DEBUG
        FrameProfiler.Measure("Game1.Draw.SceneSetupAndDrawScene", DrawSceneSetup);
        Texture2D finalTexture = _sceneRt;
        bool shouldPresent = false;
        FrameProfiler.Measure("Game1.Draw.ShaderComposite", () =>
        {
            shouldPresent = TryCompositeDrawEffects(out finalTexture);
        });
        if (!shouldPresent)
        {
            return;
        }
        FrameProfiler.Measure("Game1.Draw.Present", () => PresentToBackbuffer(finalTexture, gameTime));
#else
        DrawSceneSetup();
        if (!TryCompositeDrawEffects(out Texture2D finalTexture))
        {
            return;
        }
        PresentToBackbuffer(finalTexture, gameTime);
#endif
    }

    private void DrawSceneSetup()
    {
        EnsureRenderTargetsMatchVirtual();
        GraphicsDevice.SetRenderTarget(_sceneRt);
        GraphicsDevice.Clear(_snapshotHost?.IsActive == true ? Color.Black : Color.CornflowerBlue);
        DrawScene();
    }

    private bool TryCompositeDrawEffects(out Texture2D finalTexture)
    {
        bool hasCircularWaves = _shockwaveSystem != null && _shockwaveSystem.HasActiveWaves;
        bool hasRectangularWaves = _rectangularShockwaveSystem != null && _rectangularShockwaveSystem.HasActiveWaves;

        finalTexture = _sceneRt;

        if (ShaderRuntimeOptions.ShadersEnabled && (hasCircularWaves || hasRectangularWaves))
        {
            EnsurePostProcessTargets();
            if (hasCircularWaves)
            {
                RenderTarget2D dest = (finalTexture == _ppA) ? _ppB : _ppA;
                _shockwaveSystem.Composite(finalTexture, _ppA, _ppB, dest);
                finalTexture = dest;
            }

            if (hasRectangularWaves)
            {
                RenderTarget2D dest = (finalTexture == _ppA) ? _ppB : _ppA;
                _rectangularShockwaveSystem.Composite(finalTexture, _ppA, _ppB, dest);
                finalTexture = dest;
            }
        }

        if (_snapshotHost?.IsActive == true && _snapshotHost.TickAfterDraw(_sceneRt))
        {
            return false;
        }

        return true;
    }

    private void PresentToBackbuffer(Texture2D finalTexture, GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp);
        _spriteBatch.Draw(finalTexture, RenderDestination, Color.White);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

#if DEBUG
    private void MeasureInclusiveSceneDraw(string name, Action draw) =>
        FrameProfiler.MeasureInclusive(name, draw);
#else
    private void MeasureInclusiveSceneDraw(string name, Action draw) => draw();
#endif

    private void DrawScene()
    {
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, _spriteRasterizer, null, Display.SpriteBatchTransform);

        if (_snapshotHost?.IsActive == true)
        {
            _snapshotHost.DrawScene(_spriteBatch);
            FrameProfiler.Measure("PauseMenuDisplaySystem.Draw.Snapshot", _pauseMenuDisplaySystem.Draw);
            FrameProfiler.Measure("PauseMenuSliderDisplaySystem.Draw.Snapshot", _pauseMenuSliderDisplaySystem.Draw);
            _spriteBatch.End();
            return;
        }

        // Character-event dialogue deliberately isolates the dialogue against the
        // undimmed Climb background. All normal scene and global foreground draws
        // are skipped until the correlated dialogue completes.
        var scene = _world.EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault().GetComponent<SceneState>();
        bool backgroundOnlyClimbDialogue = IsBackgroundOnlyClimbDialogue(scene);
        if (backgroundOnlyClimbDialogue)
        {
            MeasureInclusiveSceneDraw("ClimbSceneSystem.DrawBackgroundOnly", _climbSceneSystem.DrawBackgroundOnly);
            FrameProfiler.Measure("DialogDisplaySystem.Draw", _dialogDisplaySystem.Draw);
            DrawCursor();
            return;
        }

        // Delegate drawing to active parent systems
        switch(scene.Current)
        {
            case SceneId.TitleMenu:
            {
                FrameProfiler.Measure("DrippingBloodDisplaySystem.Draw", _drippingBloodDisplaySystem.Draw);
                FrameProfiler.Measure("TitleMenuDisplaySystem.Draw", _titleMenuDisplaySystem.Draw);
                break;
            }
            case SceneId.WayStation:
            {
                FrameProfiler.Measure("WayStationBackgroundDisplaySystem.Draw", _wayStationBackgroundDisplaySystem.Draw);
                FrameProfiler.Measure("IncenseDisplaySystem.Draw", _incenseDisplaySystem.Draw);
                FrameProfiler.Measure("WayStationPoiDisplaySystem.Draw", _wayStationPoiDisplaySystem.Draw);
                FrameProfiler.Measure("WayStationDialogueSystem.Draw", _wayStationDialogueSystem.Draw);
                FrameProfiler.Measure("WayStationClimbSettingsModalSystem.Draw", _wayStationClimbSettingsModalSystem.Draw);
                FrameProfiler.Measure("WayStationSaintsMedalsModalSystem.Draw", _wayStationSaintsMedalsModalSystem.Draw);
                break;
            }
            case SceneId.Battle:
            {
                MeasureInclusiveSceneDraw("BattleSceneSystem.Draw", _battleSceneSystem.Draw);
                // Additive trail pass for card move trails
                _spriteBatch.End();
				_spriteBatch.Begin(SpriteSortMode.Immediate, AdditiveAlphaOne, SamplerState.AnisotropicClamp, DepthStencilState.None, _spriteRasterizer, null, Display.SpriteBatchTransform);
                _battleSceneSystem.DrawAdditive();
                _spriteBatch.End();
                // Resume normal alpha-blend UI drawing state
                _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, _spriteRasterizer, null, Display.SpriteBatchTransform);
                break;
            }
            case SceneId.Climb:
            {
                MeasureInclusiveSceneDraw("ClimbSceneSystem.Draw", _climbSceneSystem.Draw);
                break;
            }
            case SceneId.Achievement:
            {
                MeasureInclusiveSceneDraw("AchievementSceneSystem.Draw", _achievementSceneSystem.Draw);
                break;
            }
        }
        FrameProfiler.Measure("HotKeySystem.Draw", _hotKeySystem.Draw);
        FrameProfiler.Measure("HotKeyProgressRingSystem.Draw", _hotKeyProgressRingSystem.Draw);
        FrameProfiler.Measure("LocationNameDisplaySystem.Draw", _locationNameDisplaySystem.Draw);
        FrameProfiler.Measure("RewardModalDisplaySystem.Draw", _rewardModalDisplaySystem.Draw);
        FrameProfiler.Measure("NarrativeEventModalDisplaySystem.Draw", _narrativeEventModalDisplaySystem.Draw);
        FrameProfiler.Measure("CardListModalSystem.DrawBackdrop", _cardListModalSystem.DrawBackdrop);
        if (_cardListModalSystem?.IsSelectableOpen() == true)
        {
            FrameProfiler.Measure("UIElementHighlightSystem.Draw.CardListModal", _uiElementHighlightSystem.Draw);
        }
        FrameProfiler.Measure("CardListModalSystem.DrawForeground", _cardListModalSystem.DrawForeground);
        FrameProfiler.Measure("BoosterPackOpeningDisplaySystem.Draw", _boosterPackOpeningDisplaySystem.Draw);
        FrameProfiler.Measure("POITitleTooltipDisplaySystem.Draw", _poiTitleTooltipDisplaySystem.Draw);
        FrameProfiler.Measure("TooltipDisplaySystem.Draw", _tooltipTextDisplaySystem.Draw);
        FrameProfiler.Measure("HintTooltipDisplaySystem.Draw", _hintTooltipDisplaySystem.Draw);
        FrameProfiler.Measure("CardTooltipDisplaySystem.Draw", _cardTooltipDisplaySystem.Draw);
        FrameProfiler.Measure("AlertDisplaySystem.Draw", _alertDisplaySystem.Draw);
		FrameProfiler.Measure("ClimbPointsAwardDisplaySystem.Draw", _climbPointsAwardDisplaySystem.Draw);
        FrameProfiler.Measure("ProfilerSystem.Draw", _profilerSystem.Draw);
        FrameProfiler.Measure("DebugMenuSystem.Draw", _debugMenuSystem.Draw);
        FrameProfiler.Measure("EntityListOverlaySystem.Draw", _entityListOverlaySystem.Draw);
        FrameProfiler.Measure("DialogDisplaySystem.Draw", _dialogDisplaySystem.Draw);
        FrameProfiler.Measure("PauseMenuDisplaySystem.Draw", _pauseMenuDisplaySystem.Draw);
        FrameProfiler.Measure("PauseMenuSliderDisplaySystem.Draw", _pauseMenuSliderDisplaySystem.Draw);
        FrameProfiler.Measure("GameOverOverlayDisplaySystem.Draw", _gameOverOverlayDisplaySystem.Draw);
        FrameProfiler.Measure("TransitionDisplaySystem.Draw", _transitionDisplaySystem.Draw);
        FrameProfiler.Measure("UIElementBorderDebugSystem.Draw", _uiElementBorderDebugSystem.Draw);
        // Cursor blur trail (additive pass before cursor) — skip in card debug mode
        DrawCursor();
    }

    private bool IsBackgroundOnlyClimbDialogue(SceneState scene)
    {
        if (scene?.Current != SceneId.Climb) return false;
        var state = _world.EntityManager.GetEntitiesWithComponent<DialogOverlayState>()
            .FirstOrDefault()
            ?.GetComponent<DialogOverlayState>();
        return state?.IsActive == true && state.BackgroundOnly;
    }

    private void DrawCursor()
    {
        _spriteBatch.End();
        if (_snapshotHost?.ShouldSkipGlobalOverlays == true) return;

        if (ShaderRuntimeOptions.ShadersEnabled)
        {
            _cursorTrailDisplaySystem.DrawTrail(_sceneRt);
        }

        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, _spriteRasterizer, null, Display.SpriteBatchTransform);
        FrameProfiler.Measure("CursorDisplaySystem.Draw", _cursorDisplaySystem.Draw);
        _spriteBatch.End();
    }

#if DEBUG
    private void PrepareCardListProfile()
    {
        var definitions = CardFactory.GetAllCards()
            .Where(pair => pair.Value != null && !pair.Value.IsWeapon && !pair.Value.IsToken)
            .Select(pair => pair.Key)
            .OrderBy(id => id)
            .ToArray();
        if (definitions.Length == 0)
        {
            throw new CardListProfileSetupException("No non-weapon cards are registered for card-list-profile");
        }

        var cards = new List<Entity>(60);
        CardData.CardColor[] colors = { CardData.CardColor.Red, CardData.CardColor.White, CardData.CardColor.Black };
        for (int i = 0; i < 60; i++)
        {
            var card = EntityFactory.CreateCardFromDefinition(
                _world.EntityManager,
                definitions[i % definitions.Length].ToKey(),
                colors[i % colors.Length],
                index: i,
                isUpgraded: i % 5 == 0);
            if (card != null) cards.Add(card);
        }
        if (cards.Count != 60)
        {
            throw new CardListProfileSetupException($"Expected 60 benchmark cards but created {cards.Count}");
        }

        _cardListModalSystem.OpenInventoryForSnapshot("60 Card Performance Profile", cards);
        GpuProfiler.ResumeCapture();
        Console.WriteLine($"[CardListProfile] prepared {cards.Count} cards at {Display.RenderWidth}x{Display.RenderHeight}");
    }

    private void UpdateCardListProfileScroll()
    {
        if (_cardListProfileOptions == null || IsCardListProfileSettling()) return;
        const int traversalFrames = 120;
        int phase = _cardListProfileRenderedFrames % (traversalFrames * 2);
        float fraction = phase <= traversalFrames
            ? phase / (float)traversalFrames
            : 2f - phase / (float)traversalFrames;
        _cardListModalSystem.SetScrollFractionForDiagnostics(fraction);
    }

    private bool IsCardListProfileSettling() =>
        _cardListProfileOptions != null &&
        _cardListProfileRenderedFrames >= CardListProfileLaunchOptions.WarmupFrames + CardListProfileLaunchOptions.MeasuredFrames;

    private void AdvanceCardListProfile()
    {
        if (_cardListProfileOptions == null) return;
        _cardListProfileMaxVisible = Math.Max(_cardListProfileMaxVisible, _cardListModalSystem.VisibleCardCountForDiagnostics);
        _cardListProfileRenderedFrames++;

        if (_cardListProfileRenderedFrames == CardListProfileLaunchOptions.WarmupFrames)
        {
            FrameProfiler.ResetSession();
            Console.WriteLine("[CardListProfile] warm-up complete; measuring 300 frames");
            return;
        }

        int measurementEnd = CardListProfileLaunchOptions.WarmupFrames + CardListProfileLaunchOptions.MeasuredFrames;
        if (_cardListProfileRenderedFrames == measurementEnd)
        {
            GpuProfiler.PauseCapture();
            Console.WriteLine($"[CardListProfile] measurement complete; resolving {GpuProfiler.PendingQueries} query pairs without blocking");
            return;
        }

        if (_cardListProfileRenderedFrames <= measurementEnd) return;
        _cardListProfileSettleFrames++;
        if (GpuProfiler.PendingQueries == 0 || _cardListProfileSettleFrames >= CardListProfileLaunchOptions.MaxQuerySettleFrames)
        {
            FinishCardListProfile();
        }
    }

    private void FinishCardListProfile()
    {
        const string scope = "CardListModalSystem.DrawForeground";
        bool hasCpu = FrameProfiler.TryGetSessionStats(scope, gpu: false, out var cpu);
        bool hasGpu = FrameProfiler.TryGetSessionStats(scope, gpu: true, out var gpu);
        bool cpuPass = hasCpu && cpu.P95Ms < 8 && cpu.MaxMs <= 16.67;
        bool gpuPass = GpuProfiler.TimerSupported && hasGpu && gpu.P95Ms < 8 && gpu.MaxMs <= 16.67;
        bool pass = cpuPass && gpuPass;
        const string reportPath = "logs/card-list-profile-report.txt";
        FrameProfiler.WriteReport(reportPath, FrameProfiler.ActiveSceneId, ShaderRuntimeOptions.ShadersEnabled);
        Console.WriteLine($"[CardListProfile] CPU p95={(hasCpu ? cpu.P95Ms.ToString("0.00") : "n/a")} ms max={(hasCpu ? cpu.MaxMs.ToString("0.00") : "n/a")} ms: {(cpuPass ? "PASS" : "FAIL")}");
        Console.WriteLine($"[CardListProfile] GPU p95={(hasGpu ? gpu.P95Ms.ToString("0.00") : "n/a")} ms max={(hasGpu ? gpu.MaxMs.ToString("0.00") : "n/a")} ms: {(gpuPass ? "PASS" : "FAIL")}");
        Console.WriteLine($"[CardListProfile] max visible submissions={_cardListProfileMaxVisible}, pending={GpuProfiler.PendingQueries}, report={reportPath}");
        Console.WriteLine($"[CardListProfile] {(pass ? "PASS" : "FAIL")}");
        Environment.ExitCode = pass ? 0 : 1;
        Exit();
    }
#else
    private bool IsCardListProfileSettling() => false;
#endif

	protected override void UnloadContent()
	{
		CardUsageTelemetryRuntime.ExportCsv();
#if DEBUG
		if (_writePerfReportOnExit)
		{
			try
			{
				var sceneEntity = _world.EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault();
				var sceneAtQuit = sceneEntity?.GetComponent<SceneState>()?.Current ?? SceneId.None;
				FrameProfiler.WriteReport("logs/performance-report.txt", sceneAtQuit, ShaderRuntimeOptions.ShadersEnabled);
			}
			catch (Exception ex)
			{
				System.Diagnostics.Debug.WriteLine($"[FrameProfiler] Report failed: {ex.Message}");
			}
		}
#endif
			LoggingService.Flush();
			GpuProfiler.Shutdown();
		try { _imageAssets?.Dispose(); } catch { }
	try { FullScreenRenderTargetPool.DisposeAll(GraphicsDevice); } catch { }
	try { CardShaderSurfacePool.DisposeAll(GraphicsDevice); } catch { }
		base.UnloadContent();
	}

    private void AllocateRenderTargets()
    {
        _sceneRt = new RenderTarget2D(GraphicsDevice, Display.RenderWidth, Display.RenderHeight, false,
            SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }

    private void EnsurePostProcessTargets()
    {
        if (_ppA != null && _ppA.Width == Display.RenderWidth && _ppA.Height == Display.RenderHeight &&
            _ppB != null && _ppB.Width == Display.RenderWidth && _ppB.Height == Display.RenderHeight)
        {
            return;
        }

        _ppA?.Dispose();
        _ppB?.Dispose();
        _ppA = new RenderTarget2D(GraphicsDevice, Display.RenderWidth, Display.RenderHeight, false, SurfaceFormat.Color, DepthFormat.None);
        _ppB = new RenderTarget2D(GraphicsDevice, Display.RenderWidth, Display.RenderHeight, false, SurfaceFormat.Color, DepthFormat.None);
    }

    private void EnsureRenderTargetsMatchVirtual()
    {
        if (_sceneRt == null || _sceneRt.Width != Display.RenderWidth || _sceneRt.Height != Display.RenderHeight)
        {
            _sceneRt?.Dispose();
            _ppA?.Dispose();
            _ppB?.Dispose();
            AllocateRenderTargets();
        }
    }
    
    private void CalculateRenderDestination()
    {
        var presentation = GraphicsDevice.PresentationParameters;
        DisplayMetrics nextDisplay = DisplayMetrics.Calculate(
            presentation.BackBufferWidth,
            presentation.BackBufferHeight,
            _cardListProfileOptions?.RenderScale ?? (_snapshotOptions == null ? null : _snapshotOptions.RenderScaleOverride ?? 1f));
        if (Display.RenderWidth != nextDisplay.RenderWidth || Display.RenderHeight != nextDisplay.RenderHeight)
        {
            FullScreenRenderTargetPool.DisposeAll(GraphicsDevice);
            CardShaderSurfacePool.DisposeAll(GraphicsDevice);
        }
        Display = nextDisplay;
        Console.WriteLine(
            $"[Display] client={Window.ClientBounds.Width}x{Window.ClientBounds.Height} " +
            $"backbuffer={Display.BackBufferWidth}x{Display.BackBufferHeight} " +
            $"render={Display.RenderWidth}x{Display.RenderHeight} " +
            $"destination={Display.RenderDestination} " +
            $"scale={Display.RenderScaleX:0.###}x{Display.RenderScaleY:0.###}");
    }

    private void ToggleFullScreen()
    {
        if (!_graphics.IsFullScreen)
        {
            // Borderless full screen: set to desktop resolution to cover taskbar and top bar
            _graphics.HardwareModeSwitch = false;
            _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            _graphics.IsFullScreen = true;
        }
        else
        {
            // Windowed mode: reset to virtual resolution
            _graphics.IsFullScreen = false;
            _graphics.PreferredBackBufferWidth = VirtualWidth;
            _graphics.PreferredBackBufferHeight = VirtualHeight;
        }
        _graphics.ApplyChanges();
        
        // Recalculate rendering destination to stretch and letterbox the 1920x1080 content
        CalculateRenderDestination();
    }

    private void ToggleDebugMenu()
    {
        var em = _world.EntityManager;
        var menuEntity = em.GetEntitiesWithComponent<DebugMenu>().FirstOrDefault();
        if (menuEntity == null)
        {
            menuEntity = _world.CreateEntity("DebugMenu");
            _world.AddComponent(menuEntity, new DebugMenu { IsOpen = true });
            _world.AddComponent(menuEntity, new Transform { Position = new Vector2(1800, 200), ZOrder = 5000 });
            _world.AddComponent(menuEntity, new UIElement { Bounds = new Rectangle(1750, 150, 150, 300), IsInteractable = true });
            _world.AddComponent(menuEntity, new InputContext
            {
                Id = "diagnostic.debug-menu",
                Priority = 900,
                IsActive = true,
                IsDiagnostic = true,
            });
            _world.AddComponent(menuEntity, new InputContextMember
            {
                ContextId = "diagnostic.debug-menu",
            });
            _world.AddComponent(menuEntity, new DontDestroyOnLoad());
        }
        else
        {
            var menu = menuEntity.GetComponent<DebugMenu>();
            menu.IsOpen = !menu.IsOpen;
            var context = menuEntity.GetComponent<InputContext>();
            if (context != null) context.IsActive = menu.IsOpen;
        }
    }

    private void ToggleEntityListOverlay()
    {
        var em = _world.EntityManager;
        var overlayEntity = em.GetEntitiesWithComponent<EntityListOverlay>().FirstOrDefault();
        if (overlayEntity == null)
        {
            overlayEntity = _world.CreateEntity("EntityListOverlay");
            _world.AddComponent(overlayEntity, new EntityListOverlay { IsOpen = true });
            _world.AddComponent(overlayEntity, new Transform { Position = Vector2.Zero, ZOrder = 50000 });
            _world.AddComponent(overlayEntity, new UIElement { IsInteractable = true });
            _world.AddComponent(overlayEntity, new InputContext
            {
                Id = "diagnostic.entity-list",
                Priority = 910,
                IsActive = true,
                IsDiagnostic = true,
            });
            _world.AddComponent(overlayEntity, new InputContextMember
            {
                ContextId = "diagnostic.entity-list",
            });
            _world.AddComponent(overlayEntity, new DontDestroyOnLoad());
        }
        else
        {
            var o = overlayEntity.GetComponent<EntityListOverlay>();
            o.IsOpen = !o.IsOpen;
            var context = overlayEntity.GetComponent<InputContext>();
            if (context != null) context.IsActive = o.IsOpen;
        }
    }
}
