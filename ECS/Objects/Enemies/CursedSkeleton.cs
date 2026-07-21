using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class CursedSkeleton : EnemyBase
{
	private int Armor = 1;

	public CursedSkeleton()
	{
		Id = EnemyId.CursedSkeleton;
		Name = "Cursed Skeleton";
		HP = 26;
		ClimbPool = ClimbEncounterPool.Throughout;

		OnStartOfBattle = (entityManager) =>
		{
			EventQueueBridge.EnqueueTriggerAction("CursedSkeleton.OnStartOfBattle", () =>
			{
				EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
			}, AppliedPassivesManagementSystem.Duration);
		};
	}

	public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
	{
		return SkeletonAttackSelectionService.GetAttackIds(EnemyAttackId.DreadStrike);
	}
}

public class DreadStrike : EnemyAttackBase
{
	private int Fear = 1;

	public DreadStrike()
	{
		Id = EnemyAttackId.DreadStrike;
		Name = "Dread Strike";
		Damage = 2;
		ConditionType = ConditionType.OnHit;
		Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Fear, Fear, ConditionType);

		OnAttackHit = (entityManager) =>
		{
			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = entityManager.GetEntity("Player"),
				Type = AppliedPassiveType.Fear,
				Delta = Fear,
			});
		};
	}
}
