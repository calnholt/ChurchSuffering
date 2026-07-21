using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class EndTurnDisplaySystemTests : IDisposable
{
	private readonly EntityManager _entityManager = new();
	private readonly PhaseState _phase;
	private readonly EndTurnDisplaySystem _system;

	public EndTurnDisplaySystemTests()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;

		var phaseEntity = _entityManager.CreateEntity("PhaseState");
		_phase = new PhaseState { Sub = SubPhase.Action };
		_entityManager.AddComponent(phaseEntity, _phase);

		var enemy = _entityManager.CreateEntity("Enemy");
		_entityManager.AddComponent(enemy, new Enemy());
		_entityManager.AddComponent(enemy, new HP { Max = 26, Current = 26 });

		_system = new EndTurnDisplaySystem(_entityManager, null, null);
		_system.Update(new GameTime());
		_system.OnChangeBattlePhaseEvent(new ChangeBattlePhaseEvent { Current = SubPhase.Action });
	}

	public void Dispose()
	{
		EventManager.Clear();
		StateSingleton.IsActive = false;
	}

	[Fact]
	public void Action_phase_shows_an_interactable_end_turn_button()
	{
		var ui = GetEndTurnUi();

		Assert.False(ui.IsHidden);
		Assert.True(ui.IsInteractable);
	}

	[Fact]
	public void Battle_animation_keeps_button_visible_and_restores_interactability()
	{
		_phase.BattleAnimationActive = true;
		_system.Update(new GameTime());

		var ui = GetEndTurnUi();
		Assert.False(ui.IsHidden);
		Assert.False(ui.IsInteractable);

		_phase.BattleAnimationActive = false;
		_system.Update(new GameTime());

		Assert.False(ui.IsHidden);
		Assert.True(ui.IsInteractable);
	}

	[Fact]
	public void Enemy_defeat_hides_and_disables_button()
	{
		_entityManager.GetEntity("Enemy").GetComponent<HP>().Current = 0;

		_system.Update(new GameTime());

		var ui = GetEndTurnUi();
		Assert.True(ui.IsHidden);
		Assert.False(ui.IsInteractable);
	}

	[Fact]
	public void Leaving_action_phase_hides_and_disables_button()
	{
		_system.OnChangeBattlePhaseEvent(new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd });

		var ui = GetEndTurnUi();
		Assert.True(ui.IsHidden);
		Assert.False(ui.IsInteractable);
	}

	private UIElement GetEndTurnUi()
	{
		return _entityManager.GetEntity("UIButton_EndTurn").GetComponent<UIElement>();
	}
}
