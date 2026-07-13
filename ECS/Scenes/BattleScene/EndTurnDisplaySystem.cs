using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using System;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Data.Tutorials;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays an "End Turn" button during the player's Action phase.
    /// When clicked, transitions to Block phase. Button mirrors EnemyAttackDisplaySystem styling.
    /// </summary>
    [DebugTab("End Turn UI")]
    public class EndTurnDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font = FontSingleton.ContentFont;
        private Texture2D _cachedButtonTexture;
        private string _cachedButtonText;

        // Visuals similar to EnemyAttackDisplaySystem confirm button
        [DebugEditable(DisplayName = "Button Width", Step = 1, Min = 80, Max = 600)]
        public int ButtonWidth { get; set; } = 220;

        [DebugEditable(DisplayName = "Button Height", Step = 1, Min = 24, Max = 200)]
        public int ButtonHeight { get; set; } = 64;

        [DebugEditable(DisplayName = "Button Offset Y", Step = 5, Min = -2000, Max = 2000)]
        public int ButtonOffsetY { get; set; } = 550;

        [DebugEditable(DisplayName = "Button Z", Step = 100, Min = 0, Max = 20000)]
        public int ButtonZ { get; set; } = 4000;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.2f, Max = 2.5f)]
        public float ButtonTextScale { get; set; } = 0.2f;

        public EndTurnDisplaySystem(EntityManager entityManager, GraphicsDevice gd, SpriteBatch sb) : base(entityManager)
        {
            _graphicsDevice = gd;
            _spriteBatch = sb;

            // Hook to support button click via input system
            EventManager.Subscribe<EndTurnRequested>(_ =>
            {
                LoggingService.Append("EndTurnDisplaySystem.OnEndTurnRequested", new System.Text.Json.Nodes.JsonObject { ["message"] = "received" });
                OnEndTurnPressed();
            });
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
            EventManager.Subscribe<OpenPayCostOverlayEvent>(_ => HideEndTurnButton());
            EventManager.Subscribe<ClosePayCostOverlayEvent>(_ => ShowEndTurnButton());
            EventManager.Subscribe<PayCostCancelRequested>(_ => ShowEndTurnButton());
            EventManager.Subscribe<PayCostSatisfied>(_ => ShowEndTurnButton());
            EventManager.Subscribe<LoadSceneEvent>(_ => HideEndTurnButton());

            EventManager.Subscribe<EndTurnDisplayEvent>(_ => {
                if (_.ShowButton)
                {
                    ShowEndTurnButton();
                }
                else
                {
                    HideEndTurnButton();
                }
            });

            			// Ensure a clickable UI entity exists and keep its base anchored; ParallaxLayer will offset Position
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            // Singletons; no per-entity iteration needed
            return System.Array.Empty<Entity>();
        }

        public void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        { 
            var ui = EntityManager.GetEntity("UIButton_EndTurn")?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsHidden = evt.Current != SubPhase.Action;
                ui.IsInteractable = evt.Current == SubPhase.Action;
            }
        }

        public void HideEndTurnButton()
        {
            var ui = EntityManager.GetEntity("UIButton_EndTurn")?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsHidden = true;
                ui.IsInteractable = false;
            }
        }
        public void ShowEndTurnButton()
        {
            var ui = EntityManager.GetEntity("UIButton_EndTurn")?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsHidden = false;
                ui.IsInteractable = true;
            }
        }

        private void OnEndTurnPressed()
        {
            if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
            if (!BattleInputGate.TryAllowTutorialAction(EntityManager, TutorialAction.EndTurn)) return;
            var ui = EntityManager.GetEntity("UIButton_EndTurn")?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsHidden = true;
                ui.IsInteractable = false;
            }
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck != null)
            {
                HandStateLoggingService.AppendHandSnapshot("EndTurnDisplaySystem.OnEndTurnPressed.handSnapshot", deck, "beforeQueuedPhaseChain", SubPhase.PlayerEnd);
            }
            LoggingService.Append("EndTurnDisplaySystem.OnEndTurnPressed.queue", new System.Text.Json.Nodes.JsonObject
            {
                ["phaseChain"] = "PlayerEnd,EnemyStart,PreBlock,Block",
                ["handCount"] = deck?.Hand.Count ?? 0,
                ["visibleHandCount"] = deck != null ? HandStateLoggingService.CountVisibleHand(deck.Hand) : 0,
                ["effectiveDrawHandCount"] = deck != null ? HandStateLoggingService.CountEffectiveDrawHand(deck.Hand) : 0
            });
            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                "Rule.ChangePhase.PlayerEnd",
                new ChangeBattlePhaseEvent { Current = SubPhase.PlayerEnd }
            ));

            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                "Rule.ChangePhase.EnemyStart",
                new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart }
            ));
            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                "Rule.ChangePhase.PreBlock",
                new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock }
            ));
            EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
                "Rule.ChangePhase.Block",
                new ChangeBattlePhaseEvent { Current = SubPhase.Block }
            ));
        }

        [DebugAction("Trigger End Turn Now")]
        public void Debug_EndTurnNow()
        {
            OnEndTurnPressed();
        }

        private Rectangle GetButtonRect()
        {
            int width = Game1.VirtualWidth;
            int height = Game1.VirtualHeight;
            int x = (int)(width * 0.5f - ButtonWidth * 0.5f);
            int y = height - ButtonHeight - 40 - ButtonOffsetY; // above bottom UI band
            return new Rectangle(x, y, ButtonWidth, ButtonHeight);
        }

        public void Draw()
        {
            var btnRect = GetButtonRect();
            var endBtn = EntityManager.GetEntity("UIButton_EndTurn");
            var ui = endBtn?.GetComponent<UIElement>();
            if (ui == null || ui.IsHidden == true) return;

			// Draw using the entity's current Transform.Position (which includes parallax offset)
			var t = endBtn?.GetComponent<Transform>();
			Vector2 drawPos = (t != null) ? t.Position : new Vector2(btnRect.X, btnRect.Y);
			var drawRect = new Rectangle((int)drawPos.X, (int)drawPos.Y, btnRect.Width, btnRect.Height);

			// Ensure cached button texture
			string label = "End Turn";
			if (_cachedButtonTexture == null || _cachedButtonText != label)
			{
				_cachedButtonTexture?.Dispose();
				_cachedButtonTexture = ButtonTextureFactory.Create(
					_graphicsDevice, label, Color.White, Color.DarkRed);
				_cachedButtonText = label;
			}
			_spriteBatch.Draw(_cachedButtonTexture,
				new Rectangle(drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height),
				Color.White);

			// Keep UI bounds aligned with drawn rect
			var endBtnUi = endBtn?.GetComponent<UIElement>();
			if (endBtnUi != null)
			{
				endBtnUi.Bounds = drawRect;
			}
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            SyncEndTurnButton();
        }

        private void SyncEndTurnButton()
        {
            bool frozen = BattleInputGate.IsBattleInputFrozen(EntityManager);
            bool enemyDefeated = BattleInputGate.IsEnemyDefeated(EntityManager);
            var btnRect = GetButtonRect();
            var endBtn = EntityManager.GetEntity("UIButton_EndTurn");
            if (endBtn == null)
            {
                endBtn = EntityManager.CreateEntity("UIButton_EndTurn");
                EntityManager.AddComponent(endBtn, new Transform { Position = new Vector2(btnRect.X, btnRect.Y), ZOrder = ButtonZ });
                EntityManager.AddComponent(endBtn, new UIElement { Bounds = btnRect, IsInteractable = true, IsHidden = true, EventType = UIElementEventType.EndTurn });
                EntityManager.AddComponent(endBtn, new HotKey { Button = FaceButton.Y });
                EntityManager.AddComponent(endBtn, ParallaxLayer.GetUIParallaxLayer());
            }
            else
            {
                var tr = endBtn.GetComponent<Transform>();
                if (tr != null)
                {
                    tr.ZOrder = ButtonZ;
                    tr.Position = new Vector2(btnRect.X, btnRect.Y);
                }
            }

            var endUi = endBtn?.GetComponent<UIElement>();
            if (endUi != null)
            {
                if (enemyDefeated)
                {
                    endUi.IsHidden = true;
                    endUi.IsInteractable = false;
                }
                else if (!endUi.IsHidden)
                {
                    endUi.IsInteractable = !frozen;
                }
            }
        }
    }
}
