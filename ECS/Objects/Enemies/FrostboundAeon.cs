using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Objects.Enemies;

public sealed class FrostboundAeon : EnemyBase
{
	public FrostboundAeon()
	{
		Id = EnemyId.FrostboundAeon;
		Name = "Frostbound Aeon";
		HP = 32;
		ClimbPool = ClimbEncounterPool.Late;
	}

	public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber) =>
		turnNumber <= 5 ? [EnemyAttackId.ChronoSlice] : [EnemyAttackId.AeonWard];
}

public sealed class ChronoSlice : EnemyAttackBase
{
	public ChronoSlice()
	{
		Id = EnemyAttackId.ChronoSlice;
		Name = "Chrono Slice";
		Damage = 10;
		Text = "The first assigned non-equipment card goes to the bottom of your deck.";

		OnBlocksConfirmed = entityManager =>
		{
			var firstCard = entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Where(card => !card.GetComponent<AssignedBlockCard>().IsEquipment)
				.OrderBy(card => card.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.FirstOrDefault();
			if (firstCard == null) return;

			var destination = firstCard.GetComponent<AssignedBlockDestinationOverride>();
			if (destination == null)
			{
				entityManager.AddComponent(firstCard, new AssignedBlockDestinationOverride
				{
					Owner = firstCard,
					Destination = CardZoneType.DrawPile,
				});
			}
			else
			{
				destination.Destination = CardZoneType.DrawPile;
			}
		};
	}
}

public sealed class AeonWard : EnemyAttackBase
{
	private const int GuardAmount = 3;

	public AeonWard()
	{
		Id = EnemyAttackId.AeonWard;
		Name = "Aeon Ward";
		Damage = 5;
		Text = $"On reveal - Gain {GuardAmount} guard.";
		OnAttackReveal = entityManager => EventManager.Publish(new ApplyPassiveEvent
		{
			Target = entityManager.GetEntity("Enemy"),
			Type = AppliedPassiveType.Guard,
			Delta = GuardAmount,
		});
	}
}
