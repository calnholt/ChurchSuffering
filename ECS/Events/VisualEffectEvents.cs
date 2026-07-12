using System;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Events
{
	public class BeginDefeatPresentationEvent
	{
		public Entity Enemy;
		public bool IsPreview;
	}

	public class PixelBurstAnimationRequested
	{
		public Texture2D Texture;
		public Vector2 Center;
		public Vector2 DrawTopLeft;
		public Vector2 DrawScale = Vector2.One;
		public int SourceEntityId;
		public Guid BurstId;
		public bool IsPreview;
	}

	public class PixelBurstAnimationCompleted
	{
		public Guid BurstId;
		public int SourceEntityId;
		public bool IsPreview;
	}

	public sealed class VisualEffectRequested
	{
		public Guid RequestId { get; init; } = Guid.NewGuid();
		public VisualEffectRecipe Recipe { get; init; }
		public Entity Source { get; init; }
		public Entity Target { get; init; }
		public VisualEffectSourceKind SourceKind { get; init; }
		public string SourceId { get; init; } = string.Empty;
		public string ContextId { get; init; } = string.Empty;
		public string DisplayName { get; init; } = string.Empty;
		public bool IsPreview { get; init; }
		public float DelaySeconds { get; init; }
		public VisualEffectTiming? TimingOverride { get; init; }
		public bool DrivesGameplayImpact { get; init; } = true;
		public Guid SequenceId { get; init; }
		public int BeatIndex { get; init; }
	}

	public sealed class VisualEffectImpactReached
	{
		public Guid RequestId { get; init; }
		public bool IsPreview { get; init; }
	}

	public sealed class VisualEffectCompleted
	{
		public Guid RequestId { get; init; }
		public bool IsPreview { get; init; }
	}

	public enum BattlePresentationKind
	{
		DamageNumber,
	}

	public sealed class BattlePresentationStarted
	{
		public Guid PresentationId { get; init; }
		public Entity Target { get; init; }
		public BattlePresentationKind Kind { get; init; }
	}

	public sealed class BattlePresentationCompleted
	{
		public Guid PresentationId { get; init; }
		public Entity Target { get; init; }
		public BattlePresentationKind Kind { get; init; }
	}
}
