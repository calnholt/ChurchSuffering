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
	[DebugTab("Climb Resource Anim")]
	public sealed class ClimbResourceAcquisitionDisplaySystem : Core.System
	{
		private sealed class FallingGem
		{
			public ClimbResourceType Type;
			public float StartSeconds;
			public float ImpactSeconds;
			public float StartX;
			public float TargetX;
			public float StartRotation;
			public float EndRotation;
			public bool AudioPublished;
		}

		private sealed class ActiveAnimation
		{
			public ClimbResourceSave Resources = new();
			public List<FallingGem> Gems = new();
			public float ElapsedSeconds;
			public float PulseStartSeconds;
			public float EndSeconds;
			public float NextAudioSeconds;
			public bool HeaderPulsePublished;
		}

		private const string InputEntityName = "Climb_ResourceAcquisitionInput";
		private const string InputContextId = "climb-resource-acquisition";
		private const float EntranceSeconds = 0.15f;
		private const float FallSeconds = 0.50f;
		private const float SettleSeconds = 0.15f;
		private const float ExitSeconds = 0.20f;
		private const float MaxCatchSpreadSeconds = 0.35f;

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly Queue<ClimbResourceSave> _queue = new();
		private readonly Action<ClimbResourceAcquisitionAnimationRequested> _requestHandler;
		private readonly Action<DeleteCachesEvent> _deleteCachesHandler;
		private Texture2D _redGem;
		private Texture2D _whiteGem;
		private Texture2D _blackGem;
		private Texture2D _pouch;
		private ActiveAnimation _active;
		private Entity _inputEntity;
		private bool _snapshotTimeLocked;

		[DebugEditable(DisplayName = "Dim Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
		public float DimOpacity { get; set; } = 0.55f;
		[DebugEditable(DisplayName = "Pouch Height", Step = 4, Min = 180, Max = 720)]
		public int PouchHeight { get; set; } = 500;
		[DebugEditable(DisplayName = "Pouch Bottom Padding", Step = 4, Min = -100, Max = 300)]
		public int PouchBottomPadding { get; set; } = 30;
		[DebugEditable(DisplayName = "Pouch Entrance Offset", Step = 4, Min = 0, Max = 400)]
		public int PouchEntranceOffset { get; set; } = 180;
		[DebugEditable(DisplayName = "Pouch Mouth Y", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float PouchMouthY { get; set; } = 0.26f;
		[DebugEditable(DisplayName = "Gem Size", Step = 4, Min = 48, Max = 280)]
		public int GemSize { get; set; } = 148;
		[DebugEditable(DisplayName = "Gem Start Spacing", Step = 4, Min = 20, Max = 260)]
		public int GemStartSpacing { get; set; } = 145;
		[DebugEditable(DisplayName = "Gem Catch Spacing", Step = 2, Min = 0, Max = 100)]
		public int GemCatchSpacing { get; set; } = 34;
		[DebugEditable(DisplayName = "Gem Rotation", Step = 0.01f, Min = 0f, Max = 2f)]
		public float GemRotationRadians { get; set; } = 0.55f;
		[DebugEditable(DisplayName = "Impact Squash", Step = 0.01f, Min = 0f, Max = 0.2f)]
		public float ImpactSquash { get; set; } = 0.04f;
		[DebugEditable(DisplayName = "Gem Drop Volume", Step = 0.01f, Min = 0f, Max = 1f)]
		public float GemDropVolume { get; set; } = 0.32f;
		[DebugEditable(DisplayName = "Audio Cooldown", Step = 0.01f, Min = 0f, Max = 1f)]
		public float AudioCooldownSeconds { get; set; } = 0.12f;

		internal bool IsAnimating => _active != null;
		internal float ActiveDurationSeconds => _active?.EndSeconds ?? 0f;

		public ClimbResourceAcquisitionDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
			_requestHandler = OnAnimationRequested;
			_deleteCachesHandler = _ => ClearAll();
			EventManager.Subscribe(_requestHandler);
			EventManager.Subscribe(_deleteCachesHandler);
			LoadTextures();
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!IsClimbScene())
			{
				DeactivateInputContext();
				return;
			}

			if (_active == null)
			{
				TryStartNext();
				return;
			}

			if (!_snapshotTimeLocked)
			{
				float elapsed = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
				float previous = _active.ElapsedSeconds;
				_active.ElapsedSeconds += elapsed;
				PublishImpactAudio(previous, _active.ElapsedSeconds);
			}

			PublishHeaderPulseIfReady();
			if (!_snapshotTimeLocked && _active.ElapsedSeconds >= _active.EndSeconds)
			{
				_active = null;
				DeactivateInputContext();
				TryStartNext();
			}
		}

		public void Draw()
		{
			if (!IsClimbScene() || _active == null) return;
			LoadTextures();

			float elapsed = _active.ElapsedSeconds;
			float entrance = EaseOutCubic(MathHelper.Clamp(elapsed / EntranceSeconds, 0f, 1f));
			float exit = 1f - MathHelper.Clamp((elapsed - _active.PulseStartSeconds) / ExitSeconds, 0f, 1f);
			float overlayAlpha = entrance * exit;
			_spriteBatch.Draw(
				_pixel,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				Color.Black * (DimOpacity * overlayAlpha));

			float pouchTop = ResolvePouchTop();
			float finalPouchCenterY = pouchTop + PouchHeight * 0.5f;
			float pouchCenterY = MathHelper.Lerp(
				finalPouchCenterY + PouchEntranceOffset,
				finalPouchCenterY,
				entrance);
			float mouthY = pouchTop + PouchHeight * PouchMouthY;

			DrawFallingGems(elapsed, mouthY, overlayAlpha);
			DrawPouch(elapsed, pouchCenterY, overlayAlpha);
		}

		private void OnAnimationRequested(ClimbResourceAcquisitionAnimationRequested evt)
		{
			if (!HasResources(evt?.Resources)) return;
			_queue.Enqueue(CloneResources(evt.Resources));
			TryStartNext();
		}

		private void TryStartNext()
		{
			if (_active != null || _queue.Count == 0 || !IsClimbScene() || HasBlockingInputContext()) return;
			Start(_queue.Dequeue());
		}

		private void Start(ClimbResourceSave resources)
		{
			var types = BuildGemSequence(resources);
			if (types.Count == 0) return;

			float stagger = CalculateStaggerSeconds(types.Count);
			var active = new ActiveAnimation { Resources = CloneResources(resources) };
			float centerX = Game1.VirtualWidth * 0.5f;
			for (int i = 0; i < types.Count; i++)
			{
				float lane = i - (types.Count - 1) * 0.5f;
				float direction = i % 2 == 0 ? 1f : -1f;
				float start = EntranceSeconds + i * stagger;
				active.Gems.Add(new FallingGem
				{
					Type = types[i],
					StartSeconds = start,
					ImpactSeconds = start + FallSeconds,
					StartX = centerX + lane * GemStartSpacing,
					TargetX = centerX + ((i % 3) - 1) * GemCatchSpacing,
					StartRotation = -direction * GemRotationRadians * 0.35f,
					EndRotation = direction * GemRotationRadians,
				});
			}

			float lastImpact = active.Gems[^1].ImpactSeconds;
			active.PulseStartSeconds = lastImpact + SettleSeconds;
			active.EndSeconds = active.PulseStartSeconds + ExitSeconds;
			_active = active;
			ActivateInputContext();
		}

		private void DrawFallingGems(float elapsed, float mouthY, float overlayAlpha)
		{
			foreach (var gem in _active.Gems)
			{
				float progress = MathHelper.Clamp((elapsed - gem.StartSeconds) / FallSeconds, 0f, 1f);
				if (progress <= 0f || progress >= 1f) continue;

				float fall = progress * progress * progress;
				float x = MathHelper.Lerp(gem.StartX, gem.TargetX, SmoothStep(progress));
				float y = MathHelper.Lerp(-GemSize * 0.65f, mouthY, fall);
				float sink = MathHelper.Clamp((progress - 0.82f) / 0.18f, 0f, 1f);
				float scale = 1f - sink * 0.38f;
				float alpha = (1f - sink) * overlayAlpha;
				float rotation = MathHelper.Lerp(gem.StartRotation, gem.EndRotation, progress);
				DrawGem(GetGemTexture(gem.Type), new Vector2(x, y), rotation, scale, alpha);
			}
		}

		private void DrawGem(Texture2D texture, Vector2 center, float rotation, float scale, float alpha)
		{
			if (texture == null || alpha <= 0f) return;
			float normalizedScale = GemSize / (float)Math.Max(texture.Width, texture.Height) * scale;
			_spriteBatch.Draw(
				texture,
				center,
				null,
				Color.White * alpha,
				rotation,
				new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
				normalizedScale,
				SpriteEffects.None,
				0f);
		}

		private void DrawPouch(float elapsed, float centerY, float alpha)
		{
			if (_pouch == null || alpha <= 0f) return;
			float squash = 0f;
			foreach (var gem in _active.Gems)
			{
				float sinceImpact = elapsed - gem.ImpactSeconds;
				if (sinceImpact < 0f || sinceImpact > 0.12f) continue;
				squash = Math.Max(squash, MathF.Sin(MathF.PI * sinceImpact / 0.12f) * ImpactSquash);
			}

			float scale = PouchHeight / (float)_pouch.Height;
			_spriteBatch.Draw(
				_pouch,
				new Vector2(Game1.VirtualWidth * 0.5f, centerY),
				null,
				Color.White * alpha,
				0f,
				new Vector2(_pouch.Width * 0.5f, _pouch.Height * 0.5f),
				new Vector2(scale * (1f + squash), scale * (1f - squash)),
				SpriteEffects.None,
				0f);
		}

		private void PublishImpactAudio(float previous, float current)
		{
			foreach (var gem in _active.Gems)
			{
				if (gem.AudioPublished || previous >= gem.ImpactSeconds || current < gem.ImpactSeconds) continue;
				gem.AudioPublished = true;
				if (gem.ImpactSeconds < _active.NextAudioSeconds || GemDropVolume <= 0f) continue;
				EventManager.Publish(new PlaySfxEvent
				{
					Track = SfxTrack.GemDrop,
					Volume = GemDropVolume,
					Pitch = GemPitch(gem.Type),
				});
				_active.NextAudioSeconds = gem.ImpactSeconds + AudioCooldownSeconds;
			}
		}

		private void PublishHeaderPulseIfReady()
		{
			if (_active.HeaderPulsePublished || _active.ElapsedSeconds < _active.PulseStartSeconds) return;
			_active.HeaderPulsePublished = true;
			EventManager.Publish(new ClimbResourceHeaderPulseRequested
			{
				Resources = CloneResources(_active.Resources),
			});
		}

		private void ActivateInputContext()
		{
			_inputEntity = EntityManager.GetEntity(InputEntityName);
			if (_inputEntity == null)
			{
				_inputEntity = EntityManager.CreateEntity(InputEntityName);
				EntityManager.AddComponent(_inputEntity, new OwnedByScene { Scene = SceneId.Climb });
				EntityManager.AddComponent(_inputEntity, new Transform { ZOrder = 10004 });
			}

			InputContextService.EnsureContext(EntityManager, _inputEntity, InputContextId, 750, true);
		}

		private void DeactivateInputContext()
		{
			var context = _inputEntity?.GetComponent<InputContext>()
				?? EntityManager.GetEntity(InputEntityName)?.GetComponent<InputContext>();
			if (context != null) context.IsActive = false;
		}

		private bool HasBlockingInputContext()
		{
			return EntityManager.GetEntitiesWithComponent<InputContext>()
				.Select(entity => entity.GetComponent<InputContext>())
				.Any(context => context?.IsActive == true
					&& !context.IsDiagnostic
					&& !string.Equals(context.Id, InputContextIds.Gameplay, StringComparison.Ordinal)
					&& !string.Equals(context.Id, InputContextId, StringComparison.Ordinal));
		}

		private float ResolvePouchTop()
		{
			return Game1.VirtualHeight - PouchBottomPadding - PouchHeight;
		}

		private Texture2D GetGemTexture(ClimbResourceType type)
		{
			return type switch
			{
				ClimbResourceType.Red => _redGem,
				ClimbResourceType.White => _whiteGem,
				_ => _blackGem,
			};
		}

		private void LoadTextures()
		{
			_redGem ??= _imageAssets.TryGetTexture("Climb_UI/red_gem");
			_whiteGem ??= _imageAssets.TryGetTexture("Climb_UI/white_gem");
			_blackGem ??= _imageAssets.TryGetTexture("Climb_UI/black_gem");
			_pouch ??= _imageAssets.TryGetTexture("Climb_UI/gem_pouch");
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		public void ClearAll()
		{
			_queue.Clear();
			_active = null;
			_snapshotTimeLocked = false;
			DeactivateInputContext();
		}

		public void Shutdown()
		{
			ClearAll();
			EventManager.Unsubscribe(_requestHandler);
			EventManager.Unsubscribe(_deleteCachesHandler);
		}

		internal void SetSnapshotState(ClimbResourceSave resources, float elapsedSeconds)
		{
			ClearAll();
			Start(CloneResources(resources));
			if (_active == null) return;
			_active.ElapsedSeconds = MathHelper.Clamp(elapsedSeconds, 0f, _active.EndSeconds);
			foreach (var gem in _active.Gems) gem.AudioPublished = true;
			_active.NextAudioSeconds = float.MaxValue;
			_snapshotTimeLocked = true;
			PublishHeaderPulseIfReady();
		}

		internal static IReadOnlyList<ClimbResourceType> BuildGemSequence(ClimbResourceSave resources)
		{
			int red = Math.Max(0, resources?.red ?? 0);
			int white = Math.Max(0, resources?.white ?? 0);
			int black = Math.Max(0, resources?.black ?? 0);
			var result = new List<ClimbResourceType>(red + white + black);
			while (red > 0 || white > 0 || black > 0)
			{
				if (red > 0)
				{
					result.Add(ClimbResourceType.Red);
					red--;
				}
				if (white > 0)
				{
					result.Add(ClimbResourceType.White);
					white--;
				}
				if (black > 0)
				{
					result.Add(ClimbResourceType.Black);
					black--;
				}
			}
			return result;
		}

		internal static float CalculateStaggerSeconds(int gemCount)
		{
			return gemCount <= 1
				? 0f
				: Math.Min(0.11f, MaxCatchSpreadSeconds / (gemCount - 1));
		}

		private static bool HasResources(ClimbResourceSave resources)
		{
			return (resources?.red ?? 0) > 0
				|| (resources?.white ?? 0) > 0
				|| (resources?.black ?? 0) > 0;
		}

		private static ClimbResourceSave CloneResources(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = Math.Max(0, resources?.red ?? 0),
				white = Math.Max(0, resources?.white ?? 0),
				black = Math.Max(0, resources?.black ?? 0),
			};
		}

		private static float GemPitch(ClimbResourceType type)
		{
			return type switch
			{
				ClimbResourceType.White => 0.08f,
				ClimbResourceType.Black => -0.08f,
				_ => 0f,
			};
		}

		private static float SmoothStep(float value)
		{
			value = MathHelper.Clamp(value, 0f, 1f);
			return value * value * (3f - 2f * value);
		}

		private static float EaseOutCubic(float value)
		{
			value = MathHelper.Clamp(value, 0f, 1f);
			float inverse = 1f - value;
			return 1f - inverse * inverse * inverse;
		}
	}
}
