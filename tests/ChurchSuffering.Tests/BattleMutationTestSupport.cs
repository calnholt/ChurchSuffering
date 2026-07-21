using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.Tests;

internal static class BattleMutationTestSupport
{
	public static BattleCardMutationDisplaySystem CreateBattleMutationPipeline(EntityManager entityManager)
	{
		CardRestrictionMutationSettings.UseInstantDurationsForTests();

		var scene = entityManager.GetEntity("SceneState")
			?? entityManager.CreateEntity("SceneState");
		entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });

		var phase = entityManager.GetEntity("PhaseState")
			?? entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phase, new PhaseState());

		return new BattleCardMutationDisplaySystem(entityManager, null, null);
	}

	public static void CompleteMutations(
		EntityManager entityManager,
		BattleCardMutationDisplaySystem mutationSystem,
		int maxSteps = 100)
	{
		var gameTime = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1));
		for (int i = 0; i < maxSteps && mutationSystem.IsBusy; i++)
		{
			mutationSystem.Update(gameTime);
		}
	}

	public static void ResetSettings()
	{
		CardRestrictionMutationSettings.ResetToDefaults();
		StateSingleton.PreventClicking = false;
	}
}
