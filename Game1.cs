#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.Diagnostics.Snapshots;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Authoring.Text;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Integration;
using Crusaders30XX.ECS.DataOriented.Integration.Host;
using Crusaders30XX.ECS.DataOriented.Integration.Host.Resources;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX;

/// <summary>
/// MonoGame host for the data-oriented runtime. MonoGame resources and hardware input remain at
/// this boundary; the ECS owns gameplay state, scheduling, events, and render extraction.
/// </summary>
public sealed class Game1 : Game, IHostCommandTarget, IHostAudioRequestSink, IHostShaderRequestSink,
    IHostRumbleRequestSink
{
    private static readonly BlendState AdditiveAlphaOne = new()
    {
        ColorSourceBlend = Blend.SourceAlpha,
        ColorDestinationBlend = Blend.One,
        ColorBlendFunction = BlendFunction.Add,
        AlphaSourceBlend = Blend.One,
        AlphaDestinationBlend = Blend.One,
        AlphaBlendFunction = BlendFunction.Add,
    };

    private readonly GraphicsDeviceManager graphics;
    private readonly DisplaySnapshotLaunchOptions? snapshotOptions;
    private readonly TestFightLaunchOptions? testFightOptions;
    private readonly CardListProfileLaunchOptions? cardListProfileOptions;
    private readonly MonoGamePlayerInputAdapter hardwareInput = new();
    private readonly CentralInputFrameAdapter inputAdapter = new();
    private readonly PresentationRequestDrainAdapter requestDrain = new();
    private readonly HostRuntimeDiagnosticsAdapter diagnostics = new();

    private SpriteBatch? spriteBatch;
    private RasterizerState? spriteRasterizer;
    private RenderTarget2D? sceneTarget;
    private DataOrientedGameRuntime? runtime;
    private HostCommandDispatcher? commandDispatcher;
    private OrderedRenderPacketHostAdapter<Texture2D>? renderAdapter;
    private CatalogHostTextureResolver<Texture2D>? textureResolver;
    private MonoGameTextureResourceFactory? textureFactory;
    private MonoGameRenderDevice? renderDevice;
    private MonoGameTextRenderSink? textRenderSink;
    private readonly StaticTextPresentationCatalog textCatalog = new();
    private MetaAuthoredScene? snapshotScene;
    private SnapshotLaunchOutput? snapshotLaunchOutput;
    private long hostFrame;
    private int drawnSnapshotFrames;
    private float rumbleStrength = 1f;

    public static bool WindowIsActive { get; private set; } = true;
    public const int VirtualWidth = DisplayMetrics.LogicalWidth;
    public const int VirtualHeight = DisplayMetrics.LogicalHeight;
    public static DisplayMetrics Display { get; private set; } =
        DisplayMetrics.Calculate(VirtualWidth, VirtualHeight);
    public static Rectangle RenderDestination => Display.RenderDestination;

    public Game1(
        DisplaySnapshotLaunchOptions? snapshotOptions = null,
        TestFightLaunchOptions? testFightOptions = null,
        CardListProfileLaunchOptions? cardListProfileOptions = null)
    {
        this.snapshotOptions = snapshotOptions;
        this.testFightOptions = testFightOptions;
        this.cardListProfileOptions = cardListProfileOptions;

        graphics = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = VirtualWidth,
            PreferredBackBufferHeight = VirtualHeight,
        };
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        IsMouseVisible = false;
        Window.ClientSizeChanged += (_, _) =>
        {
            if (graphics.PreferredBackBufferWidth == Window.ClientBounds.Width &&
                graphics.PreferredBackBufferHeight == Window.ClientBounds.Height)
                return;
            graphics.PreferredBackBufferWidth = Window.ClientBounds.Width;
            graphics.PreferredBackBufferHeight = Window.ClientBounds.Height;
            graphics.ApplyChanges();
            CalculateRenderDestination();
        };
    }

    protected override void Initialize()
    {
        CalculateRenderDestination();
        base.Initialize();
    }

    protected override void LoadContent()
    {
        spriteBatch = new SpriteBatch(GraphicsDevice);
        spriteRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
        sceneTarget = CreateSceneTarget();
        textureFactory = new MonoGameTextureResourceFactory(this);
        textureResolver = new CatalogHostTextureResolver<Texture2D>(
            ProductionHostTextureCatalog.Create(),
            textureFactory);
        textureResolver.Preload();
        renderDevice = new MonoGameRenderDevice(spriteBatch, spriteRasterizer);
        textRenderSink = new MonoGameTextRenderSink(spriteBatch, this);
        renderAdapter = new OrderedRenderPacketHostAdapter<Texture2D>();
        commandDispatcher = new HostCommandDispatcher(snapshotOptions is not null);

        SceneGroup initialScene = snapshotOptions is not null || cardListProfileOptions is not null
            ? SceneGroup.Snapshot
            : testFightOptions is not null
                ? SceneGroup.Battle
                : SceneGroup.TitleMenu;
        runtime = DataOrientedGameRuntime.Create(
            initialScene,
            save: null,
            snapshotMode: snapshotOptions is not null);

        if (snapshotOptions is not null)
        {
            SnapshotLaunchOutput launch = ResolveSnapshotLaunch(snapshotOptions);
            snapshotLaunchOutput = launch;
            snapshotScene = new SnapshotFixtureMaterializer().Materialize(
                runtime.World,
                snapshotOptions.FixtureId,
                launch.MaterializerVariantIndex);
            Console.WriteLine(
                $"[DataOriented] Snapshot '{snapshotOptions.FixtureId}' launch {launch.LaunchVariantIndex} " +
                $"materializer {launch.MaterializerVariantIndex} ready");
        }
        else if (cardListProfileOptions is not null)
        {
            snapshotScene = new SnapshotFixtureMaterializer().Materialize(
                runtime.World,
                "card-list-modal-middle",
                0);
        }
        else if (testFightOptions is not null)
        {
            DataOrientedTestFightFixture fixture = BuildTestFightFixture(testFightOptions);
            runtime.BeginTestCombat(in fixture);
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (runtime is null || commandDispatcher is null)
        {
            base.Update(gameTime);
            return;
        }

        WindowIsActive = IsActive;
        Crusaders30XX.ECS.Input.PlayerInputFrame captured = hardwareInput.Capture(
            WindowIsActive,
            RenderDestination,
            VirtualWidth,
            VirtualHeight);
        var submission = inputAdapter.Convert(in captured, RenderDestination, VirtualWidth, VirtualHeight);
        runtime.Parallax.Input = new PresentationFrameInput(
            submission.PlayerInput.Frame.PointerPosition,
            new Vector2(VirtualWidth, VirtualHeight));
        runtime.SubmitInput(in submission);
        Crusaders30XX.ECS.DataOriented.Components.PlayerInputFrame frame = submission.PlayerInput.Frame;
        HandleSmokeNavigation(in frame);
        runtime.Update(gameTime.ElapsedGameTime);
        commandDispatcher.Drain(runtime.HostCommands, this);
        requestDrain.Drain(++hostFrame, runtime.PresentationRequests, this, this, this);
        hardwareInput.TickRumble((float)gameTime.ElapsedGameTime.TotalSeconds);
        DrainBattleRequests();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        if (runtime is null || sceneTarget is null || renderAdapter is null ||
            textureResolver is null || renderDevice is null || textRenderSink is null || spriteBatch is null)
        {
            GraphicsDevice.Clear(Color.Black);
            base.Draw(gameTime);
            return;
        }

        EnsureSceneTarget();
        GraphicsDevice.SetRenderTarget(sceneTarget);
        GraphicsDevice.Clear(Color.Black);
        renderAdapter.Draw(
            runtime.Packets,
            runtime.TextPackets,
            textureResolver,
            textCatalog,
            renderDevice,
            textRenderSink);

        if (snapshotOptions is not null && ++drawnSnapshotFrames >= 2)
        {
            CompleteSnapshot(sceneTarget);
            GraphicsDevice.SetRenderTarget(null);
            Exit();
            return;
        }

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp);
        spriteBatch.Draw(sceneTarget, RenderDestination, Color.White);
        spriteBatch.End();
        base.Draw(gameTime);
    }

    protected override void UnloadContent()
    {
        if (runtime is not null)
        {
            HostRuntimeDiagnostics snapshot = diagnostics.Capture(runtime, hostFrame);
            Console.WriteLine(
                $"[DataOriented] stopped frame={snapshot.Frame} scene={snapshot.Scene} " +
                $"entities={snapshot.EntityCount} packets={snapshot.RenderPacketCount}");
        }
        snapshotScene?.Dispose();
        runtime?.Dispose();
        textureFactory?.Dispose();
        sceneTarget?.Dispose();
        spriteRasterizer?.Dispose();
        spriteBatch?.Dispose();
        base.UnloadContent();
    }

    public RasterizerState GetRasterizerState() =>
        spriteRasterizer ?? throw new InvalidOperationException("Content is not loaded.");

    void IHostCommandTarget.QuitApplication() => Exit();
    void IHostCommandTarget.ToggleFullScreen() => ToggleFullScreen();
    void IHostCommandTarget.ToggleDebugMenu() => ToggleDebugMenu();
    void IHostCommandTarget.ToggleEntityList() => ToggleEntityList();
    void IHostCommandTarget.DealDebugDamage() => DealDebugDamage();
    void IHostCommandTarget.ToggleProfiler() => ToggleProfiler();

    void IHostAudioRequestSink.Dispatch(in AudioPlaybackRequest request)
    {
        // SoundId-to-SoundEffect bindings are host resources. Requests remain deterministic even
        // when a development build has no binding for a semantic ID.
    }

    void IHostShaderRequestSink.Dispatch(in ShaderEffectRequest request)
    {
        // Shader recipes are consumed externally; no draw callback mutates ECS state.
    }

    void IHostRumbleRequestSink.Dispatch(in RumblePlaybackRequest request)
    {
        switch (request.Kind)
        {
            case RumbleRequestKind.PlaySegment:
                if (request.DurationSeconds <= 0f) return;
                float strength = Math.Clamp(request.Strength, 0f, 1f) * rumbleStrength;
                var start = ToRumbleState(request.Start).Scaled(strength);
                var end = ToRumbleState(request.End).Scaled(strength);
                RumblePattern pattern = request.DelaySeconds > 0f
                    ? new RumblePattern(
                        new RumbleSegment(RumbleMotorState.Zero, RumbleMotorState.Zero, request.DelaySeconds),
                        new RumbleSegment(start, end, request.DurationSeconds))
                    : new RumblePattern(new RumbleSegment(start, end, request.DurationSeconds));
                hardwareInput.PlayRumblePattern(pattern, (RumbleGroup)(byte)request.Group);
                break;
            case RumbleRequestKind.ClearGroup:
                hardwareInput.ClearRumbleGroup((RumbleGroup)(byte)request.Group);
                break;
            case RumbleRequestKind.ClearAll:
                hardwareInput.ClearAllRumble();
                break;
            case RumbleRequestKind.SetEnabled:
                rumbleStrength = Math.Clamp(request.Strength, 0f, 1f);
                hardwareInput.SetRumbleEnabled(request.Enabled != 0);
                break;
        }
    }

    private static RumbleMotorState ToRumbleState(RumbleMotorRequest request) => new(
        request.LowFrequency,
        request.HighFrequency,
        request.LeftTrigger,
        request.RightTrigger);

    private void HandleSmokeNavigation(
        in Crusaders30XX.ECS.DataOriented.Components.PlayerInputFrame frame)
    {
        if (runtime is null || snapshotOptions is not null || testFightOptions is not null ||
            !frame.WasPressed(PlayerInputButton.Primary))
            return;

        SceneGroup scene = runtime.World.Get<SceneState>(runtime.Globals.Scene).Current;
        switch (scene)
        {
            case SceneGroup.TitleMenu:
                runtime.RequestScene(SceneGroup.WayStation);
                break;
            case SceneGroup.WayStation:
                runtime.RequestScene(SceneGroup.Climb);
                break;
            case SceneGroup.Climb when runtime.CombatSessions.Current is null:
                runtime.BeginCombat(EnemyId.TrainingDemon, seed: (ulong)Math.Max(1, hostFrame));
                runtime.RequestScene(SceneGroup.Battle);
                break;
        }
    }

    private void DrainBattleRequests()
    {
        if (runtime is null) return;
        while (runtime.BattleRequests.TryDequeue(out _))
        {
            if (runtime.CombatSessions.Current is null)
                runtime.BeginCombat(EnemyId.TrainingDemon, seed: (ulong)Math.Max(1, hostFrame));
            runtime.RequestScene(SceneGroup.Battle);
        }
    }

    private void ToggleFullScreen()
    {
        if (!graphics.IsFullScreen)
        {
            graphics.HardwareModeSwitch = false;
            graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            graphics.IsFullScreen = true;
        }
        else
        {
            graphics.IsFullScreen = false;
            graphics.PreferredBackBufferWidth = VirtualWidth;
            graphics.PreferredBackBufferHeight = VirtualHeight;
        }
        graphics.ApplyChanges();
        CalculateRenderDestination();
    }

    private void ToggleDebugMenu()
    {
        if (runtime is null) return;
        Query<DebugMenu> query = runtime.World.Query<DebugMenu>();
        foreach (QueryChunk<DebugMenu> chunk in query)
        {
            foreach (int row in chunk.Rows)
            {
                chunk.Component1[row].IsOpen ^= 1;
                return;
            }
        }
        var bundle = new SpawnBundle(1);
        bundle.Add(new DebugMenu { IsOpen = 1 });
        runtime.World.Create(in bundle);
    }

    private void ToggleEntityList()
    {
        if (runtime is null) return;
        Query<EntityListOverlay> query = runtime.World.Query<EntityListOverlay>();
        foreach (QueryChunk<EntityListOverlay> chunk in query)
        {
            foreach (int row in chunk.Rows)
            {
                chunk.Component1[row].IsOpen = !chunk.Component1[row].IsOpen;
                return;
            }
        }
        var bundle = new SpawnBundle(1);
        bundle.Add(new EntityListOverlay { IsOpen = true });
        runtime.World.Create(in bundle);
    }

    private void ToggleProfiler()
    {
        if (runtime is null) return;
        Query<ProfilerOverlay> query = runtime.World.Query<ProfilerOverlay>();
        foreach (QueryChunk<ProfilerOverlay> chunk in query)
        {
            foreach (int row in chunk.Rows)
            {
                chunk.Component1[row].IsOpen ^= 1;
                return;
            }
        }
        var bundle = new SpawnBundle(1);
        bundle.Add(new ProfilerOverlay { IsOpen = 1 });
        runtime.World.Create(in bundle);
    }

    private void DealDebugDamage()
    {
        if (runtime?.CombatSessions.Current is not { } session) return;
        ref HP hp = ref runtime.World.Get<HP>(session.Player);
        hp.Current = Math.Max(0, hp.Current - 1);
    }

    private RenderTarget2D CreateSceneTarget() => new(
        GraphicsDevice,
        VirtualWidth,
        VirtualHeight,
        false,
        SurfaceFormat.Color,
        DepthFormat.None,
        0,
        RenderTargetUsage.PreserveContents);

    private void EnsureSceneTarget()
    {
        if (sceneTarget is not null && sceneTarget.Width == VirtualWidth && sceneTarget.Height == VirtualHeight)
            return;
        sceneTarget?.Dispose();
        sceneTarget = CreateSceneTarget();
    }

    private void CalculateRenderDestination()
    {
        Display = DisplayMetrics.Calculate(
            GraphicsDevice.PresentationParameters.BackBufferWidth,
            GraphicsDevice.PresentationParameters.BackBufferHeight,
            cardListProfileOptions?.RenderScale ?? snapshotOptions?.RenderScaleOverride ?? 1f);
    }

    private static DataOrientedTestFightFixture BuildTestFightFixture(TestFightLaunchOptions options)
    {
        CardId weapon = options.WeaponId switch
        {
            "sword" => CardId.Sword,
            "dagger" => CardId.Dagger,
            "hammer" => CardId.Hammer,
            _ => throw new TestFightSetupException($"Unknown data-oriented weapon '{options.WeaponId}'."),
        };
        string normalizedEnemy = options.EnemyId.Replace("-", string.Empty, StringComparison.Ordinal);
        if (!Enum.TryParse(normalizedEnemy, ignoreCase: true, out EnemyId enemy))
            throw new TestFightSetupException($"Enemy '{options.EnemyId}' has no stable data-oriented ID.");
        return new DataOrientedTestFightFixture(weapon, enemy, options.Difficulty, Seed: 1);
    }

    private static SnapshotLaunchOutput ResolveSnapshotLaunch(DisplaySnapshotLaunchOptions options)
    {
        if (new SnapshotLaunchOutputCatalog().TryResolve(options.FixtureId, options.Args, out var output))
            return output;
        string arguments = options.Args.Length == 0 ? "<default>" : string.Join(' ', options.Args);
        throw new DisplaySnapshotSetupException(
            $"Unknown data-oriented snapshot case '{options.FixtureId} {arguments}'.");
    }

    private void CompleteSnapshot(RenderTarget2D target)
    {
        if (snapshotOptions is null || snapshotLaunchOutput is not { } launch) return;
        string root = DisplaySnapshotBaselineComparer.FindRepositoryRoot();
        float renderScale = snapshotOptions.RenderScaleOverride ?? 1f;
        string captureFileName = SnapshotLaunchOutputCatalog.GetCaptureFileName(in launch, renderScale);
        DisplaySnapshotPaths paths = DisplaySnapshotBaselineComparer.BuildPaths(
            root,
            snapshotOptions.FixtureId,
            captureFileName);
        switch (snapshotOptions.BaselineMode)
        {
            case DisplaySnapshotBaselineMode.Accept:
                DisplaySnapshotBaselineComparer.SavePng(target, paths.BaselinePath);
                break;
            case DisplaySnapshotBaselineMode.Verify:
                DisplaySnapshotComparisonResult result = DisplaySnapshotBaselineComparer.Compare(
                    GraphicsDevice,
                    target,
                    paths.BaselinePath);
                if (!result.Passed)
                {
                    DisplaySnapshotBaselineComparer.SavePng(target, paths.FailureActualPath);
                    DisplaySnapshotBaselineComparer.SaveDiffPng(GraphicsDevice, result, paths.FailureDiffPath);
                    Console.Error.WriteLine($"[DataOriented] Snapshot failed: {result.FailureMessage}");
                    Environment.ExitCode = 1;
                }
                break;
            default:
                DisplaySnapshotBaselineComparer.SavePng(target, paths.CapturePath);
                break;
        }
    }

    private sealed class MonoGameTextureResourceFactory : IHostTextureResourceFactory<Texture2D>, IDisposable
    {
        private readonly Game1 host;
        private readonly Dictionary<GeneratedTextureRecipe, Texture2D> generated = [];

        public MonoGameTextureResourceFactory(Game1 host) => this.host = host;

        public Texture2D LoadContentAsset(string contentAssetName) =>
            host.Content.Load<Texture2D>(contentAssetName);

        public Texture2D CreateGeneratedPrimitive(in GeneratedTextureRecipe recipe)
        {
            if (generated.TryGetValue(recipe, out Texture2D? texture)) return texture;
            texture = new Texture2D(
                host.GraphicsDevice,
                recipe.Width,
                recipe.Height,
                false,
                SurfaceFormat.Color);
            var pixels = new Color[recipe.Width * recipe.Height];
            FillRecipe(pixels, in recipe);
            texture.SetData(pixels);
            generated.Add(recipe, texture);
            return texture;
        }

        public void Dispose()
        {
            foreach (Texture2D texture in generated.Values) texture.Dispose();
            generated.Clear();
        }

        private static void FillRecipe(Color[] pixels, in GeneratedTextureRecipe recipe)
        {
            if (recipe.Kind == GeneratedTextureRecipeKind.ParallelogramMask)
            {
                FillParallelogramMask(pixels, in recipe);
                return;
            }
            if (recipe.Kind == GeneratedTextureRecipeKind.PassiveTrapezoidMask)
            {
                FillPassiveTrapezoidMask(pixels, in recipe);
                return;
            }

            int radius = Math.Clamp(recipe.CornerRadius, 0, Math.Min(recipe.Width, recipe.Height) / 2);
            for (var y = 0; y < recipe.Height; y++)
            {
                for (var x = 0; x < recipe.Width; x++)
                {
                    bool inside = InsideRoundedRectangle(x, y, recipe.Width, recipe.Height, radius);
                    if (!inside)
                    {
                        pixels[y * recipe.Width + x] = Color.Transparent;
                        continue;
                    }

                    bool inner = recipe.Width > 6 && recipe.Height > 6 &&
                        InsideRoundedRectangle(x - 3, y - 3, recipe.Width - 6, recipe.Height - 6,
                            Math.Max(0, radius - 3));
                    pixels[y * recipe.Width + x] = inner ? recipe.Fill : recipe.Border;
                }
            }

            if (recipe.Kind is GeneratedTextureRecipeKind.MissingCardArt or
                GeneratedTextureRecipeKind.MissingEnemyArt)
            {
                int thickness = Math.Max(2, Math.Min(recipe.Width, recipe.Height) / 60);
                int length = Math.Min(recipe.Width, recipe.Height);
                for (var value = 0; value < length; value++)
                {
                    Paint(pixels, recipe.Width, recipe.Height, value, value, thickness, recipe.Border);
                    Paint(pixels, recipe.Width, recipe.Height,
                        recipe.Width - value - 1, value, thickness, recipe.Border);
                }
            }
        }

        private static void FillParallelogramMask(
            Color[] pixels,
            in GeneratedTextureRecipe recipe)
        {
            float slant = Math.Clamp(recipe.CornerRadius, 0, recipe.Width);
            float bottom = recipe.Height - 1f;
            for (var y = 0; y < recipe.Height; y++)
            {
                float progress = recipe.Height <= 1 ? 0f : y / (float)(recipe.Height - 1);
                float left = MathHelper.Lerp(slant, 0f, progress);
                float right = MathHelper.Lerp(recipe.Width - 1f, recipe.Width - 1f - slant, progress);
                for (var x = 0; x < recipe.Width; x++)
                {
                    float px = x + .5f;
                    float py = y + .5f;
                    float minimumDistance = MathF.Min(
                        MathF.Min(px - left, right - px),
                        MathF.Min(py, bottom - py));
                    if (minimumDistance < 0f)
                    {
                        pixels[y * recipe.Width + x] = Color.Transparent;
                        continue;
                    }

                    byte alpha = (byte)Math.Clamp(
                        (int)MathF.Round(MathF.Min(1f, minimumDistance) * 255f), 0, 255);
                    pixels[y * recipe.Width + x] =
                        Color.FromNonPremultiplied(255, 255, 255, alpha);
                }
            }
        }

        private static void FillPassiveTrapezoidMask(
            Color[] pixels,
            in GeneratedTextureRecipe recipe)
        {
            float height = recipe.Height;
            float bottom = height - 1f;
            float leftBottom = MathF.Tan(MathHelper.ToRadians(11f)) * height;
            float rightTop = recipe.Width - 1f;
            float rightBottom = rightTop + MathF.Tan(MathHelper.ToRadians(-23f)) * height;
            float topSlope = MathF.Tan(MathHelper.ToRadians(2f));
            float bottomSlope = MathF.Tan(MathHelper.ToRadians(-2f));
            for (var y = 0; y < recipe.Height; y++)
            {
                float progress = recipe.Height <= 1 ? 0f : y / (float)(recipe.Height - 1);
                float left = MathHelper.Lerp(0f, leftBottom, progress);
                float right = MathHelper.Lerp(rightTop, rightBottom, progress);
                for (var x = 0; x < recipe.Width; x++)
                {
                    float px = x + .5f;
                    float py = y + .5f;
                    float top = topSlope * px;
                    float lower = bottom + bottomSlope * (px - leftBottom);
                    float distance = MathF.Min(
                        MathF.Min(px - left, right - px),
                        MathF.Min(py - top, lower - py));
                    if (distance < 0f)
                    {
                        pixels[y * recipe.Width + x] = Color.Transparent;
                        continue;
                    }
                    byte alpha = (byte)Math.Clamp(
                        (int)MathF.Round(MathF.Min(1f, distance) * 255f), 0, 255);
                    pixels[y * recipe.Width + x] =
                        Color.FromNonPremultiplied(255, 255, 255, alpha);
                }
            }
        }


        private static void Paint(
            Color[] pixels,
            int width,
            int height,
            int centerX,
            int centerY,
            int halfWidth,
            Color color)
        {
            for (int offsetY = -halfWidth; offsetY <= halfWidth; offsetY++)
            for (int offsetX = -halfWidth; offsetX <= halfWidth; offsetX++)
            {
                int x = centerX + offsetX;
                int y = centerY + offsetY;
                if ((uint)x < (uint)width && (uint)y < (uint)height)
                    pixels[y * width + x] = color;
            }
        }

        private static bool InsideRoundedRectangle(int x, int y, int width, int height, int radius)
        {
            if ((uint)x >= (uint)width || (uint)y >= (uint)height) return false;
            if (radius == 0 || (x >= radius && x < width - radius) ||
                (y >= radius && y < height - radius)) return true;
            int centerX = x < radius ? radius - 1 : width - radius;
            int centerY = y < radius ? radius - 1 : height - radius;
            int dx = x - centerX;
            int dy = y - centerY;
            return dx * dx + dy * dy <= radius * radius;
        }
    }

    private sealed class MonoGameTextRenderSink : IHostTextRenderDevice
    {
        private readonly SpriteBatch spriteBatch;
        private readonly SpriteFont hudFont;
        private readonly SpriteFont displayFont;
        private readonly SpriteFont hudBoldItalicFont;

        public MonoGameTextRenderSink(SpriteBatch spriteBatch, Game1 host)
        {
            this.spriteBatch = spriteBatch;
            hudFont = host.Content.Load<SpriteFont>("Fonts/ChakraPetch");
            displayFont = host.Content.Load<SpriteFont>("Fonts/NewRocker");
            hudBoldItalicFont = host.Content.Load<SpriteFont>("Fonts/ChakraPetch-BoldItalic");
        }

        public void BeginText() => spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp);

        public void EndText() => spriteBatch.End();

        public void Draw(in TextRenderPacket packet, string text, in TextStyleDefinition style)
        {
            SpriteFont font = style.Font.Value switch
            {
                1 => hudFont,
                2 => displayFont,
                3 => hudBoldItalicFont,
                _ => throw new InvalidOperationException($"Unknown FontAssetId {style.Font.Value}."),
            };
            if (packet.LetterSpacing > 0f && packet.Rotation == 0f)
            {
                DrawSpaced(in packet, text, in style, font);
                return;
            }
            Vector2 size = font.MeasureString(text);
            Vector2 origin = packet.Alignment switch
            {
                TextAlignment.TopLeft => Vector2.Zero,
                TextAlignment.TopCenter => new Vector2(size.X * 0.5f, 0f),
                TextAlignment.Center => size * 0.5f,
                TextAlignment.BottomCenter => new Vector2(size.X * 0.5f, size.Y),
                _ => throw new ArgumentOutOfRangeException(nameof(packet), packet.Alignment, null),
            };
            Vector2 scale = packet.Scale * style.Scale;
            if ((packet.Flags & TextPresentationFlags.DropShadow) != 0)
            {
                spriteBatch.DrawString(
                    font, text, packet.Position + new Vector2(3f, 3f), Color.Black * 0.7f,
                    packet.Rotation, origin, scale, SpriteEffects.None, 0f);
            }
            spriteBatch.DrawString(
                font, text, packet.Position, packet.Tint,
                packet.Rotation, origin, scale, SpriteEffects.None, 0f);
        }

        private void DrawSpaced(
            in TextRenderPacket packet,
            string text,
            in TextStyleDefinition style,
            SpriteFont font)
        {
            Vector2 scale = packet.Scale * style.Scale;
            float width = 0f;
            float height = 0f;
            for (var index = 0; index < text.Length; index++)
            {
                Vector2 size = font.MeasureString(text[index].ToString()) * scale;
                width += size.X;
                height = MathF.Max(height, size.Y);
            }
            width += Math.Max(0, text.Length - 1) * packet.LetterSpacing;
            Vector2 position = packet.Alignment switch
            {
                TextAlignment.TopLeft => packet.Position,
                TextAlignment.TopCenter => packet.Position - new Vector2(width / 2f, 0f),
                TextAlignment.Center => packet.Position - new Vector2(width / 2f, height / 2f),
                TextAlignment.BottomCenter => packet.Position - new Vector2(width / 2f, height),
                _ => throw new ArgumentOutOfRangeException(nameof(packet), packet.Alignment, null),
            };
            for (var index = 0; index < text.Length; index++)
            {
                string glyph = text[index].ToString();
                if ((packet.Flags & TextPresentationFlags.DropShadow) != 0)
                    spriteBatch.DrawString(font, glyph, position + new Vector2(3f, 3f),
                        Color.Black * .7f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, glyph, position, packet.Tint,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                position.X += font.MeasureString(glyph).X * scale.X + packet.LetterSpacing;
            }
        }
    }

    private sealed class MonoGameRenderDevice : IHostRenderDevice<Texture2D>
    {
        private readonly SpriteBatch spriteBatch;
        private readonly RasterizerState rasterizer;

        public MonoGameRenderDevice(SpriteBatch spriteBatch, RasterizerState rasterizer)
        {
            this.spriteBatch = spriteBatch;
            this.rasterizer = rasterizer;
        }

        public void Begin(HostRenderPass pass) => spriteBatch.Begin(
            SpriteSortMode.Immediate,
            pass == HostRenderPass.Additive ? AdditiveAlphaOne : BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            rasterizer);

        public void Draw(in RenderPacket packet, Texture2D? texture)
        {
            if (texture is null) return;
            Rectangle? source = (packet.Flags & RenderPacketFlags.HasSourceRectangle) != 0
                ? packet.SourceRectangle
                : null;
            if ((packet.Flags & RenderPacketFlags.PixelAlignedDestination) != 0 &&
                packet.Rotation == 0f)
            {
                var destination = new Rectangle(
                    (int)MathF.Round(packet.Position.X - packet.Scale.X / 2f),
                    (int)MathF.Round(packet.Position.Y - packet.Scale.Y / 2f),
                    Math.Max(1, (int)MathF.Round(packet.Scale.X)),
                    Math.Max(1, (int)MathF.Round(packet.Scale.Y)));
                spriteBatch.Draw(texture, destination, source, packet.Tint);
                return;
            }
            Vector2 sourceSize = source.HasValue
                ? new Vector2(source.Value.Width, source.Value.Height)
                : new Vector2(texture.Width, texture.Height);
            Vector2 scale = packet.Kind switch
            {
                RenderPacketKind.Card or RenderPacketKind.HandCard =>
                    packet.Scale * new Vector2(360f / sourceSize.X, 520f / sourceSize.Y),
                RenderPacketKind.Player =>
                    packet.Scale * new Vector2(260f / sourceSize.X, 390f / sourceSize.Y),
                RenderPacketKind.Enemy =>
                    packet.Scale * new Vector2(300f / sourceSize.X, 420f / sourceSize.Y),
                _ => packet.Scale / sourceSize,
            };
            spriteBatch.Draw(
                texture,
                packet.Position,
                source,
                packet.Tint,
                packet.Rotation,
                packet.Origin == Vector2.Zero ? sourceSize * 0.5f : packet.Origin,
                scale,
                SpriteEffects.None,
                0f);
        }

        public void End() => spriteBatch.End();
    }
}
