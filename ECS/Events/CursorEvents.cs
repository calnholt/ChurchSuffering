using Microsoft.Xna.Framework;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Input;

namespace ChurchSuffering.ECS.Events
{
	public class CursorStateEvent
	{
		public Vector2 Position { get; set; }
		public bool IsAPressed { get; set; }
		public bool IsAPressedEdge { get; set; }
		public bool IsSecondaryPressed { get; set; }
		public bool IsSecondaryPressedEdge { get; set; }
		public float Coverage { get; set; }
		public Entity TopEntity { get; set; }
		public PlayerInputDevice Source { get; set; }
		public float ScrollDelta { get; set; }
		public float ScrollStickY { get; set; }
	}

	public class UIElementHoverEnteredEvent
	{
		public Entity Entity { get; set; }
		public PlayerInputDevice Source { get; set; }
	}

	public class HotKeySelectEvent
	{
		public Entity Entity { get; set; }
	}
}
