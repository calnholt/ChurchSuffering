using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Data.Ids;
using Microsoft.Xna.Framework;
using Crusaders30XX.Diagnostics;
using System;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Processes start-of-turn applied passives for player and enemy.
    /// Currently supports Burn: deals 1 damage to the owner per stack at start of their turn.
    /// </summary>
    public class AppliedPassivesManagementSystem : Core.System
    {
        public static readonly float Duration = 0.5f;
        public AppliedPassivesManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
            EventManager.Subscribe<ApplyEffect>(OnApplyEffect);
            EventManager.Subscribe<RemovePassive>(OnRemovePassive, priority: 1);
            EventManager.Subscribe<UpdatePassive>(OnUpdatePassive);
		EventManager.Subscribe<LoadSceneEvent>(OnLoadScene, priority: 1);
            EventManager.Subscribe<RemoveAllPassives>(OnRemoveAllPassives);
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            var playedCard = evt?.Card?.GetComponent<CardData>()?.Card;
            if (playedCard == null || playedCard.Type != CardType.Attack || playedCard.IsWeapon) return;

            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var passives = player?.GetComponent<AppliedPassives>()?.Passives;
            if (passives == null
                || !passives.TryGetValue(AppliedPassiveType.SwordIntoShield, out int stacks)
                || stacks <= 0)
            {
                return;
            }

            AttackDamageValueService.ApplyDelta(evt.Card, stacks, "Sword Into Shield");
            EventManager.Publish(new RemovePassive
            {
                Owner = player,
                Type = AppliedPassiveType.SwordIntoShield
            });
            EventManager.Publish(new PassiveTriggered
            {
                Owner = player,
                Type = AppliedPassiveType.SwordIntoShield
            });
        }

        private void OnApplyEffect(ApplyEffect effect)
        {
            LoggingService.Append("AppliedPassivesManagementSystem.OnApplyEffect", new System.Text.Json.Nodes.JsonObject
            {
                ["effectType"] = effect.EffectType ?? "unknown",
                ["amount"] = effect.Amount,
                ["targetId"] = effect.Target?.Id ?? -1
            });
            var typeName = effect.EffectType ?? string.Empty;
            if (!Enum.TryParse<AppliedPassiveType>(typeName, true, out var passiveType)) return;
            EventManager.Publish(new ApplyPassiveEvent { Delta = effect.Amount, Target = effect.Target, Type = passiveType });
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            // No per-frame updates; event-driven
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            LoggingService.Append("AppliedPassivesManagementSystem.OnChangeBattlePhase", new System.Text.Json.Nodes.JsonObject
            {
                ["phase"] = evt.Current.ToString()
            });
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (evt == null) return;
            if (evt.Current == SubPhase.StartBattle)
            {
                var enemyBase = enemy.GetComponent<Enemy>();
                if (enemyBase != null && enemyBase.EnemyBase != null && enemyBase.EnemyBase.OnStartOfBattle != null)
                {
                    LoggingService.Append("AppliedPassivesManagementSystem.OnChangeBattlePhase.StartOfBattle", new System.Text.Json.Nodes.JsonObject { ["enemyId"] = enemyBase.EnemyBase.Id.ToKey() });
                    EventManager.Publish(new StartDebuffAnimation { TargetIsPlayer = false });
                    enemyBase.EnemyBase.OnStartOfBattle(EntityManager);
                }
                EnemyShieldsMaintenance(enemy);
                RemoveOneScarAtStartOfBattle(player);
            }
            if (evt.Current == SubPhase.PlayerEnd)
            {
                ApplyEndOfTurnPassives(player);
                RemoveTurnPassives(player);
            }
            else if (evt.Current == SubPhase.EnemyStart)
            {
                EnemyShieldsMaintenance(enemy);
                ExpireGuardAtEnemyStart(enemy);
                ApplyStartOfTurnPassives(enemy);
            }
            else if (evt.Current == SubPhase.EnemyEnd)
            {
                RemoveTurnPassives(enemy);
            }
            else if (evt.Current == SubPhase.PlayerStart)
            {
                ApplyStartOfTurnPassives(player);
            }
            else if (evt.Current == SubPhase.PreBlock)
            {
                ApplyStartOfPreBlockPassives(enemy);
                ApplySubZeroFreeze(player);
            }
        }

        private void ApplySubZeroFreeze(Entity player)
        {
            var ap = player?.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.SubZero, out int stacks) || stacks <= 0) return;

            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = stacks,
                Type = CardApplicationType.Frozen,
                Target = CardApplicationTarget.Hand,
            });
            EventManager.Publish(new PassiveTriggered { Owner = player, Type = AppliedPassiveType.SubZero });
        }

        private void OnLoadScene(LoadSceneEvent @event)
        {
            LoggingService.Append("AppliedPassivesManagementSystem.OnLoadScene", new System.Text.Json.Nodes.JsonObject
            {
                ["sceneId"] = @event.Scene.ToString()
            });
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player == null) return;
            var ap = player.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;

            var keep = @event.Scene == SceneId.Battle
                ? new HashSet<AppliedPassiveType>(GetRunLongPassives().Concat(GetQuestPassives()))
                : new HashSet<AppliedPassiveType>(GetRunLongPassives());

            foreach (var passive in ap.Passives.Keys.ToList())
            {
                if (keep.Contains(passive)) continue;
                EventManager.Publish(new RemovePassive { Owner = player, Type = passive });
            }

            if (player.HasComponent<Player>())
            {
                RunScopedStateService.SyncRunLongPassivesFromPlayer(player);
            }
        }

        private void EnemyShieldsMaintenance(Entity enemyEntity)
        {
            var enemy = enemyEntity.GetComponent<Enemy>();
            var ap = enemyEntity.GetComponent<AppliedPassives>();
            if (ap.Passives.TryGetValue(AppliedPassiveType.Shield, out int shieldAmount) && shieldAmount > 0)
            {
                EventManager.Publish(new ApplyPassiveEvent { Target = enemyEntity, Type = AppliedPassiveType.Shield, Delta = 1 });
            }
        }

        private void ExpireGuardAtEnemyStart(Entity enemy)
        {
            var ap = enemy?.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.Guard, out int guardStacks) || guardStacks <= 0) return;

            EventQueueBridge.EnqueueTriggerAction("AppliedPassivesManagementSystem.ExpireGuardAtEnemyStart", () =>
            {
                EventManager.Publish(new RemovePassive { Owner = enemy, Type = AppliedPassiveType.Guard });
                EventManager.Publish(new PassiveTriggered { Owner = enemy, Type = AppliedPassiveType.Guard });
            }, Duration);
        }

        private void RemoveOneScarAtStartOfBattle(Entity player)
        {
            var ap = player?.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.Scar, out int scarStacks) || scarStacks <= 0) return;

            EventQueueBridge.EnqueueTriggerAction("AppliedPassivesManagementSystem.RemoveOneScarAtStartOfBattle", () =>
            {
                EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Scar, Delta = -1 });
                EventManager.Publish(new PassiveTriggered { Owner = player, Type = AppliedPassiveType.Scar });
            }, Duration);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            RemoveOneFearAtBattleEnd(player);
        }

        private void RemoveOneFearAtBattleEnd(Entity player)
        {
            var ap = player?.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.Fear, out int fearStacks) || fearStacks <= 0) return;

            EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Fear, Delta = -1 });
            EventManager.Publish(new PassiveTriggered { Owner = player, Type = AppliedPassiveType.Fear });
        }
        private void ApplyStartOfTurnPassives(Entity owner)
        {
            var ap = owner.GetComponent<AppliedPassives>();
            if (ap == null) return;
            if (ap == null || ap.Passives == null || ap.Passives.Count == 0) return;
            var hasInferno = ap.Passives.TryGetValue(AppliedPassiveType.Inferno, out int infernoStacks);
            if (hasInferno && infernoStacks > 0)
            {
                EventQueueBridge.EnqueueTriggerAction("AppliedPassivesManagementSystem.ApplyStartOfTurnPassives.Inferno", () =>
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = owner, Type = AppliedPassiveType.Burn, Delta = infernoStacks });
                    EventManager.Publish(new PassiveTriggered { Owner = owner, Type = AppliedPassiveType.Inferno });
                }, Duration);
            }
            if ((ap.Passives.TryGetValue(AppliedPassiveType.Burn, out int burnStacks) || hasInferno) && (burnStacks > 0 || infernoStacks > 0))
            {
                LoggingService.Append("AppliedPassivesManagementSystem.ApplyStartOfTurnPassives.Burn", new System.Text.Json.Nodes.JsonObject { ["burn"] = burnStacks, ["inferno"] = infernoStacks });
                EventQueueBridge.EnqueueTriggerAction("AppliedPassivesManagementSystem.ApplyStartOfTurnPassives.Burn", () =>
                {
                    EventManager.Publish(new ModifyHpRequestEvent { Source = owner, Target = owner, Delta = -(burnStacks + infernoStacks), DamageType = ModifyTypeEnum.Effect });
                    EventManager.Publish(new PassiveTriggered { Owner = owner, Type = AppliedPassiveType.Burn });
                }, Duration);
            }
            if (ap.Passives.TryGetValue(AppliedPassiveType.Webbing, out int webbingStacks) && webbingStacks > 0)
            {
                EventQueueBridge.EnqueueTriggerAction("AppliedPassivesManagementSystem.ApplyStartOfTurnPassives.Webbing", () =>
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = owner, Type = AppliedPassiveType.Slow, Delta = webbingStacks });
                    EventManager.Publish(new PassiveTriggered { Owner = owner, Type = AppliedPassiveType.Webbing });
                }, Duration);
            }
        }

        private void ApplyStartOfPreBlockPassives(Entity enemy)
        {
            var ap = enemy.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null || ap.Passives.Count == 0) return;

            if (ap.Passives.TryGetValue(AppliedPassiveType.Stun, out int stunStacks) && stunStacks > 0)
            {
                var intent = enemy.GetComponent<AttackIntent>();
                if (intent == null || intent.Planned == null || intent.Planned.Count == 0) return;
                var count = stunStacks > intent.Planned.Count ? intent.Planned.Count : stunStacks;
                for (int i = 0; i < count; i++)
                {
                    EventQueueBridge.EnqueueTriggerAction("AppliedPassivesManagementSystem.ApplyStartOfTurnPassives.Stun", () =>
                    {
                        EventManager.Publish(new ShowStunnedOverlay());
                        EventManager.Publish(new PassiveTriggered { Owner = enemy, Type = AppliedPassiveType.Stun });
                        EventManager.Publish(new UpdatePassive { Owner = enemy, Type = AppliedPassiveType.Stun, Delta = -1 });
                        intent.Planned.RemoveAt(0);
                        if (intent.Planned.Count == 0)
                        {
                            EventQueue.Clear();
                            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                                "Rule.ChangePhase.EnemyEnd",
                                new ChangeBattlePhaseEvent { Current = SubPhase.EnemyEnd }
                            ));
                            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                                "Rule.ChangePhase.PlayerStart",
                                new ChangeBattlePhaseEvent { Current = SubPhase.PlayerStart }
                            ));
                            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                                "Rule.ChangePhase.Action",
                                new ChangeBattlePhaseEvent { Current = SubPhase.Action }
                            ));
                        }
                    }, Duration);
                }
            }

            if (ap.Passives.TryGetValue(AppliedPassiveType.Aggression, out int aggressionStacks) && aggressionStacks > 0)
            {
                var attackDef = GetComponentHelper.GetPlannedAttack(EntityManager);
                if (attackDef == null) return;
                attackDef.Damage += aggressionStacks;
                attackDef.AdditionalDamage += aggressionStacks;
                LoggingService.Append("AppliedPassivesManagementSystem.ApplyStartOfPreBlockPassives.Aggression", new System.Text.Json.Nodes.JsonObject { ["damage"] = attackDef.Damage });
                EventManager.Publish(new PassiveTriggered { Owner = enemy, Type = AppliedPassiveType.Aggression });
                TimerScheduler.Schedule(Duration, () =>
                {
                    EventManager.Publish(new RemovePassive { Owner = enemy, Type = AppliedPassiveType.Aggression });
                });
            }
            if (ap.Passives.TryGetValue(AppliedPassiveType.Power, out int powerStacks) && powerStacks > 0)
            {
                var attackDef = GetComponentHelper.GetPlannedAttack(EntityManager);
                if (attackDef == null) return;
                attackDef.Damage += powerStacks;
                attackDef.AdditionalDamage += powerStacks;
                LoggingService.Append("AppliedPassivesManagementSystem.ApplyStartOfPreBlockPassives.Power", new System.Text.Json.Nodes.JsonObject { ["damage"] = attackDef.Damage });
                EventManager.Publish(new PassiveTriggered { Owner = enemy, Type = AppliedPassiveType.Power });
            }
        }

        private void OnApplyPassive(ApplyPassiveEvent e)
        {
            LoggingService.Append("AppliedPassivesManagementSystem.OnApplyPassive", new System.Text.Json.Nodes.JsonObject
            {
                ["passiveType"] = e.Type.ToString(),
                ["delta"] = e.Delta,
                ["targetId"] = e.Target?.Id ?? -1
            });
            if (e == null || e.Target == null) return;
            var ap = e.Target.GetComponent<AppliedPassives>();
            if (ap == null)
            {
                // Create if missing on the target entity
                EntityManager.AddComponent(e.Target, new AppliedPassives());
                ap = e.Target.GetComponent<AppliedPassives>();
            }
            if (ap == null) return;

            ap.Passives.TryGetValue(e.Type, out int current);
            int next = current + e.Delta;
            LoggingService.Append("AppliedPassivesManagementSystem.OnApplyPassive", new System.Text.Json.Nodes.JsonObject { ["type"] = e.Type.ToString(), ["before"] = current, ["after"] = next });
            if (next <= 0)
            {
                // remove from dictionary if zero or less
                if (ap.Passives.ContainsKey(e.Type)) ap.Passives.Remove(e.Type);
            }
            else
            {
                ap.Passives[e.Type] = next;
            }

            switch (e.Type)
            {
                case AppliedPassiveType.Frostbite:
                    if (e.Delta > 0 && next >= TooltipTextService.FrostbiteThreshold)
                    {
                        ResolveFrostbiteThresholds(e.Target, ap, next);
                    }
                    break;
                default:
                    break;
            }

            if (e.Target.HasComponent<Player>() && GetRunLongPassives().Contains(e.Type))
            {
                RunScopedStateService.SyncRunLongPassivesFromPlayer(e.Target);
            }
        }

        private void ResolveFrostbiteThresholds(Entity target, AppliedPassives passives, int totalStacks)
        {
            int threshold = TooltipTextService.FrostbiteThreshold;
            if (target == null || passives?.Passives == null || threshold <= 0) return;

            int triggerCount = totalStacks / threshold;
            if (triggerCount <= 0) return;

            int remainingStacks = totalStacks % threshold;
            if (remainingStacks > 0)
            {
                passives.Passives[AppliedPassiveType.Frostbite] = remainingStacks;
            }
            else
            {
                passives.Passives.Remove(AppliedPassiveType.Frostbite);
            }

            for (int i = 0; i < triggerCount; i++)
            {
                EventManager.Publish(new FrostbiteTriggered
                {
                    Target = target,
                    DamageAmount = TooltipTextService.FrostbiteDamage,
                    TriggerIndex = i + 1,
                    TriggerCount = triggerCount
                });

                var request = new ReplaceableEffectRequest
                {
                    Kind = ReplaceableEffectKind.FrostbiteThresholdDamage,
                    OriginalSource = target,
                    OriginalTarget = target,
                    OriginalDelta = -TooltipTextService.FrostbiteDamage,
                    DamageType = ModifyTypeEnum.Effect,
                    PassiveType = AppliedPassiveType.Frostbite.ToString()
                };

                EventManager.Publish(request);

                if (!request.IsHandled)
                {
                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = target,
                        Target = target,
                        Delta = -TooltipTextService.FrostbiteDamage,
                        DamageType = ModifyTypeEnum.Effect
                    });
                }
            }
        }

        private void OnRemovePassive(RemovePassive e)
        {
            if (e == null || e.Owner == null) return;
            var ap = e.Owner.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            if (ap.Passives.TryGetValue(e.Type, out int stacks))
            {
                e.Amount = stacks;
            }
            LoggingService.Append("AppliedPassivesManagementSystem.OnRemovePassive", new System.Text.Json.Nodes.JsonObject
            {
                ["passiveType"] = e.Type.ToString(),
                ["amount"] = e.Amount,
                ["ownerId"] = e.Owner?.Id ?? -1,
                ["ownerName"] = e.Owner?.Name
            });
            if (ap.Passives.ContainsKey(e.Type))
            {
                ap.Passives.Remove(e.Type);
            }

            if (e.Owner.HasComponent<Player>() && GetRunLongPassives().Contains(e.Type))
            {
                RunScopedStateService.SyncRunLongPassivesFromPlayer(e.Owner);
            }
        }

        private void OnUpdatePassive(UpdatePassive e)
        {
            LoggingService.Append("AppliedPassivesManagementSystem.OnUpdatePassive", new System.Text.Json.Nodes.JsonObject
            {
                ["passiveType"] = e.Type.ToString(),
                ["delta"] = e.Delta,
                ["ownerId"] = e.Owner?.Id ?? -1
            });
            if (e == null || e.Owner == null) return;
            var ap = e.Owner.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            ap.Passives[e.Type] = ap.Passives[e.Type] + e.Delta;
            if (ap.Passives[e.Type] <= 0)
            {
                ap.Passives.Remove(e.Type);
            }

            if (e.Owner.HasComponent<Player>() && GetRunLongPassives().Contains(e.Type))
            {
                RunScopedStateService.SyncRunLongPassivesFromPlayer(e.Owner);
            }
        }

        private void OnRemoveAllPassives(RemoveAllPassives e)
        {
            LoggingService.Append("AppliedPassivesManagementSystem.OnRemoveAllPassives", new System.Text.Json.Nodes.JsonObject
            {
                ["ownerId"] = e.Owner?.Id ?? -1
            });
            if (e == null || e.Owner == null) return;
            var ap = e.Owner.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            ap.Passives.Clear();
        }

        private void ApplyEndOfTurnPassives(Entity owner)
        {
            if (owner == null) return;
            var ap = owner.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.CarpeDiem, out int stacks) || stacks <= 0) return;

            EventManager.Publish(new PassiveTriggered { Owner = owner, Type = AppliedPassiveType.CarpeDiem });
            EventManager.Publish(new SetCourageEvent { Amount = 0 });
        }

        private void RemoveTurnPassives(Entity owner)
        {
            foreach (var passive in GetTurnPassives().ToList())
            {
                var ap = owner.GetComponent<AppliedPassives>();
                if (ap == null || ap.Passives == null) continue;
                ap.Passives.TryGetValue(passive, out int stacks);
                EventManager.Publish(new RemovePassive { Owner = owner, Type = passive, Amount = stacks });
            }
            foreach (var passive in GetTurnPassivesToDecrement().ToList())
            {
                var ap = owner.GetComponent<AppliedPassives>();
                if (ap == null || ap.Passives == null) continue;
                if (!ap.Passives.TryGetValue(passive, out int stacks) || stacks <= 0) continue;
                EventManager.Publish(new UpdatePassive { Owner = owner, Type = passive, Delta = -1 });
                EventManager.Publish(new PassiveTriggered { Owner = owner, Type = passive });
            }
        }

        public static HashSet<AppliedPassiveType> GetTurnPassives()
        {
            return new HashSet<AppliedPassiveType>
            {
                AppliedPassiveType.DowseWithHolyWater,
                AppliedPassiveType.Aggression,
                AppliedPassiveType.Galvanize,
                AppliedPassiveType.Sharpen,
                AppliedPassiveType.Might,
                AppliedPassiveType.CarpeDiem,
                AppliedPassiveType.SwordIntoShield,
            };
        }

        public static HashSet<AppliedPassiveType> GetTurnPassivesToDecrement()
        {
            return new HashSet<AppliedPassiveType>
            {
                AppliedPassiveType.Silenced,
                AppliedPassiveType.Slow,
            };
        }

        public static HashSet<AppliedPassiveType> GetBattlePassives()
        {
            return new HashSet<AppliedPassiveType>
            {
                AppliedPassiveType.Stun,
                AppliedPassiveType.Burn,
                AppliedPassiveType.Power,
                AppliedPassiveType.Armor,
                AppliedPassiveType.Wounded,
                AppliedPassiveType.Slow,
                AppliedPassiveType.Inferno,
                AppliedPassiveType.Stealth,
                AppliedPassiveType.Poison,
                AppliedPassiveType.Siphon,
                AppliedPassiveType.Thorns,
                AppliedPassiveType.Rage,
                AppliedPassiveType.Intimidated,
                AppliedPassiveType.MindFog,
                AppliedPassiveType.Channel,
                AppliedPassiveType.Windchill,
                AppliedPassiveType.SubZero,
                AppliedPassiveType.Aegis,
                AppliedPassiveType.Guard,
                AppliedPassiveType.Anathema,
                AppliedPassiveType.Plunder,
                AppliedPassiveType.Vigor,
                AppliedPassiveType.Enflamed,
            };
        }
        public static HashSet<AppliedPassiveType> GetRunLongPassives()
        {
            return new HashSet<AppliedPassiveType>
            {
                AppliedPassiveType.Frostbite,
                AppliedPassiveType.Scar,
                AppliedPassiveType.Bleed,
                AppliedPassiveType.Shackled,
                AppliedPassiveType.Fear,
            };
        }

        public static HashSet<AppliedPassiveType> GetQuestPassives()
        {
            return new HashSet<AppliedPassiveType>
            {
                AppliedPassiveType.Webbing,
                AppliedPassiveType.Sealed,
            };
        }
    }
}
