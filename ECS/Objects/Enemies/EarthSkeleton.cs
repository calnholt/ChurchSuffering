using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class EarthSkeleton : EnemyBase
{
	private int Armor = 1;

	public EarthSkeleton()
	{
		Id = EnemyId.EarthSkeleton;
		Name = "Earth Skeleton";
		HP = 26;
		ClimbPool = ClimbEncounterPool.Throughout;

		OnStartOfBattle = (entityManager) =>
		{
			EventQueueBridge.EnqueueTriggerAction("EarthSkeleton.OnStartOfBattle", () =>
			{
				EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
			}, AppliedPassivesManagementSystem.Duration);
		};
	}

	public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
	{
		return SkeletonAttackSelectionService.GetAttackIds(EnemyAttackId.BurialStrike);
	}
}

public class BurialStrike : EnemyAttackBase
{
	private int SealedStacks = 2;

	public BurialStrike()
	{
		Id = EnemyAttackId.BurialStrike;
		Name = "Burial Strike";
		Damage = 2;
		ConditionType = ConditionType.OnHit;
		Text = EnemyAttackTextHelper.GetText(
			EnemyAttackTextType.Custom,
			0,
			ConditionType,
			100,
			$"Seal the top card of your deck for {SealedStacks}.");

		OnAttackHit = (entityManager) =>
		{
			EventManager.Publish(new ApplyCardApplicationEvent
			{
				Amount = 1,
				StacksPerCard = SealedStacks,
				Type = CardApplicationType.Sealed,
				Target = CardApplicationTarget.TopXCards,
			});
		};
	}
}
