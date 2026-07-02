using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyAttackMustBlockRequirementServiceTests
{
	[Fact]
	public void Impossible_requirement_downgrades_attack_and_allows_confirm()
	{
		var attack = CreateAttack(ConditionType.MustBeBlockedByAtLeast1Card);
		attack.Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, 1);
		var (entityManager, planned) = CreateCombat(attack);

		bool changed = EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(entityManager, planned);
		bool canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			planned.ContextId);

		Assert.True(changed);
		Assert.Equal(ConditionType.None, attack.ConditionType);
		Assert.Equal(string.Empty, attack.Text);
		Assert.True(canConfirm);
	}

	[Fact]
	public void Enough_hand_blockers_keep_requirement_active()
	{
		var attack = CreateAttack(ConditionType.MustBeBlockedByAtLeast2Cards);
		var (entityManager, planned) = CreateCombat(attack);
		AddHandCard(entityManager, CardData.CardColor.White);
		AddHandCard(entityManager, CardData.CardColor.Black);

		bool changed = EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(entityManager, planned);

		Assert.False(changed);
		Assert.Equal(ConditionType.MustBeBlockedByAtLeast2Cards, attack.ConditionType);
	}

	[Fact]
	public void Usable_equipment_counts_as_possible_blocker()
	{
		var attack = CreateAttack(ConditionType.MustBeBlockedByAtLeast1Card);
		var (entityManager, planned) = CreateCombat(attack);
		AddEquipment(entityManager, CardData.CardColor.White);

		bool changed = EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(entityManager, planned);

		Assert.False(changed);
		Assert.Equal(ConditionType.MustBeBlockedByAtLeast1Card, attack.ConditionType);
	}

	[Fact]
	public void Exact_requirement_is_possible_with_more_eligible_blockers_than_threshold()
	{
		var attack = CreateAttack(ConditionType.MustBeBlockedByExactly1Card);
		var (entityManager, planned) = CreateCombat(attack);
		AddHandCard(entityManager, CardData.CardColor.White);
		AddHandCard(entityManager, CardData.CardColor.Black);

		bool changed = EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(entityManager, planned);

		Assert.False(changed);
		Assert.Equal(ConditionType.MustBeBlockedByExactly1Card, attack.ConditionType);
	}

	[Fact]
	public void Blocking_restrictions_reduce_possible_blockers()
	{
		var attack = CreateAttack(ConditionType.MustBeBlockedByAtLeast1Card);
		attack.BlockingRestrictionType = BlockingRestrictionType.OnlyRed;
		var (entityManager, planned) = CreateCombat(attack);
		AddHandCard(entityManager, CardData.CardColor.White);

		bool changed = EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(entityManager, planned);

		Assert.True(changed);
		Assert.Equal(ConditionType.None, attack.ConditionType);
	}

	[Fact]
	public void Composite_text_removes_only_must_block_sentence()
	{
		var attack = CreateAttack(ConditionType.MustBeBlockedByAtLeast2Cards);
		attack.Text = $"On attack - Gain 1 burn.\n\n{EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, 2)}\n\n* Increased by channel.";
		var (entityManager, planned) = CreateCombat(attack);
		AddHandCard(entityManager, CardData.CardColor.White);

		bool changed = EnemyAttackMustBlockRequirementService.NormalizeIfImpossible(entityManager, planned);

		Assert.True(changed);
		Assert.Equal("On attack - Gain 1 burn.\n\n* Increased by channel.", attack.Text);
	}

	private static EnemyAttackBase CreateAttack(ConditionType conditionType)
	{
		return new EnemyAttackBase
		{
			Id = EnemyAttackId.Cinderbolt,
			Name = "Test Attack",
			Damage = 5,
			ConditionType = conditionType
		};
	}

	private static (EntityManager EntityManager, PlannedAttack Planned) CreateCombat(EnemyAttackBase attack)
	{
		var entityManager = new EntityManager();
		var phase = entityManager.CreateEntity("PhaseState");
		var enemy = entityManager.CreateEntity("Enemy");
		var deck = entityManager.CreateEntity("Deck");
		var planned = new PlannedAttack
		{
			AttackId = attack.Id,
			ContextId = "test-context",
			AttackDefinition = attack
		};

		entityManager.AddComponent(phase, new PhaseState { Sub = SubPhase.Block });
		entityManager.AddComponent(deck, new Deck());
		entityManager.AddComponent(enemy, new AttackIntent
		{
			Planned = [planned]
		});

		return (entityManager, planned);
	}

	private static Entity AddHandCard(EntityManager entityManager, CardData.CardColor color)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData
		{
			Card = new CardBase(),
			Color = color
		});
		entityManager.GetEntitiesWithComponent<Deck>()
			.Single()
			.GetComponent<Deck>()
			.Hand
			.Add(card);
		return card;
	}

	private static Entity AddEquipment(EntityManager entityManager, CardData.CardColor color)
	{
		var equipment = entityManager.CreateEntity("Equipment");
		entityManager.AddComponent(equipment, new EquippedEquipment
		{
			Equipment = new TestEquipment
			{
				Block = 1,
				Uses = 1,
				RemainingUses = 1,
				Color = color,
				Slot = EquipmentSlot.Arms
			}
		});
		return equipment;
	}

	private sealed class TestEquipment : EquipmentBase
	{
	}
}
