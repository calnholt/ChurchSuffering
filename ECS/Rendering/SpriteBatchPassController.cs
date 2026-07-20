using System;
using Crusaders30XX.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

/// <summary>
/// Brackets a scene-owned SpriteBatch pass and restores the scene's immediate UI state.
/// The controller owns graphics submission state only; it does not hold game state.
/// </summary>
internal sealed class SpriteBatchPassController
{
    private readonly SpriteBatch _spriteBatch;
    private readonly RasterizerState _rasterizerState;

    public SpriteBatchPassController(SpriteBatch spriteBatch, RasterizerState rasterizerState)
    {
        _spriteBatch = spriteBatch ?? throw new ArgumentNullException(nameof(spriteBatch));
        _rasterizerState = rasterizerState ?? throw new ArgumentNullException(nameof(rasterizerState));
    }

    public void RunDeferred(string profileScope, Action draw) =>
        Run(SpriteSortMode.Deferred, profileScope, draw);

    public void RunIsolatedImmediate(string profileScope, Action draw) =>
        Run(SpriteSortMode.Immediate, profileScope, draw);

    private void Run(SpriteSortMode sortMode, string profileScope, Action draw)
    {
        if (draw == null) return;
        _spriteBatch.End();
        try
        {
            FrameProfiler.Measure(profileScope, () =>
            {
                Begin(sortMode);
                try
                {
                    draw();
                }
                finally
                {
                    _spriteBatch.End();
                }
            });
        }
        finally
        {
            Begin(SpriteSortMode.Immediate);
        }
    }

    private void Begin(SpriteSortMode sortMode)
    {
        _spriteBatch.Begin(
            sortMode,
            BlendState.AlphaBlend,
            SamplerState.AnisotropicClamp,
            DepthStencilState.None,
            _rasterizerState,
            null,
            Game1.Display.SpriteBatchTransform);
    }
}
