using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Objects.Enemies
{
	public class Medusa : EnemyBase
	{
		public Medusa()
		{
			Id = EnemyId.Medusa;
			Name = "Medusa";
			HP = 37;
		}

		private static readonly EnemyAttackId[] BaseAttacks = new[]
		{
			EnemyAttackId.Gaze, EnemyAttackId.PetrifyingGaze, EnemyAttackId.VipersCurse
		};

		private static readonly EnemyAttackId[] SealReliantAttacks = new[]
		{
			EnemyAttackId.BasiliskGlare, EnemyAttackId.SerpentStrike, EnemyAttackId.StoneSkin, EnemyAttackId.CrumblingStone
		};

		public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
		{
			var playerHasSeals = GetComponentHelper.GetHandOfCards(entityManager)
				.Any(c => c.GetComponent<Sealed>() != null);

			var pool = playerHasSeals
				? BaseAttacks.Concat(SealReliantAttacks).ToArray()
				: BaseAttacks;

			var random = new Random();
			var attacks = new List<EnemyAttackId>();
			for (int i = 0; i < 3; i++)
			{
				attacks.Add(pool[random.Next(pool.Length)]);
			}
			return attacks;
		}
	}
}

public class Gaze : EnemyAttackBase
{
	private int RevealSeals = 3;
	private int HitSeals = 3;

	public Gaze()
	{
		Id = EnemyAttackId.Gaze;
		Name = "Gaze";
		Damage = new Random().Next(2, 6);
		ConditionType = ConditionType.OnHit;
		Text = $"On hit - Top card of your deck gains {HitSeals} seals.";

		OnAttackHit = (entityManager) =>
		{
			EventManager.Publish(new SealCardsEvent { Amount = HitSeals, Type = SealType.TopOfDrawPile });
		};
	}
}

public class BasiliskGlare : EnemyAttackBase
{
	public BasiliskGlare()
	{
		Id = EnemyAttackId.BasiliskGlare;
		Name = "Basilisk Glare";
		Damage = new Random().Next(2, 6);
		ConditionType = ConditionType.OnHit;
		Text = "This cannot be blocked by sealed cards.";

		OnAttackReveal = (entityManager) =>
		{
			var cards = GetComponentHelper.GetHandOfCards(entityManager).Where(x => x.GetComponent<Sealed>() != null).ToList();
			cards.ForEach(x => {
				var cannotBlock = new CannotBlockThisAttack{ Reason = "This attack cannot be blocked by sealed cards." };
				entityManager.AddComponent(x, cannotBlock);
			});
		};

		OnBlocksConfirmed = (entityManager) =>
		{
			var cards = GetComponentHelper.GetHandOfCards(entityManager);
			cards.ForEach(x => {
				entityManager.RemoveComponent<CannotBlockThisAttack>(x);
			});
		};
	}
}

public class SerpentStrike : EnemyAttackBase
{
	private int SealDelta = 1;

	public SerpentStrike()
	{
		Id = EnemyAttackId.SerpentStrike;
		Name = "Serpent Strike";
		Damage = new Random().Next(2, 6);
		ConditionType = ConditionType.OnHit;
		Text = $"On hit - All sealed cards you own gain {SealDelta} seal.";

		OnAttackHit = (entityManager) =>
		{
			EventManager.Publish(new ModifySealsEvent { Delta = SealDelta });
		};
	}
}

public class PetrifyingGaze : EnemyAttackBase
{
	private int SealsGained = 2;

	public PetrifyingGaze()
	{
		Id = EnemyAttackId.PetrifyingGaze;
		Name = "Petrifying Gaze";
		Damage = new Random().Next(2, 6);
		Text = $"Each card that blocks this gains {SealsGained} seals.";

		OnBlockProcessed = (entityManager, card) =>
		{
			var sealedComp = card.GetComponent<Sealed>();
			if (sealedComp != null)
			{
				sealedComp.Seals += SealsGained;
			}
			else
			{
				entityManager.AddComponent(card, new Sealed { Owner = card, Seals = SealsGained });
			}
		};
	}
}

public class StoneSkin : EnemyAttackBase
{
	private int BlockReduction = 1;

	public StoneSkin()
	{
		Id = EnemyAttackId.StoneSkin;
		Name = "Stone Skin";
		Damage = new Random().Next(2, 6);
		Text = $"Sealed cards block for {BlockReduction} less.";

		ProgressOverride = (entityManager) =>
		{
			var p = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>().FirstOrDefault().GetComponent<EnemyAttackProgress>();
			var sealedBlockCards = entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Where(e => !e.GetComponent<AssignedBlockCard>().IsEquipment && e.GetComponent<Sealed>() != null)
				.ToList();
			if (sealedBlockCards.Count > 0)
			{
				p.AssignedBlockTotal -= sealedBlockCards.Count * BlockReduction;
			}
			return false;
		};
	}
}

public class VipersCurse : EnemyAttackBase
{
	private int SealsApplied = 2;

	public VipersCurse()
	{
		Id = EnemyAttackId.VipersCurse;
		Name = "Viper's Curse";
		Damage = new Random().Next(1, 4);
		Text = $"On attack - Random card in hand gains {SealsApplied} seal.";

		OnAttackReveal = (entityManager) =>
		{
			EventManager.Publish(new SealCardsEvent { Amount = SealsApplied, Type = SealType.Hand });
		};
	}
}

public class CrumblingStone : EnemyAttackBase
{
	private int SealsRemoved = 2;

	public CrumblingStone()
	{
		Id = EnemyAttackId.CrumblingStone;
		Name = "Crumbling Stone";
		Damage = new Random().Next(2, 6);
		Text = $"Blocking cards lose {SealsRemoved} seals.";

		OnBlockProcessed = (entityManager, card) =>
		{
			var sealedComp = card.GetComponent<Sealed>();
			if (sealedComp != null)
			{
				sealedComp.Seals = Math.Max(0, sealedComp.Seals - SealsRemoved);
				if (sealedComp.Seals <= 0)
				{
					entityManager.RemoveComponent<Sealed>(card);
				}
			}
		};
	}
}
