using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class AppliedPassivesDisplaySystemAnimationTests : IDisposable
{
	public AppliedPassivesDisplaySystemAnimationTests() => EventManager.Clear();
	public void Dispose() => EventManager.Clear();

	[Fact]
	public void New_passive_enters_and_settles()
	{
		var (system, owner, passives) = CreateSystem();
		passives.Passives[AppliedPassiveType.Burn] = 2;

		Update(system, 0f);

		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float initialAlpha, out float initialScale, out bool isExiting));
		Assert.Equal(0f, initialAlpha);
		Assert.Equal(system.AppearStartScale, initialScale);
		Assert.False(isExiting);

		Update(system, system.AppearSeconds);

		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float settledAlpha, out float settledScale, out isExiting));
		Assert.Equal(1f, settledAlpha);
		Assert.Equal(1f, settledScale);
		Assert.False(isExiting);
	}

	[Fact]
	public void Removed_passive_holds_its_slot_until_exit_finishes()
	{
		var (system, owner, passives) = CreateSystem();
		passives.Passives[AppliedPassiveType.Burn] = 2;
		passives.Passives[AppliedPassiveType.Armor] = 1;
		Update(system, system.AppearSeconds);

		passives.Passives.Remove(AppliedPassiveType.Burn);
		Update(system, system.DisappearSeconds / 2f);

		Assert.Equal(2, system.GetDisplayedChipCount(owner.Id));
		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float alpha, out float scale, out bool isExiting));
		Assert.True(isExiting);
		Assert.InRange(alpha, 0f, 1f);
		Assert.InRange(scale, system.DisappearEndScale, 1f);

		Update(system, system.DisappearSeconds / 2f);

		Assert.False(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out _, out _, out _));
		Assert.Equal(1, system.GetDisplayedChipCount(owner.Id));
	}

	[Fact]
	public void Stack_change_does_not_restart_animation()
	{
		var (system, owner, passives) = CreateSystem();
		passives.Passives[AppliedPassiveType.Burn] = 1;
		Update(system, system.AppearSeconds);

		passives.Passives[AppliedPassiveType.Burn] = 3;
		Update(system, 0f);

		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float alpha, out float scale, out bool isExiting));
		Assert.Equal(1f, alpha);
		Assert.Equal(1f, scale);
		Assert.False(isExiting);
	}

	[Fact]
	public void Positive_stack_gain_restarts_chip_ripple()
	{
		var (system, owner, passives) = CreateSystem();
		passives.Passives[AppliedPassiveType.Burn] = 1;
		Update(system, system.AppearSeconds);

		EventManager.Publish(new ApplyPassiveEvent
		{
			Target = owner,
			Type = AppliedPassiveType.Burn,
			Delta = 2,
		});

		Assert.True(system.TryGetRipple(owner.Id, AppliedPassiveType.Burn, out float elapsed));
		Assert.Equal(0f, elapsed);
	}

	[Fact]
	public void Reappearing_passive_reverses_from_current_presentation()
	{
		var (system, owner, passives) = CreateSystem();
		passives.Passives[AppliedPassiveType.Burn] = 1;
		Update(system, system.AppearSeconds);
		passives.Passives.Remove(AppliedPassiveType.Burn);
		Update(system, system.DisappearSeconds / 2f);
		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float exitAlpha, out float exitScale, out _));

		passives.Passives[AppliedPassiveType.Burn] = 1;
		Update(system, 0f);

		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float reversedAlpha, out float reversedScale, out bool isExiting));
		Assert.Equal(exitAlpha, reversedAlpha);
		Assert.Equal(exitScale, reversedScale);
		Assert.False(isExiting);

		Update(system, system.AppearSeconds);
		Assert.True(system.TryGetChipPresentation(owner.Id, AppliedPassiveType.Burn, out float finalAlpha, out float finalScale, out _));
		Assert.Equal(1f, finalAlpha);
		Assert.Equal(1f, finalScale);
	}

	[Fact]
	public void Delete_caches_clears_animation_state()
	{
		var (system, owner, passives) = CreateSystem();
		passives.Passives[AppliedPassiveType.Burn] = 1;
		Update(system, 0f);

		EventManager.Publish(new DeleteCachesEvent());

		Assert.Equal(0, system.GetDisplayedChipCount(owner.Id));
	}

	private static (AppliedPassivesDisplaySystem System, Entity Owner, AppliedPassives Passives) CreateSystem()
	{
		var entityManager = new EntityManager();
		var owner = entityManager.CreateEntity("PassiveOwner");
		var passives = new AppliedPassives();
		entityManager.AddComponent(owner, passives);
		var system = new AppliedPassivesDisplaySystem(entityManager, null, null);
		return (system, owner, passives);
	}

	private static void Update(AppliedPassivesDisplaySystem system, float seconds)
	{
		system.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds)));
	}
}
