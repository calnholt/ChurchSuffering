using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Central coordinator that advances the new main/sub-phase model when ProceedToNextPhase is published.
	/// Minimal scaffolding for now: wires the sequence described by the user.
	/// </summary>
	public class PhaseCoordinatorSystem : Core.System
	{
		private readonly HashSet<Entity> _actionPhaseSuppressed = new HashSet<Entity>();

		public PhaseCoordinatorSystem(EntityManager em) : base(em)
		{
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChangedForInteractability);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PhaseState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private PhaseState GetOrCreate()
		{
			var e = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (e == null) return null;
			return e.GetComponent<PhaseState>();
		}

	private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
	{
		LoggingService.Append("PhaseCoordinatorSystem.OnChangeBattlePhaseEvent", new System.Text.Json.Nodes.JsonObject
		{
			["phase"] = evt.Current.ToString()
		});
		var ps = GetOrCreate();
		if (ps == null) return;
		if (evt.Current == SubPhase.EnemyStart) {
			// Don't increment on first EnemyStart (coming from StartBattle) since TurnNumber already starts at 1
			// Check the current phase state before updating it
			int oldTurn = ps.TurnNumber;
			if (ps.Sub != SubPhase.StartBattle) {
				ps.TurnNumber++;
				LoggingService.Append("PhaseCoordinatorSystem.OnChangePhaseFromEnemy", new System.Text.Json.Nodes.JsonObject { ["oldTurn"] = oldTurn, ["newTurn"] = ps.TurnNumber, ["previousSub"] = ps.Sub.ToString() });
			} else {
				LoggingService.Append("PhaseCoordinatorSystem.OnChangePhaseFromEnemy", new System.Text.Json.Nodes.JsonObject { ["message"] = "Not incrementing turn (coming from StartBattle)", ["turn"] = ps.TurnNumber });
			}
		}
		if (evt.Current == SubPhase.EnemyStart || evt.Current == SubPhase.PreBlock || evt.Current == SubPhase.Block || evt.Current == SubPhase.EnemyAttack || evt.Current == SubPhase.EnemyEnd) {
			ps.Main = MainPhase.EnemyTurn;
		}
		else if (evt.Current == SubPhase.StartBattle) {
			ps.Main = MainPhase.StartBattle;
		}
		else {
			ps.Main = MainPhase.PlayerTurn;
		}
		ps.Sub = evt.Current;
	}

		private void OnPhaseChangedForInteractability(ChangeBattlePhaseEvent evt)
		{
			LoggingService.Append("PhaseCoordinatorSystem.OnPhaseChangedForInteractability", new System.Text.Json.Nodes.JsonObject
			{
				["phase"] = evt.Current.ToString(),
				["suppressedCount"] = _actionPhaseSuppressed.Count
			});
			// Restore any previously suppressed cards
			foreach (var e in _actionPhaseSuppressed)
			{
				var ui = e.GetComponent<UIElement>();
				ui?.Restore();
			}
			_actionPhaseSuppressed.Clear();

			// During Action phase, suppress non-hand cards
			if (evt.Current == SubPhase.Action)
			{
				var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
				var deck = deckEntity?.GetComponent<Deck>();
				if (deck != null)
				{
					foreach (var e in EntityManager.GetEntitiesWithComponent<CardData>())
					{
						if (deck.Hand.Contains(e)) continue;
						var ui = e.GetComponent<UIElement>();
						if (ui != null && ui.BaseInteractable)
						{
							ui.Suppress();
							_actionPhaseSuppressed.Add(e);
						}
					}
				}
			}
		}

	}
}


