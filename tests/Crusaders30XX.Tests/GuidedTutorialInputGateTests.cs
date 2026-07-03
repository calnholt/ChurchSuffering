using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class GuidedTutorialInputGateTests
{
	[Fact]
	public void All_tutorial_actions_are_allowed()
	{
		EventManager.Clear();
		var manager = new EntityManager();
		var stateEntity = manager.CreateEntity("GuidedTutorial");
		manager.AddComponent(stateEntity, new GuidedTutorial { Section = 3 });
		var card = EntityFactory.CreateCardFromDefinition(manager, "smite", CardData.CardColor.Black);
		int messages = 0;
		EventManager.Subscribe<CantPlayCardMessage>(_ => messages++);

		Assert.True(BattleInputGate.TryAllowTutorialAction(manager, TutorialAction.PlayCard, card));
		Assert.True(BattleInputGate.TryAllowTutorialAction(manager, TutorialAction.PledgeCard, card));
		Assert.True(BattleInputGate.TryAllowTutorialAction(manager, TutorialAction.ConfirmBlocks));
		Assert.True(BattleInputGate.TryAllowTutorialAction(manager, TutorialAction.EndTurn));

		Assert.Equal(0, messages);
	}

	[Fact]
	public void Gate_returns_true_without_tutorial_state()
	{
		var manager = new EntityManager();

		Assert.True(BattleInputGate.IsTutorialActionAllowed(manager, TutorialAction.PlayCard));
		Assert.True(BattleInputGate.IsTutorialActionAllowed(manager, TutorialAction.ConfirmBlocks));
		Assert.True(BattleInputGate.IsTutorialActionAllowed(manager, TutorialAction.EndTurn));
	}

	[Fact]
	public void Gate_returns_true_with_tutorial_state()
	{
		var manager = new EntityManager();
		var stateEntity = manager.CreateEntity("GuidedTutorial");
		manager.AddComponent(stateEntity, new GuidedTutorial { Section = 5 });

		Assert.True(BattleInputGate.IsTutorialActionAllowed(manager, TutorialAction.PlayCard));
		Assert.True(BattleInputGate.IsTutorialActionAllowed(manager, TutorialAction.ConfirmBlocks));
		Assert.True(BattleInputGate.IsTutorialActionAllowed(manager, TutorialAction.EndTurn));
	}
}
