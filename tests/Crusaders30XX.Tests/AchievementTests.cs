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
