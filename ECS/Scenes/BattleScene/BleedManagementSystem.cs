using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Handles bleed penalty when the player confirms blocks with 2+ cards of the same color.
    /// Each qualifying color deals 1 HP and removes 1 bleed stack (while stacks remain).
    /// </summary>
    public class BleedManagementSystem : Core.System
    {
        private const int ConfirmPriority = 10;

        public BleedManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ConfirmBlocksRequested>(OnConfirmBlocksRequested, ConfirmPriority);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        /// <summary>
        /// Returns how many colors have 2 or more blocking cards assigned.
        /// </summary>
        public static int GetQualifyingSameColorCount(EnemyAttackProgress progress)
        {
            if (progress == null) return 0;
            int count = 0;
            if (progress.PlayedRed >= 2) count++;
            if (progress.PlayedWhite >= 2) count++;
            if (progress.PlayedBlack >= 2) count++;
            return count;
        }

        private void OnConfirmBlocksRequested(ConfirmBlocksRequested _)
        {
            var player = EntityManager.GetEntity("Player");
            if (player == null) return;

            var ap = player.GetComponent<AppliedPassives>();
            if (ap?.Passives == null) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.Bleed, out int bleedStacks) || bleedStacks <= 0) return;

            if (!EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress)) return;

            int qualifyingColors = GetQualifyingSameColorCount(progress);
            if (qualifyingColors <= 0) return;

            int toApply = Math.Min(qualifyingColors, bleedStacks);
            for (int i = 0; i < toApply; i++)
            {
                LoggingService.Append("BleedManagementSystem.OnConfirmBlocksRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["trigger"] = i + 1,
                    ["qualifyingColors"] = qualifyingColors,
                    ["bleedStacksBefore"] = bleedStacks
                });
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = player,
                    Delta = -1,
                    DamageType = ModifyTypeEnum.Effect
                });
                EventManager.Publish(new PassiveTriggered { Owner = player, Type = AppliedPassiveType.Bleed });
                EventManager.Publish(new UpdatePassive { Owner = player, Type = AppliedPassiveType.Bleed, Delta = -1 });
                bleedStacks--;
            }
        }
    }
}
