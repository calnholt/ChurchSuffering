using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Enemies;

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
