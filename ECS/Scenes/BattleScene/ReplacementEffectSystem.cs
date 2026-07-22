using System;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    public class ReplacementEffectSystem : Core.System
    {
        public ReplacementEffectSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ReplaceableEffectRequest>(OnReplaceableEffectRequest);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnReplaceableEffectRequest(ReplaceableEffectRequest request)
        {
            if (request == null || request.IsHandled) return;

            var medals = EntityManager.GetEntitiesWithComponent<EquippedMedal>()
                .OrderBy(entity => entity.Id)
                .ToList();

            foreach (var medalEntity in medals)
            {
                var equipped = medalEntity.GetComponent<EquippedMedal>();
                if (equipped?.EquippedOwner != request.OriginalTarget) continue;
                if (equipped.Medal is not IReplacementEffectProvider provider) continue;
                if (!provider.TryReplace(request)) continue;

                request.IsHandled = true;
                request.HandlingMedalEntity ??= medalEntity;
                request.HandlingMedalId = string.IsNullOrWhiteSpace(request.HandlingMedalId)
                    ? equipped.Medal.Id
                    : request.HandlingMedalId;

                EventManager.Publish(new MedalTriggered
                {
                    MedalEntity = request.HandlingMedalEntity,
                    MedalId = request.HandlingMedalId
                });

                ExecuteActions(request);
                LogRequest(request);
                return;
            }

            var pledgedCards = EntityManager.GetEntitiesWithComponent<Pledge>()
                .OrderBy(entity => entity.Id)
                .ToList();

            foreach (var cardEntity in pledgedCards)
            {
                var card = cardEntity.GetComponent<CardData>()?.Card;
                if (card is not IReplacementEffectProvider provider) continue;
                if (!provider.TryReplace(request)) continue;

                request.IsHandled = true;
                ExecuteActions(request);
                LogRequest(request);
                return;
            }

            LogRequest(request);
        }

        private static void LogRequest(ReplaceableEffectRequest request)
        {
            LoggingService.Append("ReplacementEffectSystem.OnReplaceableEffectRequest", new JsonObject
            {
                ["kind"] = request.Kind.ToString(),
                ["targetId"] = request.OriginalTarget?.Id ?? -1,
                ["handled"] = request.IsHandled,
                ["medalId"] = request.HandlingMedalId ?? string.Empty,
                ["actionCount"] = request.Actions.Count
            });
        }

        private static void ExecuteActions(ReplaceableEffectRequest request)
        {
            foreach (var action in request.Actions)
            {
                if (action == null) continue;
                switch (action.Type)
                {
                    case ReplacementEffectActionType.ModifyHp:
                        EventManager.Publish(new ModifyHpRequestEvent
                        {
                            Source = action.Source,
                            Target = action.Target,
                            Delta = action.Delta,
                            DamageType = action.DamageType
                        });
                        break;
                    case ReplacementEffectActionType.ApplyPassive:
                        EventManager.Publish(new ApplyPassiveEvent
                        {
                            Target = action.Target,
                            Type = action.PassiveType,
                            Delta = action.Delta
                        });
                        break;
                }
            }
        }
    }
}
