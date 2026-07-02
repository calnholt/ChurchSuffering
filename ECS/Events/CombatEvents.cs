using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using MonoGame.Extended.Collections;

namespace Crusaders30XX.ECS.Events
{

	public class IntentPlanned
	{
		public string AttackId;
		public string ContextId;
		public int Step;
		public string TelegraphText;
	}


	public class ResolveAttack
	{
		public string ContextId;
	}

	public class ApplyEffect
	{
		public string EffectType;
		public int Amount;
		public string Status;
		public int Stacks;
		public Entity Source;
		public Entity Target;
		public int Percentage;
		public string attackId;
	}

	public class AttackResolved
	{
		public string ContextId;
		public bool WasConditionMet;
	}

	// Fired when the absorb tween completes and the enemy is about to attack
	public class EnemyAbsorbComplete
	{
		public string ContextId;
	}

	// Fired when the enemy attack animation should deal damage to the player
	public class EnemyAttackImpactNow
	{
		public string ContextId;
	}

	public class ResolvingEnemyDamageEvent
	{
		public string ContextId;
		public int BaseDamage;
		public int AssignedBlock;
		public bool WillHit;
	}

	public class EnemyDamageAppliedEvent
	{
		public string ContextId;
		public int FinalDamage;
		public int TotalDamage;
		public bool WasHit;
	}

	// Enemy debuff animation when applying negative effects to player
	public class StartDebuffAnimation
	{
		public bool TargetIsPlayer; // Which entity animates (false = enemy animates)
	}

	public class DebuffAnimationComplete
	{
		public bool TargetIsPlayer;
	}

	// Shows a temporary "Stunned!" overlay on the enemy
	public class ShowStunnedOverlay
	{
		public string ContextId;
	}

	// Fired when a battle is won (enemy defeated), to trigger scene transition
	public class ShowTransition
	{
		public SceneId Scene;
		public bool SkipHold;
		public bool SkipWipe;
		public bool EndRunOnLoad;
	}

	public class TransitionCompleteEvent
	{
		public SceneId Scene;
	}

	public class DialogEnded
	{
		
	}

	public class TriggerEnemyAttackDisplayEvent
	{
		public string ContextId;
	}

	public class OnEnemyAttackHitEvent
	{
	}

	public class EquipmentActivateEvent
	{
		public Entity EquipmentEntity;
	}

	public class EnemyKilledEvent
	{
		public Entity Enemy;
	}

	public class EnemyPhaseLethalEvent
	{
		public Entity Enemy { get; set; }
	}

	public class EnemyPhaseResetEvent
	{
		public Entity Enemy { get; set; }
		public int CurrentPhase { get; set; }
	}
}
