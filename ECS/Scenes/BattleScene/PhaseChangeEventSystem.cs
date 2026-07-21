using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using System.Collections.Generic;
using System;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
    public class PhaseChangeEventSystem : Core.System
    {
        private bool _waitingForAnimation = false;
        private int _lastSeenAttackSequence = -1;
        private int _lastTurn = -1;
        private bool _firstBlockProcessed = false;

        public PhaseChangeEventSystem(EntityManager entityManager) : base(entityManager)
        {
            // Simplified handler: only handle subsequent blocks in same turn
            EventManager.Subscribe<ChangeBattlePhaseEvent>(evt =>
            {
                LoggingService.Append("PhaseChangeEventSystem.OnChangeBattlePhaseEvent", new JsonObject {
                    { "Current", evt.Current.ToString() },
                    { "Previous", evt.Previous.ToString() }
                });
                if (evt.Current == SubPhase.Block)
                {
                    var phaseState = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
                    if (phaseState != null && phaseState.TurnNumber != _lastTurn)
                    {
                        // New turn detected - reset state (CheckAndTriggerNextAttack will handle first block)
                        _lastTurn = phaseState.TurnNumber;
                        _firstBlockProcessed = false;
                    }

                // For subsequent blocks in same turn, clear waiting flag and reset sequence so attack display triggers
                if (_firstBlockProcessed)
                {
                    _waitingForAnimation = false;
                    _lastSeenAttackSequence = -1;
                }
                }
            });

            EventManager.Subscribe<BattlePhaseAnimationCompleteEvent>(evt =>
            {
                LoggingService.Append("PhaseChangeEventSystem.OnBattlePhaseAnimationCompleteEvent", new JsonObject {
                    { "SubPhase", evt.SubPhase.ToString() }
                });
                // Mark first block as processed when Block phase animation completes
                if (evt.SubPhase == SubPhase.Block)
                {
                    _firstBlockProcessed = true;
                }
                _waitingForAnimation = false;
                CheckAndTriggerNextAttack();
            });
            EventManager.Subscribe<DeleteCachesEvent>(_ => {
                LoggingService.Append("PhaseChangeEventSystem.OnDeleteCachesEvent", new JsonObject { });
                _waitingForAnimation = false;
                _lastSeenAttackSequence = -1;
                _lastTurn = -1;
                _firstBlockProcessed = false;
            });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<AttackIntent>();
        }

        protected override void UpdateEntity(Entity entity, Microsoft.Xna.Framework.GameTime gameTime)
        {
            CheckAndTriggerNextAttack();
        }

        private void CheckAndTriggerNextAttack()
        {
            var attackIntentEntity = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
            if (attackIntentEntity == null)
            {
                return;
            }

            var intent = attackIntentEntity.GetComponent<AttackIntent>();
            if (intent == null || intent.Planned.Count == 0)
            {
                return;
            }

            var currentSequence = intent.ActiveAttackSequence;

            var phaseState = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
            if (phaseState == null)
            {
                return;
            }

            // Detect new turn
            if (phaseState.TurnNumber != _lastTurn)
            {
                _lastTurn = phaseState.TurnNumber;
                _firstBlockProcessed = false;
            }

            // If we're in Block phase and haven't processed first block yet, wait for animation
            if (phaseState.Sub == SubPhase.Block && !_firstBlockProcessed)
            {
                _waitingForAnimation = true;
                _lastSeenAttackSequence = -1;
                return; // Don't trigger - wait for BattlePhaseAnimationCompleteEvent
            }
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (enemy == null)
            {
                return;
            }
            var enemyCmp = enemy.GetComponent<Enemy>();
            if (enemyCmp == null)
            {
                return;
            }
            if (currentSequence != _lastSeenAttackSequence && phaseState.Sub == SubPhase.Block && enemyCmp.CurrentHealth > 0)
            {
                if (!_waitingForAnimation)
                {
                    EventManager.Publish(new TriggerEnemyAttackDisplayEvent());
                    _lastSeenAttackSequence = currentSequence;
                }
            }
        }
    }
}
