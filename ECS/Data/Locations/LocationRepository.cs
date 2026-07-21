using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Numerics;
using ChurchSuffering.ECS.Components;
using System;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Data.Locations
{
	public static class LocationRepository
	{
		public static Dictionary<string, LocationDefinition> LoadFromFolder(string folderAbsPath)
		{
			var map = new Dictionary<string, LocationDefinition>();
			if (!Directory.Exists(folderAbsPath)) return map;
			var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
			foreach (var file in Directory.GetFiles(folderAbsPath, "*.json"))
			{
				try
				{
					var json = File.ReadAllText(file);
					// Deserialize into DTO matching the JSON, then map to runtime definition types
					var dto = JsonSerializer.Deserialize<LocationFileDto>(json, opts);
					var def = Map(dto);
					if (def?.id != null) map[def.id] = def;
				}
				catch (System.Exception ex)
				{
					System.Console.WriteLine($"[LocationRepository] Failed to parse {file}: {ex.Message}");
				}
			}
			return map;
		}

		private static LocationDefinition Map(LocationFileDto dto)
		{
			if (dto == null) return null;
			var result = new LocationDefinition
			{
				id = dto.Id,
				name = dto.Name,
				pointsOfInterest = new List<PointOfInterestDefinition>()
			};
			if (dto.PointsOfInterest != null)
			{
				foreach (var poi in dto.PointsOfInterest)
				{
					var mappedPoi = new PointOfInterestDefinition
					{
						id = poi.Id,
						name = poi.Name,
						isRevealed = poi.IsRevealed,
						revealRadius = poi.RevealRadius,
						unrevealedRadius = poi.UnrevealedRadius,
						difficulty = poi.Difficulty,
						type = Enum.Parse<PointOfInterestType>(poi.Type),
						musicTrack = Enum.Parse<MusicTrack>(poi.MusicTrack ?? "None"),
						background = poi.Background ?? string.Empty,
						rewardGold = 0,
						events = new List<LocationEventDefinition>()
					};
					if (poi.WorldPosition != null && poi.WorldPosition.Length >= 2)
					{
						mappedPoi.worldPosition = new Vector2(poi.WorldPosition[0], poi.WorldPosition[1]);
					}
					else
					{
						mappedPoi.worldPosition = new Vector2(0f, 0f);
					}
					if (poi.Events != null)
					{
						foreach (var e in poi.Events)
						{
							var eventDef = new LocationEventDefinition { id = e.Id, type = e.Type };
							if (e.Modifications != null)
							{
								foreach (var mod in e.Modifications)
								{
									eventDef.modifications.Add(new EnemyModification { Type = mod.Type, Delta = mod.Delta });
								}
							}
							mappedPoi.events.Add(eventDef);
						}
					}
					if (poi.Tribulations != null)
					{
						mappedPoi.tribulations = new List<TribulationDefinition>();
						foreach (var trib in poi.Tribulations)
						{
							mappedPoi.tribulations.Add(new TribulationDefinition
							{
								text = trib.Text,
								trigger = trib.Trigger
							});
						}
					}
					// Reward mapping
					if (poi.Reward != null)
					{
						mappedPoi.rewardGold = poi.Reward.Gold;
					}
					// ForSale mapping
					if (poi.ForSale != null && poi.ForSale.Count > 0)
					{
						mappedPoi.forSale = new List<ForSaleItemDefinition>();
						foreach (var fs in poi.ForSale)
						{
							if (fs == null) continue;
							mappedPoi.forSale.Add(new ForSaleItemDefinition
							{
								id = fs.Id,
								type = fs.Type,
								price = fs.Price,
								isPurchased = fs.IsPurchased
							});
						}
					}
					result.pointsOfInterest.Add(mappedPoi);
				}
			}
			return result;
		}

		private class LocationFileDto
		{
			public string Id { get; set; }
			public string Name { get; set; }
			public List<PointOfInterestFileDto> PointsOfInterest { get; set; }
		}

		private class PointOfInterestFileDto
		{
			public string Id { get; set; }
			public float[] WorldPosition { get; set; }
			public int RevealRadius { get; set; }
			public int UnrevealedRadius { get; set; }
			public int Difficulty { get; set; }
			public bool IsRevealed { get; set; }
			public string Name { get; set; }
			public string Type { get; set; }
			public string Background { get; set; }
			public List<EventFileDto> Events { get; set; }
			public List<TribulationFileDto> Tribulations { get; set; }
			public RewardFileDto Reward { get; set; }
			public List<ForSaleItemFileDto> ForSale { get; set; }
			public string MusicTrack { get; set; }
		}
		private class RewardFileDto
		{
			public int Gold { get; set; }
		}

		private class EventFileDto
		{
			public string Id { get; set; }
			public string Type { get; set; }
			public string Difficulty { get; set; }
			public List<ModificationFileDto> Modifications { get; set; }
		}

		private class ModificationFileDto
		{
			public string Type { get; set; }
			public int Delta { get; set; }
		}

		private class TribulationFileDto
		{
			public string Text { get; set; }
			public string Trigger { get; set; }
		}

		private class ForSaleItemFileDto
		{
			public string Id { get; set; }
			public string Type { get; set; }
			public int Price { get; set; }
			public bool IsPurchased { get; set; }
		}
	}
}

