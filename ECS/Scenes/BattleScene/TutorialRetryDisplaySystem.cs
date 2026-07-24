using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Input;

namespace ChurchSuffering.ECS.Systems
{
	internal class TutorialRetryDisplaySystem : Core.System
	{
		private const string RetryButtonEntityName = "UIButton_TutorialRetry";

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private Texture2D _cachedButtonTexture;
		private string _cachedButtonText;
		private bool _retryRequested;

		[DebugEditable(DisplayName = "Button Width", Step = 1, Min = 40, Max = 400)]
		public int ButtonWidth { get; set; } = 140;

		[DebugEditable(DisplayName = "Button Height", Step = 1, Min = 20, Max = 120)]
		public int ButtonHeight { get; set; } = 48;

		[DebugEditable(DisplayName = "Button X", Step = 5, Min = 0, Max = 2000)]
		public int ButtonX { get; set; } = 16;

		[DebugEditable(DisplayName = "Button Y", Step = 5, Min = 0, Max = 2000)]
		public int ButtonY { get; set; } = 16;

		[DebugEditable(DisplayName = "Button Z", Step = 100, Min = 0, Max = 20000)]
		public int ButtonZ { get; set; } = 5000;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.1f, Max = 2.5f)]
		public float ButtonTextScale { get; set; } = 0.15f;

		[DebugEditable(DisplayName = "Hold Duration (sec)", Step = 0.05f, Min = 0.1f, Max = 5f)]
		public float HoldDuration { get; set; } = 0.75f;

		public TutorialRetryDisplaySystem(EntityManager entityManager, GraphicsDevice gd, SpriteBatch sb) : base(entityManager)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<HotKeyHoldCompletedEvent>(OnHotKeyHoldCompleted);
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			_retryRequested = false;
			DestroyButton();
		}

		private void OnHotKeyHoldCompleted(HotKeyHoldCompletedEvent evt)
		{
			if (evt.Entity?.Name != RetryButtonEntityName) return;
			TryRequestRetry();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (!GuidedTutorialService.IsActive(EntityManager)) return;
			if (_retryRequested) return;
			if (!Game1.WindowIsActive || StateSingleton.IsActive) return;

			EnsureButton();

			var retryBtn = EntityManager.GetEntity(RetryButtonEntityName);
			if (retryBtn?.GetComponent<UIElement>()?.IsClicked == true)
			{
				retryBtn.GetComponent<UIElement>().IsClicked = false;
				TryRequestRetry();
			}
		}

		public void Draw()
		{
			if (!GuidedTutorialService.IsActive(EntityManager) || _retryRequested || StateSingleton.IsActive)
			{
				DestroyButton();
				return;
			}

			EnsureButton();

			string label = "Retry";
			if (_cachedButtonTexture == null || _cachedButtonText != label)
			{
				_cachedButtonTexture?.Dispose();
				_cachedButtonTexture = ButtonTextureFactory.Create(
					_graphicsDevice, label, Color.White, Color.DarkRed);
				_cachedButtonText = label;
			}

			var drawRect = GetButtonRect();
			_spriteBatch.Draw(_cachedButtonTexture,
				new Rectangle(drawRect.X, drawRect.Y, drawRect.Width, drawRect.Height),
				Color.White);
		}

		private void EnsureButton()
		{
			var btnRect = GetButtonRect();
			var retryBtn = EntityManager.GetEntity(RetryButtonEntityName);
			if (retryBtn == null)
			{
				retryBtn = EntityManager.CreateEntity(RetryButtonEntityName);
				EntityManager.AddComponent(retryBtn, new Transform { Position = new Vector2(btnRect.X, btnRect.Y), ZOrder = ButtonZ });
				EntityManager.AddComponent(retryBtn, new UIElement { Bounds = btnRect, IsInteractable = true, IsHidden = false });
				EntityManager.AddComponent(retryBtn, new HotKey
				{
					Button = FaceButton.View,
					KeyboardButton = PlayerButton.Enter,
					RequiresHold = true,
					HoldDurationSeconds = HoldDuration,
					Position = HotKeyPosition.Below,
				});
				EntityManager.AddComponent(retryBtn, new TutorialInteractionPermitted());
			}
			else
			{
				var ui = retryBtn.GetComponent<UIElement>();
				if (ui != null) ui.Bounds = btnRect;

				var t = retryBtn.GetComponent<Transform>();
				if (t != null) t.Position = new Vector2(btnRect.X, btnRect.Y);

				var hotKey = retryBtn.GetComponent<HotKey>();
				if (hotKey != null)
				{
					hotKey.Button = FaceButton.View;
					hotKey.KeyboardButton = PlayerButton.Enter;
					hotKey.HoldDurationSeconds = HoldDuration;
				}
			}

			bool tutorialOverlayActive = EntityManager.GetEntitiesWithComponent<InputContext>()
				.Any(e =>
				{
					var c = e.GetComponent<InputContext>();
					return c != null && c.Id == TutorialDisplaySystem.InputContextId && c.IsActive;
				});
			string targetContext = tutorialOverlayActive
				? TutorialDisplaySystem.InputContextId
				: InputContextIds.Gameplay;
			var member = retryBtn.GetComponent<InputContextMember>();
			if (member == null || member.ContextId != targetContext)
			{
				InputContextService.EnsureMember(EntityManager, retryBtn, targetContext);
			}
		}

		private Rectangle GetButtonRect()
		{
			return new Rectangle(ButtonX, ButtonY, ButtonWidth, ButtonHeight);
		}

		private void TryRequestRetry()
		{
			if (_retryRequested || StateSingleton.IsActive || !GuidedTutorialService.IsActive(EntityManager)) return;
			_retryRequested = true;
			var btn = EntityManager.GetEntity(RetryButtonEntityName);
			var ui = btn?.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.IsClicked = false;
				ui.IsInteractable = false;
				ui.IsHidden = true;
				ui.Bounds = Rectangle.Empty;
			}
			var hotKey = btn?.GetComponent<HotKey>();
			if (hotKey != null)
			{
				hotKey.IsActive = false;
			}
			EventManager.Publish(new GuidedTutorialRestartRequested());
		}

		private void DestroyButton()
		{
			var btn = EntityManager.GetEntity(RetryButtonEntityName);
			if (btn != null)
				EntityManager.DestroyEntity(btn.Id);
			_cachedButtonTexture?.Dispose();
			_cachedButtonTexture = null;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities() => System.Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	}
}
