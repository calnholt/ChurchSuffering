using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.Diagnostics;
using Microsoft.Xna.Framework;
using System;
using System.Diagnostics.Tracing;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("HP Management")]
	public class HpManagementSystem : Core.System
	{
		public HpManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ModifyHpRequestEvent>(OnModifyHpRequest);
			EventManager.Subscribe<SetHpEvent>(OnSetHp);
			EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
			EventManager.Subscribe<FullyHealEvent>(OnFullyHeal);
			EventManager.Subscribe<HealEvent>(OnHeal);
			EventManager.Subscribe<IncreaseMaxHpEvent>(OnIncreaseMaxHp);
			EventManager.Subscribe<ApplyBattleMaxHpEvent>(OnApplyBattleMaxHp);
			EventManager.Subscribe<RemovePassive>(OnRemovePassive);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<HP>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnModifyHpRequest(ModifyHpRequestEvent e)
		{
			LoggingService.Append("HpManagementSystem.OnModifyHpRequest", new System.Text.Json.Nodes.JsonObject
			{
				["delta"] = e.Delta,
				["damageType"] = e.DamageType.ToString(),
				["targetId"] = e.Target?.Id ?? -1
			});
			var target = ResolveTarget(e.Target);
			if (target == null) return;
			var hp = target.GetComponent<HP>();
			if (hp == null) return;
			int before = hp.Current;
			// Guard absorption: absorb raw incoming attack damage before passives
			if (e.Delta < 0 && e.DamageType == ModifyTypeEnum.Attack)
			{
				int guardAbsorbed = TryConsumeGuard(target, Math.Abs(e.Delta));
				if (guardAbsorbed > 0)
				{
					e.Delta += guardAbsorbed;
					if (e.Delta >= 0) return;
				}
			}
			// TODO: iterate through applied passives and apply their effects
			int passiveDelta = AppliedPassivesService.GetPassiveDelta(e);
			LoggingService.Append("HpManagementSystem.OnModifyHpRequest", new System.Text.Json.Nodes.JsonObject { ["passiveDelta"] = passiveDelta });
			int newDelta = e.Delta + passiveDelta;
			if (e.DamageType == ModifyTypeEnum.Heal && newDelta < 0)
			{
				// Prevent heals from turning into damage after passive adjustments
				newDelta = 0;
			}
			if (e.DamageType == ModifyTypeEnum.Attack && newDelta > 0)
			{
				return;
			}
			var targetPassives = target.GetComponent<AppliedPassives>()?.Passives;
			if (targetPassives != null && newDelta < 0)
			{
				// Skip aegis consumption if the attack ignores aegis
				if (!e.IgnoresAegis && TryConsumeAegis(target, targetPassives, ref newDelta))
				{
					e.Delta = newDelta - passiveDelta;
				}
			}
			if (TryConsumeShield(target, targetPassives, newDelta))
			{
				return;
			}
			int nv = hp.Current + newDelta;
			var presentationId = Guid.NewGuid();
			EventManager.Publish(new ModifyHpEvent
			{
				Source = e.Source,
				Target = target,
				Delta = newDelta,
				DamageType = e.DamageType,
				PresentationId = presentationId
			});
			hp.Current = Math.Max(0, Math.Min(hp.Max, nv));
			// If this is the player and we crossed to zero, publish PlayerDied once
			if (before > 0 && hp.Current == 0 && target.HasComponent<Player>())
			{
				EventManager.Publish(new PlayerDied { Player = target });
			}
			else if (before > 0 && hp.Current == 0 && target.HasComponent<Enemy>())
			{
				if (target.HasComponent<SuppressPortraitRender>()) return;
				var enemyBase = target.GetComponent<Enemy>()?.EnemyBase;
				if (enemyBase != null && enemyBase.Phases > 1)
				{
					EventManager.Publish(new EnemyPhaseLethalEvent
					{
						Enemy = target,
						DamagePresentationId = presentationId,
						DamageType = e.DamageType
					});
					return;
				}
				LoggingService.Append("HpManagementSystem.OnModifyHpRequest.EnemyDied", new System.Text.Json.Nodes.JsonObject
				{
					["message"] = "enemy defeated, begin presentation"
				});
				EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = target, IsPreview = false });
			}
		}

		private void OnSetHp(SetHpEvent e)
		{
			LoggingService.Append("HpManagementSystem.OnSetHp", new System.Text.Json.Nodes.JsonObject
			{
				["value"] = e.Value,
				["targetId"] = e.Target?.Id ?? -1
			});
			var target = ResolveTarget(e.Target);
			if (target == null) return;
			var hp = target.GetComponent<HP>();
			if (hp == null) return;
			int before = hp.Current;
			hp.Current = Math.Max(0, Math.Min(hp.Max, e.Value));
			if (before > 0 && hp.Current == 0 && target.HasComponent<Player>())
			{
				EventManager.Publish(new PlayerDied { Player = target });
			}
		}

		private void OnHeal(HealEvent e)
		{
			LoggingService.Append("HpManagementSystem.OnHeal", new System.Text.Json.Nodes.JsonObject
			{
				["delta"] = e.Delta,
				["targetId"] = e.Target?.Id ?? -1
			});
			var target = ResolveTarget(e.Target);
			if (target == null) return;
			var hp = target.GetComponent<HP>();
			if (hp == null) return;
			hp.Current = Math.Max(0, Math.Min(hp.Max, hp.Current + e.Delta));
		}

		private void OnFullyHeal(FullyHealEvent e)
		{
			LoggingService.Append("HpManagementSystem.OnFullyHeal", new System.Text.Json.Nodes.JsonObject
			{
				["targetId"] = e.Target?.Id ?? -1
			});
			var target = ResolveTarget(e.Target);
			if (target == null || !target.HasComponent<Player>()) return;
			var hp = target.GetComponent<HP>();
			if (hp == null) return;
			hp.Current = hp.Max; // fully heal
		}

		private Entity ResolveTarget(Entity explicitTarget)
		{
			if (explicitTarget != null) return explicitTarget;
			// Default: first Player with HP
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player != null && player.HasComponent<HP>()) return player;
			return EntityManager.GetEntitiesWithComponent<HP>().FirstOrDefault();
		}

		private void OnApplyPassive(ApplyPassiveEvent e)
		{
			if (e == null || e.IsCancelled) return;
			LoggingService.Append("HpManagementSystem.OnApplyPassive", new System.Text.Json.Nodes.JsonObject
			{
				["passiveType"] = e.Type.ToString(),
				["delta"] = e.Delta,
				["targetId"] = e.Target?.Id ?? -1
			});
			switch (e.Type)
			{
				case AppliedPassiveType.Scar:
					var hp = e.Target.GetComponent<HP>();
					if (hp == null) return;
					EnsureUnscarredMax(hp);
					var atFullHp = hp.Current == hp.Max;
					if (e.Delta > 0)
					{
						hp.Max = Math.Max(1, hp.Max - e.Delta);
						if (atFullHp)
						{
							hp.Current -= e.Delta;
						}
						hp.Current = Math.Max(0, Math.Min(hp.Current, hp.Max));
					}
					break;
				default:
					return;
			}
		}

		// Debug action: Lose X HP
		[DebugActionInt("Lose HP", Step = 1, Min = 1, Max = 999, Default = 10)]
		public void Debug_LoseHp(int amount)
		{
			EventManager.Publish(new ModifyHpRequestEvent { Source = EntityManager.GetEntity("Player"), Target = EntityManager.GetEntity("Player"), Delta = -Math.Abs(amount) });
		}

		// Debug action: Heal X HP
		[DebugActionInt("Heal HP", Step = 1, Min = 1, Max = 999, Default = 10)]
		public void Debug_HealHp(int amount)
		{
			EventManager.Publish(new ModifyHpRequestEvent { Source = EntityManager.GetEntity("Player"), Target = EntityManager.GetEntity("Player"), Delta = Math.Abs(amount) });
		}

		private bool TryConsumeShield(Entity target, System.Collections.Generic.Dictionary<AppliedPassiveType, int> passives, int newDelta)
		{
			if (newDelta >= 0 || passives == null) return false;
			if (!passives.TryGetValue(AppliedPassiveType.Shield, out var shieldStacks) || shieldStacks <= 0) return false;
			EventManager.Publish(new RemovePassive { Owner = target, Type = AppliedPassiveType.Shield });
			return true;
		}

		private bool TryConsumeAegis(Entity target, System.Collections.Generic.Dictionary<AppliedPassiveType, int> passives, ref int newDelta)
		{
			if (newDelta >= 0 || passives == null) return false;
			if (!passives.TryGetValue(AppliedPassiveType.Aegis, out var aegisAmount) || aegisAmount <= 0) return false;
			int damageAmount = Math.Abs(newDelta);
			int useAegis = Math.Min(aegisAmount, damageAmount);
			if (useAegis <= 0) return false;
			EventManager.Publish(new UpdatePassive { Owner = target, Type = AppliedPassiveType.Aegis, Delta = -useAegis });
			newDelta += useAegis;
			return true;
		}

		private int TryConsumeGuard(Entity target, int rawDamage)
		{
			int absorbed = AppliedPassivesService.GetGuardAbsorption(target, rawDamage);
			if (absorbed <= 0) return 0;
			EventManager.Publish(new RemovePassive { Owner = target, Type = AppliedPassiveType.Guard });
			EventManager.Publish(new PassiveTriggered { Owner = target, Type = AppliedPassiveType.Guard });
			return absorbed;
		}

		private void OnRemovePassive(RemovePassive e)
		{
			if (e == null || e.Owner == null || e.Type != AppliedPassiveType.Scar || e.Amount <= 0) return;
			var hp = e.Owner.GetComponent<HP>();
			if (hp == null) return;
			LoggingService.Append("HpManagementSystem.OnRemovePassive", new System.Text.Json.Nodes.JsonObject
			{
				["passiveType"] = e.Type.ToString(),
				["amount"] = e.Amount,
				["ownerId"] = e.Owner?.Id ?? -1
			});
			hp.Current = Math.Max(0, Math.Min(hp.Current, hp.Max));
		}

		private void OnIncreaseMaxHp(IncreaseMaxHpEvent e)
		{
			LoggingService.Append("HpManagementSystem.OnIncreaseMaxHp", new System.Text.Json.Nodes.JsonObject
			{
				["delta"] = e.Delta,
				["targetId"] = e.Target?.Id ?? -1
			});
			var target = ResolveTarget(e.Target);
			if (target == null) return;
			var hp = target.GetComponent<HP>();
			if (hp == null) return;
			EnsureUnscarredMax(hp);
			hp.UnscarredMax = Math.Max(1, hp.UnscarredMax + e.Delta);
			int scarPenalty = GetScarStacks(target);
			hp.Max = Math.Max(1, hp.UnscarredMax - scarPenalty);
			hp.Current = hp.Max;
		}

		private void OnApplyBattleMaxHp(ApplyBattleMaxHpEvent e)
		{
			LoggingService.Append("HpManagementSystem.OnApplyBattleMaxHp", new System.Text.Json.Nodes.JsonObject
			{
				["scarPenalty"] = e.ScarPenalty,
				["targetId"] = e.Target?.Id ?? -1
			});
			var target = ResolveTarget(e.Target);
			if (target == null) return;
			var hp = target.GetComponent<HP>();
			if (hp == null) return;
			EnsureUnscarredMax(hp);
			hp.Max = Math.Max(1, hp.UnscarredMax - Math.Max(0, e.ScarPenalty));
			hp.Current = Math.Max(0, Math.Min(hp.Current, hp.Max));
		}

		private static void EnsureUnscarredMax(HP hp)
		{
			if (hp == null) return;
			if (hp.UnscarredMax <= 0)
			{
				hp.UnscarredMax = Math.Max(1, hp.Max);
			}
		}

		private static int GetScarStacks(Entity target)
		{
			var passives = target?.GetComponent<AppliedPassives>()?.Passives;
			if (passives == null) return 0;
			return passives.TryGetValue(AppliedPassiveType.Scar, out var stacks)
				? Math.Max(0, stacks)
				: 0;
		}
	}

}
