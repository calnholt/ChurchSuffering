using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;
using Crusaders30XX.Diagnostics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Clears BattleStateInfo tracking dictionaries at key transitions.
    /// - Clear PhaseTracking whenever sub phase changes
    /// - Clear TurnTracking when Enemy turn starts
    /// - Clear BattleTracking when a new battle starts
    /// - Clear RunTracking when loading Battle scene
    /// </summary>
    [DebugTab("Battle State Info")]
    public class BattleStateInfoManagementSystem : Core.System
    {
        public BattleStateInfoManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<StartBattleRequested>(_ => ClearBattleTracking());
            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
            EventManager.Subscribe<TrackingEvent>(OnTrackingEvent);
            EventManager.Subscribe<ModifyCourageRequestEvent>(OnModifyCourage);
            EventManager.Subscribe<SetCourageEvent>(OnSetCourageEvent, priority: 10);
            EventManager.Subscribe<ApplyEffect>(OnApplyEffect);
            EventManager.Subscribe<ModifyHpEvent>(OnModifyHp);
            EventManager.Subscribe<TopCardRemovedForMillEvent>(OnTopCardRemovedForMill);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            LoggingService.Append("BattleStateInfoManagementSystem.OnChangeBattlePhase", new System.Text.Json.Nodes.JsonObject
            {
                ["current"] = evt.Current.ToString(),
                ["previous"] = evt.Previous.ToString()
            });
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player == null) return;
            var st = player.GetComponent<BattleStateInfo>();
            // Clear per-subphase tracking whenever sub phase changes
            st.PhaseTracking?.Clear();

            if (evt.Current == SubPhase.Action)
            {
                st.PlayerActionPhaseAttackHits = 0;
            }

            // When enemy turn starts, clear per-turn tracking
            if (evt.Current == SubPhase.EnemyStart)
            {
                st.TurnTracking?.Clear();
            }
        }

        private void OnModifyHp(ModifyHpEvent evt)
        {
            if (evt.DamageType != ModifyTypeEnum.Attack || evt.Delta >= 0) return;

            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
            if (phase?.Sub != SubPhase.Action) return;

            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (player == null || enemy == null) return;
            if (evt.Source != player || evt.Target != enemy) return;

            var st = player.GetComponent<BattleStateInfo>();
            if (st == null) return;
            st.PlayerActionPhaseAttackHits++;
        }

        private void OnTopCardRemovedForMill(TopCardRemovedForMillEvent evt)
        {
            if (evt?.Card == null) return;
            OnTrackingEvent(new TrackingEvent
            {
                Type = TrackingTypeEnum.CardsMilled.ToString(),
                Delta = 1
            });
        }

        private void ClearBattleTracking()
        {
            var st = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault().GetComponent<BattleStateInfo>();
            st.BattleTracking?.Clear();
        }

        private void OnLoadScene(LoadSceneEvent e)
        {
            LoggingService.Append("BattleStateInfoManagementSystem.OnLoadScene", new System.Text.Json.Nodes.JsonObject
            {
                ["scene"] = e.Scene.ToString()
            });
        }

        private void OnTrackingEvent(TrackingEvent e)
        {
          LoggingService.Append("BattleStateInfoManagementSystem.OnTrackingEvent", new System.Text.Json.Nodes.JsonObject
          {
              ["type"] = e.Type.ToString(),
              ["delta"] = e.Delta
          });
          AddToAllDictionaries(e);
        }

        private void AddToDict(Dictionary<string, int> dict, TrackingEvent e)
        {
          if (dict == null) return;
          dict.TryGetValue(e.Type.ToString(), out int current);
          int next = current + e.Delta;
          if (next == 0)
          {
              if (dict.ContainsKey(e.Type.ToString())) dict.Remove(e.Type.ToString());
          }
          else
          {
              dict[e.Type.ToString()] = next;
          }
        }

        private void AddToAllDictionaries(TrackingEvent e)
        {
          var st = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault().GetComponent<BattleStateInfo>();
          AddToDict(st.RunTracking, e);
          AddToDict(st.BattleTracking, e);
          AddToDict(st.TurnTracking, e);
          AddToDict(st.PhaseTracking, e);
        }

        private void OnModifyCourage(ModifyCourageRequestEvent e)
        {
          LoggingService.Append("BattleStateInfoManagementSystem.OnModifyCourage", new System.Text.Json.Nodes.JsonObject
          {
              ["delta"] = e.Delta
          });
          OnTrackingEvent(new TrackingEvent { Type = e.Delta > 0 ? TrackingTypeEnum.CourageGained.ToString() : TrackingTypeEnum.CourageLost.ToString(), Delta = Math.Abs(e.Delta) });
        }
        private void OnSetCourageEvent(SetCourageEvent e)
        {
          LoggingService.Append("BattleStateInfoManagementSystem.OnSetCourageEvent", new System.Text.Json.Nodes.JsonObject
          {
              ["amount"] = e.Amount
          });
          var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
          var courage = player.GetComponent<Courage>();
          var delta = courage.Amount - e.Amount;
          OnTrackingEvent(new TrackingEvent { Type = delta > 0 ? TrackingTypeEnum.CourageLost.ToString() : TrackingTypeEnum.CourageGained.ToString(), Delta = Math.Abs(delta) });
        }
        private void OnApplyEffect(ApplyEffect e)
        {
          LoggingService.Append("BattleStateInfoManagementSystem.OnApplyEffect", new System.Text.Json.Nodes.JsonObject
          {
              ["effectType"] = e.EffectType,
              ["amount"] = e.Amount,
              ["attackId"] = e.attackId
          });
          if (!string.IsNullOrEmpty(e.attackId))
          {
            OnTrackingEvent(new TrackingEvent { Type = TrackingTypeEnum.NumberOfAttacksHitPlayer.ToString(), Delta = 1 });
            OnTrackingEvent(new TrackingEvent { Type = e.attackId, Delta = 1 });
          }
        }

        [DebugAction("Print Tracking")]
        private void debug_PrintTracking()
        {
          var st = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault().GetComponent<BattleStateInfo>();
          LoggingService.Append("BattleStateInfoManagementSystem.debug_PrintTracking.RunTracking", new System.Text.Json.Nodes.JsonObject { ["tracking"] = string.Join(", ", st.RunTracking.Select(kvp => $"{kvp.Key}: {kvp.Value}")) });
          LoggingService.Append("BattleStateInfoManagementSystem.debug_PrintTracking.BattleTracking", new System.Text.Json.Nodes.JsonObject { ["tracking"] = string.Join(", ", st.BattleTracking.Select(kvp => $"{kvp.Key}: {kvp.Value}")) });
          LoggingService.Append("BattleStateInfoManagementSystem.debug_PrintTracking.TurnTracking", new System.Text.Json.Nodes.JsonObject { ["tracking"] = string.Join(", ", st.TurnTracking.Select(kvp => $"{kvp.Key}: {kvp.Value}")) });
          LoggingService.Append("BattleStateInfoManagementSystem.debug_PrintTracking.PhaseTracking", new System.Text.Json.Nodes.JsonObject { ["tracking"] = string.Join(", ", st.PhaseTracking.Select(kvp => $"{kvp.Key}: {kvp.Value}")) });
        }
    }
}


