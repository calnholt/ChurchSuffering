using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Card Modular Effects")]
	public sealed class CardModularEffectsDebugSystem : Core.System
	{
		public CardModularEffectsDebugSystem(EntityManager entityManager) : base(entityManager) { }
		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Play Effects")]
		public IEnumerable<DebugNamedAction> PlayEffects()
		{
			return CardFactory.GetAllCards()
				.Where(pair => pair.Value?.VisualEffectSequence != null)
				.Select(pair => ModularEffectDebugActionFactory.CreateSequence(EntityManager, VisualEffectSourceKind.Card, pair.Key.ToKey(), pair.Value.DisplayName, pair.Value.VisualEffectSequence));
		}
	}

	[DebugTab("Equipment Modular Effects")]
	public sealed class EquipmentModularEffectsDebugSystem : Core.System
	{
		public EquipmentModularEffectsDebugSystem(EntityManager entityManager) : base(entityManager) { }
		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Play Effects")]
		public IEnumerable<DebugNamedAction> PlayEffects()
		{
			return EquipmentFactory.GetAllEquipment()
				.Where(pair => pair.Value?.ActivationEffectRecipe != null)
				.Select(pair => ModularEffectDebugActionFactory.Create(
					EntityManager,
					VisualEffectSourceKind.Equipment,
					pair.Key.ToKey(),
					pair.Value.Name,
					pair.Value.ActivationEffectRecipe));
		}
	}

	[DebugTab("Medal Modular Effects")]
	public sealed class MedalModularEffectsDebugSystem : Core.System
	{
		public MedalModularEffectsDebugSystem(EntityManager entityManager) : base(entityManager) { }
		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Play Effects")]
		public IEnumerable<DebugNamedAction> PlayEffects()
		{
			return MedalFactory.GetAllMedals()
				.Where(pair => pair.Value?.ActivationEffectRecipe != null)
				.Select(pair => ModularEffectDebugActionFactory.Create(
					EntityManager,
					VisualEffectSourceKind.Medal,
					pair.Key.ToKey(),
					pair.Value.Name,
					pair.Value.ActivationEffectRecipe));
		}
	}

	[DebugTab("Modular FX Modules")]
	public sealed class ModularEffectModuleDebugSystem : Core.System
	{
		public ModularEffectModuleDebugSystem(EntityManager entityManager) : base(entityManager) { }
		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Play Modules")]
		public IEnumerable<DebugNamedAction> PlayModules()
		{
			return VisualEffectModuleDebugCatalog.All.Select(entry =>
				ModularEffectDebugActionFactory.Create(
					EntityManager,
					entry.SourceKind,
					entry.Label,
					entry.Label,
					VisualEffectModuleDebugCatalog.BuildRecipe(entry)));
		}
	}

	[DebugTab("Enemy Attack Modular Effects")]
	public sealed class EnemyAttackModularEffectsDebugSystem : Core.System
	{
		public EnemyAttackModularEffectsDebugSystem(EntityManager entityManager) : base(entityManager) { }
		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Play Effects")]
		public IEnumerable<DebugNamedAction> PlayEffects()
		{
			return EnemyAttackFactory.GetAllAttacks()
				.Where(pair => pair.Value?.AttackEffectSequence != null)
				.Select(pair => ModularEffectDebugActionFactory.CreateSequence(
					EntityManager,
					VisualEffectSourceKind.EnemyAttack,
					pair.Key.ToKey(),
					pair.Value.Name,
					pair.Value.AttackEffectSequence));
		}
	}

	internal static class ModularEffectDebugActionFactory
	{
		public static DebugNamedAction Create(
			EntityManager entityManager,
			VisualEffectSourceKind sourceKind,
			string sourceId,
			string displayName,
			VisualEffectRecipe recipe)
		{
			string label = $"{displayName} [{sourceId}]";
			return new DebugNamedAction
			{
				Label = label,
				IsEnabled = recipe != null,
				Invoke = () =>
				{
					var request = VisualEffectRequestFactory.ForDebugPreview(
						entityManager,
						sourceKind,
						sourceId,
						displayName,
						recipe);
					if (request != null)
					{
						EventManager.Publish(request);
					}
				}
			};
		}

		public static DebugNamedAction CreateSequence(
			EntityManager entityManager,
			VisualEffectSourceKind sourceKind,
			string sourceId,
			string displayName,
			VisualEffectSequence sequence)
		{
			string label = $"{displayName} [{sourceId}]";
			return new DebugNamedAction
			{
				Label = label,
				IsEnabled = sequence?.Beats?.Count > 0,
				Invoke = () =>
				{
					foreach (var request in VisualEffectRequestFactory.ForDebugPreviewSequence(entityManager, sourceKind, sourceId, displayName, sequence))
					{
						EventManager.Publish(request);
					}
				}
			};
		}
	}
}
