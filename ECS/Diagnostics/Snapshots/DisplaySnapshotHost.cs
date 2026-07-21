using System;
using System.IO;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots
{
    public sealed class DisplaySnapshotHost
    {
        private readonly Microsoft.Xna.Framework.Game _game;
        private readonly DisplaySnapshotLaunchOptions _options;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly ContentManager _content;
        private readonly ImageAssetService _imageAssets;

        private IDisplaySnapshotFixture _fixture;
        private DisplaySnapshotContext _ctx;
        private int _frameCount;
        private string _repositoryRoot;

        public bool IsActive => _fixture != null;
        public bool ShouldSkipGlobalOverlays => IsActive;

        private DisplaySnapshotHost(
            Microsoft.Xna.Framework.Game game,
            DisplaySnapshotLaunchOptions options,
            GraphicsDevice graphicsDevice,
            ContentManager content)
        {
            _game = game;
            _options = options;
            _graphicsDevice = graphicsDevice;
            _content = content;
            _imageAssets = new ImageAssetService(content, graphicsDevice);
        }

        public static DisplaySnapshotHost TryCreate(
            DisplaySnapshotLaunchOptions options,
            Microsoft.Xna.Framework.Game game,
            GraphicsDevice graphicsDevice,
            ContentManager content)
        {
            if (options == null) return null;
            return new DisplaySnapshotHost(game, options, graphicsDevice, content);
        }

        public void OnGameReady(World world, Entity sceneEntity, SpriteBatch spriteBatch)
        {
            if (!DisplaySnapshotRegistry.TryGet(_options.FixtureId, out _fixture))
            {
                Console.Error.WriteLine($"[DisplaySnapshot] Unknown fixture: '{_options.FixtureId}'");
                Environment.Exit(1);
            }

            _ctx = new DisplaySnapshotContext
            {
                World = world,
                GraphicsDevice = _graphicsDevice,
                SpriteBatch = spriteBatch,
                Content = _content,
                ImageAssets = _imageAssets,
                SceneEntity = sceneEntity
            };

            var sceneState = sceneEntity.GetComponent<SceneState>();
            sceneState.Current = SceneId.Snapshot;

            try
            {
                _fixture.Setup(_ctx, _options.Args);
            }
            catch (DisplaySnapshotSetupException ex)
            {
                Console.Error.WriteLine($"[DisplaySnapshot] {ex.Message}");
                Environment.Exit(1);
            }

            _frameCount = 0;
            _repositoryRoot = DisplaySnapshotBaselineComparer.FindRepositoryRoot();
            Console.WriteLine($"[DisplaySnapshot] Fixture '{_fixture.Id}' ready");
        }

        public void DrawScene(SpriteBatch spriteBatch)
        {
            _ctx = new DisplaySnapshotContext
            {
                World = _ctx.World,
                GraphicsDevice = _ctx.GraphicsDevice,
                SpriteBatch = spriteBatch,
                Content = _ctx.Content,
                ImageAssets = _ctx.ImageAssets,
                SceneEntity = _ctx.SceneEntity
            };
            _fixture.Draw(_ctx);
        }

        /// <returns>True if capture ran and the game is exiting (caller should skip backbuffer present).</returns>
        public bool TickAfterDraw(RenderTarget2D sceneRt)
        {
            _frameCount++;
            if (_frameCount < _fixture.WarmupFrames) return false;

            var paths = DisplaySnapshotBaselineComparer.BuildPaths(
                _repositoryRoot,
                _fixture.Id,
                GetOutputFileName());

            switch (_options.BaselineMode)
            {
                case DisplaySnapshotBaselineMode.Accept:
                    AcceptBaseline(sceneRt, paths);
                    break;
                case DisplaySnapshotBaselineMode.Verify:
                    VerifyBaseline(sceneRt, paths);
                    break;
                default:
                    Capture(sceneRt, paths.CapturePath);
                    break;
            }

            _graphicsDevice.SetRenderTarget(null);
            _game.Exit();
            return true;
        }

        private string GetOutputFileName()
        {
            if (!_options.RenderScaleOverride.HasValue || _options.RenderScaleOverride.Value == 1f)
            {
                return _fixture.OutputFileName;
            }

            string extension = Path.GetExtension(_fixture.OutputFileName);
            string stem = Path.GetFileNameWithoutExtension(_fixture.OutputFileName);
            string scale = _options.RenderScaleOverride.Value.ToString(
                "0.###",
                System.Globalization.CultureInfo.InvariantCulture);
            return $"{stem}@{scale}x{extension}";
        }

        private static void Capture(Texture2D scene, string path)
        {
            DisplaySnapshotBaselineComparer.SavePng(scene, path);
            Console.WriteLine($"[DisplaySnapshot] Saved: {Path.GetFullPath(path)}");
        }

        private static void AcceptBaseline(Texture2D scene, DisplaySnapshotPaths paths)
        {
            DisplaySnapshotBaselineComparer.SavePng(scene, paths.BaselinePath);
            Console.WriteLine($"[DisplaySnapshot] Accepted baseline: {Path.GetFullPath(paths.BaselinePath)}");
        }

        private void VerifyBaseline(Texture2D scene, DisplaySnapshotPaths paths)
        {
            var result = DisplaySnapshotBaselineComparer.Compare(
                _graphicsDevice,
                scene,
                paths.BaselinePath);

            if (result.Passed)
            {
                Console.WriteLine(
                    $"[DisplaySnapshot] Verified: {Path.GetFullPath(paths.BaselinePath)} " +
                    $"({result.DifferingPixelCount} differing pixels)");
                return;
            }

            DisplaySnapshotBaselineComparer.SavePng(scene, paths.FailureActualPath);
            DisplaySnapshotBaselineComparer.SaveDiffPng(
                _graphicsDevice,
                result,
                paths.FailureDiffPath);

            Console.Error.WriteLine($"[DisplaySnapshot] Verification failed: {result.FailureMessage}");
            Console.Error.WriteLine(
                $"[DisplaySnapshot] Actual: {Path.GetFullPath(paths.FailureActualPath)}");
            Console.Error.WriteLine(
                $"[DisplaySnapshot] Diff: {Path.GetFullPath(paths.FailureDiffPath)}");
            Environment.ExitCode = 1;
        }
    }
}
