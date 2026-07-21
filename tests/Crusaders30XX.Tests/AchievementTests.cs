using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Achievements;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class AchievementTests
{
	[Fact]
	public void Climb_achievement_catalog_has_expected_metadata_and_unique_grid_positions()
	{
		var achievements = ClimbAchievementCatalog.CreateAll().ToList();

		Assert.Equal(17, achievements.Count);
		Assert.Equal(17, achievements.Select(achievement => achievement.Id).Distinct().Count());
		Assert.Equal(17, achievements.Select(achievement => (achievement.Row, achievement.Column)).Distinct().Count());
		Assert.All(achievements, achievement => Assert.Equal(5, achievement.Points));

		var firstAscent = Assert.Single(achievements, achievement => achievement.Id == "first_ascent");
		Assert.True(firstAscent.StartsVisible);
		Assert.Equal((1, 4), (firstAscent.Row, firstAscent.Column));

		var veteran = Assert.Single(achievements, achievement => achievement.Id == "veteran_climber");
		Assert.Equal(5, veteran.TargetValue);
		Assert.All(achievements.Where(achievement => achievement.Id != "first_ascent"), achievement => Assert.False(achievement.StartsVisible));
	}

	[Theory]
	[InlineData("sword", 12, "sword_penance_12")]
	[InlineData("dagger", 18, "dagger_penance_18")]
	[InlineData("hammer", 24, "hammer_penance_24")]
	public void Weapon_penance_achievements_require_matching_weapon_and_at_least_threshold(
		string weaponId,
		int penanceLevel,
		string expectedAchievementId)
	{
		EventManager.Clear();
		try
		{
			var progressById = InitializeClimbAchievements();

			EventManager.Publish(new ClimbCompletedEvent
			{
				StartingWeaponId = weaponId,
				PenanceLevel = penanceLevel,
			});

			Assert.True(progressById[expectedAchievementId].IsCompleted);
			Assert.DoesNotContain(
				progressById.Where(pair => pair.Key.Contains("_penance_")),
				pair => !pair.Key.StartsWith(weaponId) && pair.Value.IsCompleted);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Penance_milestone_does_not_complete_below_threshold()
	{
		EventManager.Clear();
		try
		{
			var progressById = InitializeClimbAchievements();

			EventManager.Publish(new ClimbCompletedEvent
			{
				StartingWeaponId = "sword",
				PenanceLevel = 5,
			});

			Assert.True(progressById["first_ascent"].IsCompleted);
			Assert.True(progressById["sword_penance_0"].IsCompleted);
			Assert.False(progressById["sword_penance_6"].IsCompleted);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Veteran_climber_completes_on_fifth_climb()
	{
		EventManager.Clear();
		try
		{
			var progressById = InitializeClimbAchievements();
			var completed = new ClimbCompletedEvent
			{
				StartingWeaponId = "sword",
				PenanceLevel = 0,
			};

			for (int i = 0; i < 4; i++) EventManager.Publish(completed);

			Assert.Equal(4, progressById["veteran_climber"].CurrentValue);
			Assert.False(progressById["veteran_climber"].IsCompleted);

			EventManager.Publish(completed);

			Assert.Equal(5, progressById["veteran_climber"].CurrentValue);
			Assert.True(progressById["veteran_climber"].IsCompleted);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void FirstVictory_starts_visible_on_fresh_save()
	{
		EventManager.Clear();
		try
		{
			var progress = new AchievementProgress { AchievementId = "first_victory" };
			var achievement = new FirstVictory();
			achievement.Initialize(progress, new EntityManager());

			Assert.Equal(AchievementState.Visible, achievement.State);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static Dictionary<string, AchievementProgress> InitializeClimbAchievements()
	{
		var progressById = new Dictionary<string, AchievementProgress>();
		var entityManager = new EntityManager();
		foreach (var achievement in ClimbAchievementCatalog.CreateAll())
		{
			var progress = new AchievementProgress
			{
				AchievementId = achievement.Id,
				State = AchievementState.Visible,
			};
			progressById.Add(achievement.Id, progress);
			achievement.Initialize(progress, entityManager);
		}
		return progressById;
	}

	[Fact]
	public void Relentless_completes_on_eighth_card_in_turn()
	{
		EventManager.Clear();
		try
		{
			var progress = new AchievementProgress
			{
				AchievementId = "relentless",
				State = AchievementState.Visible,
			};
			var entityManager = new EntityManager();
			var achievement = new Relentless();
			achievement.Initialize(progress, entityManager);
			var card = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.White);

			for (int i = 0; i < 7; i++)
			{
				EventManager.Publish(new CardPlayedEvent { Card = card });
			}

			Assert.False(progress.IsCompleted);
			Assert.Equal(7, progress.CurrentValue);

			EventManager.Publish(new CardPlayedEvent { Card = card });
			Assert.True(progress.IsCompleted);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void MassRevival_completes_on_four_resurrects()
	{
		EventManager.Clear();
		try
		{
			var progress = new AchievementProgress
			{
				AchievementId = "mass_revival",
				State = AchievementState.Visible,
			};
			var achievement = new MassRevival();
			achievement.Initialize(progress, new EntityManager());

			EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = 4 });

			Assert.True(progress.IsCompleted);
			Assert.Equal(4, progress.CurrentValue);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void HexedHoard_completes_with_five_cursed_entries()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entries = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Take(5).ToList();
			foreach (var entry in entries)
			{
				SaveCache.AddRunDeckEntryRestriction(
					RunDeckService.PrimaryLoadoutId,
					entry.entryId,
					RunScopedStateService.RestrictionCursed);
			}

			var progress = new AchievementProgress
			{
				AchievementId = "hexed_hoard",
				State = AchievementState.Visible,
			};
			var achievement = new HexedHoard();
			achievement.Initialize(progress, new EntityManager());

			EventManager.Publish(new StartBattleRequested());

			Assert.True(progress.IsCompleted);
			Assert.Equal(5, progress.CurrentValue);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void FadedSpectrum_completes_when_color_missing_at_fallen_shepherd_kill()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			RemoveQualifiedColorFromDeck(CardData.CardColor.Red);

			var progress = new AchievementProgress
			{
				AchievementId = "faded_spectrum",
				State = AchievementState.Visible,
			};
			var entityManager = new EntityManager();
			var achievement = new FadedSpectrum();
			achievement.Initialize(progress, entityManager);

			var enemyEntity = entityManager.CreateEntity("Enemy");
			entityManager.AddComponent(enemyEntity, new Enemy
			{
				EnemyBase = new FallenShepherd(),
			});

			EventManager.Publish(new EnemyKilledEvent { Enemy = enemyEntity });

			Assert.True(progress.IsCompleted);
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void MasterArtificer_tracks_unique_card_ids_across_events()
	{
		EventManager.Clear();
		try
		{
			var progress = new AchievementProgress
			{
				AchievementId = "master_artificer",
				State = AchievementState.Visible,
			};
			var achievement = new MasterArtificer();
			achievement.Initialize(progress, new EntityManager());

			for (int i = 0; i < 29; i++)
			{
				EventManager.Publish(new CardUpgradeConfirmedEvent { CardId = $"card_{i}" });
			}

			Assert.False(progress.IsCompleted);
			Assert.Equal(29, progress.CurrentValue);
			Assert.Equal(29, progress.trackedCardIds.Count);

			EventManager.Publish(new CardUpgradeConfirmedEvent { CardId = "card_0" });
			Assert.False(progress.IsCompleted);
			Assert.Equal(29, progress.CurrentValue);

			EventManager.Publish(new CardUpgradeConfirmedEvent { CardId = "card_29" });
			Assert.True(progress.IsCompleted);
			Assert.Equal(30, progress.CurrentValue);
			Assert.Equal(30, progress.trackedCardIds.Count);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void RunDeckCompositionService_colorless_entries_do_not_count_toward_color_totals()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			RemoveQualifiedColorFromDeck(CardData.CardColor.Red);

			var (red, _, _) = RunDeckCompositionService.GetQualifiedColorCounts();
			Assert.Equal(0, red);
			Assert.True(RunDeckCompositionService.HasEliminatedColor());
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void RunDeckCompositionService_counts_cursed_restrictions()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var entries = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Take(3).ToList();
			foreach (var entry in entries)
			{
				SaveCache.AddRunDeckEntryRestriction(
					RunDeckService.PrimaryLoadoutId,
					entry.entryId,
					RunScopedStateService.RestrictionCursed);
			}

			Assert.Equal(3, RunDeckCompositionService.CountCursedCardsInLoadout());
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	private static void RemoveQualifiedColorFromDeck(CardData.CardColor colorToRemove)
	{
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		foreach (var entry in loadout.cards.ToList())
		{
			if (!RunDeckService.TryParseCardKey(entry.cardKey, out _, out var color, out _)) continue;
			if (color != colorToRemove) continue;

			SaveCache.SetRunDeckEntryRestrictions(
				RunDeckService.PrimaryLoadoutId,
				entry.entryId,
				[RunScopedStateService.RestrictionColorless]);
		}
	}
}
