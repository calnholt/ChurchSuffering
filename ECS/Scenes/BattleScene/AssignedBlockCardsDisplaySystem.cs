using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Events;
using System.Collections.Generic;
using System;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Assigned Block Display")]
	public class AssignedBlockCardsDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private readonly System.Collections.Generic.Dictionary<(int w, int h, int r), Texture2D> _roundedRectCache = new();
		private readonly List<Entity> _pendingReturn = new();
		// Logical base point set in UpdateEntity each frame; used by Draw to compute the banner's
		// live parallax delta without touching t.Position, which PositionTweenSystem owns for cards.
		private Vector2 _lastLogicalBasePoint;

		[DebugEditable(DisplayName = "Anchor Offset X", Step = 2, Min = -1000, Max = 1000)]
		public int AnchorOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Anchor Offset Y", Step = 2, Min = -1000, Max = 1000)]
		public int AnchorOffsetY { get; set; } = -53;
		[DebugEditable(DisplayName = "Slot Spacing X", Step = 2, Min = 10, Max = 200)]
		public int SlotSpacingX { get; set; } = 96;
		[DebugEditable(DisplayName = "Equipment Draw W", Step = 2, Min = 20, Max = 300)]
		public int CardDrawWidth { get; set; } = 100;
		[DebugEditable(DisplayName = "Equipment Draw H", Step = 2, Min = 20, Max = 400)]
		public int CardDrawHeight { get; set; } = 130;
		[DebugEditable(DisplayName = "Card Scale %", Step = 1f, Min = 10f, Max = 100f)]
		public float CardScalePercent { get; set; } = 24f;
		[DebugEditable(DisplayName = "Equipment Scale", Step = 0.02f, Min = 0.1f, Max = 1.0f)]
		public float EquipmentTargetScale { get; set; } = 0.43f;
		[DebugEditable(DisplayName = "Pullback Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float PullbackSeconds { get; set; } = 0.0f;
		[DebugEditable(DisplayName = "Launch Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float LaunchSeconds { get; set; } = 0.18f;
		[DebugEditable(DisplayName = "Impact Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ImpactSeconds { get; set; } = 0.00f;
		[DebugEditable(DisplayName = "Return Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ReturnSeconds { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Above Gap (px)", Step = 1, Min = 0, Max = 100)]
		public int AboveGap { get; set; } = 8;
		[DebugEditable(DisplayName = "Badge Text Scale", Step = 0.01f, Min = 0.05f, Max = 2.0f)]
		public float BlockTextScale { get; set; } = 1.1f;
		[DebugEditable(DisplayName = "Badge Size", Step = 1, Min = 8, Max = 80)]
		public int BadgeSize { get; set; } = 30;
		[DebugEditable(DisplayName = "Badge Offset X", Step = 1, Min = -80, Max = 80)]
		public int BadgeOffsetX { get; set; } = 13;
		[DebugEditable(DisplayName = "Badge Offset Y", Step = 1, Min = -80, Max = 80)]
		public int BadgeOffsetY { get; set; } = 14;
		[DebugEditable(DisplayName = "Badge Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int AssignedCornerRadius { get; set; } = 6;
		[DebugEditable(DisplayName = "Assigned Background Alpha", Step = 1, Min = 0, Max = 255)]
		public int AssignedBackgroundAlpha { get; set; } = 255;
		[DebugEditable(DisplayName = "Colorless Background R", Step = 1, Min = 0, Max = 255)]
		public int ColorlessBackgroundR { get; set; } = 92;
		[DebugEditable(DisplayName = "Colorless Background G", Step = 1, Min = 0, Max = 255)]
		public int ColorlessBackgroundG { get; set; } = 96;
		[DebugEditable(DisplayName = "Colorless Background B", Step = 1, Min = 0, Max = 255)]
		public int ColorlessBackgroundB { get; set; } = 102;
		[DebugEditable(DisplayName = "Colorless Foreground R", Step = 1, Min = 0, Max = 255)]
		public int ColorlessForegroundR { get; set; } = 235;
		[DebugEditable(DisplayName = "Colorless Foreground G", Step = 1, Min = 0, Max = 255)]
		public int ColorlessForegroundG { get; set; } = 235;
		[DebugEditable(DisplayName = "Colorless Foreground B", Step = 1, Min = 0, Max = 255)]
		public int ColorlessForegroundB { get; set; } = 235;

		[DebugEditable(DisplayName = "Equip Icon Height", Step = 1, Min = 8, Max = 128)]
		public int EquipIconHeight { get; set; } = 99;
		[DebugEditable(DisplayName = "Equip Icon Gap", Step = 1, Min = 0, Max = 64)]
		public int EquipIconGap { get; set; } = 6;
		[DebugEditable(DisplayName = "Equip Icon Offset X", Step = 1, Min = -200, Max = 200)]
		public int EquipIconOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Equip Icon Offset Y", Step = 1, Min = -200, Max = 200)]
		public int EquipIconOffsetY { get; set; } = 10;

		public AssignedBlockCardsDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets) : base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_imageAssets = imageAssets;
			EventManager.Subscribe<UnassignCardAsBlockRequested>(OnUnassignCardAsBlockRequested);
			EventManager.Subscribe<BlockAssignmentAdded>(OnBlockAssignmentAdded);
			EventManager.Subscribe<BlockAssignmentRemoved>(OnBlockAssignmentRemoved);
			EventManager.Subscribe<CardMoved>(OnCardMoved);
		}

		private void OnUnassignCardAsBlockRequested(UnassignCardAsBlockRequested evt)
		{
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
			LoggingService.Append("AssignedBlockCardsDisplaySystem.OnUnassignCardAsBlockRequested", new System.Text.Json.Nodes.JsonObject { ["entityId"] = evt.CardEntity.Id });
			var abc = evt.CardEntity.GetComponent<AssignedBlockCard>();
			// Immediately move B HotKey to the previous assigned (if available)
			var hk = evt.CardEntity.GetComponent<HotKey>();
			if (hk != null && hk.Button == FaceButton.B)
			{
				EntityManager.RemoveComponent<HotKey>(evt.CardEntity);
				AssignHotKeyToPrevious(evt.CardEntity, abc?.ContextId, abc?.AssignedAtTicks ?? long.MaxValue);
			}
			abc.StartPos = abc.CurrentPos;
			abc.StartScale = abc.CurrentScale;
			abc.StartRotation = abc.CurrentRotation;
			abc.Phase = AssignedBlockCard.PhaseState.Returning;
			abc.Elapsed = 0f;
			var ui = evt.CardEntity.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.IsInteractable = false;
				ui.IsHovered = false;
				ui.IsClicked = false;
			}
			ReserveReturningCardAtHandEnd(evt.CardEntity, abc);
			EventManager.Publish(new BlockAssignmentRemoved
			{
				Card = evt.CardEntity,
				DeltaBlock = -abc.BlockAmount,
				Color = abc.ColorKey,
				ContextId = abc.ContextId
			});
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Equip, Volume = 0.5f, Pitch = -0.5f});
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<AssignedBlockCard>();
		}

		public override void Update(GameTime gameTime)
		{
			// If we're processing the enemy attack, this system should not accept input or retarget cards
			var phaseStateEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
			if (phaseStateEntity == null) return;
			var phase = phaseStateEntity.GetComponent<PhaseState>();
			bool isProcessing = phase.Sub == SubPhase.EnemyAttack;
			if (isProcessing)
			{
				base.Update(gameTime);
				return;
			}
			// Ensure all assigned block cards have UIElement so clicks can be detected
			var ensureList = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>();
			foreach (var e in ensureList)
			{
				if (e.GetComponent<UIElement>() == null)
				{
					EntityManager.AddComponent(e, new UIElement { IsInteractable = true });
				}
			}

			base.Update(gameTime);
			var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			// Process any returns after the main iteration to avoid collection-modified errors
			if (_pendingReturn.Count > 0)
			{
				for (int i = 0; i < _pendingReturn.Count; i++)
				{
					var card = _pendingReturn[i];
					bool isEquipment = card.GetComponent<EquippedEquipment>() != null;
					if (isEquipment)
					{
						// Clear assignment and return equipment to default zone
						EntityManager.RemoveComponent<AssignedBlockCard>(card);
						var zone = card.GetComponent<EquipmentZone>();
						if (zone == null) { zone = new EquipmentZone(); EntityManager.AddComponent(card, zone); }
						zone.Zone = EquipmentZoneType.Default;
						var ui = card.GetComponent<UIElement>();
						if (ui != null) { ui.IsInteractable = true; ui.IsHovered = false; ui.Tooltip = string.Empty; }
					}
					else
					{
						EventManager.Publish(new CardMoveRequested
						{
							Card = card,
							Deck = deckEntity,
							Destination = CardZoneType.Hand,
							Reason = "ReturnAfterAssignment"
						});
					}
				}
				_pendingReturn.Clear();
			}

			// Self-heal: ensure B hotkey is on the newest assigned card for the current context
			{
				var enemyTip = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
				var paTip = enemyTip?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
				if (paTip != null && !string.IsNullOrEmpty(paTip.ContextId))
				{
					MaintainLatestHotKeyForContext(paTip.ContextId);
				}
			}
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var abc = entity.GetComponent<AssignedBlockCard>();
			var t = entity.GetComponent<Transform>();
			if (abc == null) return;
			// Click to return to hand when idle on the banner
			var ui = entity.GetComponent<UIElement>();
			if (ui == null)
			{
				ui = new UIElement { IsInteractable = true };
				EntityManager.AddComponent(entity, ui);
			}

			// Ensure bounds reflect where the card is currently drawn and control interactivity
			{
				var enemyTip = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
				var paTip = enemyTip?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
				bool isForCurrentContext = paTip != null && abc.ContextId == paTip.ContextId;
				bool showDuringPhase = abc.Phase == AssignedBlockCard.PhaseState.Idle || abc.Phase == AssignedBlockCard.PhaseState.Impact || abc.Phase == AssignedBlockCard.PhaseState.Launch || abc.Phase == AssignedBlockCard.PhaseState.Pullback;
				bool shouldShowTooltip = isForCurrentContext && showDuringPhase;
				var rectNow = GetAssignedBlockRect(entity, abc, abc.CurrentPos, abc.CurrentScale);

				// Always keep bounds in sync for cards in the current context; disable otherwise
				if (isForCurrentContext)
				{
					ui.Bounds = rectNow;
					ui.IsInteractable = (abc.Phase == AssignedBlockCard.PhaseState.Idle || abc.Phase == AssignedBlockCard.PhaseState.Impact) && !StateSingleton.IsActive;
				}
				else
				{
					ui.IsInteractable = false;
				}

				// Tooltip content management
				// For non-equipment cards, ensure card tooltip override while assigned for the current context
				bool isCardAssignment = !abc.IsEquipment;
				if (isCardAssignment && isForCurrentContext && showDuringPhase)
				{
					// Capture original tooltip config once
					var backup = entity.GetComponent<TooltipOverrideBackup>();
					if (backup == null)
					{
						var ctExisting = entity.GetComponent<CardTooltip>();
						backup = new TooltipOverrideBackup
						{
							OriginalType = ui?.TooltipType ?? TooltipType.Text,
							OriginalPosition = ui?.TooltipPosition ?? TooltipPosition.Above,
							OriginalOffsetPx = ui?.TooltipOffsetPx ?? 30,
							HadCardTooltip = (ctExisting != null),
							OriginalCardTooltipId = ctExisting?.CardId ?? string.Empty
						};
						EntityManager.AddComponent(entity, backup);
					}
					// Apply override: show full card tooltip below
					var cd = entity.GetComponent<CardData>();
					if (ui != null)
					{
						ui.TooltipType = TooltipType.Card;
						ui.TooltipPosition = TooltipPosition.Below;
						ui.TooltipOffsetPx = 10;
					}
					var ct = entity.GetComponent<CardTooltip>();
					if (ct == null)
					{
						EntityManager.AddComponent(entity, new CardTooltip { CardId = cd?.Card.CardId ?? string.Empty });
					}
					else
					{
						ct.CardId = cd?.Card.CardId ?? string.Empty;
					}
				}

				if (shouldShowTooltip)
				{
					// Equipment still uses text tooltip above
					if (abc.IsEquipment)
					{
						ui.Tooltip = abc.Tooltip ?? string.Empty;
						ui.TooltipOffsetPx = 10;
						ui.TooltipPosition = TooltipPosition.Above;
					}
					// Cards: no text tooltip; card tooltip override already applied above
				}
				else
				{
					ui.IsHovered = false;
					// For equipment, maintain/reset the text tooltip when not showing
					if (abc.IsEquipment)
					{
						string tip = string.Empty;
						var cdReset = ui.Owner?.GetComponent<CardData>();
						var card = CardFactory.Create(cdReset?.Card.CardId ?? string.Empty);
						if (card != null)
						{
							tip = card.Tooltip ?? string.Empty;
						}
						ui.Tooltip = tip;
						ui.TooltipOffsetPx = 30;
					}
					// Cards: do not write text tooltip while assigned
				}
			}
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			abc.Elapsed += dt;

			// Determine current context's index among assigned cards for layout
			var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			var pa = enemy?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
			if (pa == null) return;
			// Only animate if for this context; otherwise keep as-is
			bool forCurrent = abc.ContextId == pa.ContextId;
			int indexInContext = 0;
			int countInContext = 1;
			if (forCurrent)
			{
				var list = GetRelevantEntities()
					.Where(e => e.GetComponent<AssignedBlockCard>()?.ContextId == pa.ContextId)
					.OrderBy(e => e.GetComponent<AssignedBlockCard>().AssignedAtTicks) // oldest to newest left->right
					.ToList();
				indexInContext = list.FindIndex(e => e == entity);
				countInContext = list.Count;
			}

			// Compute slot target from banner anchor if available, else viewport center
			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			if (anchorEntity == null)
			{
				anchorEntity = EntityManager.CreateEntity("EnemyAttackBannerAnchor");
				EntityManager.AddComponent(anchorEntity, new EnemyAttackBannerAnchor());
				EntityManager.AddComponent(anchorEntity, new Transform());
				EntityManager.AddComponent(anchorEntity, ParallaxLayer.GetUIParallaxLayer());
				EntityManager.AddComponent(anchorEntity, new UIElement { Bounds = new Rectangle(0, 0, 1, 1), IsInteractable = false });
			}
			var anchorT = anchorEntity.GetComponent<Transform>();
			// Read logical position written this frame by EnemyAttackDisplaySystem (pre-parallax).
			// UIElement.Bounds is set in Draw() and contains last frame's post-parallax position.
			Vector2 basePoint = anchorT?.Position ?? new Vector2(Game1.VirtualWidth * 0.5f, Game1.VirtualHeight * 0.5f);
			_lastLogicalBasePoint = basePoint;
			var center = new Vector2(basePoint.X + AnchorOffsetX, basePoint.Y + AnchorOffsetY);
			float offsetIndex = indexInContext - (countInContext - 1) * 0.5f;
			float targetScale = GetTargetScale(abc);
			// Place centers so that cards sit above the banner baseline
			float slotY = center.Y - AboveGap - GetVisualBottomOffset(abc, targetScale);
			var slotTarget = new Vector2(center.X + offsetIndex * SlotSpacingX, slotY);
			abc.TargetPos = slotTarget;

			int? returnZOrder = null;
			switch (abc.Phase)
			{
				case AssignedBlockCard.PhaseState.Pullback:
				{
					abc.CurrentRotation = 0f;
					float p = PullbackSeconds <= 0f ? 1f : MathHelper.Clamp(abc.Elapsed / PullbackSeconds, 0f, 1f);
					var back = abc.StartPos + new Vector2(-30, -20);
					abc.CurrentPos = Vector2.Lerp(abc.StartPos, back, p);
					abc.CurrentScale = MathHelper.Lerp(abc.StartScale, targetScale * 0.8f, p);
					if (p >= 1f) { abc.Phase = AssignedBlockCard.PhaseState.Launch; abc.Elapsed = 0f; }
					break;
				}
				case AssignedBlockCard.PhaseState.Launch:
				{
					abc.CurrentRotation = 0f;
					float p = LaunchSeconds <= 0f ? 1f : MathHelper.Clamp(abc.Elapsed / LaunchSeconds, 0f, 1f);
					float ease = 1f - (float)System.Math.Pow(1f - p, 3);
					abc.CurrentPos = Vector2.Lerp(abc.CurrentPos, abc.TargetPos, ease);
					abc.CurrentScale = MathHelper.Lerp(abc.CurrentScale, targetScale, ease);
					if (p >= 1f) { abc.Phase = AssignedBlockCard.PhaseState.Impact; abc.Elapsed = 0f; }
					break;
				}
				case AssignedBlockCard.PhaseState.Impact:
				{
					abc.CurrentRotation = 0f;
					float p = ImpactSeconds <= 0f ? 1f : MathHelper.Clamp(abc.Elapsed / ImpactSeconds, 0f, 1f);
					abc.CurrentPos = abc.TargetPos + new Vector2(0, (1f - p) * 6f);
					abc.CurrentScale = targetScale * (1f + 0.08f * (1f - p));
					if (p >= 1f) { abc.Phase = AssignedBlockCard.PhaseState.Idle; abc.Elapsed = 0f; MaintainLatestHotKeyForContext(pa.ContextId); }
					break;
				}
				case AssignedBlockCard.PhaseState.Idle:
				{
					abc.CurrentRotation = 0f;
					float slide = 1f - (float)System.Math.Exp(-10f * dt);
					abc.CurrentPos = Vector2.Lerp(abc.CurrentPos, abc.TargetPos, slide);
					abc.CurrentScale = MathHelper.Lerp(abc.CurrentScale, targetScale, slide);
					break;
				}
				case AssignedBlockCard.PhaseState.Returning:
				{
					float p = ReturnSeconds <= 0f ? 1f : MathHelper.Clamp(abc.Elapsed / ReturnSeconds, 0f, 1f);
					float ease = 1f - (float)System.Math.Pow(1f - p, 3);
					var handPose = ResolveReturnHandPose(entity, abc);
					returnZOrder = handPose.ZOrder;
					abc.CurrentPos = Vector2.Lerp(abc.StartPos, handPose.Position, ease);
					abc.CurrentScale = MathHelper.Lerp(abc.StartScale, handPose.Scale, ease);
					abc.CurrentRotation = MathHelper.Lerp(abc.StartRotation, handPose.Rotation, ease);
					if (p >= 1f)
					{
						abc.CurrentPos = handPose.Position;
						abc.CurrentScale = handPose.Scale;
						abc.CurrentRotation = handPose.Rotation;
						SyncPositionTween(entity, handPose.Position);
						_pendingReturn.Add(entity);
					}
					else
					{
						SyncPositionTween(entity, abc.CurrentPos);
					}
					break;
				}
			}

			// Reflect animation to Transform so any other draws match position/scale if needed
			if (t == null)
			{
				t = new Transform();
				EntityManager.AddComponent(entity, t);
			}
			t.Position = abc.CurrentPos;
			t.Scale = new Vector2(abc.CurrentScale, abc.CurrentScale);
			t.Rotation = abc.CurrentRotation;
			if (returnZOrder.HasValue)
			{
				t.ZOrder = returnZOrder.Value;
			}
		}

		private void ReserveReturningCardAtHandEnd(Entity card, AssignedBlockCard assignment)
		{
			if (card == null || assignment == null || assignment.IsEquipment) return;
			var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
			if (deck == null || deck.Hand.Contains(card)) return;
			deck.Hand.Add(card);
		}

		private (Vector2 Position, float Scale, float Rotation, int ZOrder) ResolveReturnHandPose(Entity card, AssignedBlockCard assignment)
		{
			var transform = card?.GetComponent<Transform>();
			Vector2 fallbackPosition = assignment?.ReturnTargetPos ?? transform?.Position ?? Vector2.Zero;
			float fallbackScale = transform?.Scale.X ?? 1f;
			float fallbackRotation = transform?.Rotation ?? 0f;
			int fallbackZOrder = transform?.ZOrder ?? 0;

			if (card != null && assignment != null && !assignment.IsEquipment)
			{
				var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
				if (deck?.Hand.Contains(card) == true)
				{
					var tween = card.GetComponent<PositionTween>();
					Vector2 position = fallbackPosition;
					if (tween != null && tween.Target != Vector2.Zero)
					{
						position = tween.Target;
					}

					return (
						position,
						transform?.Scale.X ?? fallbackScale,
						transform?.Rotation ?? fallbackRotation,
						transform?.ZOrder ?? fallbackZOrder);
				}
			}

			return (fallbackPosition, fallbackScale, fallbackRotation, fallbackZOrder);
		}

		private void SyncPositionTween(Entity card, Vector2 position)
		{
			var tween = card?.GetComponent<PositionTween>();
			if (tween == null) return;
			tween.Current = position;
			tween.Target = position;
			tween.Initialized = true;
		}

		private void OnBlockAssignmentAdded(BlockAssignmentAdded evt)
		{
			if (evt == null || string.IsNullOrEmpty(evt.ContextId)) return;
			RemoveAssignedBlockHotKey(evt.Card);
			// Immediately remove stale HotKeys for this context; the newest idle assignment gets B later.
			RemovePreviousForContext(evt.ContextId, evt.Card);
		}

		private void OnBlockAssignmentRemoved(BlockAssignmentRemoved evt)
		{
			if (evt == null || string.IsNullOrEmpty(evt.ContextId)) return;
			RemoveAssignedBlockHotKey(evt.Card);
			// Restore tooltip settings for cards (ignore equipment)
			if (evt.Card != null && evt.Card.GetComponent<EquippedEquipment>() == null)
			{
				var backup = evt.Card.GetComponent<TooltipOverrideBackup>();
				var ui = evt.Card.GetComponent<UIElement>();
				if (backup != null && ui != null)
				{
					ui.TooltipType = backup.OriginalType;
					ui.TooltipPosition = backup.OriginalPosition;
					ui.TooltipOffsetPx = backup.OriginalOffsetPx;
					var ct = evt.Card.GetComponent<CardTooltip>();
					if (backup.HadCardTooltip)
					{
						if (ct == null) { EntityManager.AddComponent(evt.Card, new CardTooltip { CardId = backup.OriginalCardTooltipId ?? string.Empty }); }
						else { ct.CardId = backup.OriginalCardTooltipId ?? string.Empty; }
					}
					else
					{
						if (ct != null) { EntityManager.RemoveComponent<CardTooltip>(evt.Card); }
					}
					EntityManager.RemoveComponent<TooltipOverrideBackup>(evt.Card);
				}
			}
			MaintainLatestHotKeyForContext(evt.ContextId);
		}

		private void OnCardMoved(CardMoved evt)
		{
			if (evt == null) return;
			if (evt.From == CardZoneType.AssignedBlock)
			{
				var removedAssignedAt = evt.Card?.GetComponent<AssignedBlockCard>()?.AssignedAtTicks ?? long.MaxValue;
				RemoveAssignedBlockHotKey(evt.Card);
				AssignHotKeyToPrevious(evt.Card, evt.ContextId, removedAssignedAt);
				if (!string.IsNullOrEmpty(evt.ContextId))
				{
					MaintainLatestHotKeyForContext(evt.ContextId);
				}
			}
		}

		private void AssignHotKeyToPrevious(Entity removed, string contextId, long removedAssignedAt)
		{
			if (string.IsNullOrEmpty(contextId)) return;
			var prev = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Where(e => {
					if (e == removed) return false;
					var a = e.GetComponent<AssignedBlockCard>();
					return a != null && a.ContextId == contextId && a.Phase == AssignedBlockCard.PhaseState.Idle && a.AssignedAtTicks <= removedAssignedAt;
				})
				.OrderByDescending(e => e.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.FirstOrDefault();
			if (prev == null) return;
			var hk = prev.GetComponent<HotKey>();
			if (hk == null) EntityManager.AddComponent(prev, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Top });
			else
			{
				hk.Button = FaceButton.B;
				hk.Position = HotKeyPosition.Top;
			}
		}

		private void MaintainLatestHotKeyForContext(string contextId)
		{
			if (string.IsNullOrEmpty(contextId)) return;

			// Clean up: remove B hotkeys from entities that are no longer assigned block items
			foreach (var e in EntityManager.GetEntitiesWithComponent<HotKey>().ToList())
			{
				var hk = e.GetComponent<HotKey>();
				if (hk == null || hk.Button != FaceButton.B) continue;
				var abc = e.GetComponent<AssignedBlockCard>();
				var ui = e.GetComponent<UIElement>();
				bool isAssignedBlockHotKey = abc != null || (ui != null && ui.EventType == UIElementEventType.UnassignCardAsBlock);
				if (!isAssignedBlockHotKey) continue;
				if (abc == null || abc.ContextId != contextId || abc.Phase != AssignedBlockCard.PhaseState.Idle)
				{
					RemoveAssignedBlockHotKey(e);
				}
			}

			// Apply B only to the newest Idle assignment for this context
			var candidates = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Where(ent => {
					var a = ent.GetComponent<AssignedBlockCard>();
					return a != null && a.ContextId == contextId && a.Phase == AssignedBlockCard.PhaseState.Idle;
				})
				.OrderBy(ent => ent.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.ToList();
			var newest = candidates.LastOrDefault();
			foreach (var ent in candidates)
			{
				var hk = ent.GetComponent<HotKey>();
				if (ent == newest)
				{
					if (hk == null)
					{
						EntityManager.AddComponent(ent, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Top });
					}
					else
					{
						hk.Button = FaceButton.B;
						hk.Position = HotKeyPosition.Top;
					}
				}
				else
				{
					if (hk != null && hk.Button == FaceButton.B)
					{
						EntityManager.RemoveComponent<HotKey>(ent);
					}
				}
			}
		}

		private void RemovePreviousForContext(string contextId, Entity exclude)
		{
			// Find the previous (most recent) idle assignment other than the new one and remove its HotKey
			var prev = EntityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Where(e => {
					if (e == exclude) return false;
					var a = e.GetComponent<AssignedBlockCard>();
					return a != null && a.ContextId == contextId && a.Phase == AssignedBlockCard.PhaseState.Idle;
				})
				.OrderByDescending(e => e.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.FirstOrDefault();
			if (prev == null) return;
			var hkPrev = prev.GetComponent<HotKey>();
			if (hkPrev != null && hkPrev.Button == FaceButton.B)
			{
				RemoveAssignedBlockHotKey(prev);
			}
		}

		private void RemoveAssignedBlockHotKey(Entity entity)
		{
			var hk = entity?.GetComponent<HotKey>();
			if (hk != null)
			{
				EntityManager.RemoveComponent<HotKey>(entity);
			}
		}

		public void Draw()
		{
			// Draw assigned cards for the current context
			var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			var pa = enemy?.GetComponent<AttackIntent>()?.Planned?.FirstOrDefault();
			if (pa == null) return;
			var list = GetRelevantEntities()
				.Where(e => e.GetComponent<AssignedBlockCard>()?.ContextId == pa.ContextId)
				.OrderBy(e => e.GetComponent<AssignedBlockCard>().AssignedAtTicks)
				.ToList();
			if (list.Count == 0) return;
			// Assigned cards keep their visual animation in AssignedBlockCard because PositionTweenSystem
			// owns card Transform.Position. Add the banner's live parallax delta, then sync bounds to it.
			var anchorEntity = EntityManager.GetEntitiesWithComponent<EnemyAttackBannerAnchor>().FirstOrDefault();
			var anchorT = anchorEntity?.GetComponent<Transform>();
			Vector2 parallaxDelta = anchorT != null ? anchorT.Position - _lastLogicalBasePoint : Vector2.Zero;
			// Draw oldest first so newer overlapping assignments appear on top.
			for (int i = 0; i < list.Count; i++)
			{
				var card = list[i];
				var abc = card.GetComponent<AssignedBlockCard>();
				if (abc == null) continue;
				if (!abc.IsEquipment && abc.Phase == AssignedBlockCard.PhaseState.Returning) continue;
				var pos = abc.CurrentPos + parallaxDelta;
				var rect = GetAssignedBlockRect(card, abc, pos, abc.CurrentScale);
				var ui = card.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.Bounds = rect;
				}

				if (!abc.IsEquipment && card.GetComponent<CardData>() != null)
				{
					EventManager.Publish(new CardRenderScaledEvent
					{
						Card = card,
						Position = pos,
						Scale = abc.CurrentScale
					});
					DrawBlockBadge(rect, card, abc);
					continue;
				}

				DrawEquipmentAssignment(rect, card, abc);
			}
		}

		private void DrawEquipmentAssignment(Rectangle rect, Entity card, AssignedBlockCard abc)
		{
			var (bg, fg) = GetBadgeColors(card, abc);
			int radius = System.Math.Max(0, AssignedCornerRadius);
			var rounded = GetRoundedRectTexture(rect.Width, rect.Height, radius);
			var center = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
			_spriteBatch.Draw(rounded, center, null, bg, 0f, new Vector2(rounded.Width / 2f, rounded.Height / 2f), Vector2.One, SpriteEffects.None, 0f);
			if (_font != null)
			{
				string text = $"{abc.BlockAmount}";
				float textScale = GetBadgeTextScale(text, rect);
				var textSize = _font.MeasureString(text) * textScale;
				float tx = rect.X + (rect.Width - textSize.X) * 0.5f;
				float ty = rect.Y + (rect.Height - textSize.Y) * 0.5f;
				_spriteBatch.DrawString(_font, text, new Vector2(tx, ty), fg, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
				if (abc.Phase == AssignedBlockCard.PhaseState.Idle && !string.IsNullOrWhiteSpace(abc.EquipmentType))
				{
					var tex = SafeLoadIcon(abc.EquipmentType);
					if (tex != null)
					{
						float iconH = System.Math.Max(8, EquipIconHeight) * abc.CurrentScale;
						float iconW = tex.Height > 0 ? iconH * (tex.Width / (float)tex.Height) : iconH;
						float gap = System.Math.Max(0, EquipIconGap);
						float iconX = rect.X + rect.Width * 0.5f - iconW * 0.5f + EquipIconOffsetX;
						float iconY = rect.Y - gap - iconH + EquipIconOffsetY;
						_spriteBatch.Draw(tex, new Rectangle((int)iconX, (int)iconY, (int)iconW, (int)iconH), Color.White);
					}
				}
			}
		}

		private void DrawBlockBadge(Rectangle cardRect, Entity card, AssignedBlockCard abc)
		{
			if (_font == null) return;
			var (bg, fg) = GetBadgeColors(card, abc);
			int badgeSize = System.Math.Max(8, BadgeSize);
			var badgeRect = new Rectangle(
				cardRect.Right - badgeSize + BadgeOffsetX,
				cardRect.Bottom - badgeSize + BadgeOffsetY,
				badgeSize,
				badgeSize);
			int radius = System.Math.Clamp(AssignedCornerRadius, 0, badgeSize / 2);
			var rounded = GetRoundedRectTexture(badgeRect.Width, badgeRect.Height, radius);
			_spriteBatch.Draw(rounded, badgeRect, bg);

			string text = $"{abc.BlockAmount}";
			float textScale = GetBadgeTextScale(text, badgeRect);
			var textSize = _font.MeasureString(text) * textScale;
			var textPos = new Vector2(
				badgeRect.X + (badgeRect.Width - textSize.X) * 0.5f,
				badgeRect.Y + (badgeRect.Height - textSize.Y) * 0.5f);
			_spriteBatch.DrawString(_font, text, textPos, fg, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
		}

		private float GetBadgeTextScale(string text, Rectangle bounds)
		{
			if (_font == null || bounds.Width <= 0 || bounds.Height <= 0) return 0f;
			var textSizeAtOne = _font.MeasureString(text);
			if (textSizeAtOne.X <= 0f || textSizeAtOne.Y <= 0f) return 0f;

			float fill = MathHelper.Clamp(BlockTextScale, 0.05f, 1.0f);
			float maxTextWidth = bounds.Width * fill;
			float maxTextHeight = bounds.Height * fill;
			return System.Math.Min(maxTextWidth / textSizeAtOne.X, maxTextHeight / textSizeAtOne.Y);
		}

		private (Color Background, Color Foreground) GetBadgeColors(Entity card, AssignedBlockCard abc)
		{
			bool isColorless = card.HasComponent<Colorless>();
			Color bg = isColorless
				? new Color(ClampByte(ColorlessBackgroundR), ClampByte(ColorlessBackgroundG), ClampByte(ColorlessBackgroundB))
				: abc.DisplayBgColor;
			Color fg = isColorless
				? new Color(ClampByte(ColorlessForegroundR), ClampByte(ColorlessForegroundG), ClampByte(ColorlessForegroundB))
				: abc.DisplayFgColor;
			bg = new Color(bg.R, bg.G, bg.B, (byte)System.Math.Clamp(AssignedBackgroundAlpha, 0, 255));
			return (bg, fg);
		}

		private Rectangle GetAssignedBlockRect(Entity entity, AssignedBlockCard abc, Vector2 position, float scale)
		{
			if (!abc.IsEquipment && entity.GetComponent<CardData>() != null)
			{
				return CardGeometryService.GetVisualRect(EntityManager, position, scale);
			}

			int width = System.Math.Max(1, (int)System.Math.Round(CardDrawWidth * scale));
			int height = System.Math.Max(1, (int)System.Math.Round(CardDrawHeight * scale));
			return new Rectangle(
				(int)System.Math.Round(position.X - width / 2f),
				(int)System.Math.Round(position.Y - height / 2f),
				width,
				height);
		}

		private float GetTargetScale(AssignedBlockCard abc)
		{
			if (abc.IsEquipment)
			{
				return MathHelper.Clamp(EquipmentTargetScale, 0.1f, 1.0f);
			}

			return MathHelper.Clamp(CardScalePercent, 10f, 100f) * 0.01f;
		}

		private float GetVisualBottomOffset(AssignedBlockCard abc, float scale)
		{
			if (abc.IsEquipment)
			{
				return CardDrawHeight * scale * 0.5f;
			}

			var rect = CardGeometryService.GetVisualRect(EntityManager, Vector2.Zero, scale);
			return rect.Bottom;
		}

		private Texture2D GetRoundedRectTexture(int width, int height, int radius)
		{
			var key = (width, height, radius);
			if (_roundedRectCache.TryGetValue(key, out var tex)) return tex;
			var texture = Rendering.RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
			_roundedRectCache[key] = texture;
			return texture;
		}

		private Texture2D SafeLoadIcon(string type)
		{
			string key = (type ?? string.Empty).Trim().ToLowerInvariant();
			string assetName = key; // expects head.png, chest.png, arms.png, legs.png
			return _imageAssets.TryGetTexture(assetName);
		}

		private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);
	}
}
