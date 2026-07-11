using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems;

/// <summary>Owns persistent collection progression and bridges it to scene events.</summary>
public sealed class CollectionProgressionSystem : Core.System
{
	public CollectionProgressionSystem(EntityManager entityManager) : base(entityManager)
	{
		EventManager.Subscribe<AchievementSeenEvent>(OnAchievementSeen);
		EventManager.Subscribe<AchievementAnimationsComplete>(_ => TryOpenQueuedPack());
		EventManager.Subscribe<ClaimPendingClimbPointsEvent>(_ => ClaimPendingClimbPoints());
		EventManager.Subscribe<ClimbEndedEvent>(OnClimbEnded);
		EventManager.Subscribe<BoosterPackOpeningDismissedEvent>(OnBoosterDismissed);
		EventManager.Subscribe<LoadSceneEvent>(evt =>
		{
			if (evt.Scene == SceneId.Achievement) TryOpenQueuedPack();
		});
	}

	protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
	protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

	private void OnAchievementSeen(AchievementSeenEvent evt)
	{
		if (evt == null || evt.Points <= 0) return;
		var collection = SaveCache.GetCollection();
		collection.totalPoints += evt.Points;
		CollectionProgressionRules.ReconcileEarnedPacks(collection);
		SaveCache.SaveCollection(collection);
	}

	private void ClaimPendingClimbPoints()
	{
		var collection = SaveCache.GetCollection();
		if (collection.pendingClimbPoints <= 0) return;
		collection.totalPoints += collection.pendingClimbPoints;
		collection.pendingClimbPoints = 0;
		CollectionProgressionRules.ReconcileEarnedPacks(collection);
		SaveCache.SaveCollection(collection);
		TryOpenQueuedPack();
	}

	private void OnClimbEnded(ClimbEndedEvent evt)
	{
		if (evt == null) return;
		int points = CollectionProgressionRules.CalculateClimbPoints(
			evt.TimeReached,
			evt.CompletedFinalBoss,
			evt.Abandoned);
		if (points <= 0) return;
		var collection = SaveCache.GetCollection();
		collection.pendingClimbPoints += points;
		SaveCache.SaveCollection(collection);
	}

	private void OnBoosterDismissed(BoosterPackOpeningDismissedEvent evt)
	{
		if (evt?.WasAuthoritativePack != true) return;
		var collection = SaveCache.GetCollection();
		if (collection.pendingBoosterPacks.Count == 0) return;
		collection.pendingBoosterPacks.RemoveAt(0);
		SaveCache.SaveCollection(collection);
		TryOpenQueuedPack();
	}

	private void TryOpenQueuedPack()
	{
		if (!IsAchievementScene()) return;
		if (EntityManager.GetEntity("BoosterPackOpeningOverlay")?.GetComponent<BoosterPackOpeningOverlayState>()?.IsOpen == true) return;
		var collection = SaveCache.GetCollection();
		var pack = collection.pendingBoosterPacks.FirstOrDefault();
		if (pack?.rewards?.Count != 3) return;
		EventManager.Publish(new ShowBoosterPackOpeningOverlayEvent { Pack = pack });
	}

	private bool IsAchievementScene()
	{
		return EntityManager.GetEntitiesWithComponent<SceneState>()
			.FirstOrDefault()
			?.GetComponent<SceneState>()
			?.Current == SceneId.Achievement;
	}
}
