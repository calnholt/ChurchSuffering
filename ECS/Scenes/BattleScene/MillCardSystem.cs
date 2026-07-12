using System;
using System.Text.Json.Nodes;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Animates milling the top card from the draw pile to the discard pile in two stages:
	/// DrawPile → Center (grow), then Center → DiscardPile (shrink).
	/// </summary>
	[DebugTab("Mill Card")]
	public class MillCardSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;

		private class MillAnim
		{
			public Entity Card;
			public float Stage1Elapsed;
			public float StageHoldElapsed;
			public float Stage2Elapsed;
			public bool InHold;
			public bool InStage2;
		}

		private readonly List<MillAnim> _anims = new List<MillAnim>();
		private readonly Queue<Entity> _queuedCards = new Queue<Entity>();

		// Durations
		[DebugEditable(DisplayName = "Stage1 Duration (s)", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public static float Stage1DurationSec { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Stage2 Duration (s)", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public static float Stage2DurationSec { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Center Hold Duration (s)", Step = 0.01f, Min = 0f, Max = 5f)]
		public static float StageHoldDurationSec { get; set; } = 0.5f;

		// Arc heights
		[DebugEditable(DisplayName = "Arc Height A→C", Step = 1, Min = -1000, Max = 1000)]
		public int ArcHeightStartToCenter { get; set; } = 160;
		[DebugEditable(DisplayName = "Arc Height C→B", Step = 1, Min = -1000, Max = 1000)]
		public int ArcHeightCenterToDiscard { get; set; } = 110;

		// Scales
		[DebugEditable(DisplayName = "Start Scale", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public float StartScale { get; set; } = 0.25f;
		[DebugEditable(DisplayName = "Mid Scale", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public float MidScale { get; set; } = 0.85f;
		[DebugEditable(DisplayName = "End Scale", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public float EndScale { get; set; } = 0.15f;

		// Easing powers
		[DebugEditable(DisplayName = "EaseOut Pow (A→C)", Step = 0.1f, Min = 0.1f, Max = 8f)]
		public float EaseOutPow { get; set; } = 1.0f;
		[DebugEditable(DisplayName = "EaseIn Pow (C→B)", Step = 0.1f, Min = 0.1f, Max = 8f)]
		public float EaseInPow { get; set; } = 1.0f;

		// Center offset
		[DebugEditable(DisplayName = "Center Offset X", Step = 1, Min = -1000, Max = 1000)]
		public int CenterOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Center Offset Y", Step = 1, Min = -1000, Max = 1000)]
		public int CenterOffsetY { get; set; } = 0;

		public MillCardSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<MillCardEvent>(OnMillRequested);
			EventManager.Subscribe<TopCardRemovedForMillEvent>(OnTopCardRemovedForMill);
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Single-frame update driver
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			LoggingService.Append("MillCardSystem.OnDeleteCachesEvent", new JsonObject { });
			// Return any milling cards to discard pile so they get shuffled back
			var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck != null)
			{
				foreach (var anim in _anims)
				{
					if (anim.Card != null && !deck.DiscardPile.Contains(anim.Card))
						deck.DiscardPile.Add(anim.Card);
				}
				while (_queuedCards.Count > 0)
				{
					var card = _queuedCards.Dequeue();
					if (card != null && !deck.DiscardPile.Contains(card))
						deck.DiscardPile.Add(card);
				}
			}
			_anims.Clear();
			_queuedCards.Clear();
			StateSingleton.PreventClicking = false;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			StateSingleton.PreventClicking = _queuedCards.Count > 0;
			if (_anims.Count == 0)
			{
				StartNextIfAllowed();
				return;
			}

			for (int i = _anims.Count - 1; i >= 0; i--)
			{
				var a = _anims[i];
				if (!a.InStage2)
				{
					if (!a.InHold)
					{
						a.Stage1Elapsed += dt;
						if (a.Stage1Elapsed >= Math.Max(0.001f, Stage1DurationSec))
						{
							a.InHold = true;
							a.StageHoldElapsed = 0f;
						}
					}
					else
					{
						a.StageHoldElapsed += dt;
						if (a.StageHoldElapsed >= Math.Max(0f, StageHoldDurationSec))
						{
							a.InHold = false;
							a.InStage2 = true;
							// As soon as the current anim enters Stage 2, allow the next queued to start
							StartNextIfAllowed();
						}
					}
				}
				else
				{
					a.Stage2Elapsed += dt;
					if (a.Stage2Elapsed >= Math.Max(0.001f, Stage2DurationSec))
					{
						// Animation complete → move card to discard
						EventManager.Publish(new CardMoveRequested
						{
							Card = a.Card,
							Destination = CardZoneType.DiscardPile,
							Reason = "MillCard"
						});
						_anims.RemoveAt(i);
					}
				}
			}

			// In case no animation is currently in Stage1/Hold, try to start next
			StartNextIfAllowed();
		}

		public void Draw()
		{
			if (_anims.Count == 0) return;

			// Resolve anchors
			Vector2 start = ResolveDrawPileAnchor();
			Vector2 end = ResolveDiscardPileAnchor();
			Vector2 center = ResolveCenterAnchor();

			for (int i = 0; i < _anims.Count; i++)
			{
				var a = _anims[i];
				Vector2 pos;
				float scale;
				if (!a.InStage2)
				{
					if (!a.InHold)
					{
						float tm = Clamp01(a.Stage1Elapsed / Math.Max(0.001f, Stage1DurationSec));
						float t = EaseOut(tm, EaseOutPow);
						pos = ArcLerp(start, center, t, ArcHeightStartToCenter);
						scale = MathHelper.Lerp(StartScale, MidScale, t);
					}
					else
					{
						pos = center;
						scale = MidScale;
					}
				}
				else
				{
					float tm = Clamp01(a.Stage2Elapsed / Math.Max(0.001f, Stage2DurationSec));
					float t = EaseIn(tm, EaseInPow);
					pos = ArcLerp(center, end, t, ArcHeightCenterToDiscard);
					scale = MathHelper.Lerp(MidScale, EndScale, t);
				}

				EventManager.Publish(new CardRenderScaledRotatedEvent
				{
					Card = a.Card,
					Position = pos,
					Scale = scale
				});
			}
		}

		[DebugAction("Mill Top Card")]
		private void Debug_MillTopCard()
		{
			EventManager.Publish(new MillCardEvent { });
		}

		[DebugActionInt("Mill N Cards", Min = 1, Max = 20, Default = 3, Step = 1)]
		private void Debug_MillNCards(int n)
		{
			for (int i = 0; i < n; i++)
			{
				EventManager.Publish(new MillCardEvent { });
			}
		}

		private void OnMillRequested(MillCardEvent evt)
		{
			LoggingService.Append("MillCardSystem.OnMillCardEvent", new JsonObject { });
			// Ask deck manager to remove the top card; animation starts when response arrives
			EventManager.Publish(new RemoveTopCardFromDrawPileRequested { });
		}

		private void OnTopCardRemovedForMill(TopCardRemovedForMillEvent evt)
		{
			LoggingService.Append("MillCardSystem.OnTopCardRemovedForMillEvent", new JsonObject {
				{ "CardName", evt?.Card?.Name }
			});
			if (evt?.Card == null) return;
			// If no animation is in Stage1/Hold, start immediately; otherwise enqueue
			bool hasStage1OrHold = _anims.Any(x => !x.InStage2);
			if (!hasStage1OrHold)
			{
				StartAnim(evt.Card);
			}
			else
			{
				_queuedCards.Enqueue(evt.Card);
			}
		}

		private void StartAnim(Entity card)
		{
			_anims.Add(new MillAnim
			{
				Card = card,
				Stage1Elapsed = 0f,
				StageHoldElapsed = 0f,
				Stage2Elapsed = 0f,
				InHold = false,
				InStage2 = false
			});
		}

		private void StartNextIfAllowed()
		{
			if (_queuedCards.Count == 0) return;
			// Only start a new one if there is no current Stage1/Hold animation
			if (_anims.Any(x => !x.InStage2)) return;
			var next = _queuedCards.Dequeue();
			StartAnim(next);
		}

		private Vector2 ResolveDrawPileAnchor()
		{
			var root = EntityManager.GetEntity("UI_DrawPileRoot");
			var t = root?.GetComponent<Transform>();
			if (t != null) return t.Position;
			return new Vector2(Game1.VirtualWidth - 60, Game1.VirtualHeight - 60);
		}

		private Vector2 ResolveDiscardPileAnchor()
		{
			var root = EntityManager.GetEntity("UI_DiscardPileRoot");
			var t = root?.GetComponent<Transform>();
			if (t != null) return t.Position;
			return new Vector2(60, Game1.VirtualHeight - 60);
		}

		private Vector2 ResolveCenterAnchor()
		{
			return new Vector2(Game1.VirtualWidth / 2f + CenterOffsetX, Game1.VirtualHeight / 2f + CenterOffsetY);
		}

		private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
		private static float EaseOut(float t, float pow) => 1f - (float)Math.Pow(1f - Clamp01(t), pow);
		private static float EaseIn(float t, float pow) => (float)Math.Pow(Clamp01(t), pow);

		private static Vector2 ArcLerp(Vector2 a, Vector2 b, float t, float arcHeight)
		{
			Vector2 ab = b - a;
			Vector2 n = new Vector2(-ab.Y, ab.X);
			float len = n.Length();
			if (len > 0.0001f) n /= len;
			// Flip to ensure arc generally goes upward on screen
			if (n.Y > 0f) n = -n;
			Vector2 p = a + ab * t;
			float wave = (float)Math.Sin(Math.PI * t);
			return p + n * arcHeight * wave;
		}

    public static float TotalDuration()
    {
      return Stage1DurationSec + Stage2DurationSec + StageHoldDurationSec;
    }
	}
}

