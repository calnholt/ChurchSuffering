using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Services
{
	public sealed class CardRestrictionMutationAnimator
	{
		public enum MutationAnimStage
		{
			Enter,
			Pulse,
			Hold,
			Exit,
		}

		public sealed class MutationAnimation
		{
			public Entity BaseCard;
			public Entity FinalCard;
			public bool HasSwapped;
			public bool PulseTriggered;
			public MutationAnimStage Stage = MutationAnimStage.Enter;
			public float EnterElapsed;
			public float PulseElapsed;
			public float HoldElapsed;
			public float ExitElapsed;
			public Action OnSwap;
			public string SfxRestrictionName = string.Empty;
			public bool PlayUpgradeSfx;
		}

		public MutationAnimation Active { get; private set; }

		public bool IsActive => Active != null;

		public void Start(MutationAnimation animation)
		{
			Active = animation;
		}

		public void Cancel()
		{
			Active = null;
		}

		/// <summary>
		/// Advances the active animation. Returns true when the animation completed this frame.
		/// </summary>
		public bool Update(float dt, EntityManager entityManager, Entity anchor)
		{
			if (Active == null) return false;

			switch (Active.Stage)
			{
				case MutationAnimStage.Enter:
					Active.EnterElapsed += dt;
					if (Active.EnterElapsed >= Math.Max(0.001f, CardRestrictionMutationSettings.EnterDurationSec))
					{
						Active.Stage = MutationAnimStage.Pulse;
						Active.PulseElapsed = 0f;
						TriggerPulse(entityManager, anchor);
					}
					break;

				case MutationAnimStage.Pulse:
					Active.PulseElapsed += dt;
					TrySwapAtPulsePeak();
					if (Active.PulseElapsed >= Math.Max(0.01f, CardRestrictionMutationSettings.PulseDurationSeconds))
					{
						EnsureSwapped();
						Active.Stage = MutationAnimStage.Hold;
						Active.HoldElapsed = 0f;
					}
					break;

				case MutationAnimStage.Hold:
					Active.HoldElapsed += dt;
					if (Active.HoldElapsed >= Math.Max(0f, CardRestrictionMutationSettings.HoldDurationSec))
					{
						Active.Stage = MutationAnimStage.Exit;
						Active.ExitElapsed = 0f;
					}
					break;

				case MutationAnimStage.Exit:
					Active.ExitElapsed += dt;
					if (Active.ExitElapsed >= Math.Max(0.001f, CardRestrictionMutationSettings.ExitDurationSec))
					{
						Active = null;
						return true;
					}
					break;
			}

			return false;
		}

		public void Draw(
			EntityManager entityManager,
			Entity anchor,
			Vector2 left,
			Vector2 center,
			Vector2 right)
		{
			if (Active == null) return;

			Vector2 pos;
			bool applyJiggle = Active.Stage is MutationAnimStage.Pulse or MutationAnimStage.Hold;

			switch (Active.Stage)
			{
				case MutationAnimStage.Enter:
				{
					float tm = Clamp01(Active.EnterElapsed / Math.Max(0.001f, CardRestrictionMutationSettings.EnterDurationSec));
					float t = EaseOut(tm, CardRestrictionMutationSettings.EaseOutPow);
					pos = ArcLerp(left, center, t, CardRestrictionMutationSettings.ArcHeightEnter);
					break;
				}
				case MutationAnimStage.Pulse:
				case MutationAnimStage.Hold:
					pos = center;
					break;
				case MutationAnimStage.Exit:
				{
					float tm = Clamp01(Active.ExitElapsed / Math.Max(0.001f, CardRestrictionMutationSettings.ExitDurationSec));
					float t = EaseIn(tm, CardRestrictionMutationSettings.EaseInPow);
					pos = ArcLerp(center, right, t, CardRestrictionMutationSettings.ArcHeightExit);
					applyJiggle = false;
					break;
				}
				default:
					return;
			}

			var card = Active.HasSwapped ? Active.FinalCard : Active.BaseCard;
			if (card == null) return;

			float renderScale = CardRestrictionMutationSettings.CardScale;
			float rotation = 0f;
			if (applyJiggle && anchor != null)
			{
				var anchorTransform = anchor.GetComponent<Transform>();
				if (anchorTransform != null)
				{
					renderScale *= anchorTransform.Scale.X;
					rotation = anchorTransform.Rotation;
				}
			}

			var cardTransform = card.GetComponent<Transform>();
			if (cardTransform != null) cardTransform.Rotation = rotation;

			EventManager.Publish(new CardRenderScaledRotatedEvent
			{
				Card = card,
				Position = pos,
				Scale = renderScale,
			});
		}

		private void TriggerPulse(EntityManager entityManager, Entity anchor)
		{
			if (Active == null || Active.PulseTriggered || anchor == null) return;
			Active.PulseTriggered = true;
			EventManager.Publish(new JigglePulseEvent
			{
				Target = anchor,
				Config = new JigglePulseConfig
				{
					PulseDurationSeconds = CardRestrictionMutationSettings.PulseDurationSeconds,
					PulseScaleAmplitude = CardRestrictionMutationSettings.PulseScaleAmplitude,
					JiggleDegrees = CardRestrictionMutationSettings.JiggleDegrees,
					PulseFrequencyHz = CardRestrictionMutationSettings.PulseFrequencyHz,
				},
			});
		}

		private void TrySwapAtPulsePeak()
		{
			if (Active == null || Active.HasSwapped || Active.PulseElapsed <= 0.01f) return;

			float dur = Math.Max(0.01f, CardRestrictionMutationSettings.PulseDurationSeconds);
			float norm = MathHelper.Clamp(Active.PulseElapsed / dur, 0f, 1f);
			float env = 1f - norm;
			env *= env;
			float phase = MathHelper.TwoPi * CardRestrictionMutationSettings.PulseFrequencyHz * Active.PulseElapsed;
			float s = (float)Math.Sin(phase);
			if (s > 0.95f && env > 0.25f)
			{
				PerformSwap();
			}
		}

		private void EnsureSwapped()
		{
			if (Active == null || Active.HasSwapped) return;
			PerformSwap();
		}

		private void PerformSwap()
		{
			Active.HasSwapped = true;
			Active.OnSwap?.Invoke();
			PlayModificationSfx();
		}

		private void PlayModificationSfx()
		{
			if (Active == null) return;

			if (Active.PlayUpgradeSfx)
			{
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.UpgradeCard, Volume = 0.5f });
				return;
			}

			var track = CardRestrictionMutationDisplayFactory.ToModificationSfx(Active.SfxRestrictionName);
			if (track == SfxTrack.None) return;
			EventManager.Publish(new PlaySfxEvent { Track = track, Volume = 0.5f });
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
			if (n.Y > 0f) n = -n;
			Vector2 p = a + ab * t;
			float wave = (float)Math.Sin(Math.PI * t);
			return p + n * arcHeight * wave;
		}
	}
}
