using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots
{
    public sealed class DisplaySnapshotContext
    {
        public World World { get; init; }
        public GraphicsDevice GraphicsDevice { get; init; }
        public SpriteBatch SpriteBatch { get; init; }
        public ContentManager Content { get; init; }
        public ImageAssetService ImageAssets { get; init; }
        public Entity SceneEntity { get; init; }
    }
}
