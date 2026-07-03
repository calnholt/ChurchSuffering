using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering;

internal static class SpriteBatchRenderTargetCompositor
{
    public static bool TryGetPrimaryRenderTarget(
        GraphicsDevice graphicsDevice,
        out RenderTargetBinding[] bindings,
        out RenderTarget2D renderTarget)
    {
        bindings = graphicsDevice.GetRenderTargets();
        renderTarget = null;
        if (bindings == null || bindings.Length == 0)
        {
            return false;
        }

        renderTarget = bindings[0].RenderTarget as RenderTarget2D;
        return renderTarget != null;
    }

    public static SpriteBatchState CaptureState(GraphicsDevice graphicsDevice)
    {
        return new SpriteBatchState(
            graphicsDevice.BlendState,
            graphicsDevice.SamplerStates[0],
            graphicsDevice.DepthStencilState,
            graphicsDevice.RasterizerState,
            graphicsDevice.ScissorRectangle);
    }

    public static void Copy(
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        Texture2D source,
        RenderTarget2D destination)
    {
        graphicsDevice.SetRenderTarget(destination);
        graphicsDevice.Clear(Color.Transparent);
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);
        spriteBatch.Draw(source, graphicsDevice.Viewport.Bounds, Color.White);
        spriteBatch.End();
    }

    public static void RestoreRenderTargets(GraphicsDevice graphicsDevice, RenderTargetBinding[] bindings)
    {
        if (bindings != null && bindings.Length > 0)
        {
            graphicsDevice.SetRenderTargets(bindings);
            return;
        }

        graphicsDevice.SetRenderTarget(null);
    }

    public static void RestoreSpriteBatch(
        GraphicsDevice graphicsDevice,
        SpriteBatch spriteBatch,
        SpriteBatchState state)
    {
        graphicsDevice.ScissorRectangle = state.ScissorRectangle;
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            state.BlendState,
            state.SamplerState,
            state.DepthStencilState,
            state.RasterizerState);
    }

    internal readonly struct SpriteBatchState
    {
        public SpriteBatchState(
            BlendState blendState,
            SamplerState samplerState,
            DepthStencilState depthStencilState,
            RasterizerState rasterizerState,
            Rectangle scissorRectangle)
        {
            BlendState = blendState;
            SamplerState = samplerState;
            DepthStencilState = depthStencilState;
            RasterizerState = rasterizerState;
            ScissorRectangle = scissorRectangle;
        }

        public BlendState BlendState { get; }
        public SamplerState SamplerState { get; }
        public DepthStencilState DepthStencilState { get; }
        public RasterizerState RasterizerState { get; }
        public Rectangle ScissorRectangle { get; }
    }
}
