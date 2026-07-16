using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems;

[DebugTab("Reward Modal")]
public sealed class RewardModalDisplaySystem : Core.System
{
	private const string ContextId = "overlay.quest-reward";
	private const string TitleText = "Quest Complete!";
	private const string SkipText = "SKIP DECK REWARD";
	private const int MaxOptions = 3;
	private const float EntranceDuration = 1.25f;
	private const float ClaimDuration = 1.45f;
	private const float SkipDuration = 0.76f;
	private const float CardScale = 244f / CardGeometrySettings.DefaultWidth;

	private static readonly Color Bone = new(238, 233, 223);
	private static readonly Color BoneDim = new(170, 165, 157);
	private static readonly Color Blood = new(196, 30, 58);
	private static readonly Color BloodDark = new(93, 7, 19);
	private static readonly Color KickerDim = new(141, 137, 130);
	private static readonly Color BadgeBorder = new(97, 95, 90);

	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;
	private readonly ContentManager _content;
	private readonly Texture2D _pixel;
	private readonly SpriteFont _titleFont;
	private readonly SpriteFont _bodyFont;
	private readonly SpriteFont _badgeFont;
	private readonly List<OptionView> _optionViews = new();
	private readonly List<Entity> _laneEntities = new();

	private Entity _skipButton;
	private QuestRewardLayout _layout;
	private bool _layoutValid;
	private bool _effectLoadAttempted;
	private float _effectTime;
	private QuestRewardOverlayEffect _atmosphere;
	private QuestRewardLayerCompositor _layerCompositor;

	[DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
	public int ZOrder { get; set; } = 52000;

	[DebugEditable(DisplayName = "Outer Side Padding", Step = 1, Min = 0, Max = 240)]
	public int OuterSidePadding { get; set; } = 86;

	[DebugEditable(DisplayName = "Masthead Height", Step = 1, Min = 80, Max = 240)]
	public int MastheadHeight { get; set; } = 145;

	[DebugEditable(DisplayName = "Footer Height", Step = 1, Min = 40, Max = 140)]
	public int FooterHeight { get; set; } = 74;

	[DebugEditable(DisplayName = "Lane Width", Step = 1, Min = 220, Max = 500)]
	public int LaneWidth { get; set; } = 330;

	[DebugEditable(DisplayName = "Lane Gap", Step = 1, Min = 0, Max = 100)]
	public int LaneGap { get; set; } = 26;

	[DebugEditable(DisplayName = "Title Pixel Height", Step = 1, Min = 20, Max = 100)]
	public int TitlePixelHeight { get; set; } = 48;

	[DebugEditable(DisplayName = "Title Tracking", Step = 0.1f, Min = 0f, Max = 12f)]
	public float TitleTracking { get; set; } = 4f;

	[DebugEditable(DisplayName = "Kicker Pixel Height", Step = 1, Min = 6, Max = 30)]
	public int KickerPixelHeight { get; set; } = 10;

	[DebugEditable(DisplayName = "Kicker Tracking", Step = 0.1f, Min = 0f, Max = 8f)]
	public float KickerTracking { get; set; } = 2.5f;

	[DebugEditable(DisplayName = "Grain Time Scale", Step = 0.01f, Min = 0f, Max = 4f)]
	public float GrainTimeScale { get; set; } = 1f;

	private sealed class OptionView
	{
		public Entity Lane { get; init; }
		public Entity OutgoingCard { get; init; }
		public Entity IncomingCard { get; init; }
		public bool IsUpgrade { get; init; }
	}

	private readonly struct QuestRewardLayout
	{
		public Rectangle[] Lanes { get; init; }
		public Vector2[] OutgoingAnchors { get; init; }
		public Vector2[] IncomingAnchors { get; init; }
		public Vector2[] SwapCenters { get; init; }
		public Rectangle SkipButton { get; init; }
	}

	internal readonly struct LayerVisual
	{
		public float Alpha { get; init; }
		public float Scale { get; init; }
		public float Blur { get; init; }
		public float Grayscale { get; init; }
		public float Brightness { get; init; }
		public Vector2 Offset { get; init; }
		public float Rotation { get; init; }
	}

	public RewardModalDisplaySystem(
		EntityManager entityManager,
		GraphicsDevice graphicsDevice,
		SpriteBatch spriteBatch,
		ImageAssetService imageAssets,
		ContentManager content) : base(entityManager)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		_content = content;
		_pixel = imageAssets.GetPixel(Color.White);
		_titleFont = FontSingleton.TitleFont;
		_bodyFont = FontSingleton.ChakraPetchFont;
		_badgeFont = content.Load<SpriteFont>("Fonts/Grenze");

		EventManager.Subscribe<ShowQuestRewardOverlay>(OnShowQuestRewardOverlay);
		EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
	}

	protected override IEnumerable<Entity> GetRelevantEntities() =>
		EntityManager.GetEntitiesWithComponent<SceneState>();

	protected override void UpdateEntity(Entity sceneEntity, GameTime gameTime)
	{
		QuestRewardOverlayState state = GetState();
		if (state == null) return;
		SceneState scene = sceneEntity.GetComponent<SceneState>();

		if (state.PendingAutoContinue && scene?.Current == state.DismissScene)
		{
			CompleteAutoContinue(state);
			return;
		}

		bool blocksInput = state.IsOpen && state.Phase != QuestRewardPresentationPhase.Hidden;
		Entity overlay = EntityManager.GetEntity("QuestRewardOverlay");
		InputContextService.EnsureContext(EntityManager, overlay, ContextId, 720, blocksInput);
		if (!blocksInput)
		{
			StateSingleton.PreventClicking = false;
			return;
		}

		if (ShaderRuntimeOptions.ShadersEnabled)
		{
			EnsureEffectsLoaded();
			_effectTime += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds) * Math.Max(0f, GrainTimeScale);
		}

		StateSingleton.PreventClicking = scene?.Current == SceneId.Climb;
		EnsureLayout(state.DeckRewardOffer?.options?.Count ?? 0);
		SyncControls(state);

		float elapsed = Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
		state.PhaseElapsedSeconds += elapsed;
		if (state.DeckColumnSelectionInProgress)
			state.DeckColumnSelectionElapsedSeconds += elapsed;

		switch (state.Phase)
		{
			case QuestRewardPresentationPhase.Entering when state.PhaseElapsedSeconds >= EntranceDuration:
				state.Phase = QuestRewardPresentationPhase.Visible;
				state.PhaseElapsedSeconds = 0f;
				SyncControls(state);
				break;
			case QuestRewardPresentationPhase.Visible:
				HandleVisibleInput(state);
				break;
			case QuestRewardPresentationPhase.Claiming when state.PhaseElapsedSeconds >= ClaimDuration:
			case QuestRewardPresentationPhase.Skipping when state.PhaseElapsedSeconds >= SkipDuration:
				CompleteAnimatedResolution(state, scene);
				break;
		}
	}

	private void OnShowQuestRewardOverlay(ShowQuestRewardOverlay evt)
	{
		LoggingService.Append("RewardModalDisplaySystem.OnShowQuestRewardOverlay", new JsonObject
		{
			["OptionCount"] = evt?.DeckRewardOffer?.options?.Count ?? 0,
			["IsEncounterReward"] = evt?.IsEncounterReward == true,
		});

		if (evt?.DeckRewardOffer?.options?.Any(option => option != null) == true)
		{
			OpenDeckOffer(evt.DeckRewardOffer, evt.IsEncounterReward, evt.ClimbResources, evt.DismissScene, previewOnly: false, skipEntrance: false);
			return;
		}

		BeginAutoContinue(evt);
	}

	private void OnLoadScene(LoadSceneEvent evt)
	{
		QuestRewardOverlayState state = GetState();
		if (state?.PendingAutoContinue == true && evt.Scene == state.DismissScene)
		{
			CompleteAutoContinue(state);
			return;
		}
		if (state?.DismissInProgress == true && evt.Scene == state.DismissScene)
		{
			CloseOverlay(state);
			return;
		}
		if (evt.Scene != SceneId.Climb || state?.IsOpen == true) return;

		ClimbEncounterRewardSave pendingEncounter = SaveCache.GetClimbState()?.pendingEncounterReward;
		if (pendingEncounter != null)
		{
			SceneId dismissScene = pendingEncounter.pendingFinalEncounter ? SceneId.Battle : SceneId.Climb;
			if (pendingEncounter.deckRewardOffer?.options?.Any(option => option != null) == true)
			{
				OpenDeckOffer(pendingEncounter.deckRewardOffer, true, pendingEncounter.resources, dismissScene, previewOnly: false, skipEntrance: false);
			}
			else
			{
				BeginAutoContinue(new ShowQuestRewardOverlay
				{
					IsEncounterReward = true,
					ClimbResources = pendingEncounter.resources,
					DismissScene = dismissScene,
				});
			}
			return;
		}

		DeckRewardOfferSave pendingOffer = SaveCache.GetPendingDeckRewardOffer();
		if (pendingOffer?.options?.Any(option => option != null) == true)
			OpenDeckOffer(pendingOffer, false, null, SceneId.Climb, previewOnly: false, skipEntrance: false);
	}

	private void OnDeleteCaches(DeleteCachesEvent _)
	{
		DestroyViews();
		_layoutValid = false;
	}

	private void BeginAutoContinue(ShowQuestRewardOverlay evt)
	{
		EnsureOverlayEntity();
		QuestRewardOverlayState state = GetState();
		ResetState(state);
		state.IsEncounterReward = evt?.IsEncounterReward == true;
		state.ClimbResources = CloneResources(evt?.ClimbResources);
		state.DismissScene = evt?.DismissScene ?? SceneId.Climb;
		state.PendingAutoContinue = true;

		SceneId current = GetCurrentScene();
		if (current == state.DismissScene)
			CompleteAutoContinue(state);
		else
			EventManager.Publish(new ShowTransition { Scene = state.DismissScene });
	}

	private void CompleteAutoContinue(QuestRewardOverlayState state)
	{
		if (state == null) return;
		PublishClimbResourceAcquisitionIfNeeded(state);
		if (state.IsEncounterReward)
			ClimbEncounterService.ResolvePendingEncounterReward(EntityManager);
		ResetState(state);
		StateSingleton.PreventClicking = false;
	}

	private void OpenDeckOffer(
		DeckRewardOfferSave offer,
		bool isEncounterReward,
		ClimbResourceSave resources,
		SceneId dismissScene,
		bool previewOnly,
		bool skipEntrance)
	{
		EnsureOverlayEntity();
		QuestRewardOverlayState state = GetState();
		if (state.IsOpen && !state.IsPreviewOnly && previewOnly) return;

		DestroyViews();
		ResetState(state);
		state.DeckRewardOffer = CloneOffer(offer);
		state.IsEncounterReward = isEncounterReward;
		state.ClimbResources = CloneResources(resources);
		state.DismissScene = dismissScene;
		state.IsPreviewOnly = previewOnly;
		state.IsOpen = state.HasDeckRewardOffer;
		state.Phase = skipEntrance ? QuestRewardPresentationPhase.Visible : QuestRewardPresentationPhase.Entering;
		state.PhaseElapsedSeconds = 0f;
		_effectTime = 0f;

		CreateViews(state.DeckRewardOffer);
		state.IsOpen = _optionViews.Count > 0;
		_layoutValid = false;
		EnsureLayout(_optionViews.Count);
		SyncControls(state);
	}

	public void OpenDeckOfferForSnapshot(DeckRewardOfferSave offer)
	{
		OpenDeckOffer(offer, false, null, SceneId.Climb, previewOnly: true, skipEntrance: true);
	}

	[DebugAction("Preview Random Rewards")]
	public void Debug_PreviewRandomRewards()
	{
		QuestRewardOverlayState state = GetState();
		if (state?.IsOpen == true && !state.IsPreviewOnly) return;
		OpenDeckOffer(BuildRandomDebugOffer(Random.Shared), false, null, GetCurrentScene(), previewOnly: true, skipEntrance: false);
	}

	private void HandleVisibleInput(QuestRewardOverlayState state)
	{
		for (int i = 0; i < _optionViews.Count; i++)
		{
			UIElement ui = _optionViews[i].Lane?.GetComponent<UIElement>();
			if (ui?.IsClicked != true) continue;
			ui.IsClicked = false;
			if (!TryCommitSelection(state, i)) return;
			BeginClaim(state, i);
			return;
		}

		UIElement skipUi = _skipButton?.GetComponent<UIElement>();
		if (skipUi?.IsClicked != true) return;
		skipUi.IsClicked = false;
		CommitSkip(state);
		BeginSkip(state);
	}

	private void BeginClaim(QuestRewardOverlayState state, int selectedIndex)
	{
		DisableInputs();
		state.DismissInProgress = true;
		state.DeckColumnSelectionInProgress = true;
		state.SelectedDeckRewardColumnIndex = selectedIndex;
		state.DeckColumnSelectionElapsedSeconds = 0f;
		state.Phase = QuestRewardPresentationPhase.Claiming;
		state.PhaseElapsedSeconds = 0f;

		DeckRewardOfferOptionSave option = state.DeckRewardOffer?.options?.ElementAtOrDefault(selectedIndex);
		if (string.Equals(option?.kind, DeckRewardOfferKinds.Upgrade, StringComparison.OrdinalIgnoreCase))
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.UpgradeCard, Volume = 0.5f });
		else
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.TakeReward, Volume = 0.5f });
	}

	private void BeginSkip(QuestRewardOverlayState state)
	{
		DisableInputs();
		state.DismissInProgress = true;
		state.Phase = QuestRewardPresentationPhase.Skipping;
		state.PhaseElapsedSeconds = 0f;
	}

	private void CompleteAnimatedResolution(QuestRewardOverlayState state, SceneState scene)
	{
		if (state == null) return;
		state.Phase = QuestRewardPresentationPhase.Hidden;
		if (state.IsPreviewOnly)
		{
			CloseOverlay(state);
			return;
		}

		if (scene?.Current != state.DismissScene)
		{
			EventManager.Publish(new ShowTransition { Scene = state.DismissScene });
			return;
		}

		CloseOverlay(state);
	}

	private void CloseOverlay(QuestRewardOverlayState state)
	{
		if (state == null) return;
		if (!state.IsPreviewOnly)
		{
			PublishClimbResourceAcquisitionIfNeeded(state);
			if (state.IsEncounterReward)
				ClimbEncounterService.ResolvePendingEncounterReward(EntityManager);
		}
		DestroyViews();
		ResetState(state);
		StateSingleton.PreventClicking = false;
	}

	private void SyncControls(QuestRewardOverlayState state)
	{
		bool interactive = state?.Phase == QuestRewardPresentationPhase.Visible && !state.DismissInProgress;
		for (int i = 0; i < _optionViews.Count; i++)
		{
			Entity lane = _optionViews[i].Lane;
			Transform transform = lane?.GetComponent<Transform>();
			if (transform != null) transform.ZOrder = ZOrder + 10 + i;
			UIElement ui = lane?.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.Bounds = i < _layout.Lanes.Length ? _layout.Lanes[i] : Rectangle.Empty;
				ui.IsInteractable = interactive;
				ui.LayerType = UILayerType.Overlay;
				if (!interactive) ui.IsClicked = false;
			}
		}

		UIElement skipUi = _skipButton?.GetComponent<UIElement>();
		if (skipUi != null)
		{
			skipUi.Bounds = _layout.SkipButton;
			skipUi.IsInteractable = interactive;
			skipUi.LayerType = UILayerType.Overlay;
			if (!interactive) skipUi.IsClicked = false;
		}
		HotKey hotKey = _skipButton?.GetComponent<HotKey>();
		if (hotKey != null) hotKey.IsActive = interactive;

		foreach (OptionView view in _optionViews)
		{
			PreparePreviewCard(view.OutgoingCard);
			PreparePreviewCard(view.IncomingCard);
		}
	}

	private void DisableInputs()
	{
		foreach (Entity lane in _laneEntities)
		{
			UIElement ui = lane?.GetComponent<UIElement>();
			if (ui == null) continue;
			ui.IsInteractable = false;
			ui.IsClicked = false;
		}
		UIElement skipUi = _skipButton?.GetComponent<UIElement>();
		if (skipUi != null)
		{
			skipUi.IsInteractable = false;
			skipUi.IsClicked = false;
		}
		HotKey hotKey = _skipButton?.GetComponent<HotKey>();
		if (hotKey != null) hotKey.IsActive = false;
	}

	private void EnsureLayout(int optionCount)
	{
		if (_layoutValid && _layout.Lanes?.Length == optionCount) return;
		int count = Math.Clamp(optionCount, 0, MaxOptions);
		int stageTop = 26 + MastheadHeight;
		int footerTop = Game1.VirtualHeight - 28 - FooterHeight;
		var stage = new Rectangle(OuterSidePadding, stageTop, Game1.VirtualWidth - OuterSidePadding * 2, Math.Max(1, footerTop - stageTop));
		var footer = new Rectangle(OuterSidePadding, footerTop, Game1.VirtualWidth - OuterSidePadding * 2, FooterHeight);

		var lanes = new Rectangle[count];
		var outgoing = new Vector2[count];
		var incoming = new Vector2[count];
		var swaps = new Vector2[count];
		int totalWidth = count * LaneWidth + Math.Max(0, count - 1) * LaneGap;
		int startX = (Game1.VirtualWidth - totalWidth) / 2;
		float cardHeight = CardGeometrySettings.DefaultHeight * CardScale;
		float stackHeight = cardHeight * 2f + 42f + 16f;
		float stackTop = stage.Y + (stage.Height - stackHeight) / 2f;
		for (int i = 0; i < count; i++)
		{
			lanes[i] = new Rectangle(startX + i * (LaneWidth + LaneGap), stage.Y, LaneWidth, stage.Height);
			float centerX = lanes[i].Center.X;
			float outgoingVisualCenter = stackTop + cardHeight / 2f;
			float swapCenter = stackTop + cardHeight + 8f + 21f;
			float incomingVisualCenter = stackTop + cardHeight + 8f + 42f + 8f + cardHeight / 2f;
			outgoing[i] = CardAnchorForVisualCenter(new Vector2(centerX, outgoingVisualCenter), CardScale);
			incoming[i] = CardAnchorForVisualCenter(new Vector2(centerX, incomingVisualCenter), CardScale);
			swaps[i] = new Vector2(centerX, swapCenter);
		}

		float skipScale = ScaleForPixelHeight(_bodyFont, SkipText, 11f);
		float skipWidth = MeasureTracked(_bodyFont, SkipText, skipScale, 1.6f).X + 52f;
		var skip = new Rectangle((int)(Game1.VirtualWidth / 2f - skipWidth / 2f), footer.Center.Y - 24, (int)Math.Ceiling(skipWidth), 48);
		_layout = new QuestRewardLayout
		{
			Lanes = lanes,
			OutgoingAnchors = outgoing,
			IncomingAnchors = incoming,
			SwapCenters = swaps,
			SkipButton = skip,
		};
		_layoutValid = true;
	}

	public void Draw()
	{
		QuestRewardOverlayState state = GetState();
		if (state?.IsOpen != true || state.Phase == QuestRewardPresentationPhase.Hidden || _titleFont == null) return;
		SceneId scene = GetCurrentScene();
		if (scene != SceneId.Battle && scene != SceneId.Climb && scene != SceneId.Snapshot) return;

		EnsureLayout(_optionViews.Count);
		DrawAtmosphere(state);
		DrawHorizontalRule(171, GetOverlayAlpha(state));
		DrawHorizontalRule(Game1.VirtualHeight - 101, GetOverlayAlpha(state));

		bool canFilter = ShaderRuntimeOptions.ShadersEnabled && _layerCompositor?.IsAvailable == true;
		switch (state.Phase)
		{
			case QuestRewardPresentationPhase.Entering:
				DrawEntrance(state, canFilter);
				break;
			case QuestRewardPresentationPhase.Claiming:
				DrawClaim(state, canFilter);
				break;
			case QuestRewardPresentationPhase.Skipping:
				DrawSkip(state, canFilter);
				break;
			default:
				DrawForeground(state, Vector2.Zero, 1f);
				break;
		}
	}

	private void DrawAtmosphere(QuestRewardOverlayState state)
	{
		float alpha = GetOverlayAlpha(state);
		if (ShaderRuntimeOptions.ShadersEnabled && _atmosphere?.IsAvailable == true)
		{
			RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
			Texture2D sceneSource = previousTargets.Length > 0 ? previousTargets[0].RenderTarget as Texture2D : null;
			if (sceneSource == null)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (0.62f * alpha));
				return;
			}
			SamplerState savedSampler = _graphicsDevice.SamplerStates[0];
			DepthStencilState savedDepth = _graphicsDevice.DepthStencilState;
			RasterizerState savedRasterizer = _graphicsDevice.RasterizerState;
			Rectangle savedScissor = _graphicsDevice.ScissorRectangle;
			_spriteBatch.End();
			using var tempLease = FullScreenRenderTargetPool.Acquire(_graphicsDevice, Game1.Display.RenderWidth, Game1.Display.RenderHeight);
			bool effectStarted = false;
			try
			{
				_graphicsDevice.SetRenderTarget(tempLease.Target);
				_graphicsDevice.Clear(Color.Black);
				_atmosphere.Time = _effectTime;
				_atmosphere.OverlayAlpha = alpha;
				_atmosphere.FlashProgress = state.Phase == QuestRewardPresentationPhase.Entering
					? state.PhaseElapsedSeconds / 0.82f
					: -1f;
				_atmosphere.Begin(_spriteBatch);
				effectStarted = true;
				_atmosphere.Draw(_spriteBatch, sceneSource);
				_atmosphere.End(_spriteBatch);
				effectStarted = false;

				_graphicsDevice.SetRenderTargets(previousTargets);
				_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone);
				_spriteBatch.Draw(tempLease.Target, _graphicsDevice.Viewport.Bounds, Color.White);
				_spriteBatch.End();
			}
			finally
			{
				if (effectStarted) _atmosphere.End(_spriteBatch);
				if (previousTargets.Length > 0) _graphicsDevice.SetRenderTargets(previousTargets);
				else _graphicsDevice.SetRenderTarget(null);
				_graphicsDevice.Textures[0] = null;
				_graphicsDevice.SamplerStates[0] = savedSampler;
				_graphicsDevice.ScissorRectangle = savedScissor;
				// Reward cards contain transparent art pixels. Always return to the
				// game's normal alpha UI batch; preserving a transient Opaque device
				// state makes those pixels overwrite the card surface with black.
				_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, savedSampler, savedDepth, savedRasterizer, null, Game1.Display.SpriteBatchTransform);
			}
			return;
		}

		_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * (0.62f * alpha));
		for (int i = 0; i < 10; i++)
		{
			float t = i / 9f;
			int insetX = (int)(300 * t);
			int insetY = (int)(100 * t);
			_spriteBatch.Draw(_pixel, new Rectangle(insetX, insetY, Game1.VirtualWidth - insetX * 2, Game1.VirtualHeight - insetY * 2), Color.Black * (0.025f * t * alpha));
		}
	}

	private void DrawEntrance(QuestRewardOverlayState state, bool canFilter)
	{
		float titleT = EaseBackOut(Progress(state.PhaseElapsedSeconds - 0.12f, 0.62f));
		var titleVisual = new LayerVisual
		{
			Alpha = Progress(state.PhaseElapsedSeconds - 0.12f, 0.62f),
			Scale = MathHelper.Lerp(1.65f, 1f, titleT),
			Blur = MathHelper.Lerp(10f, 0f, titleT),
			Brightness = 1f,
		};
		DrawFilteredOrDirect(() => DrawMasthead(titleVisual), titleVisual, canFilter);

		for (int i = 0; i < _optionViews.Count; i++)
		{
			int index = i;
			float delay = i == 1 ? 0.23f : 0.33f;
			float raw = Progress(state.PhaseElapsedSeconds - delay, 0.68f);
			float eased = EaseOutCubic(raw);
			var visual = new LayerVisual
			{
				Alpha = raw,
				Scale = MathHelper.Lerp(0.72f, 1f, eased),
				Blur = MathHelper.Lerp(9f, 0f, eased),
				Grayscale = 1f - eased,
				Brightness = 1f,
			};
			DrawFilteredOrDirect(() => DrawLane(index, state, visual, drawChrome: true, drawOutgoing: true, drawIncoming: true), visual, canFilter);
		}

		float footerAlpha = Progress(state.PhaseElapsedSeconds - 0.69f, 0.42f);
		DrawFooter(state, new Vector2(0f, 0f), footerAlpha);
	}

	private void DrawClaim(QuestRewardOverlayState state, bool canFilter)
	{
		float elapsed = state.PhaseElapsedSeconds;
		float chromeT = EaseInCubic(Progress(elapsed, 0.38f));
		DrawMasthead(new LayerVisual { Alpha = 1f - chromeT, Scale = 1f, Offset = new Vector2(0f, -18f * chromeT), Brightness = 1f });
		DrawFooter(state, new Vector2(0f, -18f * chromeT), 1f - chromeT);

		for (int i = 0; i < _optionViews.Count; i++)
		{
			int index = i;
			if (i != state.SelectedDeckRewardColumnIndex)
			{
				float t = EaseInCubic(Progress(elapsed, 0.43f));
				var visual = new LayerVisual { Alpha = 1f - t, Scale = MathHelper.Lerp(1f, 0.86f, t), Blur = 9f * t, Brightness = 1f };
				DrawFilteredOrDirect(() => DrawLane(index, state, visual, true, true, true), visual, canFilter);
				continue;
			}

			float selectedChromeT = EaseInCubic(Progress(elapsed, 0.26f));
			DrawLane(index, state, new LayerVisual { Alpha = 1f - selectedChromeT, Scale = 1f, Offset = new Vector2(0f, -18f * selectedChromeT), Brightness = 1f }, true, false, false);

			float outgoingT = EaseInCubic(Progress(elapsed, 0.52f));
			var outgoing = new LayerVisual
			{
				Alpha = 1f - outgoingT,
				Scale = MathHelper.Lerp(1f, 0.86f, outgoingT),
				Blur = 7f * outgoingT,
				Offset = new Vector2(0f, 110f * outgoingT),
				Rotation = MathHelper.ToRadians(-5f * outgoingT),
				Brightness = 1f,
			};
			DrawFilteredOrDirect(() => DrawLane(index, state, outgoing, false, true, false), outgoing, canFilter);

			LayerVisual incoming = ComputeIncomingClaimVisual(elapsed);
			DrawFilteredOrDirect(() => DrawLane(index, state, incoming, false, false, true), incoming, canFilter);
		}
	}

	private void DrawSkip(QuestRewardOverlayState state, bool canFilter)
	{
		float t = EaseInCubic(Progress(state.PhaseElapsedSeconds, SkipDuration));
		var visual = new LayerVisual
		{
			Alpha = 1f - t,
			Scale = 1f,
			Blur = 7f * t,
			Offset = new Vector2(0f, 24f * t),
			Brightness = 1f,
		};
		DrawFilteredOrDirect(() => DrawForeground(state, visual.Offset, visual.Alpha), visual, canFilter);
	}

	private void DrawFilteredOrDirect(Action draw, LayerVisual visual, bool canFilter)
	{
		if (visual.Alpha <= 0.001f) return;
		if (canFilter && (visual.Blur > 0.05f || visual.Grayscale > 0.01f || Math.Abs(visual.Brightness - 1f) > 0.01f))
		{
			_layerCompositor.DrawLayer(draw, visual.Blur, visual.Grayscale, visual.Brightness <= 0f ? 1f : visual.Brightness, 1f);
			return;
		}
		draw();
	}

	private void DrawForeground(QuestRewardOverlayState state, Vector2 offset, float alpha)
	{
		DrawMasthead(new LayerVisual { Alpha = alpha, Scale = 1f, Offset = offset, Brightness = 1f });
		for (int i = 0; i < _optionViews.Count; i++)
			DrawLane(i, state, new LayerVisual { Alpha = alpha, Scale = 1f, Offset = offset, Brightness = 1f }, true, true, true);
		DrawFooter(state, offset, alpha);
	}

	private void DrawMasthead(LayerVisual visual)
	{
		if (visual.Alpha <= 0.001f) return;
		float scale = ScaleForPixelHeight(_titleFont, TitleText, TitlePixelHeight) * Math.Max(0.01f, visual.Scale);
		Vector2 size = MeasureTracked(_titleFont, TitleText, scale, TitleTracking);
		Vector2 position = new(Game1.VirtualWidth / 2f - size.X / 2f, 62f - size.Y / 2f);
		position = ScaleAround(position, new Vector2(Game1.VirtualWidth / 2f, 88f), visual.Scale) + visual.Offset;
		DrawTracked(_titleFont, TitleText, position + new Vector2(0f, 4f), scale, Color.Black * (0.85f * visual.Alpha), TitleTracking);
		DrawTracked(_titleFont, TitleText, position, scale, Color.White * visual.Alpha, TitleTracking);

		float ruleY = 129f + visual.Offset.Y;
		DrawMastheadRule(new Vector2(Game1.VirtualWidth / 2f, ruleY), visual.Alpha, visual.Scale);
	}

	private void DrawMastheadRule(Vector2 center, float alpha, float scale)
	{
		int half = Math.Max(1, (int)Math.Round(80f * scale));
		for (int i = 0; i < 8; i++)
		{
			float t0 = i / 8f;
			float t1 = (i + 1) / 8f;
			Color color = Blood * (alpha * t1);
			int x0 = (int)(center.X - 9 - half + half * t0);
			int x1 = (int)(center.X - 9 - half + half * t1);
			_spriteBatch.Draw(_pixel, new Rectangle(x0, (int)center.Y, Math.Max(1, x1 - x0), 1), color);
			int rx0 = (int)(center.X + 9 + half * t0);
			int rx1 = (int)(center.X + 9 + half * t1);
			_spriteBatch.Draw(_pixel, new Rectangle(rx0, (int)center.Y, Math.Max(1, rx1 - rx0), 1), Blood * (alpha * (1f - t0)));
		}
		DrawDiamond(center, 7f * scale, Blood * alpha);
	}

	private void DrawLane(int index, QuestRewardOverlayState state, LayerVisual visual, bool drawChrome, bool drawOutgoing, bool drawIncoming)
	{
		if (index < 0 || index >= _optionViews.Count || index >= _layout.Lanes.Length || visual.Alpha <= 0.001f) return;
		OptionView view = _optionViews[index];
		Rectangle baseLane = _layout.Lanes[index];
		Vector2 laneCenter = new(baseLane.Center.X, baseLane.Center.Y);
		Rectangle lane = TransformRect(baseLane, laneCenter, visual.Scale, visual.Offset);
		bool hovered = state.Phase == QuestRewardPresentationPhase.Visible && (view.Lane?.GetComponent<UIElement>()?.IsHovered ?? false);
		bool selected = state.SelectedDeckRewardColumnIndex == index;

		if (drawChrome)
		{
			DrawLaneBackground(lane, view.IsUpgrade, hovered || selected, visual.Alpha);
			DrawKicker(index, lane, view.IsUpgrade, hovered || selected, visual.Alpha);
			Vector2 swap = TransformPoint(_layout.SwapCenters[index], laneCenter, visual.Scale, visual.Offset);
			if (view.IsUpgrade) DrawPlus(swap, 30f * visual.Scale, Bone * visual.Alpha);
			else DrawArrow(swap, 30f * visual.Scale, 38f * visual.Scale, Blood * visual.Alpha);
		}

		float hoverScale = hovered && state.Phase == QuestRewardPresentationPhase.Visible ? 1.025f : 1f;
		if (drawOutgoing)
		{
			Vector2 anchor = TransformPoint(_layout.OutgoingAnchors[index], laneCenter, visual.Scale, visual.Offset);
			DrawCard(view.OutgoingCard, anchor, CardScale * visual.Scale, visual.Alpha, visual.Rotation);
		}
		if (drawIncoming)
		{
			Vector2 anchor = TransformPoint(_layout.IncomingAnchors[index], laneCenter, visual.Scale, visual.Offset);
			if (hovered) DrawCardGlow(anchor, CardScale * visual.Scale * hoverScale, view.IsUpgrade ? Bone : Blood, visual.Alpha);
			DrawCard(view.IncomingCard, anchor, CardScale * visual.Scale * hoverScale, visual.Alpha, visual.Rotation);
		}
	}

	private void DrawLaneBackground(Rectangle lane, bool isUpgrade, bool active, float alpha)
	{
		Color accent = isUpgrade ? Bone : Blood;
		const int segments = 24;
		for (int i = 0; i < segments; i++)
		{
			float t0 = i / (float)segments;
			float t1 = (i + 1) / (float)segments;
			float center = (t0 + t1) * 0.5f;
			float opacity = center <= 0.5f
				? MathHelper.Lerp(0.38f, 0.58f, center * 2f)
				: MathHelper.Lerp(0.58f, 0.42f, (center - 0.5f) * 2f);
			if (active) opacity = Math.Min(0.72f, opacity + 0.10f);
			float accentMix = active ? MathHelper.Lerp(0.08f, 0.025f, 1f - Math.Abs(center * 2f - 1f)) : 0f;
			Color fill = Color.Lerp(Color.Black, accent, accentMix) * (opacity * alpha);
			int y0 = lane.Y + (int)Math.Round(lane.Height * t0);
			int y1 = lane.Y + (int)Math.Round(lane.Height * t1);
			_spriteBatch.Draw(_pixel, new Rectangle(lane.X, y0, lane.Width, Math.Max(1, y1 - y0)), fill);
		}
		DrawGradientLine(new Rectangle(lane.X, lane.Y, lane.Width, active ? 3 : 1), accent, active ? alpha : 0.35f * alpha);
	}

	private void DrawKicker(int index, Rectangle lane, bool isUpgrade, bool active, float alpha)
	{
		Vector2 badgeCenter = new(lane.X + 27f, lane.Y + 18f);
		Color accent = isUpgrade ? Bone : Blood;
		Texture2D circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, 15);
		_spriteBatch.Draw(circle, badgeCenter, null, (active ? accent : BadgeBorder) * alpha, 0f, new Vector2(15f), 1f, SpriteEffects.None, 0f);
		Texture2D inner = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, 13);
		_spriteBatch.Draw(inner, badgeCenter, null, (active ? Color.Lerp(new Color(17, 17, 17), accent, 0.48f) : new Color(17, 17, 17)) * alpha, 0f, new Vector2(13f), 1f, SpriteEffects.None, 0f);

		string number = (index + 1).ToString();
		float numberScale = ScaleForPixelHeight(_badgeFont, number, 16f);
		Vector2 numberSize = _badgeFont.MeasureString(number) * numberScale;
		_spriteBatch.DrawString(_badgeFont, number, badgeCenter - numberSize / 2f, Color.White * alpha, 0f, Vector2.Zero, numberScale, SpriteEffects.None, 0f);

		string label = isUpgrade ? "UPGRADE" : "REPLACE";
		float labelScale = ScaleForPixelHeight(_bodyFont, label, KickerPixelHeight);
		Vector2 labelSize = MeasureTracked(_bodyFont, label, labelScale, KickerTracking);
		Vector2 labelPos = new(badgeCenter.X + 22f, badgeCenter.Y - labelSize.Y / 2f);
		DrawTracked(_bodyFont, label, labelPos, labelScale, (active ? Color.White : KickerDim) * alpha, KickerTracking);
	}

	private void DrawFooter(QuestRewardOverlayState state, Vector2 offset, float alpha)
	{
		if (alpha <= 0.001f) return;
		Rectangle rect = _layout.SkipButton;
		rect.Offset((int)Math.Round(offset.X), (int)Math.Round(offset.Y));
		bool hovered = state.Phase == QuestRewardPresentationPhase.Visible && (_skipButton?.GetComponent<UIElement>()?.IsHovered ?? false);
		_spriteBatch.Draw(_pixel, rect, new Color(6, 6, 6) * (0.70f * alpha));
		DrawBorder(rect, (hovered ? Color.White : Color.White * 0.38f) * alpha, 1);
		float scale = ScaleForPixelHeight(_bodyFont, SkipText, 11f);
		Vector2 size = MeasureTracked(_bodyFont, SkipText, scale, 1.6f);
		DrawTracked(_bodyFont, SkipText, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), scale, (hovered ? Color.White : new Color(189, 185, 178)) * alpha, 1.6f);
	}

	private void DrawHorizontalRule(int y, float alpha)
	{
		DrawGradientLine(new Rectangle(96, y, Game1.VirtualWidth - 192, 1), Color.White, 0.30f * alpha);
	}

	private void DrawGradientLine(Rectangle rect, Color color, float alpha)
	{
		const int segments = 24;
		for (int i = 0; i < segments; i++)
		{
			float t0 = i / (float)segments;
			float t1 = (i + 1) / (float)segments;
			float center = (t0 + t1) * 0.5f;
			float fade = 1f - Math.Abs(center * 2f - 1f);
			int x0 = rect.X + (int)Math.Round(rect.Width * t0);
			int x1 = rect.X + (int)Math.Round(rect.Width * t1);
			_spriteBatch.Draw(_pixel, new Rectangle(x0, rect.Y, Math.Max(1, x1 - x0), rect.Height), color * (alpha * fade));
		}
	}

	private void DrawCard(Entity card, Vector2 anchor, float scale, float alpha, float rotation)
	{
		if (card == null || !card.IsActive || scale <= 0.001f || alpha <= 0.001f) return;
		EventManager.Publish(new CardRenderScaledEvent
		{
			Card = card,
			Position = anchor,
			Scale = scale,
			Alpha = alpha,
			Rotation = rotation,
		});
	}

	private void DrawCardGlow(Vector2 anchor, float scale, Color accent, float alpha)
	{
		Rectangle rect = CardGeometryService.GetVisualRect(EntityManager, anchor, scale);
		for (int i = 12; i >= 2; i -= 2)
		{
			Rectangle expanded = new(rect.X - i, rect.Y - i, rect.Width + i * 2, rect.Height + i * 2);
			DrawBorder(expanded, accent * (alpha * 0.035f), 2);
		}
		DrawBorder(rect, accent * alpha, 2);
	}

	private void DrawBorder(Rectangle rect, Color color, int thickness)
	{
		_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
		_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
	}

	private void DrawPlus(Vector2 center, float size, Color color)
	{
		float thickness = Math.Max(2f, size * 0.13f);
		_spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - size / 2f), (int)(center.Y - thickness / 2f), (int)size, (int)thickness), color);
		_spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - thickness / 2f), (int)(center.Y - size / 2f), (int)thickness, (int)size), color);
	}

	private void DrawArrow(Vector2 center, float width, float height, Color color)
	{
		float shaft = Math.Max(3f, width * 0.14f);
		float top = center.Y - height / 2f;
		float headY = center.Y + height * 0.12f;
		_spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - shaft / 2f), (int)top, (int)shaft, (int)(headY - top)), color);
		DrawTriangle(new Vector2(center.X - width / 2f, headY), new Vector2(center.X + width / 2f, headY), new Vector2(center.X, center.Y + height / 2f), color);
	}

	private void DrawDiamond(Vector2 center, float size, Color color)
	{
		DrawTriangle(new Vector2(center.X, center.Y - size / 2f), new Vector2(center.X - size / 2f, center.Y), new Vector2(center.X + size / 2f, center.Y), color);
		DrawTriangle(new Vector2(center.X, center.Y + size / 2f), new Vector2(center.X - size / 2f, center.Y), new Vector2(center.X + size / 2f, center.Y), color);
	}

	private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
	{
		Vector2[] vertices = { a, b, c };
		Array.Sort(vertices, (left, right) => left.Y.CompareTo(right.Y));
		Vector2 top = vertices[0];
		Vector2 middle = vertices[1];
		Vector2 bottom = vertices[2];
		for (int y = (int)Math.Floor(top.Y); y <= (int)Math.Ceiling(bottom.Y); y++)
		{
			float fullT = bottom.Y <= top.Y ? 0f : (y - top.Y) / (bottom.Y - top.Y);
			Vector2 first = Vector2.Lerp(top, bottom, fullT);
			Vector2 second = y <= middle.Y
				? Vector2.Lerp(top, middle, middle.Y <= top.Y ? 0f : (y - top.Y) / (middle.Y - top.Y))
				: Vector2.Lerp(middle, bottom, bottom.Y <= middle.Y ? 0f : (y - middle.Y) / (bottom.Y - middle.Y));
			int x = (int)Math.Floor(Math.Min(first.X, second.X));
			int width = Math.Max(1, (int)Math.Ceiling(Math.Max(first.X, second.X)) - x);
			_spriteBatch.Draw(_pixel, new Rectangle(x, y, width, 1), color);
		}
	}

	private void CreateViews(DeckRewardOfferSave offer)
	{
		if (offer?.options == null) return;
		for (int i = 0; i < Math.Min(MaxOptions, offer.options.Count); i++)
		{
			DeckRewardOfferOptionSave option = offer.options[i];
			if (option == null) continue;
			bool isUpgrade = string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, StringComparison.OrdinalIgnoreCase);
			string incomingKey = isUpgrade ? option.upgradedCardKey : option.incomingCardKey;
			Entity outgoing = CreatePreviewCard(option.outgoingCardKey);
			Entity incoming = CreatePreviewCard(incomingKey);
			if (outgoing == null || incoming == null)
			{
				if (outgoing != null) EntityManager.DestroyEntity(outgoing.Id);
				if (incoming != null) EntityManager.DestroyEntity(incoming.Id);
				continue;
			}

			ApplyDeckRewardPreviewRestrictions(EntityManager, outgoing, option, false);
			ApplyDeckRewardPreviewRestrictions(EntityManager, incoming, option, true);
			Entity lane = CreateLaneEntity(_optionViews.Count);
			_optionViews.Add(new OptionView { Lane = lane, OutgoingCard = outgoing, IncomingCard = incoming, IsUpgrade = isUpgrade });
		}
		EnsureSkipButton();
	}

	private Entity CreatePreviewCard(string cardKey)
	{
		if (!RunDeckService.TryParseCardKey(cardKey, out string cardId, out CardData.CardColor color, out bool upgraded)) return null;
		Entity card = EntityFactory.CreateCardFromDefinition(EntityManager, cardId, color, suppressStatDeltaDisplay: true, isUpgraded: upgraded);
		if (card == null) return null;
		UIElement ui = card.GetComponent<UIElement>();
		if (ui != null)
		{
			ui.IsInteractable = false;
			ui.EventType = UIElementEventType.None;
			ui.LayerType = UILayerType.Overlay;
			ui.Bounds = Rectangle.Empty;
		}
		Transform transform = card.GetComponent<Transform>();
		if (transform != null) transform.ZOrder = ZOrder + 2;
		if (!card.HasComponent<ParallaxLayer>()) EntityManager.AddComponent(card, ParallaxLayer.GetUIParallaxLayer());
		InputContextService.EnsureMember(EntityManager, card, ContextId);
		return card;
	}

	private Entity CreateLaneEntity(int index)
	{
		Entity lane = EntityManager.CreateEntity($"QuestRewardLane_{index}");
		EntityManager.AddComponent(lane, new Transform { ZOrder = ZOrder + 10 + index });
		EntityManager.AddComponent(lane, new UIElement { Bounds = Rectangle.Empty, IsInteractable = false, EventType = UIElementEventType.None, LayerType = UILayerType.Overlay });
		EntityManager.AddComponent(lane, ParallaxLayer.GetUIParallaxLayer());
		EntityManager.AddComponent(lane, new DontDestroyOnLoad());
		InputContextService.EnsureMember(EntityManager, lane, ContextId);
		_laneEntities.Add(lane);
		return lane;
	}

	private void EnsureSkipButton()
	{
		if (_skipButton?.IsActive == true) return;
		_skipButton = EntityManager.CreateEntity("QuestRewardSkipButton");
		EntityManager.AddComponent(_skipButton, new Transform { ZOrder = ZOrder + 20 });
		EntityManager.AddComponent(_skipButton, new UIElement { Bounds = Rectangle.Empty, IsInteractable = false, EventType = UIElementEventType.None, LayerType = UILayerType.Overlay });
		EntityManager.AddComponent(_skipButton, new HotKey { Button = FaceButton.Y, IsActive = false });
		EntityManager.AddComponent(_skipButton, ParallaxLayer.GetUIParallaxLayer());
		EntityManager.AddComponent(_skipButton, new DontDestroyOnLoad());
		InputContextService.EnsureMember(EntityManager, _skipButton, ContextId);
	}

	private void DestroyViews()
	{
		foreach (OptionView view in _optionViews)
		{
			if (view.OutgoingCard != null) EntityManager.DestroyEntity(view.OutgoingCard.Id);
			if (view.IncomingCard != null) EntityManager.DestroyEntity(view.IncomingCard.Id);
		}
		_optionViews.Clear();
		foreach (Entity lane in _laneEntities)
			if (lane != null) EntityManager.DestroyEntity(lane.Id);
		_laneEntities.Clear();
		if (_skipButton != null)
		{
			EntityManager.DestroyEntity(_skipButton.Id);
			_skipButton = null;
		}
	}

	private void PreparePreviewCard(Entity card)
	{
		UIElement ui = card?.GetComponent<UIElement>();
		if (ui == null) return;
		ui.IsInteractable = false;
		ui.IsClicked = false;
		ui.Bounds = Rectangle.Empty;
		ui.LayerType = UILayerType.Overlay;
	}

	private void EnsureOverlayEntity()
	{
		Entity overlay = EntityManager.GetEntity("QuestRewardOverlay");
		if (overlay == null)
		{
			overlay = EntityManager.CreateEntity("QuestRewardOverlay");
			EntityManager.AddComponent(overlay, new Transform { ZOrder = ZOrder });
			EntityManager.AddComponent(overlay, new UIElement { Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), LayerType = UILayerType.Overlay });
			EntityManager.AddComponent(overlay, new QuestRewardOverlayState());
			EntityManager.AddComponent(overlay, new DontDestroyOnLoad());
		}
		InputContextService.EnsureContext(EntityManager, overlay, ContextId, 720, false);
	}

	private void EnsureEffectsLoaded()
	{
		if (_effectLoadAttempted || !ShaderRuntimeOptions.ShadersEnabled) return;
		_effectLoadAttempted = true;
		try
		{
			Effect atmosphere = _content.Load<Effect>("Shaders/QuestRewardOverlay");
			Effect blur = _content.Load<Effect>("Shaders/GaussianBlur");
			Effect filter = _content.Load<Effect>("Shaders/QuestRewardLayerFilter");
			_atmosphere = new QuestRewardOverlayEffect(_graphicsDevice, atmosphere);
			_layerCompositor = new QuestRewardLayerCompositor(_graphicsDevice, _spriteBatch, blur, filter);
		}
		catch (Exception ex)
		{
			LoggingService.Append("RewardModalDisplaySystem.EffectLoadFailed", new JsonObject { ["Message"] = ex.Message });
		}
	}

	private QuestRewardOverlayState GetState() =>
		EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();

	private SceneId GetCurrentScene() => EntityManager.GetEntitiesWithComponent<SceneState>()
		.FirstOrDefault()?.GetComponent<SceneState>()?.Current ?? SceneId.None;

	private static void ResetState(QuestRewardOverlayState state)
	{
		if (state == null) return;
		state.IsOpen = false;
		state.DeckRewardOffer = null;
		state.IsEncounterReward = false;
		state.ClimbResources = null;
		state.DismissScene = SceneId.Climb;
		state.DismissInProgress = false;
		state.IsPreviewOnly = false;
		state.PendingAutoContinue = false;
		state.Phase = QuestRewardPresentationPhase.Hidden;
		state.PhaseElapsedSeconds = 0f;
		state.DeckColumnSelectionInProgress = false;
		state.SelectedDeckRewardColumnIndex = -1;
		state.DeckColumnSelectionElapsedSeconds = 0f;
	}

	private static DeckRewardOfferSave CloneOffer(DeckRewardOfferSave offer)
	{
		if (offer == null) return null;
		return new DeckRewardOfferSave
		{
			rewardGold = offer.rewardGold,
			options = offer.options?.Where(option => option != null).Take(MaxOptions).Select(option => new DeckRewardOfferOptionSave
			{
				kind = option.kind,
				loadoutIndex = option.loadoutIndex,
				outgoingEntryId = option.outgoingEntryId,
				outgoingCardKey = option.outgoingCardKey,
				incomingCardKey = option.incomingCardKey,
				upgradedCardKey = option.upgradedCardKey,
			}).ToList() ?? new List<DeckRewardOfferOptionSave>(),
		};
	}

	private static ClimbResourceSave CloneResources(ClimbResourceSave resources) => resources == null ? null : new ClimbResourceSave
	{
		red = Math.Max(0, resources.red),
		white = Math.Max(0, resources.white),
		black = Math.Max(0, resources.black),
	};

	internal static DeckRewardOfferSave BuildRandomDebugOffer(Random random)
	{
		random ??= Random.Shared;
		var cards = CardFactory.GetAllCards()
			.Where(pair => pair.Value != null && !pair.Value.IsWeapon && !pair.Value.IsToken)
			.Select(pair => pair.Key.ToKey())
			.OrderBy(_ => random.Next())
			.Take(5)
			.ToArray();
		if (cards.Length < 5) return new DeckRewardOfferSave();
		string[] colors = { "white", "red", "black" };
		string Key(string id, int offset) => $"{id}|{colors[(random.Next(colors.Length) + offset) % colors.Length]}";
		string upgradeBase = Key(cards[4], 2);
		return new DeckRewardOfferSave
		{
			options = new List<DeckRewardOfferOptionSave>
			{
				new() { kind = DeckRewardOfferKinds.Exchange, outgoingCardKey = Key(cards[0], 0), incomingCardKey = Key(cards[1], 1) },
				new() { kind = DeckRewardOfferKinds.Exchange, outgoingCardKey = Key(cards[2], 1), incomingCardKey = Key(cards[3], 2) },
				new() { kind = DeckRewardOfferKinds.Upgrade, outgoingCardKey = upgradeBase, upgradedCardKey = RunDeckService.BuildUpgradedCardKey(upgradeBase) },
			},
		};
	}

	internal static bool TryCommitSelection(QuestRewardOverlayState state, int optionIndex)
	{
		if (state?.IsPreviewOnly == true) return true;
		return QuestCardRewardService.ApplyPendingOfferOption(optionIndex);
	}

	internal static void CommitSkip(QuestRewardOverlayState state)
	{
		if (state?.IsPreviewOnly == true) return;
		QuestCardRewardService.SkipPendingOffer();
	}

	internal static bool PublishClimbResourceAcquisitionIfNeeded(QuestRewardOverlayState state)
	{
		if (state?.IsEncounterReward != true || state.DismissScene != SceneId.Climb || state.ClimbResources == null) return false;
		if (state.ClimbResources.red <= 0 && state.ClimbResources.white <= 0 && state.ClimbResources.black <= 0) return false;
		EventManager.Publish(new ClimbResourceAcquisitionAnimationRequested { Resources = CloneResources(state.ClimbResources) });
		return true;
	}

	internal static void ApplyDeckRewardPreviewRestrictions(EntityManager entityManager, Entity card, DeckRewardOfferOptionSave option, bool forIncomingCard)
	{
		if (entityManager == null || card == null || option == null || string.IsNullOrWhiteSpace(option.outgoingEntryId)) return;
		RunScopedStateService.ApplySavedRestrictionsToCard(entityManager, card, option.outgoingEntryId);
	}

	public static bool IsOverlayOpen(EntityManager entityManager) =>
		entityManager?.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>()?.IsOpen == true;

	public static bool ShouldSuppressBattleSceneDisplay(EntityManager entityManager)
	{
		QuestRewardOverlayState state = entityManager?.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
		return state != null && (state.IsOpen || state.DismissInProgress);
	}

	internal static LayerVisual ComputeEntranceLaneVisual(int index, float elapsed)
	{
		float delay = index == 1 ? 0.23f : 0.33f;
		float raw = Progress(elapsed - delay, 0.68f);
		float eased = EaseOutCubic(raw);
		return new LayerVisual { Alpha = raw, Scale = MathHelper.Lerp(0.72f, 1f, eased), Blur = MathHelper.Lerp(9f, 0f, eased), Grayscale = 1f - eased, Brightness = 1f };
	}

	internal static LayerVisual ComputeIncomingClaimVisual(float elapsed)
	{
		float t = Progress(elapsed - 0.08f, 1.37f);
		if (t <= 0.32f)
		{
			float local = EaseOutCubic(t / 0.32f);
			return new LayerVisual { Alpha = 1f, Scale = MathHelper.Lerp(1.025f, 1.18f, local), Offset = new Vector2(0f, -150f * local), Brightness = MathHelper.Lerp(1f, 1.12f, local) };
		}
		if (t <= 0.68f)
			return new LayerVisual { Alpha = 1f, Scale = 1.18f, Offset = new Vector2(0f, -150f), Brightness = 1.12f };
		float exit = EaseInCubic((t - 0.68f) / 0.32f);
		return new LayerVisual
		{
			Alpha = 1f - exit,
			Scale = MathHelper.Lerp(1.18f, 0.72f, exit),
			Offset = new Vector2(0f, MathHelper.Lerp(-150f, -330f, exit)),
			Blur = 10f * exit,
			Brightness = MathHelper.Lerp(1.12f, 1.55f, exit),
		};
	}

	private static float GetOverlayAlpha(QuestRewardOverlayState state)
	{
		if (state?.Phase == QuestRewardPresentationPhase.Skipping)
			return 1f - EaseInCubic(Progress(state.PhaseElapsedSeconds, SkipDuration));
		if (state?.Phase != QuestRewardPresentationPhase.Claiming) return 1f;
		float fadeStart = 0.08f + 1.37f * 0.68f;
		return 1f - EaseInCubic(Progress(state.PhaseElapsedSeconds - fadeStart, ClaimDuration - fadeStart));
	}

	private static float Progress(float elapsed, float duration) => MathHelper.Clamp(elapsed / Math.Max(0.001f, duration), 0f, 1f);
	private static float EaseOutCubic(float t) => 1f - (float)Math.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3f);
	private static float EaseInCubic(float t) => (float)Math.Pow(MathHelper.Clamp(t, 0f, 1f), 3f);
	private static float EaseBackOut(float t)
	{
		t = MathHelper.Clamp(t, 0f, 1f);
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		return 1f + c3 * (float)Math.Pow(t - 1f, 3f) + c1 * (float)Math.Pow(t - 1f, 2f);
	}

	private static Vector2 CardAnchorForVisualCenter(Vector2 visualCenter, float scale)
	{
		float offset = CardGeometrySettings.DefaultOffsetYExtra * scale;
		return new Vector2(visualCenter.X, visualCenter.Y + offset);
	}

	private static Vector2 TransformPoint(Vector2 point, Vector2 center, float scale, Vector2 offset) => center + (point - center) * scale + offset;
	private static Vector2 ScaleAround(Vector2 point, Vector2 center, float scale) => center + (point - center) * scale;
	private static Rectangle TransformRect(Rectangle rect, Vector2 center, float scale, Vector2 offset)
	{
		Vector2 topLeft = TransformPoint(new Vector2(rect.X, rect.Y), center, scale, offset);
		return new Rectangle((int)Math.Round(topLeft.X), (int)Math.Round(topLeft.Y), Math.Max(1, (int)Math.Round(rect.Width * scale)), Math.Max(1, (int)Math.Round(rect.Height * scale)));
	}

	private static float ScaleForPixelHeight(SpriteFont font, string text, float pixelHeight)
	{
		if (font == null || string.IsNullOrEmpty(text)) return 1f;
		return pixelHeight / Math.Max(1f, font.MeasureString(text).Y);
	}

	private static Vector2 MeasureTracked(SpriteFont font, string text, float scale, float tracking)
	{
		if (font == null || string.IsNullOrEmpty(text)) return Vector2.Zero;
		float width = 0f;
		float height = 0f;
		for (int i = 0; i < text.Length; i++)
		{
			Vector2 size = font.MeasureString(text[i].ToString()) * scale;
			width += size.X;
			height = Math.Max(height, size.Y);
			if (i < text.Length - 1) width += tracking;
		}
		return new Vector2(width, height);
	}

	private void DrawTracked(SpriteFont font, string text, Vector2 position, float scale, Color color, float tracking)
	{
		float x = position.X;
		foreach (char character in text)
		{
			string glyph = character.ToString();
			_spriteBatch.DrawString(font, glyph, new Vector2(x, position.Y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			x += font.MeasureString(glyph).X * scale + tracking;
		}
	}
}
