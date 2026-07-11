using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Achievement Meter")]
	public class AchievementMeterDisplaySystem : Core.System
	{
		private const string ClaimButtonName = "AchievementClaimClimbPointsButton";
		private const float SegmentEpsilon = 0.01f;
		private readonly SpriteBatch _spriteBatch;
		private readonly Texture2D _pixel;
		private Entity _claimButtonEntity;
		private float _displayedProgress;
		private bool _isClaiming;
		private int _claimTargetTotal;
		private int _claimPersistedTotal;
		private float _animatedDisplayTotal;
		private bool _pausedForBooster;

		[DebugEditable(DisplayName = "Meter X", Step = 10, Min = 100, Max = 1000)]
		public int MeterX { get; set; } = 332;

		[DebugEditable(DisplayName = "Meter Width", Step = 20, Min = 300, Max = 1200)]
		public int MeterWidth { get; set; } = 850;

		[DebugEditable(DisplayName = "Meter Height", Step = 1, Min = 6, Max = 40)]
		public int MeterHeight { get; set; } = 16;

		[DebugEditable(DisplayName = "Claim Button Width", Step = 10, Min = 180, Max = 600)]
		public int ClaimButtonWidth { get; set; } = 370;

		[DebugEditable(DisplayName = "Claim Button Height", Step = 2, Min = 30, Max = 100)]
		public int ClaimButtonHeight { get; set; } = 54;

		[DebugEditable(DisplayName = "Label Scale", Step = 0.01f, Min = 0.05f, Max = 0.4f)]
		public float LabelScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Value Scale", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float ValueScale { get; set; } = 0.13f;

		[DebugEditable(DisplayName = "Animation Speed", Step = 0.5f, Min = 1f, Max = 20f)]
		public float AnimationSpeed { get; set; } = 5f;

		public AchievementMeterDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<LoadSceneEvent>(evt =>
			{
				if (evt.Scene != SceneId.Achievement) return;
				_isClaiming = false;
				_pausedForBooster = false;
				_displayedProgress = CalculateProgress();
				EnsureClaimButton();
			});
			EventManager.Subscribe<BoosterPackOpeningDismissedEvent>(_ =>
			{
				if (!_isClaiming || !_pausedForBooster) return;
				_pausedForBooster = false;
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => EntityManager.GetEntitiesWithComponent<SceneState>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (entity.GetComponent<SceneState>()?.Current != SceneId.Achievement) return;
			float amount = MathHelper.Clamp((float)gameTime.ElapsedGameTime.TotalSeconds * AnimationSpeed, 0f, 1f);
			if (_isClaiming)
			{
				UpdateClaimSequence(amount);
			}
			else
			{
				_displayedProgress = MathHelper.Lerp(_displayedProgress, CalculateProgress(), amount);
			}
			UpdateClaimButton();
		}

		private void UpdateClaimSequence(float amount)
		{
			if (_pausedForBooster) return;

			int nextThreshold = CollectionProgressionRules.GetNextLevelThresholdTotal(_claimPersistedTotal);
			bool willCrossLevel = nextThreshold > _claimPersistedTotal && nextThreshold <= _claimTargetTotal;
			float segmentTarget = willCrossLevel ? nextThreshold : _claimTargetTotal;

			_animatedDisplayTotal = MathHelper.Lerp(_animatedDisplayTotal, segmentTarget, amount);
			_displayedProgress = CalculateProgressFromTotal(_animatedDisplayTotal);

			if (_animatedDisplayTotal < segmentTarget - SegmentEpsilon) return;

			int awardedTotal = (int)Math.Round(segmentTarget);
			bool levelComplete = willCrossLevel && segmentTarget == nextThreshold;
			_claimPersistedTotal = awardedTotal;
			_animatedDisplayTotal = segmentTarget;

			EventManager.Publish(new ClimbPointsSegmentAwardedEvent
			{
				NewTotalPoints = awardedTotal,
				TriggeredLevelComplete = levelComplete,
			});

			if (levelComplete)
			{
				_pausedForBooster = true;
			}
			else if (_claimPersistedTotal >= _claimTargetTotal)
			{
				_isClaiming = false;
			}
		}

		private void StartClaim()
		{
			int pending = SaveCache.GetCollection().pendingClimbPoints;
			if (pending <= 0 || _isClaiming || IsBoosterOverlayOpen()) return;
			_isClaiming = true;
			_claimPersistedTotal = GetTotalPoints();
			_claimTargetTotal = _claimPersistedTotal + pending;
			_animatedDisplayTotal = _claimPersistedTotal;
			_pausedForBooster = false;
		}

		private bool IsBoosterOverlayOpen()
		{
			return EntityManager.GetEntity("BoosterPackOpeningOverlay")
				?.GetComponent<BoosterPackOpeningOverlayState>()
				?.IsOpen == true;
		}

		private static int GetTotalPoints() => SaveCache.GetCollection().totalPoints;
		private int GetDisplayTotalPoints() => _isClaiming ? (int)Math.Round(_animatedDisplayTotal) : GetTotalPoints();
		private (int Level, int PointsInLevel, int PointsRequired) GetLevelState() => CollectionProgressionRules.GetLevelState(GetTotalPoints());
		private (int Level, int PointsInLevel, int PointsRequired) GetDisplayLevelState() => CollectionProgressionRules.GetLevelState(GetDisplayTotalPoints());

		private float CalculateProgress()
		{
			var state = GetLevelState();
			return state.PointsRequired <= 0 ? 0f : state.PointsInLevel / (float)state.PointsRequired;
		}

		private static float CalculateProgressFromTotal(float totalPoints)
		{
			var state = CollectionProgressionRules.GetLevelState((int)Math.Round(totalPoints));
			return state.PointsRequired <= 0 ? 0f : state.PointsInLevel / (float)state.PointsRequired;
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene?.Current != SceneId.Achievement) return;

			var rail = AchievementSceneDrawHelpers.FooterRail;
			AchievementSceneDrawHelpers.DrawPanel(_spriteBatch, _pixel, rail);
			var state = GetDisplayLevelState();
			int labelY = rail.Y + 19;

			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, "COLLECTION LEVEL", new Vector2(rail.X + 28, labelY), LabelScale, AchievementSceneDrawHelpers.MutedWhite);
			AchievementSceneDrawHelpers.DrawTitleText(_spriteBatch, state.Level.ToString(), new Vector2(rail.X + 212, rail.Y + 17), 0.25f, Color.White);

			string points = $"{state.PointsInLevel} / {state.PointsRequired}";
			var pointSize = AchievementSceneDrawHelpers.MeasureBodyText(points, LabelScale);
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, points, new Vector2(MeterX + MeterWidth - pointSize.X, labelY), LabelScale, AchievementSceneDrawHelpers.WarmWhite);

			int meterY = rail.Y + 57;
			var track = new Rectangle(MeterX, meterY, MeterWidth, MeterHeight);
			_spriteBatch.Draw(_pixel, track, AchievementSceneDrawHelpers.Black3);
			AchievementSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, track, Color.White * 0.18f);
			int fill = (int)(track.Width * MathHelper.Clamp(_displayedProgress, 0f, 1f));
			if (fill > 0) _spriteBatch.Draw(_pixel, new Rectangle(track.X, track.Y, fill, track.Height), AchievementSceneDrawHelpers.Red);

			string total = $"TOTAL {GetDisplayTotalPoints()} POINTS";
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, total, new Vector2(MeterX, rail.Bottom - 27), LabelScale, AchievementSceneDrawHelpers.MutedWhite);
			DrawClaimButton();
		}

		private void EnsureClaimButton()
		{
			if (_claimButtonEntity != null && EntityManager.GetEntity(_claimButtonEntity.Name) != null) return;
			_claimButtonEntity = EntityManager.CreateEntity(ClaimButtonName);
			EntityManager.AddComponent(_claimButtonEntity, new Transform { ZOrder = 205 });
			EntityManager.AddComponent(_claimButtonEntity, new UIElement { TooltipType = TooltipType.None });
			EntityManager.AddComponent(_claimButtonEntity, new OwnedByScene { Scene = SceneId.Achievement });
		}

		private Rectangle GetClaimButtonRect()
		{
			var rail = AchievementSceneDrawHelpers.FooterRail;
			return new Rectangle(rail.Right - ClaimButtonWidth - 28, rail.Y + (rail.Height - ClaimButtonHeight) / 2, ClaimButtonWidth, ClaimButtonHeight);
		}

		private void UpdateClaimButton()
		{
			EnsureClaimButton();
			var ui = _claimButtonEntity?.GetComponent<UIElement>();
			if (ui == null) return;
			ui.Bounds = GetClaimButtonRect();
			ui.IsHidden = false;
			int pending = SaveCache.GetCollection().pendingClimbPoints;
			ui.IsInteractable = pending > 0 && !_isClaiming && !IsBoosterOverlayOpen();
			if (ui.IsClicked && ui.IsInteractable)
			{
				StartClaim();
				ui.IsClicked = false;
			}
		}

		private void DrawClaimButton()
		{
			var ui = _claimButtonEntity?.GetComponent<UIElement>();
			if (ui == null || ui.IsHidden) return;
			int pending = SaveCache.GetCollection().pendingClimbPoints;
			bool enabled = pending > 0 && !_isClaiming;
			Color fill = enabled
				? ui.IsHovered ? AchievementSceneDrawHelpers.RedDim : AchievementSceneDrawHelpers.Black3
				: AchievementSceneDrawHelpers.Black2;
			Color border = enabled
				? ui.IsHovered ? AchievementSceneDrawHelpers.RedBright : Color.White * 0.52f
				: Color.White * 0.10f;
			_spriteBatch.Draw(_pixel, ui.Bounds, fill);
			AchievementSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, ui.Bounds, border, 2);
			string label = enabled ? $"CLAIM +{pending} CLIMB POINTS" : "NO CLIMB POINTS TO CLAIM";
			var size = AchievementSceneDrawHelpers.MeasureBodyText(label, ValueScale);
			var position = new Vector2(ui.Bounds.Center.X - size.X / 2f, ui.Bounds.Center.Y - size.Y / 2f);
			AchievementSceneDrawHelpers.DrawBodyText(_spriteBatch, label, position, ValueScale, enabled ? AchievementSceneDrawHelpers.WarmWhite : AchievementSceneDrawHelpers.MutedWhite * 0.45f);
		}
	}
}
