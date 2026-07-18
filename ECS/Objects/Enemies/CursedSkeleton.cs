using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Enemies;

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
