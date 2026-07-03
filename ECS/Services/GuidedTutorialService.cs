using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Services
{
	public static class GuidedTutorialService
	{
		public static bool IsActive(EntityManager entityManager) =>
			GetState(entityManager) != null;

		public static GuidedTutorial GetState(EntityManager entityManager) =>
			entityManager?.GetEntitiesWithComponent<GuidedTutorial>()
				.FirstOrDefault(e => e.IsActive)
				?.GetComponent<GuidedTutorial>();

		public static void Start(World world)
		{
			if (world == null) return;
			Cleanup(world.EntityManager);

			var sectionDef = GuidedTutorialDefinitions.GetSection(1);

			var stateEntity = world.CreateEntity("GuidedTutorial");
			world.AddComponent(stateEntity, new GuidedTutorial
			{
				Section = 1,
				TurnWithinSection = 1,
				PlayerHp = sectionDef.PlayerHp,
			});
			world.AddComponent(stateEntity, new DontDestroyOnLoad());

			var deckEntity = EntityFactory.CreateDeck(world);
			world.AddComponent(deckEntity, new StockHand { Section = 1, TurnWithinSection = 1 });
			world.AddComponent(deckEntity, new DontDestroyOnLoad());

			var player = EntityFactory.CreatePlayer(world);
			player.GetComponent<Player>().DeckEntity = deckEntity;
			player.GetComponent<HP>().Max = 25;
			player.GetComponent<HP>().UnscarredMax = 25;
			player.GetComponent<HP>().Current = sectionDef.PlayerHp;
			player.GetComponent<Intellect>().Value = sectionDef.Turns[0].StockHand.Count;
			player.GetComponent<MaxHandSize>().Value = sectionDef.Turns[0].StockHand.Count;
			player.GetComponent<EquippedWeapon>().WeaponId = "sword";
			player.GetComponent<EquippedTemperanceAbility>().AbilityId = "unsheath";

			foreach (var equipment in world.EntityManager.GetEntitiesWithComponent<EquippedEquipment>().ToList())
				world.EntityManager.DestroyEntity(equipment.Id);
			foreach (var medal in world.EntityManager.GetEntitiesWithComponent<EquippedMedal>().ToList())
				world.EntityManager.DestroyEntity(medal.Id);

			var queuedEntity = world.CreateEntity("QueuedEvents");
			var queued = new QueuedEvents
			{
				CurrentIndex = -1,
				LocationId = string.Empty,
				QuestIndex = -1,
			};
			queued.Events.Add(new QueuedEvent { EventId = "horde" });
			world.AddComponent(queuedEntity, queued);
			world.AddComponent(queuedEntity, new PendingQuestDialog
			{
				DialogId = "guided_tutorial",
				SegmentId = "intro",
				RequestId = Guid.NewGuid(),
				WillShowDialog = true,
			});
			world.AddComponent(queuedEntity, new DontDestroyOnLoad());
			EventManager.Publish(new ShowTransition { Scene = SceneId.Battle, SkipHold = true });
		}

		public static void PrepareStockHand(EntityManager entityManager)
		{
			var state = GetState(entityManager);
			var deckEntity = entityManager?.GetEntitiesWithComponent<StockHand>().FirstOrDefault();
			var deck = deckEntity?.GetComponent<Deck>();
			if (state == null || deck == null || state.StockHandPrepared) return;

			var stock = deckEntity.GetComponent<StockHand>();
			stock.Section = state.Section;
			stock.TurnWithinSection = state.TurnWithinSection;

			var retainedPledges = deck.Hand
				.Where(card => card.HasComponent<Pledge>())
				.ToList();
			foreach (var card in deck.Cards.ToList())
			{
				if (retainedPledges.Contains(card)) continue;
				entityManager.DestroyEntity(card.Id);
			}

			deck.Cards.Clear();
			deck.DrawPile.Clear();
			deck.DiscardPile.Clear();
			deck.ExhaustPile.Clear();
			deck.Hand.Clear();
			foreach (var retained in retainedPledges)
			{
				deck.Cards.Add(retained);
				deck.Hand.Add(retained);
			}

			var sectionDef = GuidedTutorialDefinitions.GetSection(state.Section);

			int totalTurns = sectionDef.Turns.Count;
			int cardIndex = 0;
			for (int t = 1; t <= totalTurns; t++)
			{
				var turn = GuidedTutorialDefinitions.GetTurn(stock.Section, t);
				foreach (var definition in turn.StockHand)
				{
					if (retainedPledges.Any(card =>
						string.Equals(card.GetComponent<CardData>()?.Card?.CardId, definition.CardId, StringComparison.OrdinalIgnoreCase) &&
						card.GetComponent<CardData>()?.Color == definition.Color))
					{
						continue;
					}

					var card = EntityFactory.CreateCardFromDefinition(
						entityManager,
						definition.CardId,
						definition.Color,
						index: cardIndex++);
					if (card == null) continue;
					if (definition.IsColorless)
					{
						card.AddComponent(new Colorless { Owner = card });
					}
					deck.Cards.Add(card);
					deck.DrawPile.Add(card);
				}
			}

			state.StockHandPrepared = true;
		}

		public static void BeginNextTurn(EntityManager entityManager, int turn)
		{
			var state = GetState(entityManager);
			if (state == null || turn <= state.TurnWithinSection) return;
			var sectionDef = GuidedTutorialDefinitions.GetSection(state.Section);
			state.TurnWithinSection = turn;
			state.BlockedCardIdsThisTurn.Clear();
			state.ConfirmedAttackCountThisTurn = 0;
			if (!sectionDef.ShowDrawPile)
			{
				state.StockHandPrepared = false;
				PrepareStockHand(entityManager);
			}
		}

		public static void RestartSection(EntityManager entityManager)
		{
			var state = GetState(entityManager);
			if (state == null) return;
			state.IsRestart = true;
			state.TurnWithinSection = 1;
			state.StockHandPrepared = false;
			state.BlockedCardIdsThisTurn.Clear();
			state.ConfirmedAttackCountThisTurn = 0;
			EventManager.Publish(new ShowTransition { Scene = SceneId.Battle, SkipHold = true });
		}

		public static void AdvanceToNextSection(EntityManager entityManager)
		{
			var state = GetState(entityManager);
			if (state == null) return;

			var player = entityManager.GetEntity("Player");
			if (player != null)
			{
				state.BaselineCourage = player.GetComponent<Courage>()?.Amount ?? 0;
				state.BaselineTemperance = player.GetComponent<Temperance>()?.Amount ?? 0;
			}

			state.Section++;
			state.TurnWithinSection = 1;
			state.StockHandPrepared = false;
			state.IsRestart = false;

			var sectionDef = GuidedTutorialDefinitions.GetSection(state.Section);
			state.PlayerHp = sectionDef.PlayerHp;

			EventManager.Publish(new ShowTransition { Scene = SceneId.Battle, SkipHold = true });
		}

		public static void Complete(EntityManager entityManager)
		{
			var state = GetState(entityManager);
			if (state != null) state.IsCompleted = true;
			foreach (var key in GuidedTutorialDefinitions.CoveredTutorialKeys)
				SaveCache.MarkTutorialSeen(key);
			SaveCache.CompleteGuidedTutorial();
			Cleanup(entityManager);
			EventManager.Publish(new ShowTransition { Scene = SceneId.WayStation });
		}

		public static void Cleanup(EntityManager entityManager)
		{
			if (entityManager == null) return;
			foreach (var entity in entityManager.GetEntitiesWithComponent<GuidedTutorial>().ToList())
				entityManager.DestroyEntity(entity.Id);
			foreach (var entity in entityManager.GetEntitiesWithComponent<StockHand>().ToList())
			{
				var deck = entity.GetComponent<Deck>();
				foreach (var card in deck?.Cards?.ToList() ?? [])
					entityManager.DestroyEntity(card.Id);
				entityManager.DestroyEntity(entity.Id);
			}
			if (!SaveCache.IsRunActive())
			{
				RunPlayerService.DestroyRunPlayer(entityManager);
				entityManager.DestroyEntity("QueuedEvents");
			}
		}
	}
}
