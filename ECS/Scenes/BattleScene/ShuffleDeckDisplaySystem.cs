using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Shuffle Deck Display")]
	public class ShuffleDeckDisplaySystem : Core.System
	{
		private const string CardBackAsset = "card_back";
		private const string DefaultTargetEntityName = "UI_DrawPileRoot";
		private const int CardCount = 20;
		private const int PerArm = 5;
		private const float MockupCardWidth = 52f;

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly List<ShuffleCard> _cards = new();
		private Texture2D _cardBackTexture;
		private Texture2D _shadowPixel;
		private Guid _activeRequestId = Guid.Empty;
		private string _targetEntityName = DefaultTargetEntityName;
		private float _elapsedSeconds;
		private bool _isActive;
		private bool _deckAssembleSfxPlayed;

		[DebugEditable(DisplayName = "Card Scale Percent", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float CardScalePercent { get; set; } = 0.65f;
		[DebugEditable(DisplayName = "Converge Duration", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float CornerConvergeDuration { get; set; } = 0.65f;
		[DebugEditable(DisplayName = "Cross Hold Duration", Step = 0.05f, Min = 0f, Max = 3f)]
		public float CrossHoldDuration { get; set; } = 0.4f;
		[DebugEditable(DisplayName = "Deck Assemble Duration", Step = 0.05f, Min = 0.05f, Max = 5f)]
		public float DeckAssembleDuration { get; set; } = 0.35f;
		[DebugEditable(DisplayName = "Corner Margin Scale", Step = 0.01f, Min = 0f, Max = 4f)]
		public float CornerMarginScale { get; set; } = 1f;
		[DebugEditable(DisplayName = "Cross Arm Scale", Step = 0.01f, Min = 0.1f, Max = 4f)]
		public float CrossArmScale { get; set; } = 1f;
		[DebugEditable(DisplayName = "Slot Gap Scale", Step = 0.01f, Min = 0.1f, Max = 4f)]
		public float SlotGapScale { get; set; } = 1f;
		[DebugEditable(DisplayName = "Bezier Bias", Step = 0.01f, Min = -2f, Max = 2f)]
		public float BezierBias { get; set; } = 0.4f;
		[DebugEditable(DisplayName = "Arm Slot Stagger", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float ArmSlotStagger { get; set; } = 0.08f;
		[DebugEditable(DisplayName = "Corner Offset Stagger", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float CornerOffsetStagger { get; set; } = 0.04f;
		[DebugEditable(DisplayName = "Deck Card Stagger", Step = 0.01f, Min = 0f, Max = 0.2f)]
		public float DeckCardStagger { get; set; } = 0.025f;
		[DebugEditable(DisplayName = "Target Scale At Pile", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float TargetScaleAtPile { get; set; } = 0.72f;
		[DebugEditable(DisplayName = "Converge Stack Offset Y", Step = 0.1f, Min = -20f, Max = 20f)]
		public float ConvergeStackOffsetY { get; set; } = -1.2f;
		[DebugEditable(DisplayName = "Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ShadowAlpha { get; set; } = 0.45f;
		[DebugEditable(DisplayName = "Shadow Offset X", Step = 0.5f, Min = -40f, Max = 40f)]
		public float ShadowOffsetX { get; set; } = 5f;
		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 0.5f, Min = -40f, Max = 40f)]
		public float ShadowOffsetY { get; set; } = 7f;

		private enum CrossArm
		{
			Top,
			Bottom,
			Left,
			Right
		}

		private enum StartCorner
		{
			TopLeft,
			TopRight,
			BottomLeft,
			BottomRight
		}

		private sealed class ShuffleCard
		{
			public int Index;
			public int Slot;
			public CrossArm Arm;
			public StartCorner Corner;
			public Vector2 Start;
			public Vector2 Control;
			public Vector2 Target;
			public float StartRotation;
			public float TargetRotation;
			public float Stagger;
			public Vector2 CurrentPosition;
			public float CurrentRotation;
			public float CurrentScale;
			public float CurrentAlpha;
		}

		public ShuffleDeckDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			EventManager.Subscribe<ShuffleDeckAnimationRequested>(OnRequested);
			EventManager.Subscribe<DeleteCachesEvent>(_ => CancelActiveAnimation());
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!_isActive) return;

			float previousElapsed = _elapsedSeconds;
			_elapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
			float phase1End = Math.Max(0.001f, CornerConvergeDuration) + Math.Max(0f, CrossHoldDuration);
			if (!_deckAssembleSfxPlayed && previousElapsed < phase1End && _elapsedSeconds >= phase1End)
			{
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.DeckShuffle, Volume = 0.5f });
				_deckAssembleSfxPlayed = true;
			}

			float totalDuration = GetTotalDuration();
			UpdateCards();
			if (_elapsedSeconds >= totalDuration)
			{
				CompleteActiveAnimation();
			}
		}

		public void Draw()
		{
			if (!_isActive || _cardBackTexture == null) return;

			foreach (var card in _cards.OrderBy(c => c.CurrentPosition.Y + c.CurrentRotation * 0.1f))
			{
				DrawCardShadow(card);
				DrawCardBack(card);
			}
		}

		private void OnRequested(ShuffleDeckAnimationRequested evt)
		{
			if (evt == null || evt.RequestId == Guid.Empty) return;
			if (GuidedTutorialService.IsActive(EntityManager))
			{
				EventManager.Publish(new ShuffleDeckAnimationCompleted { RequestId = evt.RequestId });
				return;
			}

			if (_isActive)
			{
				CompleteActiveAnimation();
			}

			EnsureTextures();
			if (_cardBackTexture == null)
			{
				EventManager.Publish(new ShuffleDeckAnimationCompleted { RequestId = evt.RequestId });
				return;
			}

			_activeRequestId = evt.RequestId;
			_targetEntityName = string.IsNullOrWhiteSpace(evt.TargetEntityName)
				? DefaultTargetEntityName
				: evt.TargetEntityName;
			_elapsedSeconds = 0f;
			_isActive = true;
			_deckAssembleSfxPlayed = false;
			SetBattleAnimationActive(true);
			BuildCards();
			UpdateCards();
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.DeckShuffle, Volume = 0.5f });
		}

		private void BuildCards()
		{
			_cards.Clear();
			int index = 0;
			for (int slot = 0; slot < PerArm; slot++)
			{
				AddCard(index++, CrossArm.Top, slot);
				AddCard(index++, CrossArm.Bottom, slot);
				AddCard(index++, CrossArm.Left, slot);
				AddCard(index++, CrossArm.Right, slot);
			}
		}

		private void AddCard(int index, CrossArm arm, int slot)
		{
			var corner = ResolveCorner(arm, slot);
			var start = GetCornerStart(corner);
			var target = GetCrossTarget(arm, slot);
			var control = (start.Position + target.Position) * 0.5f
				+ (target.Position - GetScreenCenter()) * BezierBias;
			_cards.Add(new ShuffleCard
			{
				Index = index,
				Slot = slot,
				Arm = arm,
				Corner = corner,
				Start = start.Position,
				Control = control,
				Target = target.Position,
				StartRotation = start.Rotation,
				TargetRotation = target.Rotation,
				Stagger = slot * ArmSlotStagger + IsDelayedCorner(corner) * CornerOffsetStagger,
				CurrentPosition = start.Position,
				CurrentRotation = start.Rotation,
				CurrentScale = 1f,
				CurrentAlpha = 0f,
			});
		}

		private void UpdateCards()
		{
			float phase1 = Math.Max(0.001f, CornerConvergeDuration);
			float hold = Math.Max(0f, CrossHoldDuration);
			float phase2 = Math.Max(0.001f, DeckAssembleDuration);
			var deckTarget = ResolveDeckTarget();

			if (_elapsedSeconds < phase1)
			{
				float progress = MathHelper.Clamp(_elapsedSeconds / phase1, 0f, 1f);
				foreach (var card in _cards)
				{
					float localT = GetStaggeredProgress(progress, card.Stagger);
					float eased = EaseOutCubic(localT);
					card.CurrentPosition = Bezier2(card.Start, card.Control, card.Target, eased);
					card.CurrentRotation = MathHelper.Lerp(card.StartRotation, card.TargetRotation, eased);
					card.CurrentScale = 1f;
					card.CurrentAlpha = localT > 0f ? 1f : 0f;
				}
			}
			else if (_elapsedSeconds < phase1 + hold)
			{
				foreach (var card in _cards)
				{
					card.CurrentPosition = card.Target;
					card.CurrentRotation = card.TargetRotation;
					card.CurrentScale = 1f;
					card.CurrentAlpha = 1f;
				}
			}
			else
			{
				float progress = MathHelper.Clamp((_elapsedSeconds - phase1 - hold) / phase2, 0f, 1f);
				foreach (var card in _cards)
				{
					float localT = GetStaggeredProgress(progress, card.Index * DeckCardStagger);
					float eased = EaseInOutCubic(localT);
					var target = deckTarget + new Vector2(0f, card.Index * ConvergeStackOffsetY);
					card.CurrentPosition = Vector2.Lerp(card.Target, target, eased);
					card.CurrentRotation = MathHelper.Lerp(card.TargetRotation, 0f, eased);
					card.CurrentScale = MathHelper.Lerp(1f, TargetScaleAtPile, eased);
					card.CurrentAlpha = 1f;
				}
			}
		}

		private void DrawCardBack(ShuffleCard card)
		{
			var size = GetCardSize() * card.CurrentScale;
			var scale = new Vector2(
				size.X / Math.Max(1, _cardBackTexture.Width),
				size.Y / Math.Max(1, _cardBackTexture.Height));
			var origin = new Vector2(_cardBackTexture.Width / 2f, _cardBackTexture.Height / 2f);
			_spriteBatch.Draw(
				_cardBackTexture,
				card.CurrentPosition,
				null,
				Color.White * card.CurrentAlpha,
				MathHelper.ToRadians(card.CurrentRotation),
				origin,
				scale,
				SpriteEffects.None,
				0f);
		}

		private void DrawCardShadow(ShuffleCard card)
		{
			if (_shadowPixel == null || ShadowAlpha <= 0f || card.CurrentAlpha <= 0f) return;
			var size = GetCardSize() * card.CurrentScale;
			var origin = new Vector2(0.5f, 0.5f);
			var scale = new Vector2(size.X, size.Y);
			_spriteBatch.Draw(
				_shadowPixel,
				card.CurrentPosition + new Vector2(ShadowOffsetX, ShadowOffsetY),
				null,
				Color.Black * (ShadowAlpha * card.CurrentAlpha),
				MathHelper.ToRadians(card.CurrentRotation),
				origin,
				scale,
				SpriteEffects.None,
				0f);
		}

		private void CompleteActiveAnimation()
		{
			if (!_isActive) return;
			var completedId = _activeRequestId;
			_isActive = false;
			_activeRequestId = Guid.Empty;
			_cards.Clear();
			SetBattleAnimationActive(false);
			if (completedId != Guid.Empty)
			{
				EventManager.Publish(new ShuffleDeckAnimationCompleted { RequestId = completedId });
			}
		}

		private void CancelActiveAnimation()
		{
			_isActive = false;
			_activeRequestId = Guid.Empty;
			_elapsedSeconds = 0f;
			_deckAssembleSfxPlayed = false;
			_cards.Clear();
			SetBattleAnimationActive(false);
		}

		private void SetBattleAnimationActive(bool active)
		{
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>()
				.FirstOrDefault()
				?.GetComponent<PhaseState>();
			if (phase != null) phase.BattleAnimationActive = active;
		}

		private void EnsureTextures()
		{
			_cardBackTexture ??= _imageAssets.TryGetTexture(CardBackAsset);
			_shadowPixel ??= _imageAssets.GetPixel(Color.White);
		}

		private Vector2 GetCardSize()
		{
			return new Vector2(
				CardGeometrySettings.DefaultWidth * CardScalePercent,
				CardGeometrySettings.DefaultHeight * CardScalePercent);
		}

		private float GetSpatialScale() => GetCardSize().X / MockupCardWidth;

		private Vector2 GetScreenCenter() => new(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f);

		private float GetCornerMargin() => 60f * GetSpatialScale() * CornerMarginScale;

		private float GetCrossArmLength() => 55f * GetSpatialScale() * CrossArmScale;

		private float GetSlotGap() => 14f * GetSpatialScale() * SlotGapScale;

		private Vector2 ResolveDeckTarget()
		{
			var root = EntityManager.GetEntity(_targetEntityName);
			var tr = root?.GetComponent<Transform>();
			if (tr != null) return tr.Position;
			return new Vector2(Game1.VirtualWidth - 60f, Game1.VirtualHeight - 60f);
		}

		private (Vector2 Position, float Rotation) GetCornerStart(StartCorner corner)
		{
			float margin = GetCornerMargin();
			return corner switch
			{
				StartCorner.TopLeft => (new Vector2(-margin, -margin), 45f),
				StartCorner.TopRight => (new Vector2(Game1.VirtualWidth + margin, -margin), -45f),
				StartCorner.BottomLeft => (new Vector2(-margin, Game1.VirtualHeight + margin), -45f),
				StartCorner.BottomRight => (new Vector2(Game1.VirtualWidth + margin, Game1.VirtualHeight + margin), 45f),
				_ => (Vector2.Zero, 0f)
			};
		}

		private (Vector2 Position, float Rotation) GetCrossTarget(CrossArm arm, int slot)
		{
			var center = GetScreenCenter();
			float crossArm = GetCrossArmLength();
			float slotGap = GetSlotGap();
			float offset = (slot - (PerArm - 1) / 2f) * slotGap;
			return arm switch
			{
				CrossArm.Top => (new Vector2(center.X + offset * 0.3f, center.Y - crossArm + slot * slotGap * 0.55f), 0f),
				CrossArm.Bottom => (new Vector2(center.X + offset * 0.3f, center.Y + crossArm - slot * slotGap * 0.55f), 180f),
				CrossArm.Left => (new Vector2(center.X - crossArm + slot * slotGap * 0.55f, center.Y + offset * 0.3f), -90f),
				CrossArm.Right => (new Vector2(center.X + crossArm - slot * slotGap * 0.55f, center.Y + offset * 0.3f), 90f),
				_ => (center, 0f)
			};
		}

		private static StartCorner ResolveCorner(CrossArm arm, int slot)
		{
			bool even = slot % 2 == 0;
			return arm switch
			{
				CrossArm.Top => even ? StartCorner.TopLeft : StartCorner.TopRight,
				CrossArm.Bottom => even ? StartCorner.BottomLeft : StartCorner.BottomRight,
				CrossArm.Left => even ? StartCorner.TopLeft : StartCorner.BottomLeft,
				CrossArm.Right => even ? StartCorner.TopRight : StartCorner.BottomRight,
				_ => StartCorner.TopLeft
			};
		}

		private static float IsDelayedCorner(StartCorner corner)
			=> corner == StartCorner.TopLeft || corner == StartCorner.BottomRight ? 0f : 1f;

		private static float GetStaggeredProgress(float progress, float stagger)
		{
			stagger = MathHelper.Clamp(stagger, 0f, 0.98f);
			return MathHelper.Clamp((progress - stagger) / Math.Max(0.001f, 1f - stagger), 0f, 1f);
		}

		private float GetTotalDuration()
			=> Math.Max(0.001f, CornerConvergeDuration)
				+ Math.Max(0f, CrossHoldDuration)
				+ Math.Max(0.001f, DeckAssembleDuration);

		private static Vector2 Bezier2(Vector2 p0, Vector2 p1, Vector2 p2, float t)
		{
			float u = 1f - t;
			return u * u * p0 + 2f * u * t * p1 + t * t * p2;
		}

		private static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float u = 1f - t;
			return 1f - u * u * u;
		}

		private static float EaseInOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t < 0.5f
				? 4f * t * t * t
				: 1f - MathF.Pow(-2f * t + 2f, 3f) / 2f;
		}

		[DebugAction("Trigger Shuffle Animation")]
		public void Debug_TriggerShuffleAnimation()
		{
			OnRequested(new ShuffleDeckAnimationRequested
			{
				RequestId = Guid.NewGuid(),
				Reason = "DebugReplay",
				TargetEntityName = DefaultTargetEntityName,
			});
		}
	}
}
