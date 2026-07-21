using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Components
{
    public class PositionTween : IComponent
    {
        public Entity Owner { get; set; }

        // Where the layout system wants this entity (written by layout systems each frame)
        public Vector2 Target { get; set; } = Vector2.Zero;

        // Internally-tracked smooth position (owned exclusively by PositionTweenSystem)
        public Vector2 Current { get; set; } = Vector2.Zero;

        // Exponential decay rate for smoothing
        public float Speed { get; set; } = 10f;

        // False until first update; lets layout systems set Current to a spawn point before the system takes over
        public bool Initialized { get; set; } = false;
    }
}
