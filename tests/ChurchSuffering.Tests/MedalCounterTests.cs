using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.Medals;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class MedalCounterTests
{
	[Fact]
	public void StBenedict_resets_counter_after_three_pledges()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StBenedict();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			for (int i = 0; i < 2; i++)
			{
				EventManager.Publish(new PledgeAddedEvent { Card = entityManager.CreateEntity($"Card_{i}") });
			}

			Assert.Equal(2, medal.CurrentCount);

			EventManager.Publish(new PledgeAddedEvent { Card = entityManager.CreateEntity("Card_2") });

			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StPaulMiki_resets_and_triggers_once_per_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StPaulMiki();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);

			var blackCard = entityManager.CreateEntity("BlackCard");
			entityManager.AddComponent(blackCard, new CardData { Color = CardData.CardColor.Black });

			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			Assert.Equal(0, medal.CurrentCount);

			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			Assert.Equal(0, medal.CurrentCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StSimonOfCyrene_applies_anathema_on_start_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var enemy = entityManager.CreateEntity("Enemy");
			entityManager.AddComponent(enemy, new AppliedPassives());
			var medal = new StSimonOfCyrene();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);
			var applied = new List<ApplyPassiveEvent>();
			EventManager.Subscribe<ApplyPassiveEvent>(evt => applied.Add(evt));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			medal.Activate();

			Assert.Equal(1, activateCount);
			Assert.Single(applied);
			Assert.Same(enemy, applied[0].Target);
			Assert.Equal(AppliedPassiveType.Anathema, applied[0].Type);
			Assert.Equal(1, applied[0].Delta);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StClare_deals_damage_on_start_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			var enemy = entityManager.CreateEntity("Enemy");
			var medal = new StClare();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);
			var damageRequests = new List<ModifyHpRequestEvent>();
			EventManager.Subscribe<ModifyHpRequestEvent>(evt => damageRequests.Add(evt));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			medal.Activate();

			Assert.Equal(1, activateCount);
			Assert.Single(damageRequests);
			Assert.Same(player, damageRequests[0].Source);
			Assert.Same(enemy, damageRequests[0].Target);
			Assert.Equal(-2, damageRequests[0].Delta);
			Assert.Equal(ModifyTypeEnum.Effect, damageRequests[0].DamageType);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StHomobonus_grants_climb_resources_after_three_encounters()
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			var climb = SaveCache.GetClimbState();
			climb.resources = new ClimbResourceSave { red = 1, white = 1, black = 1 };
			climb.pendingEncounterReward = new ClimbEncounterRewardSave
			{
				resources = new ClimbResourceSave { red = 0, white = 0, black = 0 },
			};
			SaveCache.SaveClimbState(climb);

			var entityManager = new EntityManager();
			var medal = new StHomobonus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			for (int i = 0; i < 2; i++)
			{
				EventManager.Publish(new ShowQuestRewardOverlay { IsEncounterReward = true });
			}

			Assert.Equal(2, medal.CurrentCount);
			Assert.Equal(1, SaveCache.GetClimbState().resources.red);
			Assert.Equal(1, SaveCache.GetClimbState().resources.white);
			Assert.Equal(1, SaveCache.GetClimbState().resources.black);

			var thirdEncounterReward = new ShowQuestRewardOverlay
			{
				IsEncounterReward = true,
				ClimbResources = new ClimbResourceSave { red = 1, white = 0, black = 0 },
			};
			EventManager.Publish(thirdEncounterReward);

			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(2, SaveCache.GetClimbState().resources.red);
			Assert.Equal(2, SaveCache.GetClimbState().resources.white);
			Assert.Equal(2, SaveCache.GetClimbState().resources.black);
			Assert.Equal(2, thirdEncounterReward.ClimbResources.red);
			Assert.Equal(1, thirdEncounterReward.ClimbResources.white);
			Assert.Equal(1, thirdEncounterReward.ClimbResources.black);
			Assert.Equal(1, SaveCache.GetClimbState().pendingEncounterReward.resources.red);
			Assert.Equal(1, SaveCache.GetClimbState().pendingEncounterReward.resources.white);
			Assert.Equal(1, SaveCache.GetClimbState().pendingEncounterReward.resources.black);

			EventManager.Publish(new ShowQuestRewardOverlay { IsEncounterReward = false });
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void StPeter_unsubscribes_on_dispose()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medalEntity = entityManager.CreateEntity("Medal");
			var medal = new StPeter();
			medal.Initialize(entityManager, medalEntity);
			entityManager.AddComponent(medalEntity, new EquippedMedal { Medal = medal });

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var blackCard = entityManager.CreateEntity("BlackCard");
			entityManager.AddComponent(blackCard, new CardData { Color = CardData.CardColor.Black });

			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			Assert.Equal(1, activateCount);

			medal.Dispose();

			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StPeter_does_not_trigger_after_run_end()
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();

			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new Player());

			RunMedalService.AcquireAndEquip(entityManager, "st_peter");

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			RunLifecycleService.EndCurrentRun(entityManager);

			var blackCard = entityManager.CreateEntity("BlackCard");
			entityManager.AddComponent(blackCard, new CardData { Color = CardData.CardColor.Black });

			for (int i = 0; i < 3; i++)
			{
				EventManager.Publish(new CardBlockedEvent { Card = blackCard });
			}

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void StJerome_grants_courage_when_player_gains_aggression()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new Player());
			entityManager.AddComponent(player, new Courage());

			var medal = new StJerome();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = player,
				Type = AppliedPassiveType.Aggression,
				Delta = 3
			});
			Assert.Equal(1, activateCount);

			var enemy = entityManager.CreateEntity("Enemy");
			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = enemy,
				Type = AppliedPassiveType.Aggression,
				Delta = 1
			});
			Assert.Equal(1, activateCount);

			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = player,
				Type = AppliedPassiveType.Aggression,
				Delta = -1
			});
			Assert.Equal(1, activateCount);

			ModifyCourageRequestEvent courageEvent = null;
			EventManager.Subscribe<ModifyCourageRequestEvent>(evt => courageEvent = evt);
			medal.Activate();

			Assert.NotNull(courageEvent);
			Assert.Equal(ModifyCourageType.Gain, courageEvent.Type);
			Assert.Equal(1, courageEvent.Delta);
			Assert.Equal("st_jerome", courageEvent.Reason);

			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = player,
				Type = AppliedPassiveType.Aggression,
				Delta = 2
			});
			Assert.Equal(2, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void MedalFactory_includes_st_rita_and_st_longinus()
	{
		Assert.IsType<StRita>(MedalFactory.Create("st_rita"));
		Assert.IsType<StMonica>(MedalFactory.Create("st_monica"));
		Assert.IsType<StLonginus>(MedalFactory.Create("st_longinus"));
		Assert.IsType<StElijah>(MedalFactory.Create("st_elijah"));
		Assert.IsType<StLazarus>(MedalFactory.Create("st_lazarus"));
		Assert.Contains(MedalId.StRita, MedalFactory.GetAllMedals().Keys);
		Assert.Contains(MedalId.StMonica, MedalFactory.GetAllMedals().Keys);
		Assert.Contains(MedalId.StLonginus, MedalFactory.GetAllMedals().Keys);
		Assert.Contains(MedalId.StElijah, MedalFactory.GetAllMedals().Keys);
		Assert.Contains(MedalId.StLazarus, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void StLazarus_increments_on_mill_and_triggers_at_two()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StLazarus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_1")
			});

			Assert.Equal(1, medal.CurrentCount);
			Assert.Equal(0, activateCount);

			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_2")
			});

			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StLazarus_third_mill_starts_new_cycle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StLazarus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			for (int i = 0; i < 2; i++)
			{
				EventManager.Publish(new TopCardRemovedForMillEvent
				{
					Card = entityManager.CreateEntity($"MilledCard_{i}")
				});
			}

			Assert.Equal(0, medal.CurrentCount);

			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_3")
			});

			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StLazarus_activate_publishes_resurrect_1()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StLazarus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			DrawRandomCardFromDiscardEvent resurrectEvent = null;
			EventManager.Subscribe<DrawRandomCardFromDiscardEvent>(evt => resurrectEvent = evt);

			medal.Activate();

			Assert.NotNull(resurrectEvent);
			Assert.Equal(1, resurrectEvent.Amount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StLazarus_unsubscribes_on_dispose()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medalEntity = entityManager.CreateEntity("Medal");
			var medal = new StLazarus();
			medal.Initialize(entityManager, medalEntity);

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_1")
			});
			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_2")
			});
			Assert.Equal(1, activateCount);

			medal.Dispose();

			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_3")
			});
			EventManager.Publish(new TopCardRemovedForMillEvent
			{
				Card = entityManager.CreateEntity("MilledCard_4")
			});
			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StMonica_emits_activate_on_trigger_temperance()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			var medal = new StMonica();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new TriggerTemperance { Owner = player, AbilityId = "angelic_aura" });

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StMonica_does_not_trigger_when_owner_is_null()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StMonica();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new TriggerTemperance { Owner = null, AbilityId = "angelic_aura" });

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StMonica_activate_publishes_resurrect_1()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StMonica();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			DrawRandomCardFromDiscardEvent resurrectEvent = null;
			EventManager.Subscribe<DrawRandomCardFromDiscardEvent>(evt => resurrectEvent = evt);

			medal.Activate();

			Assert.NotNull(resurrectEvent);
			Assert.Equal(1, resurrectEvent.Amount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StRita_emits_activate_on_curse_removal()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			_ = new CursedManagementSystem(entityManager);
			var medal = new StRita();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var card = EntityFactory.CreateCardFromDefinition(
				entityManager,
				"increase_faith",
				CardData.CardColor.Black,
				index: 0);
			CursedManagementSystem.ApplyCursedRuntime(entityManager, card);

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = CardApplicationType.Cursed,
			});

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StRita_emits_activate_on_cursed_runtime_card_removal()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			_ = new CursedManagementSystem(entityManager);
			var medal = new StRita();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var card = EntityFactory.CreateCardFromDefinition(
				entityManager,
				"increase_faith",
				CardData.CardColor.Black,
				index: 0);
			CursedManagementSystem.ApplyCursedRuntime(entityManager, card);

			Assert.Equal(Curse.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = CardApplicationType.Cursed,
			});

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StRita_does_not_trigger_on_card_play_without_removal()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StRita();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var curseCard = entityManager.CreateEntity("CurseCard");
			entityManager.AddComponent(curseCard, new CardData { Card = new Curse() });

			EventManager.Publish(new CardPlayedEvent { Card = curseCard, PlayedAsCurse = true });

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StRita_resets_and_triggers_once_per_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			_ = new CursedManagementSystem(entityManager);
			var medal = new StRita();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var card = EntityFactory.CreateCardFromDefinition(
				entityManager,
				"increase_faith",
				CardData.CardColor.Black,
				index: 0);
			CursedManagementSystem.ApplyCursedRuntime(entityManager, card);

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card,
				Type = CardApplicationType.Cursed,
			});
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			var card2 = EntityFactory.CreateCardFromDefinition(
				entityManager,
				"tempest",
				CardData.CardColor.White,
				index: 1);
			CursedManagementSystem.ApplyCursedRuntime(entityManager, card2);

			EventManager.Publish(new RemoveCardApplication
			{
				Card = card2,
				Type = CardApplicationType.Cursed,
			});
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StRita_does_not_trigger_on_non_curse_play()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StRita();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var strikeCard = entityManager.CreateEntity("StrikeCard");
			entityManager.AddComponent(strikeCard, new CardData { Card = CardFactory.Create("strike") });

			EventManager.Publish(new CardPlayedEvent { Card = strikeCard });

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StRita_activate_publishes_resurrect_2()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StRita();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			DrawRandomCardFromDiscardEvent resurrectEvent = null;
			EventManager.Subscribe<DrawRandomCardFromDiscardEvent>(evt => resurrectEvent = evt);

			medal.Activate();

			Assert.NotNull(resurrectEvent);
			Assert.Equal(2, resurrectEvent.Amount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StLonginus_emits_activate_on_thorned_pledge()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StLonginus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var thornedCard = entityManager.CreateEntity("ThornedCard");
			entityManager.AddComponent(thornedCard, new Thorned { Owner = thornedCard });

			EventManager.Publish(new PledgeAddedEvent { Card = thornedCard });

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StLonginus_does_not_trigger_on_normal_pledge()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StLonginus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var normalCard = entityManager.CreateEntity("NormalCard");

			EventManager.Publish(new PledgeAddedEvent { Card = normalCard });

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StLonginus_activate_requests_kunai_to_hand()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var deckEntity = entityManager.CreateEntity("Deck");
			entityManager.AddComponent(deckEntity, new Deck());

			var medal = new StLonginus();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			CardMoveRequested moveRequest = null;
			EventManager.Subscribe<CardMoveRequested>(evt => moveRequest = evt);

			medal.Activate();

			Assert.NotNull(moveRequest);
			Assert.Equal(CardZoneType.Hand, moveRequest.Destination);
			Assert.Equal("kunai", moveRequest.Card?.GetComponent<CardData>()?.Card?.CardId);
			Assert.Equal("st_longinus", moveRequest.Reason);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StElijah_emits_activate_on_scorched_pledge()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StElijah();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var scorchedCard = entityManager.CreateEntity("ScorchedCard");
			entityManager.AddComponent(scorchedCard, new Scorched { Owner = scorchedCard });

			EventManager.Publish(new PledgeAddedEvent { Card = scorchedCard });

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StElijah_does_not_trigger_on_normal_pledge()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StElijah();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var normalCard = entityManager.CreateEntity("NormalCard");

			EventManager.Publish(new PledgeAddedEvent { Card = normalCard });

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StElijah_resets_and_triggers_once_per_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StElijah();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);

			var scorchedCard = entityManager.CreateEntity("ScorchedCard");
			entityManager.AddComponent(scorchedCard, new Scorched { Owner = scorchedCard });

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new PledgeAddedEvent { Card = scorchedCard });
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			EventManager.Publish(new PledgeAddedEvent { Card = scorchedCard });
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StElijah_activate_applies_burn_to_enemy()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			entityManager.CreateEntity("Enemy");
			var medal = new StElijah();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			ApplyPassiveEvent appliedEvent = null;
			EventManager.Subscribe<ApplyPassiveEvent>(evt => appliedEvent = evt);

			medal.Activate();

			Assert.NotNull(appliedEvent);
			Assert.Same(entityManager.GetEntity("Enemy"), appliedEvent.Target);
			Assert.Equal(AppliedPassiveType.Burn, appliedEvent.Type);
			Assert.Equal(1, appliedEvent.Delta);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StThomasAquinas_OnAcquire_reduces_max_hp()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			var medal = new StThomasAquinas();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			IncreaseMaxHpEvent hpEvent = null;
			EventManager.Subscribe<IncreaseMaxHpEvent>(evt => hpEvent = evt);

			medal.OnAcquire();

			Assert.NotNull(hpEvent);
			Assert.Same(player, hpEvent.Target);
			Assert.Equal(-10, hpEvent.Delta);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StThomasAquinas_OnAcquire_increases_max_hand_size()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new MaxHandSize { Value = 4 });
			var medal = new StThomasAquinas();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			medal.OnAcquire();

			Assert.Equal(5, player.GetComponent<MaxHandSize>().Value);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void MedalFactory_includes_st_thomas_aquinas()
	{
		Assert.IsType<StThomasAquinas>(MedalFactory.Create("st_thomas_aquinas"));
		Assert.Contains(MedalId.StThomasAquinas, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void StIgnatius_emits_activate_when_action_phase_starts_with_enough_courage()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new Courage { Amount = 5 });
			var medal = new StIgnatius();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });

			Assert.Equal(1, activateCount);
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StIgnatius_does_not_trigger_when_courage_is_below_threshold()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new Courage { Amount = 4 });
			var medal = new StIgnatius();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });

			Assert.Equal(0, activateCount);
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StIgnatius_resets_and_triggers_once_per_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			entityManager.AddComponent(player, new Courage { Amount = 6 });
			var medal = new StIgnatius();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StIgnatius_activate_applies_aggression_to_player()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var player = entityManager.CreateEntity("Player");
			var medal = new StIgnatius();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			ApplyPassiveEvent appliedEvent = null;
			EventManager.Subscribe<ApplyPassiveEvent>(evt => appliedEvent = evt);

			medal.Activate();

			Assert.NotNull(appliedEvent);
			Assert.Same(player, appliedEvent.Target);
			Assert.Equal(AppliedPassiveType.Aggression, appliedEvent.Type);
			Assert.Equal(2, appliedEvent.Delta);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void MedalFactory_includes_st_ignatius()
	{
		Assert.IsType<StIgnatius>(MedalFactory.Create("st_ignatius"));
		Assert.Contains(MedalId.StIgnatius, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void StBartholomew_emits_activate_on_8_damage_attack()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (player, enemy) = CreatePlayerAndEnemy(entityManager);
			var medal = new StBartholomew();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			PublishPlayerAttackDamage(player, enemy, 8);

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StBartholomew_does_not_trigger_below_threshold()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (player, enemy) = CreatePlayerAndEnemy(entityManager);
			var medal = new StBartholomew();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			PublishPlayerAttackDamage(player, enemy, 7);

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StBartholomew_does_not_trigger_on_effect_damage()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (player, enemy) = CreatePlayerAndEnemy(entityManager);
			var medal = new StBartholomew();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new ModifyHpRequestEvent
			{
				Source = player,
				Target = enemy,
				Delta = -8,
				DamageType = ModifyTypeEnum.Effect
			});

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StBartholomew_resets_and_triggers_once_per_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (player, enemy) = CreatePlayerAndEnemy(entityManager);
			var medal = new StBartholomew();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			PublishPlayerAttackDamage(player, enemy, 8);
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			PublishPlayerAttackDamage(player, enemy, 8);
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StBartholomew_activate_applies_wounded_to_enemy()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			entityManager.CreateEntity("Enemy");
			var medal = new StBartholomew();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			ApplyPassiveEvent appliedEvent = null;
			EventManager.Subscribe<ApplyPassiveEvent>(evt => appliedEvent = evt);

			medal.Activate();

			Assert.NotNull(appliedEvent);
			Assert.Same(entityManager.GetEntity("Enemy"), appliedEvent.Target);
			Assert.Equal(AppliedPassiveType.Wounded, appliedEvent.Type);
			Assert.Equal(1, appliedEvent.Delta);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void MedalFactory_includes_st_bartholomew()
	{
		Assert.IsType<StBartholomew>(MedalFactory.Create("st_bartholomew"));
		Assert.Contains(MedalId.StBartholomew, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void StAnthonyOfPadua_emits_activate_and_shuffle_on_empty_draw_with_discard()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (deckEntity, deck) = CreateDeck(entityManager);
			deck.DiscardPile.Add(CreateCard(entityManager));
			var medal = new StAnthonyOfPadua();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			int activateCount = 0;
			ShuffleRandomCardsFromDiscardToDrawPileEvent shuffleEvent = null;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);
			EventManager.Subscribe<ShuffleRandomCardsFromDiscardToDrawPileEvent>(evt => shuffleEvent = evt);

			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });

			Assert.Equal(1, activateCount);
			Assert.NotNull(shuffleEvent);
			Assert.Same(deckEntity, shuffleEvent.Deck);
			Assert.Equal(4, shuffleEvent.Amount);
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAnthonyOfPadua_does_not_trigger_when_discard_is_empty()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (deckEntity, _) = CreateDeck(entityManager);
			var medal = new StAnthonyOfPadua();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			int activateCount = 0;
			ShuffleRandomCardsFromDiscardToDrawPileEvent shuffleEvent = null;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);
			EventManager.Subscribe<ShuffleRandomCardsFromDiscardToDrawPileEvent>(evt => shuffleEvent = evt);

			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });

			Assert.Equal(0, activateCount);
			Assert.Null(shuffleEvent);
			Assert.Equal(1, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAnthonyOfPadua_triggers_once_per_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (deckEntity, deck) = CreateDeck(entityManager);
			deck.DiscardPile.Add(CreateCard(entityManager));
			var medal = new StAnthonyOfPadua();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			int activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });
			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });

			Assert.Equal(1, activateCount);
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAnthonyOfPadua_resets_on_start_battle()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (deckEntity, deck) = CreateDeck(entityManager);
			deck.DiscardPile.Add(CreateCard(entityManager));
			var medal = new StAnthonyOfPadua();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			int activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });

			Assert.Equal(2, activateCount);
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAnthonyOfPadua_unsubscribes_on_dispose()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var (deckEntity, deck) = CreateDeck(entityManager);
			deck.DiscardPile.Add(CreateCard(entityManager));
			var medal = new StAnthonyOfPadua();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));
			medal.OnAcquire();

			int activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			medal.Dispose();
			EventManager.Publish(new DrawPileEmptyEvent { Deck = deckEntity });

			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void MedalFactory_includes_st_anthony_of_padua()
	{
		Assert.IsType<StAnthonyOfPadua>(MedalFactory.Create("st_anthony_of_padua"));
		Assert.Contains(MedalId.StAnthonyOfPadua, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void MedalFactory_includes_st_adrian()
	{
		Assert.IsType<StAdrian>(MedalFactory.Create("st_adrian"));
		Assert.Contains(MedalId.StAdrian, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void MedalFactory_includes_st_jude()
	{
		Assert.IsType<StJude>(MedalFactory.Create("st_jude"));
		Assert.Contains(MedalId.StJude, MedalFactory.GetAllMedals().Keys);
	}

	[Fact]
	public void StJude_does_not_increment_on_non_kunai_or_non_hand()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StJude();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var strike = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.White);
			EventManager.Publish(new CardMoved
			{
				Card = strike,
				To = CardZoneType.Hand
			});
			Assert.Equal(0, medal.CurrentCount);

			var kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White);
			EventManager.Publish(new CardMoved
			{
				Card = kunai,
				To = CardZoneType.DiscardPile
			});
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StJude_increments_and_triggers_every_fourth_kunai()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StJude();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			for (int i = 0; i < 3; i++)
			{
				PublishKunaiMovedToHand(entityManager);
				Assert.Equal(i + 1, medal.CurrentCount);
				Assert.Equal(0, activateCount);
			}

			PublishKunaiMovedToHand(entityManager);
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			for (int i = 0; i < 3; i++)
			{
				PublishKunaiMovedToHand(entityManager);
			}
			PublishKunaiMovedToHand(entityManager);
			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(2, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StJude_activate_upgrades_armed_kunai()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StJude();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			Entity fourthKunai = null;
			for (int i = 0; i < 4; i++)
			{
				fourthKunai = PublishKunaiMovedToHand(entityManager);
			}

			medal.Activate();

			var card = fourthKunai.GetComponent<CardData>().Card;
			Assert.True(card.IsUpgraded);
			Assert.Contains("3 times", card.Text);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StJude_already_upgraded_fourth_kunai_resets_counter_without_error()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StJude();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			for (int i = 0; i < 3; i++)
			{
				PublishKunaiMovedToHand(entityManager);
			}

			var upgradedKunai = EntityFactory.CreateCardFromDefinition(
				entityManager, "kunai", CardData.CardColor.White, false, 0, isUpgraded: true);
			EventManager.Publish(new CardMoved
			{
				Card = upgradedKunai,
				To = CardZoneType.Hand
			});

			Assert.Equal(0, medal.CurrentCount);
			Assert.Equal(1, activateCount);

			medal.Activate();
			Assert.True(upgradedKunai.GetComponent<CardData>().Card.IsUpgraded);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StJude_start_battle_clears_mid_cycle_count()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StJude();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			PublishKunaiMovedToHand(entityManager);
			PublishKunaiMovedToHand(entityManager);
			Assert.Equal(2, medal.CurrentCount);

			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.StartBattle });
			Assert.Equal(0, medal.CurrentCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAdrian_emits_activate_on_red_attack_when_proc_succeeds()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StAdrian { ProcChancePercent = 100 };
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var card = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.Red);
			EventManager.Publish(new PlayCardRequested { Card = card, CostsPaid = true });

			Assert.Equal(1, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAdrian_does_not_emit_activate_on_non_red_or_non_attack()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StAdrian { ProcChancePercent = 100 };
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			var activateCount = 0;
			EventManager.Subscribe<MedalActivateEvent>(_ => activateCount++);

			var whiteAttack = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.White);
			EventManager.Publish(new PlayCardRequested { Card = whiteAttack, CostsPaid = true });
			Assert.Equal(0, activateCount);

			var redPrayer = EntityFactory.CreateCardFromDefinition(entityManager, "increase_faith", CardData.CardColor.Red);
			EventManager.Publish(new PlayCardRequested { Card = redPrayer, CostsPaid = true });
			Assert.Equal(0, activateCount);

			var redAttackUnpaid = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.Red);
			EventManager.Publish(new PlayCardRequested { Card = redAttackUnpaid, CostsPaid = false });
			Assert.Equal(0, activateCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void StAdrian_activate_publishes_courage_loss()
	{
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var medal = new StAdrian();
			medal.Initialize(entityManager, entityManager.CreateEntity("Medal"));

			ModifyCourageRequestEvent courageEvent = null;
			EventManager.Subscribe<ModifyCourageRequestEvent>(evt => courageEvent = evt);
			medal.Activate();

			Assert.NotNull(courageEvent);
			Assert.Equal(ModifyCourageType.Lost, courageEvent.Type);
			Assert.Equal(-1, courageEvent.Delta);
			Assert.Equal("st_adrian", courageEvent.Reason);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static (Entity Player, Entity Enemy) CreatePlayerAndEnemy(EntityManager entityManager)
	{
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new AppliedPassives());

		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new AppliedPassives());

		return (player, enemy);
	}

	private static Entity PublishKunaiMovedToHand(EntityManager entityManager)
	{
		var kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White);
		EventManager.Publish(new CardMoved
		{
			Card = kunai,
			To = CardZoneType.Hand
		});
		return kunai;
	}

	private static void PublishPlayerAttackDamage(Entity player, Entity enemy, int damage)
	{
		EventManager.Publish(new ModifyHpRequestEvent
		{
			Source = player,
			Target = enemy,
			Delta = -damage,
			DamageType = ModifyTypeEnum.Attack
		});
	}

	private static (Entity Entity, Deck Deck) CreateDeck(EntityManager entityManager)
	{
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		return (deckEntity, deck);
	}

	private static Entity CreateCard(EntityManager entityManager)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData { Card = new Strike() });
		return card;
	}
}
