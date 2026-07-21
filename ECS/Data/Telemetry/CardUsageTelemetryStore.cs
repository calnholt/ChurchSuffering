using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ChurchSuffering.ECS.Data.Telemetry
{
	public sealed class CardUsageTelemetryStore
	{
		private readonly object _lock = new();
		private readonly string _jsonPath;
		private readonly string _csvPath;
		private readonly Func<DateTimeOffset> _utcNow;
		private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
		private CardUsageTelemetryDocument _document;

		public CardUsageTelemetryStore(
			string jsonPath,
			string csvPath,
			Func<DateTimeOffset> utcNow = null)
		{
			_jsonPath = jsonPath ?? string.Empty;
			_csvPath = csvPath ?? string.Empty;
			_utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
			_document = Load();
		}

		public CardUsageTelemetryDocument Snapshot()
		{
			lock (_lock)
			{
				var json = JsonSerializer.Serialize(_document, _jsonOptions);
				return JsonSerializer.Deserialize<CardUsageTelemetryDocument>(json, _jsonOptions)
					?? CreateEmptyDocument();
			}
		}

		public void ReconcileRun(bool isRunActive, int runMapSeed)
		{
			lock (_lock)
			{
				if (!isRunActive)
				{
					if (_document.activeRun != null)
					{
						ArchiveActiveRun();
						Persist();
						ExportCsvInternal();
					}
					return;
				}

				if (_document.activeRun != null && _document.activeRun.runMapSeed == runMapSeed)
				{
					return;
				}

				if (_document.activeRun != null)
				{
					ArchiveActiveRun();
				}

				_document.activeRun = CreateRun(runMapSeed);
				Persist();
				ExportCsvInternal();
			}
		}

		public void StartNewRun(int runMapSeed)
		{
			lock (_lock)
			{
				if (_document.activeRun != null)
				{
					ArchiveActiveRun();
				}

				_document.activeRun = CreateRun(runMapSeed);
				Persist();
				ExportCsvInternal();
			}
		}

		public void EndActiveRun()
		{
			lock (_lock)
			{
				if (_document.activeRun == null) return;
				ArchiveActiveRun();
				Persist();
				ExportCsvInternal();
			}
		}

		public void Record(
			string cardId,
			string name,
			string type,
			CardUsageKind kind)
		{
			if (string.IsNullOrWhiteSpace(cardId)) return;

			lock (_lock)
			{
				if (_document.activeRun == null) return;

				Increment(
					GetOrCreate(_document.activeRun.cards, cardId, name, type),
					kind);
				Increment(
					GetOrCreate(_document.lifetime, cardId, name, type),
					kind);
				Persist();
			}
		}

		public void ExportCsv()
		{
			lock (_lock)
			{
				ExportCsvInternal();
			}
		}

		private CardUsageTelemetryDocument Load()
		{
			if (string.IsNullOrWhiteSpace(_jsonPath) || !File.Exists(_jsonPath))
			{
				return CreateEmptyDocument();
			}

			try
			{
				var json = File.ReadAllText(_jsonPath);
				var document = JsonSerializer.Deserialize<CardUsageTelemetryDocument>(json, _jsonOptions);
				if (document == null || document.version != CardUsageTelemetryDocument.CURRENT_VERSION)
				{
					throw new InvalidDataException(
						$"Unsupported telemetry version {document?.version ?? 0}.");
				}

				Normalize(document);
				return document;
			}
			catch (Exception ex)
			{
				BackupCorruptFile();
				Console.WriteLine($"[CardUsageTelemetry] Failed to load {_jsonPath}: {ex.Message}");
				return CreateEmptyDocument();
			}
		}

		private void Persist()
		{
			if (string.IsNullOrWhiteSpace(_jsonPath)) return;

			try
			{
				var directory = Path.GetDirectoryName(_jsonPath);
				if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
				var json = JsonSerializer.Serialize(_document, _jsonOptions);
				WriteAtomic(_jsonPath, json);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[CardUsageTelemetry] Failed to write {_jsonPath}: {ex.Message}");
			}
		}

		private void ExportCsvInternal()
		{
			if (string.IsNullOrWhiteSpace(_csvPath)) return;

			try
			{
				var directory = Path.GetDirectoryName(_csvPath);
				if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

				var sb = new StringBuilder(4096);
				sb.AppendLine("scope,runId,runMapSeed,startedAtUtc,endedAtUtc,cardId,name,type,played,blocked,discardedForCost,totalUses");

				foreach (var run in _document.runs.OrderBy(r => r.startedAtUtc))
				{
					AppendRunRows(sb, "run", run);
				}

				if (_document.activeRun != null)
				{
					AppendRunRows(sb, "active", _document.activeRun);
				}

				foreach (var card in _document.lifetime.Values.OrderBy(c => c.cardId, StringComparer.OrdinalIgnoreCase))
				{
					AppendCsvRow(sb, "lifetime", string.Empty, null, null, null, card);
				}

				WriteAtomic(_csvPath, sb.ToString());
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[CardUsageTelemetry] Failed to write {_csvPath}: {ex.Message}");
			}
		}

		private static void AppendRunRows(StringBuilder sb, string scope, CardUsageRun run)
		{
			if (run?.cards == null) return;
			foreach (var card in run.cards.Values.OrderBy(c => c.cardId, StringComparer.OrdinalIgnoreCase))
			{
				AppendCsvRow(
					sb,
					scope,
					run.runId,
					run.runMapSeed,
					run.startedAtUtc,
					run.endedAtUtc,
					card);
			}
		}

		private static void AppendCsvRow(
			StringBuilder sb,
			string scope,
			string runId,
			int? runMapSeed,
			DateTimeOffset? startedAtUtc,
			DateTimeOffset? endedAtUtc,
			CardUsageCounts card)
		{
			var values = new[]
			{
				scope,
				runId,
				runMapSeed?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
				startedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
				endedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
				card.cardId,
				card.name,
				card.type,
				card.played.ToString(CultureInfo.InvariantCulture),
				card.blocked.ToString(CultureInfo.InvariantCulture),
				card.discardedForCost.ToString(CultureInfo.InvariantCulture),
				card.Total.ToString(CultureInfo.InvariantCulture),
			};
			sb.AppendLine(string.Join(",", values.Select(EscapeCsv)));
		}

		private void ArchiveActiveRun()
		{
			var run = _document.activeRun;
			if (run == null) return;
			run.endedAtUtc ??= _utcNow();
			_document.runs.Add(run);
			_document.activeRun = null;
		}

		private CardUsageRun CreateRun(int runMapSeed)
		{
			return new CardUsageRun
			{
				runId = Guid.NewGuid().ToString("N"),
				runMapSeed = runMapSeed,
				startedAtUtc = _utcNow(),
			};
		}

		private static CardUsageCounts GetOrCreate(
			IDictionary<string, CardUsageCounts> cards,
			string cardId,
			string name,
			string type)
		{
			if (!cards.TryGetValue(cardId, out var counts) || counts == null)
			{
				counts = new CardUsageCounts { cardId = cardId };
				cards[cardId] = counts;
			}

			if (!string.IsNullOrWhiteSpace(name)) counts.name = name;
			if (!string.IsNullOrWhiteSpace(type)) counts.type = type;
			return counts;
		}

		private static void Increment(CardUsageCounts counts, CardUsageKind kind)
		{
			switch (kind)
			{
				case CardUsageKind.Played:
					counts.played++;
					break;
				case CardUsageKind.Blocked:
					counts.blocked++;
					break;
				case CardUsageKind.DiscardedForCost:
					counts.discardedForCost++;
					break;
			}
		}

		private void BackupCorruptFile()
		{
			try
			{
				if (!File.Exists(_jsonPath)) return;
				string timestamp = _utcNow().ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
				string backupPath = $"{_jsonPath}.corrupt-{timestamp}";
				File.Copy(_jsonPath, backupPath, overwrite: false);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[CardUsageTelemetry] Failed to back up corrupt file: {ex.Message}");
			}
		}

		private static void WriteAtomic(string path, string contents)
		{
			string tempPath = $"{path}.tmp";
			File.WriteAllText(tempPath, contents);
			File.Move(tempPath, path, overwrite: true);
		}

		private static string EscapeCsv(string value)
		{
			value ??= string.Empty;
			if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\r') && !value.Contains('\n'))
			{
				return value;
			}
			return $"\"{value.Replace("\"", "\"\"")}\"";
		}

		private static CardUsageTelemetryDocument CreateEmptyDocument()
		{
			return new CardUsageTelemetryDocument();
		}

		private static void Normalize(CardUsageTelemetryDocument document)
		{
			document.runs ??= new List<CardUsageRun>();
			document.lifetime = NormalizeCards(document.lifetime);
			if (document.activeRun != null)
			{
				document.activeRun.cards = NormalizeCards(document.activeRun.cards);
			}

			foreach (var run in document.runs)
			{
				if (run != null) run.cards = NormalizeCards(run.cards);
			}
		}

		private static Dictionary<string, CardUsageCounts> NormalizeCards(
			Dictionary<string, CardUsageCounts> cards)
		{
			var normalized = new Dictionary<string, CardUsageCounts>(StringComparer.OrdinalIgnoreCase);
			if (cards == null) return normalized;
			foreach (var pair in cards)
			{
				if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value == null) continue;
				pair.Value.cardId = string.IsNullOrWhiteSpace(pair.Value.cardId)
					? pair.Key
					: pair.Value.cardId;
				normalized[pair.Key] = pair.Value;
			}
			return normalized;
		}
	}
}
