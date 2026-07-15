using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	/// <summary>
	/// Enemy arsenal of attack IDs that can be planned/executed.
	/// </summary>
	public class EnemyArsenal : IComponent
	{
		public Entity Owner { get; set; }
		public List<EnemyAttackId> AttackIds { get; set; } = new();
	}

	/// <summary>
	/// Per-enemy list of planned attacks for the current/next turns.
	/// </summary>
	public class AttackIntent : IComponent
	{
		public Entity Owner { get; set; }
		public List<PlannedAttack> Planned { get; set; } = new();
		public int ActiveAttackSequence { get; set; }
	}

	/// <summary>
	/// Optional preview list of planned attacks for the next turn.
	/// </summary>
	public class NextTurnAttackIntent : IComponent
	{
		public Entity Owner { get; set; }
		public List<PlannedAttack> Planned { get; set; } = new();
	}

	public class PlannedAttack
	{
		public EnemyAttackId AttackId;
		public int ResolveStep;
		public bool WasBlocked;
		public bool IsAmbush;
		public EnemyAttackBase AttackDefinition;
	}

	// Stun is now tracked per PlannedAttack via IsStunned flag


	/// <summary>
	/// Current active enemy attack progress snapshot used by UI and logic. One entity per enemy.
	/// </summary>
	public class EnemyAttackProgress : IComponent
	{
		public Entity Owner { get; set; }

		public Entity Enemy { get; set; }
		public EnemyAttackId AttackId { get; set; }
		public int AttackSequence { get; set; }

		// Typed counters replacing generic dictionary keys
		public int AssignedBlockTotal { get; set; }
		public int AdditionalConditionalDamageTotal { get; set; }
		public int PreventedDamageFromBlockCondition { get; set; }
		public int PlayedCards { get; set; }
		public int PlayedRed { get; set; }
		public int PlayedWhite { get; set; }
		public int PlayedBlack { get; set; }

		// Derived values for display and resolution previews
		// Null means no attack-specific modifier has been applied; consumers use AssignedBlockTotal.
		public int? EffectiveAssignedBlockTotal { get; set; }
		public bool IsConditionMet { get; set; }
		public int ActualDamage { get; set; }
		public int AegisTotal { get; set; }
		public int DamageBeforePrevention { get; set; }
		public int BaseDamage { get; set; }
		public int TotalPreventedDamage { get; internal set; }
		public bool FullyPreventedBySpecial { get; set; }
		public bool IgnoresAegis { get; set; }
    }

	/// <summary>
	/// Marker component; an entity with this and a Transform defines the enemy attack banner anchor position.
	/// The Transform.Position is the center-bottom point of the banner.
	/// </summary>
	public class EnemyAttackBannerAnchor : IComponent
	{
		public Entity Owner { get; set; }
	}

	/// <summary>
	/// Shared, presentation-only geometry for the enemy attack banner. The display system writes
	/// logical animation values during Update; the late layout system resolves parallax-adjusted
	/// screen bounds after all transforms have settled.
	/// </summary>
	public class EnemyAttackBannerPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsVisible { get; set; }
		public int LogicalWidth { get; set; }
		public int LogicalHeight { get; set; }
		public float PanelScaleX { get; set; } = 1f;
		public float PanelScaleY { get; set; } = 1f;
		public float Alpha { get; set; } = 1f;
		public float ContentScale { get; set; } = 1f;
		public float OrnamentProgress { get; set; } = 1f;
		public float SkullScale { get; set; } = 1f;
		public float SkullTint { get; set; }
		public float TextAlpha { get; set; } = 1f;
		public float TextOffsetY { get; set; }
		public float FlashAlpha { get; set; }
		public float RingOneProgress { get; set; } = 1f;
		public float RingTwoProgress { get; set; } = 1f;
		public float ImpactIntensity { get; set; } = 0.25f;
		public float AbsorbProgress { get; set; }
		public Vector2 AbsorbStart { get; set; }
		public Vector2 AbsorbTarget { get; set; }
		public Vector2 RecoilOffset { get; set; }
		public Rectangle RenderBounds { get; set; }
		public Rectangle LocalTextBounds { get; set; }
		public Rectangle TextBounds { get; set; }
		public Rectangle ConfirmBounds { get; set; }
		public bool HasKeywordTooltip { get; set; }
		public bool ShowConfirm { get; set; }
		public int ConfirmWidth { get; set; }
		public int ConfirmHeight { get; set; }
		public int ConfirmOffsetY { get; set; }
	}

	/// <summary>
	/// Marks a card as currently assigned as block to a specific attack context and carries its animation state.
	/// </summary>
	public class AssignedBlockCard : IComponent
	{
		public Entity Owner { get; set; }
		public int BlockAmount { get; set; }
		public long AssignedAtTicks { get; set; }
		// Display data (self-contained; systems shouldn't need to inspect card/equipment):
		public bool IsEquipment { get; set; } = false;
		public List<CardData.CardColor> ColorKeys { get; set; } = new();
		public string Tooltip { get; set; } = string.Empty;
		public Color DisplayBgColor { get; set; } = Color.White;
		public Color DisplayFgColor { get; set; } = Color.Black;
		public Vector2 ReturnTargetPos { get; set; } = Vector2.Zero;
		public string EquipmentType { get; set; } = string.Empty; // Head | Chest | Arms | Legs for equipment
	}

	/// <summary>
	/// Presentation-only pose and animation state for an assigned blocker.
	/// Written by assigned-block presentation systems and read by render/input systems.
	/// </summary>
	public class AssignedBlockPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public enum PhaseState { Pullback, Launch, Impact, Idle, Returning }
		public PhaseState Phase { get; set; } = PhaseState.Pullback;
		public Vector2 StartPos { get; set; }
		public Vector2 TargetPos { get; set; }
		public Vector2 CurrentPos { get; set; }
		public float StartScale { get; set; } = 1f;
		public float TargetScale { get; set; } = 0.4f;
		public float CurrentScale { get; set; } = 1f;
		public float StartRotation { get; set; } = 0f;
		public float CurrentRotation { get; set; } = 0f;
		public float Elapsed { get; set; } = 0f;
		public Vector2 RenderPos { get; set; }
		public Rectangle RenderBounds { get; set; }
		public float RailFlash { get; set; }
		public bool ReturnCompletionPublished { get; set; }
	}

	/// <summary>
	/// Aggregate render state for the assigned-block rail attached to the attack banner.
	/// </summary>
	public class AssignedBlockRailPresentation : IComponent
	{
		public Entity Owner { get; set; }
		public Vector2 LogicalAnchorPos { get; set; }
		public float VerticalOffset { get; set; }
		public Rectangle Bounds { get; set; }
		public float Flash { get; set; }
	}

	/// <summary>
	/// Singleton component holding ambush overlay/timer state for the current ambush context.
	/// </summary>
	public class AmbushState : IComponent
	{
		public Entity Owner { get; set; }
		public int ActiveAttackSequence { get; set; }
		public bool IsActive { get; set; }
		public bool IntroActive { get; set; }
		public float TimerDurationSeconds { get; set; } = 20f;
		public float TimerRemainingSeconds { get; set; } = 0f;
		public bool FiredAutoConfirm { get; set; } = false;
	}

	/// <summary>
	/// Tribulation component attached to player for quest-based modifiers.
	/// Stores quest ID and tribulation data from quest definition.
	/// </summary>
	public class Tribulation : IComponent
	{
		public Entity Owner { get; set; }
		public Entity PlayerOwner { get; set; } // player entity that owns this tribulation
		public string QuestId { get; set; }
		public string Text { get; set; }
		public string Trigger { get; set; }
	}


	public class ExhaustOnBlock : IComponent
	{
		public Entity Owner { get; set; }
	}
}
