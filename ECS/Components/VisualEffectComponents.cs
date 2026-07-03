using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	public sealed class ActiveVisualEffect : IComponent
	{
		public Entity Owner { get; set; }
		public Guid RequestId { get; set; }
		public VisualEffectRecipe Recipe { get; set; }
		public VisualEffectTiming Timing { get; set; }
		public Entity Source { get; set; }
		public Entity Target { get; set; }
		public Vector2 SourceAnchor { get; set; }
		public Vector2 TargetAnchor { get; set; }
		public Vector2 ImpactAnchor { get; set; }
		public int DirectionSign { get; set; } = 1;
		public float ElapsedSeconds { get; set; }
		public bool ImpactPublished { get; set; }
		public bool CompletionPublished { get; set; }
		public bool IsPreview { get; set; }
		public VisualEffectSourceKind SourceKind { get; set; }
		public string SourceId { get; set; } = string.Empty;
		public string ContextId { get; set; } = string.Empty;
		public string DisplayName { get; set; } = string.Empty;
	}

	public sealed class ActorPresentationState : IComponent
	{
		public Entity Owner { get; set; }
		public Vector2 DrawOffset { get; set; } = Vector2.Zero;
		public Vector2 ScaleMultiplier { get; set; } = Vector2.One;
		public Color TintColor { get; set; } = Color.White;
		public float DamageFlashTimer { get; set; }
	}

	public sealed class BattlePresentationTransform : IComponent
	{
		public Entity Owner { get; set; }
		public Vector2 Offset { get; set; } = Vector2.Zero;
		public Vector2 Scale { get; set; } = Vector2.One;
	}
}
