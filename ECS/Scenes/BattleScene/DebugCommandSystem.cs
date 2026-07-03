using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// System that listens to DebugCommandEvent and triggers corresponding actions
    /// </summary>
    [Crusaders30XX.Diagnostics.DebugTab("Commands")]
    public class DebugCommandSystem : Core.System
    {
        public DebugCommandSystem(EntityManager entityManager) : base(entityManager)
        {
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            // Not tied to specific entities
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        // Debug actions discoverable by the DebugMenuSystem
        [DebugAction("Draw Card")]
        public void Debug_DrawCard()
        {
            EventManager.Publish(new RequestDrawCardsEvent { Count = 1 });
        }

        [DebugAction("Redraw Hand")]
        public void Debug_RedrawHand()
        {
            EventManager.Publish(new RedrawHandEvent { DrawCount = 4 });
        }

        [DebugAction("+ Courage")]
        public void Debug_IncreaseCourage()
        {
            EventManager.Publish(new ModifyCourageRequestEvent { Delta = 1, Type = ModifyCourageType.Gain });
        }

        [DebugAction("- Courage")]
        public void Debug_DecreaseCourage()
        {
            EventManager.Publish(new ModifyCourageRequestEvent { Delta = -1, Type = ModifyCourageType.Lost });
        }

        [DebugAction("Set Battlefield: Desert")]
        public void Debug_SetBattlefield_Desert()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Desert });
        }

        [DebugAction("Set Battlefield: Forest")]
        public void Debug_SetBattlefield_Forest()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Forest });
        }

        [DebugAction("Set Battlefield: Cathedral")]
        public void Debug_SetBattlefield_Cathedral()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Cathedral });
        }

        [DebugAction("Set Battlefield: Tundra")]
        public void Debug_SetBattlefield_Tundra()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Tundra });
        }

        [DebugAction("Set Battlefield: Jungle")]
        public void Debug_SetBattlefield_Jungle()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Jungle });
        }

        [DebugAction("Set Battlefield: Volcano")]
        public void Debug_SetBattlefield_Volcano()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Volcano });
        }

        [DebugAction("Set Battlefield: The Gate")]
        public void Debug_SetBattlefield_TheGate()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.TheGate });
        }

        [DebugAction("Set Battlefield: Gothic")]
        public void Debug_SetBattlefield_Gothic()
        {
            EventManager.Publish(new ChangeBattleLocationEvent { Location = BattleLocation.Gothic });
        }

        [DebugActionInt("Player: Deal Damage", Step = 1, Min = 1, Max = 999, Default = 999)]
        public void Debug_PlayerDealDamage(int amount)
        {
            EventManager.Publish(new ModifyHpRequestEvent { Source = EntityManager.GetEntity("Player"), Target = EntityManager.GetEntity("Enemy"), Delta = -Math.Abs(amount) });
        }
        [DebugActionInt("Gain AP", Step = 1, Min = 1, Max = 999, Default = 5)]
        public void Debug_GainAp(int amount)
        {
            EventManager.Publish(new ModifyActionPointsEvent { Delta = Math.Abs(amount) });
        }
        [DebugActionInt("Apply Burn (enemy)", Step = 1, Min = 1, Max = 999, Default = 3)]
        public void Debug_ApplyBurn(int amount)
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Burn, Delta = amount });
        }
        [DebugActionInt("Apply Burn (player)", Step = 1, Min = 1, Max = 999, Default = 3)]
        public void Debug_ApplyBurnPlayer(int amount)
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Burn, Delta = amount });
        }
        [DebugActionInt("Apply aegis (player)", Step = 1, Min = 1, Max = 999, Default = 3)]
        public void Debug_ApplyAegisPlayer(int amount)
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Aegis, Delta = amount });
        }
		[DebugAction("Test Burn vs Aegis")]
		public void Debug_TestBurnVsAegis()
		{
			var player = EntityManager.GetEntity("Player");
			if (player == null) return;
			var hp = player.GetComponent<HP>();
			if (hp == null) return;
			var passivesComponent = player.GetComponent<AppliedPassives>();
			if (passivesComponent == null)
			{
				EntityManager.AddComponent(player, new AppliedPassives());
				passivesComponent = player.GetComponent<AppliedPassives>();
			}
			var passives = passivesComponent.Passives ??= new System.Collections.Generic.Dictionary<AppliedPassiveType, int>();
			int beforeHp = hp.Current;
			int beforeAegis = passives.TryGetValue(AppliedPassiveType.Aegis, out var aegisStacks) ? aegisStacks : 0;
			if (beforeAegis <= 0)
			{
				EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aegis, Delta = 1 });
				beforeAegis = 1;
			}
			EventManager.Publish(new ModifyHpRequestEvent { Source = player, Target = player, Delta = -1, DamageType = ModifyTypeEnum.Effect });
			int afterHp = hp.Current;
			int afterAegis = passives.TryGetValue(AppliedPassiveType.Aegis, out var updatedAegis) ? updatedAegis : 0;
			Console.WriteLine($"[DebugCommandSystem] Burn vs Aegis => HP {beforeHp}->{afterHp}, Aegis {beforeAegis}->{afterAegis}");
			if (afterHp != beforeHp)
			{
				Console.WriteLine("[DebugCommandSystem] Burn vs Aegis WARNING: HP changed; restoring prior value.");
				EventManager.Publish(new SetHpEvent { Target = player, Value = beforeHp });
			}
			if (afterAegis != beforeAegis)
			{
				EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aegis, Delta = beforeAegis - afterAegis });
			}
		}
        [DebugAction("Activate Temperance")]
        public void Debug_ActivateTemperance()
        {
            EventManager.Publish(new TriggerTemperance { Owner = EntityManager.GetEntity("Player"), AbilityId = "radiance" });
        }
        [DebugAction("Apply Stun")]
        public void Debug_ApplyStun()
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Stun, Delta = 1 });
        }
        [DebugAction("Apply Wounded (Player)")]
        public void Debug_ApplyWounded_Player()
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Wounded, Delta = 1 });
        }
        [DebugAction("Game Over")]
        public void Debug_GameOver()
        {
            EventManager.Publish(new PlayerDied { Player = EntityManager.GetEntity("Player") });
        }
        [DebugAction("Location POC")]
        public void Debug_LocationPOC()
        {
            EventManager.Publish(new ShowTransition { Scene = SceneId.Location });
        }
        [DebugActionInt("Apply Power", Step = 1, Min = 1, Max = 999, Default = 1)]
        public void Debug_ApplyPower(int amount)
        {
            EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Power, Delta = amount });
        }
        [DebugAction("Play Sword Attack SFX")]
        public void Debug_PlaySwordAttack()
        {
            EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.SwordAttack, Volume = 0.5f });
        }
        [DebugAction("Toggle Pledge")]
        public void Debug_TogglePledge()
        {
            StateSingleton.IsPledgeEnabled = !StateSingleton.IsPledgeEnabled;
        }

        [DebugAction("Apply Brittle (hand)")]
        public void Debug_ApplyBrittleHand()
        {
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = 1,
                Type = CardApplicationType.Brittle,
                Target = CardApplicationTarget.Hand,
            });
        }

        [DebugAction("Apply Frozen (hand)")]
        public void Debug_ApplyFrozenHand()
        {
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = 1,
                Type = CardApplicationType.Frozen,
                Target = CardApplicationTarget.Hand,
            });
        }

        [DebugAction("Apply Scorched (hand)")]
        public void Debug_ApplyScorchedHand()
        {
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = 1,
                Type = CardApplicationType.Scorched,
                Target = CardApplicationTarget.Hand,
            });
        }

        [DebugAction("Apply Thorned (hand)")]
        public void Debug_ApplyThornedHand()
        {
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = 1,
                Type = CardApplicationType.Thorned,
                Target = CardApplicationTarget.Hand,
            });
        }

        [DebugAction("Apply Colorless (hand)")]
        public void Debug_ApplyColorlessHand()
        {
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = 1,
                Type = CardApplicationType.Colorless,
                Target = CardApplicationTarget.Hand,
            });
        }

        [DebugAction("Remove Colorless (hand)")]
        public void Debug_RemoveColorlessHand()
        {
            EventManager.Publish(new RemoveCardApplications
            {
                Amount = 1,
                Type = CardApplicationType.Colorless,
                Target = CardApplicationTarget.Hand,
            });
        }
    }
}
