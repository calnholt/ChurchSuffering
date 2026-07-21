using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("WayStation Penance Motion")]
	public sealed class WayStationPenanceMotionSystem : Core.System
	{
		[DebugEditable(DisplayName = "Entrance Seconds", Step = 0.01f, Min = 0.1f, Max = 4f)] public float EntranceSeconds { get; set; } = 1.45f;
		[DebugEditable(DisplayName = "Exit Seconds", Step = 0.01f, Min = 0.1f, Max = 2f)] public float ExitSeconds { get; set; } = 0.47f;
		[DebugEditable(DisplayName = "Weapon Rise", Step = 0.01f, Min = 0f, Max = 120f)] public float WeaponRiseOffset { get; set; } = 46f;
		[DebugEditable(DisplayName = "Exit Drop", Step = 0.01f, Min = 0f, Max = 120f)] public float ExitDropOffset { get; set; } = 36f;
		[DebugEditable(DisplayName = "Weapon Flash Glow", Step = 0.01f, Min = 0f, Max = 2f)] public float WeaponFlashGlow { get; set; } = 1f;
		[DebugEditable(DisplayName = "Ignite Peak", Step = 0.01f, Min = 1f, Max = 2f)] public float IgnitePeakScale { get; set; } = 1.42f;
		[DebugEditable(DisplayName = "Count Peak", Step = 0.01f, Min = 1f, Max = 2f)] public float CountPeakScale { get; set; } = 1.55f;

		public WayStationPenanceMotionSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<WayStationPenanceSelectionChangedEvent>(OnSelectionChanged);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<WayStationPenanceMotion>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			var state = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)
				?.GetComponent<WayStationPenanceModalState>();
			if (state == null) return;

			float delta = (float)Math.Max(0, gameTime.ElapsedGameTime.TotalSeconds);
			if (state.RequestedVisible && state.Phase == WayStationPenanceModalPhase.Hidden)
			{
				state.Phase = WayStationPenanceModalPhase.Entering;
				state.ElapsedSeconds = 0f;
				state.InteractionEnabled = false;
			}
			else if (!state.RequestedVisible && state.Phase is WayStationPenanceModalPhase.Entering or WayStationPenanceModalPhase.Visible)
			{
				state.Phase = WayStationPenanceModalPhase.Exiting;
				state.ElapsedSeconds = 0f;
				state.InteractionEnabled = false;
			}

			state.ElapsedSeconds += delta;
			switch (state.Phase)
			{
				case WayStationPenanceModalPhase.Entering:
					ApplyEntrance(state.ElapsedSeconds);
					if (state.ElapsedSeconds >= EntranceSeconds)
					{
						state.Phase = WayStationPenanceModalPhase.Visible;
						state.ElapsedSeconds = 0f;
						state.InteractionEnabled = true;
						SetAllSettled();
					}
					break;
				case WayStationPenanceModalPhase.Visible:
					state.InteractionEnabled = true;
					UpdateTransitions(delta);
					break;
				case WayStationPenanceModalPhase.Exiting:
					ApplyExit(state.ElapsedSeconds);
					if (state.ElapsedSeconds >= ExitSeconds)
					{
						state.Phase = WayStationPenanceModalPhase.Hidden;
						state.ElapsedSeconds = 0f;
						state.InteractionEnabled = false;
						SetAllHidden();
					}
					break;
				case WayStationPenanceModalPhase.Hidden:
					state.InteractionEnabled = false;
					SetAllHidden();
					break;
			}
		}

		private void ApplyEntrance(float elapsed)
		{
			foreach (var entity in GetRelevantEntities())
			{
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				(float delay, float duration) = EntranceTiming(motion.Role, motion.Index);
				float linear = Progress(elapsed, delay, duration);
				float rise = CubicBezier(linear, 0.16f, 1f, 0.3f, 1f);
				float slam = CubicBezier(linear, 0.2f, 1.35f, 0.4f, 1f);
				motion.Opacity = linear;
				motion.Offset = Vector2.Zero;
				motion.Scale = 1f;
				motion.WidthProgress = 1f;

				switch (motion.Role)
				{
					case WayStationPenanceMotionRole.Masthead:
						motion.Scale = MathHelper.Lerp(1.35f, 1f, slam);
						break;
					case WayStationPenanceMotionRole.Rule:
						motion.WidthProgress = rise;
						break;
					case WayStationPenanceMotionRole.Weapon:
					case WayStationPenanceMotionRole.Footer:
						motion.Offset = new Vector2(0f, MathHelper.Lerp(WeaponRiseOffset, 0f, rise));
						break;
					case WayStationPenanceMotionRole.TrackLabel:
						motion.Offset = new Vector2(0f, MathHelper.Lerp(18f, 0f, rise));
						break;
					case WayStationPenanceMotionRole.Track:
						motion.WidthProgress = MathHelper.Lerp(0.12f, 1f, rise);
						break;
					case WayStationPenanceMotionRole.Node:
						motion.Scale = MathHelper.Lerp(0.2f, 1f, rise);
						motion.Offset = new Vector2(0f, MathHelper.Lerp(10f, 0f, rise));
						break;
					case WayStationPenanceMotionRole.Fill:
						motion.WidthProgress = rise;
						break;
					case WayStationPenanceMotionRole.Tally:
						motion.Scale = MathHelper.Lerp(0.85f, 1f, slam);
						break;
				}
			}
		}

		private void ApplyExit(float elapsed)
		{
			foreach (var entity in GetRelevantEntities())
			{
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				(float delay, float duration) = ExitTiming(motion.Role, motion.Index);
				float p = Progress(elapsed, delay, duration);
				motion.Opacity = 1f - p;
				motion.Scale = 1f;
				motion.WidthProgress = 1f;
				motion.Offset = motion.Role switch
				{
					WayStationPenanceMotionRole.Masthead or WayStationPenanceMotionRole.Rule => new Vector2(0f, -30f * p),
					WayStationPenanceMotionRole.Weapon or WayStationPenanceMotionRole.Track or WayStationPenanceMotionRole.TrackLabel
						or WayStationPenanceMotionRole.Node or WayStationPenanceMotionRole.Fill or WayStationPenanceMotionRole.Tally
						or WayStationPenanceMotionRole.Footer => new Vector2(0f, ExitDropOffset * p),
					_ => Vector2.Zero,
				};
			}
		}

		private void UpdateTransitions(float delta)
		{
			foreach (var entity in GetRelevantEntities())
			{
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				if (motion.TransitionKind == WayStationPenanceTransitionKind.None) continue;
				motion.TransitionElapsed += delta;
				float p = Progress(motion.TransitionElapsed, motion.TransitionDelay, motion.TransitionDuration);
				if (p <= 0f) continue;

				switch (motion.TransitionKind)
				{
					case WayStationPenanceTransitionKind.WeaponFlash:
						motion.Glow = WeaponFlashGlow * (1f - p);
						motion.Scale = 1f;
						break;
					case WayStationPenanceTransitionKind.Ignite:
						motion.Scale = TwoPhaseScale(p, 1f, IgnitePeakScale, 1f, 0.4f);
						motion.Glow = 1f - p;
						break;
					case WayStationPenanceTransitionKind.Extinguish:
						motion.Scale = TwoPhaseScale(p, 1f, 0.8f, 1f, 0.45f);
						break;
					case WayStationPenanceTransitionKind.CountBump:
						motion.Scale = TwoPhaseScale(p, 1f, CountPeakScale, 1f, 0.4f);
						motion.Glow = 1f - p;
						break;
					case WayStationPenanceTransitionKind.EnterTally:
						motion.Opacity = p;
						motion.Scale = MathHelper.Lerp(0.85f, 1f, CubicBezier(p, 0.2f, 1.35f, 0.4f, 1f));
						motion.WidthProgress = CubicBezier(p, 0.16f, 1f, 0.3f, 1f);
						break;
					case WayStationPenanceTransitionKind.ExitTally:
						motion.Opacity = 1f - p;
						motion.Scale = MathHelper.Lerp(1f, 0.92f, p);
						motion.WidthProgress = 1f - CubicBezier(p, 0.16f, 1f, 0.3f, 1f);
						break;
					case WayStationPenanceTransitionKind.FooterSwap:
						motion.Opacity = p;
						motion.Offset = new Vector2(0f, MathHelper.Lerp(10f, 0f, p));
						break;
					case WayStationPenanceTransitionKind.NodeRipple:
						motion.Scale = TwoPhaseScale(p, 1f, 1.16f, 1f, 0.4f);
						motion.Glow = MathF.Sin(p * MathF.PI) * 0.7f;
						break;
					case WayStationPenanceTransitionKind.FillSweep:
						motion.AnimatedValue = MathHelper.Lerp(
							motion.StartValue,
							motion.TargetValue,
							CubicBezier(p, 0.16f, 1f, 0.3f, 1f));
						motion.WidthProgress = 1f;
						break;
				}

				if (p < 1f) continue;
				var tally = entity.GetComponent<WayStationPenanceTallyPresentation>();
				if (tally != null) tally.DisplayedCount = tally.CurrentCount;
				motion.TransitionKind = WayStationPenanceTransitionKind.None;
				motion.TransitionElapsed = 0f;
				motion.Opacity = tally?.IsActive == false ? 0f : 1f;
				motion.Offset = Vector2.Zero;
				motion.Scale = 1f;
				motion.WidthProgress = tally?.IsActive == false ? 0f : 1f;
				motion.Glow = 0f;
				motion.AnimatedValue = -1f;
			}
		}

		private void OnSelectionChanged(WayStationPenanceSelectionChangedEvent evt)
		{
			foreach (var entity in GetRelevantEntities())
			{
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				if (motion.Role == WayStationPenanceMotionRole.Weapon
					&& evt.WeaponChanged
					&& entity.GetComponent<WayStationPenanceWeaponPresentation>()?.IsSelected == true)
				{
					Start(motion, WayStationPenanceTransitionKind.WeaponFlash, 0f, 0.5f);
				}
				else if (motion.Role == WayStationPenanceMotionRole.Node)
				{
					int level = entity.GetComponent<WayStationPenanceNodePresentation>()?.Level ?? 0;
					if (evt.WeaponChanged)
						Start(motion, WayStationPenanceTransitionKind.NodeRipple, Math.Max(0, level - 1) * 0.01f, 0.25f);
					else if (evt.NewLevel > evt.OldLevel && level > evt.OldLevel && level <= evt.NewLevel)
						Start(motion, WayStationPenanceTransitionKind.Ignite, (level - evt.OldLevel - 1) * 0.045f, 0.5f);
					else if (evt.NewLevel < evt.OldLevel && level > evt.NewLevel && level <= evt.OldLevel)
						Start(motion, WayStationPenanceTransitionKind.Extinguish, (evt.OldLevel - level) * 0.028f, 0.32f);
				}
				else if (motion.Role == WayStationPenanceMotionRole.Tally)
				{
					var tally = entity.GetComponent<WayStationPenanceTallyPresentation>();
					if (tally == null) continue;
					if (tally.DisplayedCount == 0 && tally.CurrentCount > 0)
						Start(motion, WayStationPenanceTransitionKind.EnterTally, motion.Index * 0.07f, 0.45f);
					else if (tally.DisplayedCount > 0 && tally.CurrentCount == 0)
						Start(motion, WayStationPenanceTransitionKind.ExitTally, 0f, 0.42f);
					else if (tally.DisplayedCount != tally.CurrentCount)
						Start(motion, WayStationPenanceTransitionKind.CountBump, 0f, 0.4f);
				}
				else if (motion.Role == WayStationPenanceMotionRole.Footer)
				{
					Start(motion, WayStationPenanceTransitionKind.FooterSwap, 0f, 0.34f);
				}
				else if (motion.Role == WayStationPenanceMotionRole.Fill)
				{
					motion.StartValue = evt.OldLevel;
					motion.TargetValue = evt.NewLevel;
					motion.AnimatedValue = evt.OldLevel;
					Start(motion, WayStationPenanceTransitionKind.FillSweep, 0f, 0.5f);
				}
			}
		}

		private static void Start(WayStationPenanceMotion motion, WayStationPenanceTransitionKind kind, float delay, float duration)
		{
			motion.TransitionKind = kind;
			motion.TransitionElapsed = 0f;
			motion.TransitionDelay = delay;
			motion.TransitionDuration = duration;
		}

		private void SetAllSettled()
		{
			foreach (var entity in GetRelevantEntities())
			{
				var motion = entity.GetComponent<WayStationPenanceMotion>();
				var tally = entity.GetComponent<WayStationPenanceTallyPresentation>();
				if (tally != null) tally.DisplayedCount = tally.CurrentCount;
				motion.Opacity = tally?.IsActive == false ? 0f : 1f;
				motion.Offset = Vector2.Zero;
				motion.Scale = 1f;
				motion.WidthProgress = tally?.IsActive == false ? 0f : 1f;
				motion.Glow = 0f;
				motion.AnimatedValue = -1f;
				motion.TransitionKind = WayStationPenanceTransitionKind.None;
			}
		}

		private void SetAllHidden()
		{
			foreach (var entity in GetRelevantEntities()) entity.GetComponent<WayStationPenanceMotion>().Opacity = 0f;
		}

		private static (float Delay, float Duration) EntranceTiming(WayStationPenanceMotionRole role, int index) => role switch
		{
			WayStationPenanceMotionRole.Root => (0f, 0.5f),
			WayStationPenanceMotionRole.Masthead => (0.06f, 0.55f),
			WayStationPenanceMotionRole.Rule => (0.26f, 0.5f),
			WayStationPenanceMotionRole.Weapon => (0.16f + index * 0.09f, 0.6f),
			WayStationPenanceMotionRole.TrackLabel => (0.42f, 0.45f),
			WayStationPenanceMotionRole.Track => (0.48f, 0.55f),
			WayStationPenanceMotionRole.Node => (0.56f + index * 0.018f, 0.4f),
			WayStationPenanceMotionRole.Fill => (0.65f, 0.5f),
			WayStationPenanceMotionRole.Tally => (0.82f + index * 0.07f, 0.45f),
			WayStationPenanceMotionRole.Footer => (0.94f, 0.5f),
			WayStationPenanceMotionRole.Close => (0.95f, 0.3f),
			_ => (0f, 0.5f),
		};

		private static (float Delay, float Duration) ExitTiming(WayStationPenanceMotionRole role, int index) => role switch
		{
			WayStationPenanceMotionRole.Masthead or WayStationPenanceMotionRole.Rule => (0f, 0.3f),
			WayStationPenanceMotionRole.Close => (0f, 0.22f),
			WayStationPenanceMotionRole.Weapon => (index * 0.045f, 0.32f),
			WayStationPenanceMotionRole.TrackLabel or WayStationPenanceMotionRole.Track
				or WayStationPenanceMotionRole.Node or WayStationPenanceMotionRole.Fill
				or WayStationPenanceMotionRole.Tally => (0.08f, 0.3f),
			WayStationPenanceMotionRole.Footer => (0.04f, 0.26f),
			_ => (0f, 0.47f),
		};

		private static float Progress(float elapsed, float delay, float duration)
		{
			return MathHelper.Clamp((elapsed - delay) / Math.Max(0.0001f, duration), 0f, 1f);
		}

		private static float TwoPhaseScale(float p, float start, float peak, float end, float peakAt)
		{
			return p <= peakAt
				? MathHelper.Lerp(start, peak, p / Math.Max(0.0001f, peakAt))
				: MathHelper.Lerp(peak, end, (p - peakAt) / Math.Max(0.0001f, 1f - peakAt));
		}

		public static float CubicBezier(float x, float x1, float y1, float x2, float y2)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			float t = x;
			for (int i = 0; i < 8; i++)
			{
				float currentX = Bezier(t, x1, x2) - x;
				float derivative = BezierDerivative(t, x1, x2);
				if (Math.Abs(currentX) < 0.00001f || Math.Abs(derivative) < 0.00001f) break;
				t = MathHelper.Clamp(t - currentX / derivative, 0f, 1f);
			}
			return Bezier(t, y1, y2);
		}

		private static float Bezier(float t, float p1, float p2)
		{
			float inv = 1f - t;
			return 3f * inv * inv * t * p1 + 3f * inv * t * t * p2 + t * t * t;
		}

		private static float BezierDerivative(float t, float p1, float p2)
		{
			float inv = 1f - t;
			return 3f * inv * inv * p1 + 6f * inv * t * (p2 - p1) + 3f * t * t * (1f - p2);
		}
	}
}
