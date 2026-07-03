using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// System for handling quest initialization when a POI is selected.
    /// Subscribes to QuestSelectRequested and builds QueuedEvents for the quest.
    /// </summary>
    public class QuestStartSystem : Core.System
    {
        private const string DesertLocationId = "desert";

        public QuestStartSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<QuestSelectRequested>(OnQuestSelectRequested);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            yield break;
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        private void OnQuestSelectRequested(QuestSelectRequested e)
        {
            var poi = e.Entity.GetComponent<PointOfInterest>();
            if (poi != null)
            {
                TryStartQuestFromPoi(poi);
            }
        }

        private void TryStartQuestFromPoi(PointOfInterest poi)
        {
            if (poi == null || string.IsNullOrEmpty(poi.Id)) return;
            if (poi.Type == PointOfInterestType.Shop
                || poi.Type == PointOfInterestType.Treasure
                || poi.Type == PointOfInterestType.Event) return;

            if (!SaveCache.TryGetRunNode(poi.Id, out var node, out int questIndex)) return;
            if (!node.isRevealed || node.isCompleted) return;
            var battleEnemyIds = node.ResolveBattleEnemyIds();
            if (battleEnemyIds.Count == 0) return;

            var qeEntity = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
            if (qeEntity == null)
            {
                qeEntity = EntityManager.CreateEntity("QueuedEvents");
                EntityManager.AddComponent(qeEntity, new QueuedEvents());
                EntityManager.AddComponent(qeEntity, new DontDestroyOnLoad());
            }
            var qe = qeEntity.GetComponent<QueuedEvents>();
            qe.CurrentIndex = -1;
            qe.Events.Clear();
            qe.LocationId = DesertLocationId;
            qe.QuestIndex = questIndex;

            foreach (string enemyId in battleEnemyIds)
            {
                qe.Events.Add(new QueuedEvent
                {
                    EventId = enemyId,
                    EventType = QueuedEventType.Enemy,
                });
            }

            SaveCache.SetLastLocation(poi.Id);
            SaveCache.SetPendingBattleNode(poi.Id);
            EventManager.Publish(new QuestSelected { LocationId = DesertLocationId, QuestIndex = questIndex, QuestId = poi.Id });
            EventManager.Publish(new ShowTransition { Scene = SceneId.Battle });
        }
    }
}
