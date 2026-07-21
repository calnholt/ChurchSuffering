using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class FrostSkeleton : EnemyBase
{
	private int Armor = 1;

	public FrostSkeleton()
	{
		Id = EnemyId.FrostSkeleton;
		Name = "Frost Skeleton";
		HP = 26;
		ClimbPool = ClimbEncounterPool.Throughout;

		OnStartOfBattle = (entityManager) =>
		{
			EventQueueBridge.EnqueueTriggerAction("FrostSkeleton.OnStartOfBattle", () =>
			{
				EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
			}, AppliedPassivesManagementSystem.Duration);
		};
	}

	public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
	{
		return SkeletonAttackSelectionService.GetAttackIds(EnemyAttackId.RimeStrike);
	}
}

public class RimeStrike : EnemyAttackBase
{
	private int Frostbite = 1;

	public RimeStrike()
	{
		Id = EnemyAttackId.RimeStrike;
		Name = "Rime Strike";
		Damage = 2;
		ConditionType = ConditionType.OnHit;
		Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Frostbite, Frostbite, ConditionType);

		OnAttackHit = (entityManager) =>
		{
			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = entityManager.GetEntity("Player"),
				Type = AppliedPassiveType.Frostbite,
				Delta = Frostbite,
			});
		};
	}
}
