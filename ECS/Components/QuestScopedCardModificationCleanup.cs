using System;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Components
{
    /// <summary>
    /// Removes quest-scoped ModifiedDamage/ModifiedBlock when the quest node completes.
    /// </summary>
    public class QuestScopedCardModificationCleanup : IComponent, IDisposable
    {
        public Entity Owner { get; set; }
        public string ModificationReason { get; set; } = "";
        public bool UseBlock { get; set; }
        private EntityManager _entityManager;
        private bool _disposed;

        public void Initialize(EntityManager entityManager, Entity card)
        {
            Owner = card;
            _entityManager = entityManager;
            EventManager.Subscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        private void OnQuestComplete(ShowQuestRewardOverlay evt)
        {
            if (_disposed || Owner == null) return;
            if (UseBlock)
            {
                BlockValueService.RemoveModification(Owner, ModificationReason);
            }
            else
            {
                AttackDamageValueService.RemoveModification(Owner, ModificationReason);
            }
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            EventManager.Unsubscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }
    }
}
