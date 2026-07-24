using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.Tutorials;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Equipment;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class GuidedTutorialDefinitionTests
{
	[Theory]
	[InlineData(1, "smite:Black,smite:Black")]
	[InlineData(2, "smite:Black,litany_of_wrath:Black,smite:Black")]
	[InlineData(3, "smite:Black,litany_of_wrath:Black,smite:Black,reckoning:Black")]
	[InlineData(4, "absolution:Black,litany_of_wrath:Black,smite:Black,reckoning:Black")]
	[InlineData(5, "smite:Black,smite:Black,smite:Black,smite:Black")]
	[InlineData(6, "stab:Black,smite:Red,smite:White,smite:White")]
	[InlineData(7, "smite:White,smite:White,smite:Black,smite:Black")]
	public void Stock_hands_are_exact(int section, string expected)
	{
		string actual = string.Join(",", GuidedTutorialDefinitions.GetTurn(section, 1).StockHand
			.Select(card => $"{card.CardId}:{card.Color}"));
		Assert.Equal(expected, actual);
	}

	[Fact]
	public void Section_8_has_two_turns()
	{
		Assert.Equal(2, GuidedTutorialDefinitions.GetTurnCount(8));

		string turn1 = string.Join(",", GuidedTutorialDefinitions.GetTurn(8, 1).StockHand
			.Select(card => $"{card.CardId}:{card.Color}"));
		Assert.Equal("courageous:Black,smite:Black,smite:Black,fervor:Black", turn1);

		string turn2 = string.Join(",", GuidedTutorialDefinitions.GetTurn(8, 2).StockHand
			.Select(card => $"{card.CardId}:{card.Color}"));
		Assert.Equal("litany_of_wrath:Red,absolution:Red,reckoning:Black,smite:Red", turn2);
	}

	[Fact]
	public void Enemy_hp_and_teach_flags_are_exact()
	{
		Assert.Equal(3, GuidedTutorialDefinitions.GetSection(1).EnemyHp);
		Assert.True(GuidedTutorialDefinitions.GetSection(1).IsTeachSection);

		Assert.Equal(6, GuidedTutorialDefinitions.GetSection(2).EnemyHp);
		Assert.True(GuidedTutorialDefinitions.GetSection(2).IsTeachSection);

		Assert.Equal(8, GuidedTutorialDefinitions.GetSection(3).EnemyHp);
		Assert.True(GuidedTutorialDefinitions.GetSection(3).IsTeachSection);

		Assert.Equal(10, GuidedTutorialDefinitions.GetSection(4).EnemyHp);
		Assert.Equal(9, GuidedTutorialDefinitions.GetSection(4).PlayerHp);

		Assert.Equal(5, GuidedTutorialDefinitions.GetSection(5).EnemyHp);
		Assert.True(GuidedTutorialDefinitions.GetSection(5).IsTeachSection);

		Assert.Equal(12, GuidedTutorialDefinitions.GetSection(8).EnemyHp);
		Assert.True(GuidedTutorialDefinitions.GetSection(8).ShowDrawPile);
	}

	[Fact]
	public void Section_8_has_pending_dialog()
	{
		Assert.Equal("last_of_them", GuidedTutorialDefinitions.GetSection(8).PendingDialogKey);
	}

	[Fact]
	public void Section_3_has_catch_breath_dialog()
	{
		Assert.Equal("catch_breath", GuidedTutorialDefinitions.GetSection(3).PendingDialogKey);
	}

	[Fact]
	public void Section_4_has_sword_retrieved_dialog()
	{
		Assert.Equal("sword_retrieved", GuidedTutorialDefinitions.GetSection(4).PendingDialogKey);
	}

	[Fact]
	public void Attack_ids_map_to_correct_damage_values()
	{
		Assert.Equal(9, EnemyAttackFactory.Create("tutorial_horde_strike_9").Damage);
		Assert.Equal(8, EnemyAttackFactory.Create("tutorial_horde_strike_8").Damage);
		Assert.Equal(6, EnemyAttackFactory.Create("tutorial_horde_strike_6").Damage);
		Assert.Equal(5, EnemyAttackFactory.Create("tutorial_horde_strike_5").Damage);
		Assert.Equal(3, EnemyAttackFactory.Create("tutorial_horde_strike_3").Damage);
	}

	[Fact]
	public void Teach_section_messages_are_correct()
	{
		Assert.Equal(
			["teach_win", "teach_loss", "teach_enemy_attack"],
			GuidedTutorialDefinitions.GetMessageKeys(1, 1, SubPhase.Block, 0));

		Assert.Empty(GuidedTutorialDefinitions.GetMessageKeys(2, 1, SubPhase.Block, 0));
		Assert.Equal(
			["teach_free_actions"],
			GuidedTutorialDefinitions.GetMessageKeys(2, 1, SubPhase.Action, 0));

		Assert.Empty(GuidedTutorialDefinitions.GetMessageKeys(3, 1, SubPhase.Block, 0));
		Assert.Equal(
			["teach_reckoning_discard"],
			GuidedTutorialDefinitions.GetMessageKeys(3, 1, SubPhase.Action, 0));

		Assert.Equal(
			["teach_black_block"],
			GuidedTutorialDefinitions.GetMessageKeys(5, 1, SubPhase.Block, 0));
		Assert.Equal(
			["teach_weapon"],
			GuidedTutorialDefinitions.GetMessageKeys(5, 1, SubPhase.Action, 0));

		Assert.Equal(
			["teach_red_courage", "teach_courage_hud"],
			GuidedTutorialDefinitions.GetMessageKeys(6, 1, SubPhase.Block, 0));

		Assert.Equal(
			["teach_white_temperance", "teach_temperance_hud"],
			GuidedTutorialDefinitions.GetMessageKeys(7, 1, SubPhase.Block, 0));

		Assert.Equal(
			["teach_intent_pips"],
			GuidedTutorialDefinitions.GetMessageKeys(8, 1, SubPhase.Block, 0));
		Assert.Equal(
			["teach_pledge"],
			GuidedTutorialDefinitions.GetMessageKeys(8, 1, SubPhase.Action, 0));
	}

	[Fact]
	public void Teach_messages_have_correct_targets()
	{
		var win = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_win");
		Assert.Equal("entity_name", win.targetType);
		Assert.Equal("Enemy", win.targetId);

		var loss = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_loss");
		Assert.Equal(PlayerHudLayoutSystem.HealthEntityName, loss.targetId);

		var enemyAttack = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_enemy_attack");
		Assert.Equal("entity_name", enemyAttack.targetType);
		Assert.Equal(EnemyAttackBannerAnchor.EntityName, enemyAttack.targetId);

		var courageHud = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_courage_hud");
		Assert.Equal(PlayerHudLayoutSystem.CourageEntityName, courageHud.targetId);

		var temperanceHud = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_temperance_hud");
		Assert.Equal(PlayerHudLayoutSystem.TemperanceEntityName, temperanceHud.targetId);

		var intent = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_intent_pips");
		Assert.Equal("EnemyIntentPips", intent.targetId);

		var pledge = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_pledge");
		Assert.Equal("UI_PlayerHudPledge", pledge.targetId);

		var reckoning = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_reckoning_discard");
		Assert.Equal("ui_region", reckoning.targetType);
		Assert.Equal("reckoning", reckoning.targetId);
		Assert.Equal("has_reckoning_in_hand", reckoning.condition);

		var freeActions = GuidedTutorialDefinitions.GuidedMessages.Single(msg => msg.key == "teach_free_actions");
		Assert.Equal("ui_region", freeActions.targetType);
		Assert.Equal("litany_of_wrath", freeActions.targetId);
		Assert.Equal("has_litany_of_wrath_in_hand", freeActions.condition);
	}

	[Theory]
	[InlineData("top", HotKeyPosition.Top)]
	[InlineData("bottom", HotKeyPosition.Below)]
	[InlineData("left", HotKeyPosition.Left)]
	[InlineData("right", HotKeyPosition.Right)]
	[InlineData(null, HotKeyPosition.Top)]
	[InlineData("unknown", HotKeyPosition.Top)]
	public void Tutorial_bubble_orientation_maps_to_hotkey_position(string orientation, HotKeyPosition expected)
	{
		Assert.Equal(expected, TutorialDisplaySystem.MapBubbleOrientationToHotKeyPosition(orientation));
	}

	[Fact]
	public void Tutorial_targets_player_hud_health_and_full_hand_bounds()
	{
		var loss = GuidedTutorialDefinitions.GuidedMessages.Single(message => message.key == "teach_loss");
		Assert.Equal(PlayerHudLayoutSystem.HealthEntityName, loss.targetId);

		var bounds = TutorialManager.UnionBounds(
		[
			new Rectangle(100, 400, 120, 180),
			new Rectangle(200, 360, 120, 180),
			new Rectangle(300, 420, 120, 180),
		]);
		Assert.Equal(new Rectangle(100, 360, 320, 240), bounds);
	}

	[Fact]
	public void Named_entity_target_resolves_parent_transformed_hud_bounds()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var root = manager.CreateEntity("HUD");
			manager.AddComponent(root, new Transform { Position = new Vector2(400, 500) });
			var health = manager.CreateEntity(PlayerHudLayoutSystem.HealthEntityName);
			manager.AddComponent(health, new Transform { Position = new Vector2(20, 30) });
			manager.AddComponent(health, new ParentTransform { Parent = root });
			manager.AddComponent(health, new UIElement
			{
				Bounds = new Rectangle(0, 0, 300, 36),
			});
			var tutorialManager = new TutorialManager(manager);

			Assert.Equal(
				new Rectangle(420, 530, 300, 36),
				tutorialManager.GetEntityBounds(PlayerHudLayoutSystem.HealthEntityName));
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Enemy_attack_tutorial_uses_banner_late_layout_bounds()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var anchor = manager.CreateEntity(EnemyAttackBannerAnchor.EntityName);
			manager.AddComponent(anchor, new EnemyAttackBannerAnchor());
			manager.AddComponent(anchor, new Transform { Position = new Vector2(960, 240) });
			manager.AddComponent(anchor, new UIElement { Bounds = new Rectangle(0, 0, 1, 1) });
			manager.AddComponent(anchor, new EnemyAttackBannerPresentation
			{
				IsVisible = true,
				LogicalWidth = 480,
				LogicalHeight = 180,
			});

			new EnemyAttackBannerLateLayoutSystem(manager).Update(new GameTime());
			var target = new TutorialManager(manager).GetEntityTarget(EnemyAttackBannerAnchor.EntityName);

			Assert.Equal(new Rectangle(720, 240, 480, 180), target.Bounds);
			Assert.Equal(0f, target.Rotation);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Card_tutorial_target_preserves_rotation_and_exposes_axis_aligned_envelope()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var deckEntity = manager.CreateEntity("Deck");
			var deck = new Deck();
			manager.AddComponent(deckEntity, deck);
			var card = EntityFactory.CreateCardFromDefinition(manager, "smite", CardData.CardColor.Black);
			card.GetComponent<UIElement>().Bounds = new Rectangle(300, 400, 120, 180);
			card.GetComponent<Transform>().Rotation = MathHelper.PiOver2;
			deck.Hand.Add(card);

			var tutorialManager = new TutorialManager(manager);
			var target = tutorialManager.GetUIRegionTarget("first_black_card");
			Assert.Equal(new Rectangle(300, 400, 120, 180), target.Bounds);
			Assert.Equal(MathHelper.PiOver2, target.Rotation);
			Assert.Equal(new Rectangle(270, 430, 180, 120), target.AxisAlignedBounds);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Equipment_tutorial_resolves_parent_transformed_equipment_bounds()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var player = manager.CreateEntity("Player");
			manager.AddComponent(player, new Player());

			var root = manager.CreateEntity(EquipmentDisplaySystem.RootEntityName);
			manager.AddComponent(root, new Transform { Position = new Vector2(30, 200) });
			manager.AddComponent(root, new EquipmentDisplayRoot());

			var equipment = manager.CreateEntity("Equip_Head");
			var equipmentModel = EquipmentFactory.Create("helm_of_seeing");
			equipmentModel.Initialize(manager, equipment);
			manager.AddComponent(equipment, new Transform { Position = Vector2.Zero });
			manager.AddComponent(equipment, new ParentTransform { Parent = root });
			manager.AddComponent(equipment, new UIElement { Bounds = new Rectangle(0, 0, 108, 133) });
			manager.AddComponent(equipment, new EquippedEquipment
			{
				EquippedOwner = player,
				Equipment = equipmentModel,
			});
			manager.AddComponent(equipment, new EquipmentZone { Zone = EquipmentZoneType.Default });

			var phaseEntity = manager.CreateEntity("PhaseState");
			manager.AddComponent(phaseEntity, new PhaseState
			{
				Main = MainPhase.PlayerTurn,
				Sub = SubPhase.Block,
				TurnNumber = 1,
			});

			var tutorialManager = new TutorialManager(manager);
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Block });
			tutorialManager.Update(new GameTime());

			Assert.Equal("equipment", tutorialManager.ActiveTutorial?.key);
			Assert.Equal(
				new Rectangle(30, 200, 108, 133),
				Assert.Single(tutorialManager.ResolveTargetBounds()));
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Free_actions_tutorial_queues_only_when_litany_is_in_hand()
	{
		Assert.Equal("teach_free_actions", RunSectionTwoActionTutorial(hasLitany: true)?.key);
		Assert.Null(RunSectionTwoActionTutorial(hasLitany: false));
	}

	[Fact]
	public void Free_actions_tutorial_targets_litany_card_bounds()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			AddGuidedTutorialState(manager, section: 2);
			AddPhaseState(manager);
			AddDeckWithLitany(manager, includeLitany: true);

			var tutorialManager = new TutorialManager(manager);
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
			tutorialManager.Update(new GameTime());

			Assert.Equal(
				new Rectangle(300, 400, 120, 180),
				Assert.Single(tutorialManager.ResolveTargetBounds()));
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Reckoning_discard_tutorial_queues_only_when_reckoning_is_in_hand()
	{
		Assert.Equal("teach_reckoning_discard", RunSectionThreeActionTutorial(hasReckoning: true)?.key);
		Assert.Null(RunSectionThreeActionTutorial(hasReckoning: false));
	}

	[Fact]
	public void Reckoning_discard_tutorial_targets_reckoning_card_bounds()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			AddGuidedTutorialState(manager);
			AddPhaseState(manager);
			AddDeckWithHand(manager, includeReckoning: true);

			var tutorialManager = new TutorialManager(manager);
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
			tutorialManager.Update(new GameTime());

			Assert.Equal(
				new Rectangle(300, 400, 120, 180),
				Assert.Single(tutorialManager.ResolveTargetBounds()));
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Enemy_start_advances_turn_for_section_8_without_repreparing_hand()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var stateEntity = manager.CreateEntity("GuidedTutorial");
			var state = new GuidedTutorial
			{
				Section = 8,
				TurnWithinSection = 1,
				StockHandPrepared = true,
			};
			manager.AddComponent(stateEntity, state);

			var deckEntity = manager.CreateEntity("Deck");
			var deck = new Deck();
			manager.AddComponent(deckEntity, deck);
			manager.AddComponent(deckEntity, new StockHand
			{
				Section = 8,
				TurnWithinSection = 1,
			});

			var phaseEntity = manager.CreateEntity("PhaseState");
			manager.AddComponent(phaseEntity, new PhaseState
			{
				Sub = SubPhase.PlayerEnd,
				TurnNumber = 1,
			});

			_ = new GuidedTutorialDirectorSystem(manager);
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });

			Assert.Equal(2, state.TurnWithinSection);
			Assert.True(state.StockHandPrepared);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Restart_section_sets_flag_and_resets_turn()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var stateEntity = manager.CreateEntity("GuidedTutorial");
			var state = new GuidedTutorial
			{
				Section = 3,
				TurnWithinSection = 2,
				StockHandPrepared = true,
				ConfirmedAttackCountThisTurn = 1,
			};
			state.BlockedCardIdsThisTurn.Add("smite");
			manager.AddComponent(stateEntity, state);

			GuidedTutorialService.RestartSection(manager);

			Assert.True(state.IsRestart);
			Assert.Equal(1, state.TurnWithinSection);
			Assert.False(state.StockHandPrepared);
			Assert.Empty(state.BlockedCardIdsThisTurn);
			Assert.Equal(0, state.ConfirmedAttackCountThisTurn);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Restart_request_cleans_transient_battle_state_and_ignores_spam()
	{
		EventManager.Clear();
		EventQueue.Clear();
		TimerScheduler.Clear();
		try
		{
			var manager = new EntityManager();
			var stateEntity = manager.CreateEntity("GuidedTutorial");
			var state = new GuidedTutorial
			{
				Section = 6,
				TurnWithinSection = 2,
				StockHandPrepared = true,
				ConfirmedAttackCountThisTurn = 1,
			};
			state.BlockedCardIdsThisTurn.Add("smite");
			manager.AddComponent(stateEntity, state);

			var phaseEntity = manager.CreateEntity("PhaseState");
			var phase = new PhaseState
			{
				Main = MainPhase.EnemyTurn,
				Sub = SubPhase.Block,
				TurnNumber = 4,
				DefeatPresentationActive = true,
				PendingBlockConfirm = true,
			};
			manager.AddComponent(phaseEntity, phase);

			var card = manager.CreateEntity("Card");
			manager.AddComponent(card, new CardData());
			manager.AddComponent(card, new AssignedBlockCard());
			manager.AddComponent(card, new MarkedForSpecificDiscard());
			manager.AddComponent(card, new CannotBlockThisAttack());

			var progressEntity = manager.CreateEntity("EnemyAttackProgress[1]");
			manager.AddComponent(progressEntity, new EnemyAttackProgress { AttackSequence = 1 });

			var enemy = manager.CreateEntity("Enemy");
			var intent = new AttackIntent { ActiveAttackSequence = 1 };
			intent.Planned.Add(new PlannedAttack { AttackId = EnemyAttackId.TutorialHordeStrike6 });
			var nextIntent = new NextTurnAttackIntent();
			nextIntent.Planned.Add(new PlannedAttack { AttackId = EnemyAttackId.TutorialHordeStrike8 });
			manager.AddComponent(enemy, intent);
			manager.AddComponent(enemy, nextIntent);

			var payCostEntity = manager.CreateEntity("PayCostOverlayState");
			var payCost = new PayCostOverlayState { IsOpen = true, CardToPlay = card };
			payCost.SelectedCards.Add(card);
			payCost.ConsumedCostByCardId[card.Id] = "Black";
			manager.AddComponent(payCostEntity, payCost);

			var ambushEntity = manager.CreateEntity("AmbushState");
			var ambush = new AmbushState
			{
				ActiveAttackSequence = 1,
				IsActive = true,
				IntroActive = true,
				TimerRemainingSeconds = 10f,
				FiredAutoConfirm = true,
			};
			manager.AddComponent(ambushEntity, ambush);

			var paymentEntity = manager.CreateEntity("LastPaymentCache");
			var payment = new LastPaymentCache { CardPlayed = card, HasData = true };
			payment.PaymentCards.Add(card);
			manager.AddComponent(paymentEntity, payment);

			EventQueue.EnqueueRule(new EventQueue.LogEvent("stale", "stale"));
			bool timerFired = false;
			TimerScheduler.Schedule(0.1f, () => timerFired = true);

			int transitionCount = 0;
			int deleteCachesCount = 0;
			EventManager.Subscribe<ShowTransition>(_ => transitionCount++);
			EventManager.Subscribe<DeleteCachesEvent>(_ => deleteCachesCount++);

			_ = new GuidedTutorialDirectorSystem(manager);

			EventManager.Publish(new GuidedTutorialRestartRequested());
			EventManager.Publish(new GuidedTutorialRestartRequested());
			TimerScheduler.Update(1f);

			Assert.Equal(1, transitionCount);
			Assert.Equal(1, deleteCachesCount);
			Assert.False(timerFired);
			Assert.True(EventQueue.IsIdle);
			Assert.True(state.IsRestart);
			Assert.Equal(1, state.TurnWithinSection);
			Assert.False(state.StockHandPrepared);
			Assert.Empty(state.BlockedCardIdsThisTurn);
			Assert.Equal(0, state.ConfirmedAttackCountThisTurn);
			Assert.Equal(MainPhase.StartBattle, phase.Main);
			Assert.Equal(SubPhase.StartBattle, phase.Sub);
			Assert.Equal(1, phase.TurnNumber);
			Assert.False(phase.DefeatPresentationActive);
			Assert.False(phase.PendingBlockConfirm);
			Assert.Empty(manager.GetEntitiesWithComponent<EnemyAttackProgress>());
			Assert.Empty(intent.Planned);
			Assert.Empty(nextIntent.Planned);
			Assert.False(card.HasComponent<AssignedBlockCard>());
			Assert.False(card.HasComponent<MarkedForSpecificDiscard>());
			Assert.False(card.HasComponent<CannotBlockThisAttack>());
			Assert.False(payCost.IsOpen);
			Assert.Null(payCost.CardToPlay);
			Assert.Empty(payCost.SelectedCards);
			Assert.Empty(payCost.ConsumedCostByCardId);
			Assert.False(ambush.IsActive);
			Assert.False(ambush.IntroActive);
			Assert.Equal(0f, ambush.TimerRemainingSeconds);
			Assert.False(ambush.FiredAutoConfirm);
			Assert.Equal(0, ambush.ActiveAttackSequence);
			Assert.False(payment.HasData);
			Assert.Null(payment.CardPlayed);
			Assert.Empty(payment.PaymentCards);
		}
		finally
		{
			EventManager.Clear();
			EventQueue.Clear();
			TimerScheduler.Clear();
		}
	}

	[Fact]
	public void Advance_to_next_section_snapshots_courage_and_temperance()
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			var stateEntity = manager.CreateEntity("GuidedTutorial");
			var state = new GuidedTutorial
			{
				Section = 1,
				TurnWithinSection = 1,
				PlayerHp = 1,
			};
			manager.AddComponent(stateEntity, state);

			var player = manager.CreateEntity("Player");
			manager.AddComponent(player, new Courage { Amount = 2 });
			manager.AddComponent(player, new Temperance { Amount = 1 });
			manager.AddComponent(player, new HP { Current = 5, Max = 25 });

			GuidedTutorialService.AdvanceToNextSection(manager);

			Assert.Equal(2, state.Section);
			Assert.Equal(1, state.TurnWithinSection);
			Assert.False(state.StockHandPrepared);
			Assert.False(state.IsRestart);
			Assert.Equal(2, state.BaselineCourage);
			Assert.Equal(1, state.BaselineTemperance);
			Assert.Equal(1, state.PlayerHp); // Section 2 has player HP 1
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static TutorialDefinition RunSectionTwoActionTutorial(bool hasLitany)
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			AddGuidedTutorialState(manager, section: 2);
			AddPhaseState(manager);
			AddDeckWithLitany(manager, hasLitany);

			TutorialDefinition started = null;
			EventManager.Subscribe<TutorialStartedEvent>(evt => started = evt.Tutorial);

			var tutorialManager = new TutorialManager(manager);
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
			tutorialManager.Update(new GameTime());

			return started;
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static TutorialDefinition RunSectionThreeActionTutorial(bool hasReckoning)
	{
		EventManager.Clear();
		try
		{
			var manager = new EntityManager();
			AddGuidedTutorialState(manager);
			AddPhaseState(manager);
			AddDeckWithHand(manager, hasReckoning);

			TutorialDefinition started = null;
			EventManager.Subscribe<TutorialStartedEvent>(evt => started = evt.Tutorial);

			var tutorialManager = new TutorialManager(manager);
			EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
			tutorialManager.Update(new GameTime());

			return started;
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static void AddGuidedTutorialState(EntityManager manager, int section = 3)
	{
		var stateEntity = manager.CreateEntity("GuidedTutorial");
		manager.AddComponent(stateEntity, new GuidedTutorial
		{
			Section = section,
			TurnWithinSection = 1,
		});
	}

	private static void AddPhaseState(EntityManager manager)
	{
		var phaseEntity = manager.CreateEntity("PhaseState");
		manager.AddComponent(phaseEntity, new PhaseState
		{
			Main = MainPhase.PlayerTurn,
			Sub = SubPhase.Action,
			TurnNumber = 1,
		});
	}

	private static void AddDeckWithLitany(EntityManager manager, bool includeLitany)
	{
		var deckEntity = manager.CreateEntity("Deck");
		var deck = new Deck();
		manager.AddComponent(deckEntity, deck);

		var smite = EntityFactory.CreateCardFromDefinition(manager, "smite", CardData.CardColor.Black);
		smite.GetComponent<UIElement>().Bounds = new Rectangle(100, 400, 120, 180);
		deck.Hand.Add(smite);

		if (!includeLitany) return;

		var litany = EntityFactory.CreateCardFromDefinition(manager, "litany_of_wrath", CardData.CardColor.Black);
		litany.GetComponent<UIElement>().Bounds = new Rectangle(300, 400, 120, 180);
		deck.Hand.Add(litany);
	}

	private static void AddDeckWithHand(EntityManager manager, bool includeReckoning)
	{
		var deckEntity = manager.CreateEntity("Deck");
		var deck = new Deck();
		manager.AddComponent(deckEntity, deck);

		var smite = EntityFactory.CreateCardFromDefinition(manager, "smite", CardData.CardColor.Black);
		smite.GetComponent<UIElement>().Bounds = new Rectangle(100, 400, 120, 180);
		deck.Hand.Add(smite);

		if (!includeReckoning) return;

		var reckoning = EntityFactory.CreateCardFromDefinition(manager, "reckoning", CardData.CardColor.Black);
		reckoning.GetComponent<UIElement>().Bounds = new Rectangle(300, 400, 120, 180);
		deck.Hand.Add(reckoning);
	}

	[Fact]
	public void Covered_tutorial_keys_include_all_teach_keys()
	{
		Assert.Contains("teach_win", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_loss", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_black_block", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_red_courage", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_white_temperance", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_intent_pips", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_pledge", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_reckoning_discard", GuidedTutorialDefinitions.CoveredTutorialKeys);
		Assert.Contains("teach_free_actions", GuidedTutorialDefinitions.CoveredTutorialKeys);
	}
}
