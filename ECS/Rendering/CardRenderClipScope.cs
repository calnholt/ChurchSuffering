using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering
{
    public readonly struct CardRenderClipScope : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Rectangle _previous;
        private readonly bool _active;

        private CardRenderClipScope(GraphicsDevice graphicsDevice, Rectangle previous, bool active)
        {
            _graphicsDevice = graphicsDevice;
            _previous = previous;
            _active = active;
        }

        public static CardRenderClipScope Apply(GraphicsDevice graphicsDevice, Rectangle? clipRect)
        {
            if (graphicsDevice == null || !clipRect.HasValue)
            {
                return new CardRenderClipScope(null, Rectangle.Empty, false);
            }

            Rectangle previous = graphicsDevice.ScissorRectangle;
            Rectangle next = Rectangle.Intersect(previous, Game1.Display.LogicalToRender(clipRect.Value));
            if (next.Width <= 0 || next.Height <= 0)
            {
                next = Rectangle.Empty;
            }

            graphicsDevice.ScissorRectangle = next;
            return new CardRenderClipScope(graphicsDevice, previous, true);
        }

        public void Dispose()
        {
            if (_active && _graphicsDevice != null)
            {
                _graphicsDevice.ScissorRectangle = _previous;
            }
        }
    }
}
