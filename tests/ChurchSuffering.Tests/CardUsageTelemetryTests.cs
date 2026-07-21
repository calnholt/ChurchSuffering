using System;
using System.IO;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Telemetry;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class CardUsageTelemetryTests
{
	[Fact]
	public void Tracking_system_counts_semantic_events_by_card_id()
	{
		using var files = new TelemetryTestFiles();
		var store = files.CreateStore();
		store.StartNewRun(123);
		EventManager.Clear();
		try
		{
			var entityManager = new EntityManager();
			var redStrike = CreateCard(entityManager, new Strike(), CardData.CardColor.Red);
			var whiteStrike = CreateCard(entityManager, new Strike(), CardData.CardColor.White);
			_ = new CardUsageTrackingSystem(entityManager, store);

			EventManager.Publish(new CardPlayedEvent { Card = redStrike });
			EventManager.Publish(new CardBlockedEvent { Card = whiteStrike });
			EventManager.Publish(new CardDiscardedForCostEvent { Card = redStrike });
			EventManager.Publish(new PayCostSatisfied
			{
				CardToPlay = whiteStrike,
				PaymentCards = new() { redStrike },
			});

			var document = store.Snapshot();
			var counts = Assert.Single(document.activeRun.cards).Value;
			Assert.Equal("strike", counts.cardId);
			Assert.Equal(1, counts.played);
			Assert.Equal(1, counts.blocked);
			Assert.Equal(1, counts.discardedForCost);
			Assert.Equal(3, counts.Total);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Store_archives_runs_and_preserves_lifetime_totals_after_reload()
	{
		using var files = new TelemetryTestFiles();
		var now = new DateTimeOffset(2026, 6, 11, 12, 0, 0, TimeSpan.Zero);
		var store = files.CreateStore(() => now);
		store.StartNewRun(100);
		store.Record("strike", "Strike", CardType.Attack.ToString(), CardUsageKind.Played);
		store.EndActiveRun();

		now = now.AddHours(1);
		store.StartNewRun(200);
		store.Record("strike", "Strike", CardType.Attack.ToString(), CardUsageKind.Blocked);

		var reloaded = files.CreateStore(() => now);
		var document = reloaded.Snapshot();

		var archived = Assert.Single(document.runs);
		Assert.Equal(100, archived.runMapSeed);
		Assert.NotNull(archived.endedAtUtc);
		Assert.Equal(200, document.activeRun.runMapSeed);
		Assert.Equal(1, document.lifetime["strike"].played);
		Assert.Equal(1, document.lifetime["strike"].blocked);
	}

	[Fact]
	public void Reconcile_archives_an_orphaned_active_run_when_gameplay_save_is_inactive()
	{
		using var files = new TelemetryTestFiles();
		var store = files.CreateStore();
		store.StartNewRun(321);
		store.Record("strike", "Strike", CardType.Attack.ToString(), CardUsageKind.Played);

		store.ReconcileRun(isRunActive: false, runMapSeed: 0);

		var document = store.Snapshot();
		Assert.Null(document.activeRun);
		Assert.Single(document.runs);
		Assert.Equal(1, document.lifetime["strike"].played);
	}

	[Fact]
	public void Csv_contains_archived_active_and_lifetime_rows()
	{
		using var files = new TelemetryTestFiles();
		var store = files.CreateStore();
		store.StartNewRun(10);
		store.Record("strike", "Strike", CardType.Attack.ToString(), CardUsageKind.Played);
		store.EndActiveRun();
		store.StartNewRun(20);
		store.Record("fervor", "Fervor, Blessed", CardType.Prayer.ToString(), CardUsageKind.DiscardedForCost);
		store.ExportCsv();

		var csv = File.ReadAllText(files.CsvPath);
		Assert.Contains("scope,runId,runMapSeed", csv);
		Assert.Contains("run,", csv);
		Assert.Contains("active,", csv);
		Assert.Contains("lifetime,", csv);
		Assert.Contains("\"Fervor, Blessed\"", csv);
	}

	[Fact]
	public void Corrupt_json_is_backed_up_before_starting_fresh()
	{
		using var files = new TelemetryTestFiles();
		File.WriteAllText(files.JsonPath, "{not valid json");

		var store = files.CreateStore();

		Assert.Empty(store.Snapshot().runs);
		Assert.Single(Directory.GetFiles(files.DirectoryPath, "card-usage.json.corrupt-*"));
	}

	private static Entity CreateCard(
		EntityManager entityManager,
		CardBase definition,
		CardData.CardColor color)
	{
		var card = entityManager.CreateEntity($"{definition.CardId}_{color}");
		entityManager.AddComponent(card, new CardData
		{
			Card = definition,
			Color = color,
		});
		return card;
	}

	private sealed class TelemetryTestFiles : IDisposable
	{
		public string DirectoryPath { get; } =
			Path.Combine(Path.GetTempPath(), $"ChurchSuffering-Telemetry-{Guid.NewGuid():N}");
		public string JsonPath => Path.Combine(DirectoryPath, "card-usage.json");
		public string CsvPath => Path.Combine(DirectoryPath, "card-usage.csv");

		public TelemetryTestFiles()
		{
			Directory.CreateDirectory(DirectoryPath);
		}

		public CardUsageTelemetryStore CreateStore(Func<DateTimeOffset> utcNow = null)
		{
			return new CardUsageTelemetryStore(JsonPath, CsvPath, utcNow);
		}

		public void Dispose()
		{
			if (Directory.Exists(DirectoryPath))
			{
				Directory.Delete(DirectoryPath, recursive: true);
			}
		}
	}
}
