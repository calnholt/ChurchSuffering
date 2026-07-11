using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Save;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Reward Modal")]
	public class RewardModalDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;
		private readonly List<Entity> _rewardCardEntities = new();
		private readonly List<DeckRewardOptionView> _deckRewardOptionViews = new();
		private readonly List<Entity> _deckRewardLaneEntities = new();
		private Entity _deckRewardSkipButton;
		private Entity _rewardMedalEntity;
		private Entity _rewardEquipmentEntity;
		private QuestRewardLayout _layout;

		private bool _layoutValid;
		private bool _textMetricsValid;
		private bool _drawInBattleOrSnapshot;
		private int _cachedVw;
		private int _cachedVh;
		private bool _cachedShowGold;
		private bool _cachedShowCard;
		private bool _cachedShowMedal;
		private bool _cachedShowEquipment;
		private bool _cachedShowClimbResources;
		private int _cachedRewardGold;
		private int _cachedRewardCardCount;
		private LayoutSignature _layoutSignature;
		private CachedTextMetrics _textMetrics;
		private readonly HorizontalGradientRuleCache _gradientRuleCache;

		private static readonly Color LeftColTint = new Color(0, 0, 0) * 0.35f;
		private static readonly Color ColumnDivider = new Color(255, 255, 255) * 0.15f;
		private static readonly Color GoldLabelColor = new Color(160, 128, 48);
		private static readonly Color GoldAmountColor = new Color(232, 200, 74);
		private static readonly Color StageLabelColor = new Color(200, 192, 184);
		private static readonly Color ClimbRedColor = new Color(206, 55, 64);
		private static readonly Color ClimbWhiteColor = new Color(230, 224, 204);
		private static readonly Color ClimbBlackColor = new Color(92, 76, 104);

		private static readonly Color ExchangeColFillTop = new Color(0, 0, 0) * 0.45f;
		private static readonly Color ExchangeColFillMid = new Color(30, 0, 0) * 0.15f;
		private static readonly Color ExchangeColHoverFillTop = new Color(40, 0, 0) * 0.50f;
		private static readonly Color ExchangeColHoverFillMid = new Color(80, 0, 0) * 0.25f;
		private static readonly Color ExchangeColBorderTop = new Color(255, 255, 255) * 0.15f;
		private static readonly Color ExchangeColBorderTopHover = new Color(196, 30, 58);
		private static readonly Color ExchangeColBorderSide = new Color(255, 255, 255) * 0.12f;
		private static readonly Color ExchangeColBorderSideHover = new Color(196, 30, 58) * 0.45f;
		private static readonly Color UpgradeColFillMid = new Color(255, 255, 255) * 0.04f;
		private static readonly Color UpgradeColHoverFillTop = new Color(20, 20, 20) * 0.50f;
		private static readonly Color UpgradeColHoverFillMid = new Color(255, 255, 255) * 0.08f;
		private static readonly Color UpgradeColBorderTop = new Color(255, 255, 255) * 0.25f;
		private static readonly Color UpgradeColBorderTopHover = new Color(240, 236, 230);
		private static readonly Color UpgradeColBorderSideHover = new Color(255, 255, 255) * 0.40f;
		private static readonly Color TradeArrowColor = new Color(196, 30, 58);
		private static readonly Color UpgradePlusColor = new Color(240, 236, 230);
		private static readonly Color SkipButtonBgHover = new Color(255, 255, 255) * 0.06f;
		private static readonly Color SkipButtonBorderDefault = new Color(255, 255, 255) * 0.35f;
		private static readonly Color SkipButtonBorderHover = new Color(240, 236, 230);

		private const string GoldLabelText = "GOLD";
		private const string ClimbResourceLabelText = "CLIMB CACHE";
		private const string StageLabelText = "REWARD";
		private const string QuestStageLabelText = "CHOOSE YOUR REWARD";
		private const string ProceedLabelText = "Proceed";
		private const string SkipRewardLabelText = "Skip Reward";
		private const string ContextId = "overlay.quest-reward";
		private const int MaxRewardCardChoices = 2;

		private int _pendingCloseExitSequence = -1;
		private bool _pendingCloseTransition;
		private SceneId _pendingCloseTransitionScene = SceneId.Climb;

		[DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
		public int ZOrder { get; set; } = 52000;

		[DebugEditable(DisplayName = "Modal Width", Step = 10, Min = 200, Max = 1600)]
		public int ModalWidth { get; set; } = 920;
		[DebugEditable(DisplayName = "Quest Reward Modal Height", Step = 10, Min = 200, Max = 1200)]
		public int QuestRewardModalHeight { get; set; } = 1030;
		[DebugEditable(DisplayName = "Treasure Chest Modal Height", Step = 10, Min = 200, Max = 1200)]
		public int TreasureChestModalHeight { get; set; } = 520;
		[DebugEditable(DisplayName = "Left Col Width", Step = 10, Min = 120, Max = 600)]
		public int LeftColWidth { get; set; } = 280;
		[DebugEditable(DisplayName = "Gold Only Modal Width", Step = 10, Min = 200, Max = 800)]
		public int GoldOnlyModalWidth { get; set; } = 400;
		[DebugEditable(DisplayName = "Border Thickness", Step = 1, Min = 1, Max = 8)]
		public int BorderThickness { get; set; } = 2;
		[DebugEditable(DisplayName = "Dim Alpha", Step = 5, Min = 0, Max = 255)]
		public int DimAlpha { get; set; } = 140;

		[DebugEditable(DisplayName = "Left Padding Top", Step = 2, Min = 0, Max = 120)]
		public int LeftPaddingTop { get; set; } = 40;
		[DebugEditable(DisplayName = "Left Padding Bottom", Step = 2, Min = 0, Max = 120)]
		public int LeftPaddingBottom { get; set; } = 40;
		[DebugEditable(DisplayName = "Left Padding X", Step = 2, Min = 0, Max = 80)]
		public int LeftPaddingX { get; set; } = 44;
		[DebugEditable(DisplayName = "Left Col Gap", Step = 2, Min = 0, Max = 80)]
		public int LeftColGap { get; set; } = 32;

		[DebugEditable(DisplayName = "Title Line 1")]
		public string TitleLine1 { get; set; } = "Quest";
		[DebugEditable(DisplayName = "Title Line 2")]
		public string TitleLine2 { get; set; } = "Complete!";
		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float TitleScale { get; set; } = 0.33f;
		[DebugEditable(DisplayName = "Gold Label Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float GoldLabelScale { get; set; } = 0.14f;
		[DebugEditable(DisplayName = "Gold Amount Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float GoldAmountScale { get; set; } = 0.42f;
		[DebugEditable(DisplayName = "Climb Resource Label Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float ClimbResourceLabelScale { get; set; } = 0.10f;
		[DebugEditable(DisplayName = "Climb Resource Text Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float ClimbResourceTextScale { get; set; } = 0.13f;
		[DebugEditable(DisplayName = "Climb Resource Icon Size", Step = 1, Min = 4, Max = 60)]
		public int ClimbResourceIconSize { get; set; } = 16;
		[DebugEditable(DisplayName = "Climb Resource Row Gap", Step = 1, Min = 0, Max = 40)]
		public int ClimbResourceRowGap { get; set; } = 10;
		[DebugEditable(DisplayName = "Stage Label Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float StageLabelScale { get; set; } = 0.14f;

		[DebugEditable(DisplayName = "Red Rule Width", Step = 2, Min = 20, Max = 200)]
		public int RedRuleWidth { get; set; } = 80;
		[DebugEditable(DisplayName = "Red Rule Height", Step = 1, Min = 1, Max = 12)]
		public int RedRuleHeight { get; set; } = 3;

		[DebugEditable(DisplayName = "Right Padding Top", Step = 2, Min = 0, Max = 120)]
		public int RightPaddingTop { get; set; } = 48;
		[DebugEditable(DisplayName = "Right Padding X", Step = 2, Min = 0, Max = 80)]
		public int RightPaddingX { get; set; } = 40;
		[DebugEditable(DisplayName = "Right Padding Bottom", Step = 2, Min = 0, Max = 120)]
		public int RightPaddingBottom { get; set; } = 32;
		[DebugEditable(DisplayName = "Stage Label Gap", Step = 2, Min = 0, Max = 120)]
		public int StageLabelGap { get; set; } = 36;

		[DebugEditable(DisplayName = "Card Preview Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float CardPreviewScale { get; set; } = 0.74f;
		[DebugEditable(DisplayName = "Card Preview Offset X", Step = 2, Min = -400, Max = 400)]
		public int CardPreviewOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Card Preview Offset Y", Step = 2, Min = -400, Max = 400)]
		public int CardPreviewOffsetY { get; set; } = -20;
		[DebugEditable(DisplayName = "Card Choice Gap", Step = 2, Min = 0, Max = 240)]
		public int CardChoiceGap { get; set; } = 32;
		[DebugEditable(DisplayName = "Card Select Anim (s)", Step = 0.01f, Min = 0.05f, Max = 2f)]
		public float CardSelectionAnimationSeconds { get; set; } = 0.55f;
		[DebugEditable(DisplayName = "Deck Column Select Anim (s)", Step = 0.01f, Min = 0.05f, Max = 2f)]
		public float DeckColumnSelectionAnimationSeconds { get; set; } = 0.55f;
		[DebugEditable(DisplayName = "Deck Column Pulse Amplitude", Step = 0.01f, Min = 0f, Max = 1f)]
		public float DeckColumnPulseScaleAmplitude { get; set; } = 0.12f;
		[DebugEditable(DisplayName = "Deck Column Pulse Frequency Hz", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public float DeckColumnPulseFrequencyHz { get; set; } = 6f;

		[DebugEditable(DisplayName = "Medal Preview Size", Step = 2, Min = 40, Max = 400)]
		public int MedalPreviewSize { get; set; } = 180;
		[DebugEditable(DisplayName = "Equipment Name Scale", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float EquipmentNameScale { get; set; } = 0.14f;

		[DebugEditable(DisplayName = "Drop Shadow Offset Y", Step = 1, Min = 0, Max = 40)]
		public int DropShadowOffsetY { get; set; } = 16;

		[DebugEditable(DisplayName = "Footer Padding", Step = 2, Min = 0, Max = 60)]
		public int FooterPadding { get; set; } = 20;
		[DebugEditable(DisplayName = "Button Width", Step = 5, Min = 60, Max = 800)]
		public int ButtonWidth { get; set; } = 220;
		[DebugEditable(DisplayName = "Button Height", Step = 5, Min = 30, Max = 300)]
		public int ButtonHeight { get; set; } = 64;
		[DebugEditable(DisplayName = "Button Text Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float ButtonTextScale { get; set; } = 0.28f;

		[DebugEditable(DisplayName = "Deck Reward Modal Width", Step = 10, Min = 600, Max = 1600)]
		public int DeckModalWidth { get; set; } = 1120;
		[DebugEditable(DisplayName = "Deck Reward Modal Height", Step = 10, Min = 400, Max = 1200)]
		public int DeckModalHeight { get; set; } = 1080;
		[DebugEditable(DisplayName = "Masthead Height", Step = 2, Min = 40, Max = 200)]
		public int DeckMastheadHeight { get; set; } = 90;
		[DebugEditable(DisplayName = "Masthead Pad Top", Step = 1, Min = 0, Max = 60)]
		public int DeckMastheadPadTop { get; set; } = 22;
		[DebugEditable(DisplayName = "Masthead Pad Bottom", Step = 1, Min = 0, Max = 60)]
		public int DeckMastheadPadBottom { get; set; } = 18;
		[DebugEditable(DisplayName = "Masthead Title Scale", Step = 0.01f, Min = 0.1f, Max = 1f)]
		public float DeckMastheadTitleScale { get; set; } = 0.27f;
		[DebugEditable(DisplayName = "Deck Masthead Title")]
		public string DeckMastheadTitle { get; set; } = "Quest Complete!";
		[DebugEditable(DisplayName = "Column Gap", Step = 1, Min = 0, Max = 60)]
		public int ColumnGap { get; set; } = 14;
		[DebugEditable(DisplayName = "Column Max Width", Step = 5, Min = 200, Max = 600)]
		public int ColumnMaxWidth { get; set; } = 340;
		[DebugEditable(DisplayName = "Column Padding Top", Step = 1, Min = 0, Max = 60)]
		public int ColumnPaddingTop { get; set; } = 14;
		[DebugEditable(DisplayName = "Column Padding Bottom", Step = 1, Min = 0, Max = 60)]
		public int ColumnPaddingBottom { get; set; } = 16;
		[DebugEditable(DisplayName = "Column Padding X", Step = 1, Min = 0, Max = 60)]
		public int ColumnPaddingX { get; set; } = 12;
		[DebugEditable(DisplayName = "Card Stack Gap", Step = 1, Min = 0, Max = 40)]
		public int CardStackGap { get; set; } = 6;
		[DebugEditable(DisplayName = "Exchange Stage Pad Top", Step = 1, Min = 0, Max = 60)]
		public int ExchangeStagePadTop { get; set; } = 16;
		[DebugEditable(DisplayName = "Exchange Stage Pad Bottom", Step = 1, Min = 0, Max = 60)]
		public int ExchangeStagePadBottom { get; set; } = 16;
		[DebugEditable(DisplayName = "Exchange Stage Pad X", Step = 1, Min = 0, Max = 60)]
		public int ExchangeStagePadX { get; set; } = 24;
		[DebugEditable(DisplayName = "Deck Footer Height", Step = 2, Min = 40, Max = 200)]
		public int DeckFooterHeight { get; set; } = 88;
		[DebugEditable(DisplayName = "Skip Button Width", Step = 5, Min = 60, Max = 600)]
		public int DeckSkipButtonWidth { get; set; } = 180;
		[DebugEditable(DisplayName = "Skip Button Height", Step = 2, Min = 30, Max = 120)]
		public int DeckSkipButtonHeight { get; set; } = 56;
		[DebugEditable(DisplayName = "Skip Button Text Scale", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float DeckSkipButtonTextScale { get; set; } = 0.12f;
		[DebugEditable(DisplayName = "Arrow Scale", Step = 0.01f, Min = 0.25f, Max = 3f)]
		public float ArrowScale { get; set; } = 1.0f;
		[DebugEditable(DisplayName = "Trade Arrow Width", Step = 1, Min = 8, Max = 80)]
		public int TradeArrowWidth { get; set; } = 22;
		[DebugEditable(DisplayName = "Trade Arrow Height", Step = 1, Min = 8, Max = 80)]
		public int TradeArrowHeight { get; set; } = 36;
		[DebugEditable(DisplayName = "Upgrade Plus Size", Step = 1, Min = 8, Max = 80)]
		public int UpgradePlusSize { get; set; } = 24;
		[DebugEditable(DisplayName = "Column Top Bar Thickness", Step = 1, Min = 1, Max = 8)]
		public int ColumnTopBarThickness { get; set; } = 3;
		[DebugEditable(DisplayName = "Sheen Stagger Seconds", Step = 0.01f, Min = 0f, Max = 1f)]
		public float SheenStaggerSeconds { get; set; } = 0.12f;
		[DebugEditable(DisplayName = "Sheen Delay Seconds", Step = 0.01f, Min = 0f, Max = 3f)]
		public float SheenDelaySeconds { get; set; } = 0.52f;
		private struct DeckRewardOptionView
		{
			public Entity Lane;
			public Entity OutgoingCard;
			public Entity IncomingCard;
		}

		private struct DeckRewardOfferLayout
		{
			public Rectangle Modal;
			public Rectangle Content;
			public Rectangle Masthead;
			public Rectangle ExchangeStage;
			public Rectangle Footer;
			public Rectangle SkipButton;
			public Rectangle[] Columns;
			public Vector2[] OutgoingCardCenters;
			public Vector2[] IncomingCardCenters;
			public Vector2[] ArrowCenters;
			public bool[] IsUpgrade;
		}

		private struct QuestRewardLayout
		{
			public Rectangle Modal;
			public Rectangle Content;
			public Rectangle LeftColumn;
			public Rectangle LeftInner;
			public Rectangle Divider;
			public Rectangle RightColumn;
			public Rectangle RightInner;
			public Rectangle Footer;
			public Rectangle ProceedButton;
			public Vector2 CardCenter;
			public Vector2[] CardChoiceCenters;
			public Rectangle MedalPreviewRect;
			public float StageLabelHeight;
			public bool ShowRightColumn;
			public bool ShowGold;
			public bool ShowCard;
			public bool ShowMedal;
		}

		private struct LayoutSignature
		{
			public int ModalWidth;
			public int QuestRewardModalHeight;
			public int TreasureChestModalHeight;
			public bool IsTreasureChest;
			public int LeftColWidth;
			public int GoldOnlyModalWidth;
			public int BorderThickness;
			public int LeftPaddingTop;
			public int LeftPaddingBottom;
			public int LeftPaddingX;
			public int LeftColGap;
			public string TitleLine1;
			public string TitleLine2;
			public float TitleScale;
			public float GoldLabelScale;
			public float GoldAmountScale;
			public float ClimbResourceLabelScale;
			public float ClimbResourceTextScale;
			public int ClimbResourceIconSize;
			public int ClimbResourceRowGap;
			public float StageLabelScale;
			public int RedRuleWidth;
			public int RedRuleHeight;
			public int RightPaddingTop;
			public int RightPaddingX;
			public int RightPaddingBottom;
			public int StageLabelGap;
			public float CardPreviewScale;
			public int CardPreviewOffsetX;
			public int CardPreviewOffsetY;
			public int CardChoiceGap;
			public int FooterPadding;
			public int ButtonWidth;
			public int ButtonHeight;
			public float ButtonTextScale;
			public int CardWidth;
			public int CardHeight;
			public int CardOffsetYExtra;
		}

		private struct CachedTextMetrics
		{
			public Vector2 TitleLine1Size;
			public Vector2 TitleLine2Size;
			public Vector2 TitleLine1Pos;
			public Vector2 TitleLine2Pos;
			public int RuleY;
			public Vector2 GoldLabelSize;
			public float GoldLabelHeight;
			public Vector2 GoldLabelPos;
			public string GoldAmountText;
			public Vector2 GoldAmountSize;
			public Vector2 GoldAmountPos;
			public bool HasGoldBlock;
			public Vector2 ClimbResourceLabelPos;
			public Vector2[] ClimbResourceRowPositions;
			public bool HasClimbResourceBlock;
			public Vector2 StageLabelPos;
			public Vector2 ProceedTextPos;
			public Vector2 ProceedTextSize;
		}

		public RewardModalDisplaySystem(EntityManager entityManager, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets) : base(entityManager)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
			_gradientRuleCache = new HorizontalGradientRuleCache(gd);
			EventManager.Subscribe<ShowQuestRewardOverlay>(e => {
				LoggingService.Append("RewardModalDisplaySystem.OnShowQuestRewardOverlay", new JsonObject {
					{ "Message", e.Message },
					{ "RewardGold", e.RewardGold },
					{ "HasCardReward", e.HasCardReward },
					{ "RewardCardKey", e.RewardCardKey ?? string.Empty }
				});
				OpenQuestReward(e);
			});
			EventManager.Subscribe<TreasureChestOpened>(e => {
				LoggingService.Append("RewardModalDisplaySystem.OnTreasureChestOpened", new JsonObject {
					{ "RewardGold", e.RewardGold },
					{ "RewardMedalId", e.RewardMedalId ?? string.Empty }
				});
				OpenTreasureChest(e);
			});
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		private void OnLoadScene(LoadSceneEvent e)
		{
			if (e.Scene != SceneId.Climb) return;

			var state = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			if (state != null && state.DismissInProgress)
			{
				CloseOverlay(state);
				return;
			}

			if (state?.IsOpen == true) return;

			if (e.Scene == SceneId.Climb)
			{
				var pendingEncounterReward = SaveCache.GetClimbState()?.pendingEncounterReward;
				if (pendingEncounterReward != null)
				{
					OpenQuestReward(new ShowQuestRewardOverlay
					{
						Message = "Encounter Complete!",
						TitleLine1 = "Encounter",
						TitleLine2 = "Complete!",
						HasCardReward = pendingEncounterReward.deckRewardOffer?.options != null
							&& pendingEncounterReward.deckRewardOffer.options.Count > 0,
						DeckRewardOffer = pendingEncounterReward.deckRewardOffer,
						IsEncounterReward = true,
						ClimbResources = pendingEncounterReward.resources,
						DismissScene = pendingEncounterReward.pendingFinalEncounter ? SceneId.Battle : SceneId.Climb,
					});
					return;
				}
			}

			var pendingOffer = SaveCache.GetPendingDeckRewardOffer();
			if (pendingOffer?.options == null || pendingOffer.options.Count == 0) return;

			OpenQuestReward(new ShowQuestRewardOverlay
			{
				RewardGold = pendingOffer.rewardGold,
				HasCardReward = true,
				DeckRewardOffer = pendingOffer
			});
		}

		private void OnDeleteCaches(DeleteCachesEvent _)
		{
			InvalidateCaches();
			_gradientRuleCache.DisposeAll();
			DestroyRewardCards();
			DestroyDeckRewardControls();
			DestroyRewardMedal();
			DestroyRewardEquipment();
		}

		private void InvalidateCaches()
		{
			_layoutValid = false;
			_textMetricsValid = false;
		}

		private static bool IsTreasureChestOverlay(QuestRewardOverlayState state) =>
			string.Equals(state?.TitleLine1, "Treasure", System.StringComparison.Ordinal);

		private LayoutSignature CaptureLayoutSignature()
		{
			var overlayState = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			var settings = CardGeometryService.GetSettings(EntityManager);
			return new LayoutSignature
			{
				ModalWidth = ModalWidth,
				QuestRewardModalHeight = QuestRewardModalHeight,
				TreasureChestModalHeight = TreasureChestModalHeight,
				IsTreasureChest = IsTreasureChestOverlay(overlayState),
				LeftColWidth = LeftColWidth,
				GoldOnlyModalWidth = GoldOnlyModalWidth,
				BorderThickness = BorderThickness,
				LeftPaddingTop = LeftPaddingTop,
				LeftPaddingBottom = LeftPaddingBottom,
				LeftPaddingX = LeftPaddingX,
				LeftColGap = LeftColGap,
				TitleLine1 = overlayState?.TitleLine1 ?? TitleLine1 ?? "Quest",
				TitleLine2 = overlayState?.TitleLine2 ?? TitleLine2 ?? "Complete!",
				TitleScale = TitleScale,
				GoldLabelScale = GoldLabelScale,
				GoldAmountScale = GoldAmountScale,
				ClimbResourceLabelScale = ClimbResourceLabelScale,
				ClimbResourceTextScale = ClimbResourceTextScale,
				ClimbResourceIconSize = ClimbResourceIconSize,
				ClimbResourceRowGap = ClimbResourceRowGap,
				StageLabelScale = StageLabelScale,
				RedRuleWidth = RedRuleWidth,
				RedRuleHeight = RedRuleHeight,
				RightPaddingTop = RightPaddingTop,
				RightPaddingX = RightPaddingX,
				RightPaddingBottom = RightPaddingBottom,
				StageLabelGap = StageLabelGap,
				CardPreviewScale = CardPreviewScale,
				CardPreviewOffsetX = CardPreviewOffsetX,
				CardPreviewOffsetY = CardPreviewOffsetY,
				CardChoiceGap = CardChoiceGap,
				FooterPadding = FooterPadding,
				ButtonWidth = ButtonWidth,
				ButtonHeight = ButtonHeight,
				ButtonTextScale = ButtonTextScale,
				CardWidth = settings?.CardWidth ?? CardGeometrySettings.DefaultWidth,
				CardHeight = settings?.CardHeight ?? CardGeometrySettings.DefaultHeight,
				CardOffsetYExtra = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra
			};
		}

		private bool NeedsLayoutRebuild(int vw, int vh, bool showGold, bool showCard, bool showMedal, bool showEquipment, bool showClimbResources, int rewardGold, int rewardCardCount)
		{
			if (!_layoutValid) return true;
			if (vw != _cachedVw || vh != _cachedVh) return true;
			if (showGold != _cachedShowGold || showCard != _cachedShowCard || showMedal != _cachedShowMedal || showEquipment != _cachedShowEquipment || showClimbResources != _cachedShowClimbResources || rewardGold != _cachedRewardGold || rewardCardCount != _cachedRewardCardCount) return true;
			var sig = CaptureLayoutSignature();
			return !sig.Equals(_layoutSignature);
		}

		private void EnsureLayout(int vw, int vh, bool showGold, bool showCard, bool showMedal, bool showEquipment, bool showClimbResources, int rewardGold, int rewardCardCount, SceneState scene)
		{
			if (!NeedsLayoutRebuild(vw, vh, showGold, showCard, showMedal, showEquipment, showClimbResources, rewardGold, rewardCardCount)) return;

			_cachedVw = vw;
			_cachedVh = vh;
			_cachedShowGold = showGold;
			_cachedShowCard = showCard;
			_cachedShowMedal = showMedal;
			_cachedShowEquipment = showEquipment;
			_cachedShowClimbResources = showClimbResources;
			_cachedRewardGold = rewardGold;
			_cachedRewardCardCount = rewardCardCount;
			_layoutSignature = CaptureLayoutSignature();
			_drawInBattleOrSnapshot = scene != null
				&& (scene.Current == SceneId.Battle
					|| scene.Current == SceneId.Climb
					|| scene.Current == SceneId.Snapshot);

			_layout = ComputeLayout(vw, vh, showGold, showCard, showMedal || showEquipment, showClimbResources, rewardCardCount, _layoutSignature);
			RebuildTextMetrics(rewardGold, showGold, showClimbResources);
			_layoutValid = true;
			_textMetricsValid = true;
		}

		private void RebuildTextMetrics(int rewardGold, bool showGold, bool showClimbResources)
		{
			int centerX = _layout.LeftInner.Center.X;
			float cursorY = _layout.LeftInner.Y;

			string line1 = _layoutSignature.TitleLine1;
			string line2 = _layoutSignature.TitleLine2;

			var title1Size = _titleFont.MeasureString(line1) * TitleScale;
			var title2Size = _titleFont.MeasureString(line2) * TitleScale;
			var title1Pos = new Vector2(centerX - title1Size.X / 2f, cursorY);
			cursorY += title1Size.Y;
			var title2Pos = new Vector2(centerX - title2Size.X / 2f, cursorY);
			cursorY += title2Size.Y + LeftColGap;

			int ruleY = (int)cursorY;

			var metrics = new CachedTextMetrics
			{
				TitleLine1Size = title1Size,
				TitleLine2Size = title2Size,
				TitleLine1Pos = title1Pos,
				TitleLine2Pos = title2Pos,
				RuleY = ruleY,
				HasGoldBlock = false
			};

			if (showGold && rewardGold > 0)
			{
				float goldBlockTop = cursorY + RedRuleHeight + LeftColGap;
				float goldBlockBottom = _layout.LeftInner.Bottom;
				float goldBlockH = System.Math.Max(1f, goldBlockBottom - goldBlockTop);

				float labelH = _bodyFont != null ? _bodyFont.MeasureString(GoldLabelText).Y * GoldLabelScale : 14f;
				string goldAmount = $"+{rewardGold:N0}";
				var amountSize = _titleFont.MeasureString(goldAmount) * GoldAmountScale;
				float totalGoldH = labelH + 4f + amountSize.Y;
				float goldStartY = goldBlockTop + (goldBlockH - totalGoldH) / 2f;

				Vector2 labelSize = Vector2.Zero;
				Vector2 labelPos = Vector2.Zero;
				if (_bodyFont != null)
				{
					labelSize = _bodyFont.MeasureString(GoldLabelText) * GoldLabelScale;
					labelPos = new Vector2(centerX - labelSize.X / 2f, goldStartY);
				}

				metrics.HasGoldBlock = true;
				metrics.GoldLabelSize = labelSize;
				metrics.GoldLabelHeight = labelH;
				metrics.GoldLabelPos = labelPos;
				metrics.GoldAmountText = goldAmount;
				metrics.GoldAmountSize = amountSize;
				metrics.GoldAmountPos = new Vector2(centerX - amountSize.X / 2f, goldStartY + labelH + 4f);
			}

			if (showClimbResources && _bodyFont != null)
			{
				var overlayState = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
				var parts = GetClimbResourceRewardParts(overlayState?.ClimbResources);
				if (parts.Length > 0)
				{
					float labelH = _bodyFont.MeasureString(ClimbResourceLabelText).Y * ClimbResourceLabelScale;
					float rowH = System.Math.Max(ClimbResourceIconSize, _bodyFont.MeasureString("+9 BLACK").Y * ClimbResourceTextScale);
					float totalRowsH = rowH * parts.Length + ClimbResourceRowGap * System.Math.Max(0, parts.Length - 1);
					float totalH = labelH + 8f + totalRowsH;
					float blockTop = cursorY + RedRuleHeight + LeftColGap;
					if (metrics.HasGoldBlock)
					{
						blockTop = metrics.GoldAmountPos.Y + metrics.GoldAmountSize.Y + LeftColGap;
					}
					else
					{
						float availableH = System.Math.Max(1f, _layout.LeftInner.Bottom - blockTop);
						blockTop += System.Math.Max(0f, (availableH - totalH) / 2f);
					}

					var labelSize = _bodyFont.MeasureString(ClimbResourceLabelText) * ClimbResourceLabelScale;
					metrics.ClimbResourceLabelPos = new Vector2(centerX - labelSize.X / 2f, blockTop);
					metrics.ClimbResourceRowPositions = new Vector2[parts.Length];
					float rowY = blockTop + labelH + 8f;
					for (int i = 0; i < parts.Length; i++)
					{
						metrics.ClimbResourceRowPositions[i] = new Vector2(centerX - 72f, rowY + i * (rowH + ClimbResourceRowGap));
					}
					metrics.HasClimbResourceBlock = true;
				}
			}

			if (_bodyFont != null && _layout.ShowRightColumn)
			{
				string stageLabel = GetStageLabelText();
				var stageSize = _bodyFont.MeasureString(stageLabel) * StageLabelScale;
				float labelX = _layout.RightInner.X + (_layout.RightInner.Width - stageSize.X) / 2f;
				float labelY = _layout.RightColumn.Y + RightPaddingTop;
				metrics.StageLabelPos = new Vector2(labelX, labelY);
			}

			var proceedSize = _titleFont.MeasureString(ProceedLabelText) * ButtonTextScale;
			var r = _layout.ProceedButton;
			metrics.ProceedTextSize = proceedSize;
			metrics.ProceedTextPos = new Vector2(r.Center.X - proceedSize.X / 2f, r.Center.Y - proceedSize.Y / 2f);

			_textMetrics = metrics;
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity sceneEntity, GameTime gameTime)
		{
			var overlayEntity = EntityManager.GetEntity("QuestRewardOverlay");
			if (overlayEntity == null) return;
			var state = overlayEntity.GetComponent<QuestRewardOverlayState>();
			if (state == null) return;
			CompletePendingCloseIfReady(overlayEntity, state);
			var animation = overlayEntity.GetComponent<ModalAnimation>();
			if (state.DismissInProgress && animation?.Phase == ModalAnimationPhase.Hidden)
				return;

			bool overlayBlocksInput = state.IsOpen
				&& (animation == null || animation.Phase != ModalAnimationPhase.Hidden);
			InputContextService.EnsureContext(
				EntityManager,
				overlayEntity,
				ContextId,
				720,
				overlayBlocksInput);
			if (!state.IsOpen) return;

			if (!overlayBlocksInput)
			{
				HideProceedButton();
				StateSingleton.PreventClicking = false;
				return;
			}

			var scene = sceneEntity.GetComponent<SceneState>();
			StateSingleton.PreventClicking = scene != null && scene.Current == SceneId.Climb;

			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			bool showGold = state.RewardGold > 0;
			int rewardCardCount = _rewardCardEntities.Count;
			bool showCard = state.HasCardReward && rewardCardCount > 0;
			bool showMedal = state.HasMedalReward && !string.IsNullOrEmpty(state.RewardMedalId);
			bool showEquipment = state.HasEquipmentReward && !string.IsNullOrEmpty(state.RewardEquipmentId);
			bool showClimbResources = HasClimbResourceReward(state.ClimbResources);
			EnsureLayout(vw, vh, showGold, showCard, showMedal, showEquipment, showClimbResources, state.RewardGold, rewardCardCount, scene);

			var overlayT = overlayEntity.GetComponent<Transform>();
			if (overlayT != null) overlayT.ZOrder = ZOrder;

			if (state.HasDeckRewardOffer)
			{
				HideProceedButton();
				UpdateDeckRewardOfferControls(state, scene, gameTime);
				return;
			}

			if (showCard)
			{
				HideProceedButton();
				SyncRewardCardHitboxes(state);
				UpdateRewardCardSelection(state, gameTime);
				return;
			}

			if (showMedal && _rewardMedalEntity != null)
			{
				var medalT = _rewardMedalEntity.GetComponent<Transform>();
				var medalUi = _rewardMedalEntity.GetComponent<UIElement>();
				if (medalT != null) medalT.ZOrder = ZOrder + 1;
				if (medalUi != null) medalUi.Bounds = _layout.MedalPreviewRect;
			}
			else if (_rewardMedalEntity != null)
			{
				var medalUi = _rewardMedalEntity.GetComponent<UIElement>();
				if (medalUi != null) medalUi.Bounds = Rectangle.Empty;
			}

			if (showEquipment && _rewardEquipmentEntity != null)
			{
				var equipT = _rewardEquipmentEntity.GetComponent<Transform>();
				var equipUi = _rewardEquipmentEntity.GetComponent<UIElement>();
				if (equipT != null) equipT.ZOrder = ZOrder + 1;
				if (equipUi != null) equipUi.Bounds = _layout.MedalPreviewRect;
			}
			else if (_rewardEquipmentEntity != null)
			{
				var equipUi = _rewardEquipmentEntity.GetComponent<UIElement>();
				if (equipUi != null) equipUi.Bounds = Rectangle.Empty;
			}

			var btn = EnsureProceedButton();
			var btnUi = btn?.GetComponent<UIElement>();
			if (btnUi != null)
			{
				bool canInteract = !state.DismissInProgress && IsModalAnimationInteractive();
				btnUi.Bounds = _layout.ProceedButton;
				btnUi.IsInteractable = canInteract;
				btnUi.IsHidden = false;
				btnUi.LayerType = UILayerType.Overlay;
				var btnHotKey = btn.GetComponent<HotKey>();
				if (btnHotKey != null) btnHotKey.IsActive = canInteract;
				if (state.DismissInProgress)
				{
					btnUi.IsClicked = false;
					return;
				}
				if (btnUi.IsClicked)
				{
					btnUi.IsClicked = false;
					bool dismissToLocation = state.DismissToLocation;
					if (dismissToLocation && scene?.Current != state.DismissScene)
					{
						state.DismissInProgress = true;
						btnUi.IsInteractable = false;
						if (btnHotKey != null) btnHotKey.IsActive = false;
						RequestCloseAnimation(state, transitionAfterClose: true, state.DismissScene);
					}
					else
					{
						RequestCloseAnimation(state, transitionAfterClose: false, state.DismissScene);
					}
				}
			}
		}

		private void OpenQuestReward(ShowQuestRewardOverlay e)
		{
			EnsureOverlayEntity();
			var st = EntityManager.GetEntity("QuestRewardOverlay").GetComponent<QuestRewardOverlayState>();

			DestroyRewardCards();
			DestroyDeckRewardControls();
			DestroyRewardMedal();
			DestroyRewardEquipment();
			InvalidateCaches();
			if (!string.IsNullOrEmpty(e?.Message)) st.Message = e.Message;
			st.TitleLine1 = string.IsNullOrWhiteSpace(e?.TitleLine1) ? TitleLine1 : e.TitleLine1;
			st.TitleLine2 = string.IsNullOrWhiteSpace(e?.TitleLine2) ? TitleLine2 : e.TitleLine2;
			st.RewardGold = e?.RewardGold ?? 0;
			st.HasCardReward = e?.HasCardReward ?? false;
			st.RewardCardKey = e?.RewardCardKey ?? string.Empty;
			st.RewardCardKeys = NormalizeRewardCardKeys(e);
			st.DeckRewardOffer = CloneDeckRewardOffer(e?.DeckRewardOffer);
			st.HasMedalReward = false;
			st.RewardMedalId = string.Empty;
			st.HasEquipmentReward = false;
			st.RewardEquipmentId = string.Empty;
			st.IsEncounterReward = e?.IsEncounterReward == true;
			st.ClimbResources = e?.ClimbResources;
			st.DismissScene = e?.DismissScene ?? SceneId.Climb;
			st.DismissToLocation = st.DismissScene == SceneId.Climb;
			st.DismissInProgress = false;
			st.CardSelectionInProgress = false;
			st.SelectedRewardCardIndex = -1;
			st.CardSelectionElapsedSeconds = 0f;
			st.DeckColumnSelectionInProgress = false;
			st.SelectedDeckRewardColumnIndex = -1;
			st.DeckColumnSelectionElapsedSeconds = 0f;
			st.IsOpen = true;
			RequestOpenAnimation();

			if (st.HasDeckRewardOffer)
			{
				CreateDeckRewardOfferViews(st.DeckRewardOffer);
				st.HasCardReward = _deckRewardOptionViews.Count > 0;
			}
			else if (st.HasCardReward)
			{
				foreach (var cardKey in st.RewardCardKeys.Take(MaxRewardCardChoices))
				{
					var rewardCard = CreateRewardCard(cardKey);
					if (rewardCard != null) _rewardCardEntities.Add(rewardCard);
				}
				st.HasCardReward = _rewardCardEntities.Count > 0;
			}
		}

		private void OpenTreasureChest(TreasureChestOpened e)
		{
			EnsureOverlayEntity();
			var st = EntityManager.GetEntity("QuestRewardOverlay").GetComponent<QuestRewardOverlayState>();

			DestroyRewardCards();
			DestroyDeckRewardControls();
			DestroyRewardMedal();
			DestroyRewardEquipment();
			InvalidateCaches();
			st.Message = string.Empty;
			st.TitleLine1 = "Treasure";
			st.TitleLine2 = "Unlocked!";
			st.RewardGold = e?.RewardGold ?? 0;
			st.HasCardReward = false;
			st.RewardCardKey = string.Empty;
			st.RewardCardKeys = new List<string>();
			st.DeckRewardOffer = null;
			st.HasMedalReward = !string.IsNullOrWhiteSpace(e?.RewardMedalId);
			st.RewardMedalId = e?.RewardMedalId ?? string.Empty;
			st.HasEquipmentReward = !string.IsNullOrWhiteSpace(e?.RewardEquipmentId);
			st.RewardEquipmentId = e?.RewardEquipmentId ?? string.Empty;
			st.IsEncounterReward = false;
			st.ClimbResources = null;
			st.DismissScene = SceneId.Climb;
			st.DismissToLocation = false;
			st.DismissInProgress = false;
			st.CardSelectionInProgress = false;
			st.SelectedRewardCardIndex = -1;
			st.CardSelectionElapsedSeconds = 0f;
			st.DeckColumnSelectionInProgress = false;
			st.SelectedDeckRewardColumnIndex = -1;
			st.DeckColumnSelectionElapsedSeconds = 0f;
			st.IsOpen = true;
			RequestOpenAnimation();

			if (st.HasMedalReward && !string.IsNullOrEmpty(st.RewardMedalId))
			{
				_rewardMedalEntity = CreateRewardMedalHitbox(st.RewardMedalId);
			}
			else if (st.HasEquipmentReward && !string.IsNullOrEmpty(st.RewardEquipmentId))
			{
				_rewardEquipmentEntity = CreateRewardEquipmentHitbox(st.RewardEquipmentId);
			}
		}

		public void Open(
			string message = null,
			int rewardGold = 0,
			bool hasCardReward = false,
			string rewardCardKey = null,
			List<string> rewardCardKeys = null,
			DeckRewardOfferSave deckRewardOffer = null)
		{
			OpenQuestReward(new ShowQuestRewardOverlay
			{
				Message = message,
				RewardGold = rewardGold,
				HasCardReward = hasCardReward,
				RewardCardKey = rewardCardKey,
				RewardCardKeys = rewardCardKeys ?? new List<string>(),
				DeckRewardOffer = deckRewardOffer
			});
		}

		public void OpenEncounterRewardForSnapshot(ClimbResourceSave climbResources, DeckRewardOfferSave deckRewardOffer = null)
		{
			OpenQuestReward(new ShowQuestRewardOverlay
			{
				Message = "Encounter Complete!",
				TitleLine1 = "Encounter",
				TitleLine2 = "Complete!",
				HasCardReward = deckRewardOffer?.options != null && deckRewardOffer.options.Count > 0,
				DeckRewardOffer = deckRewardOffer,
				IsEncounterReward = true,
				ClimbResources = climbResources,
				DismissScene = SceneId.Climb,
			});
		}

		public static bool IsOverlayOpen(EntityManager entityManager)
		{
			var st = entityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			return st != null && st.IsOpen;
		}

		public static bool ShouldSuppressBattleSceneDisplay(EntityManager entityManager)
		{
			var st = entityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			return st != null && (st.IsOpen || st.DismissInProgress);
		}

		private ModalAnimation EnsureModalAnimation()
		{
			var overlay = EntityManager.GetEntity("QuestRewardOverlay");
			if (overlay == null) return null;
			var animation = overlay.GetComponent<ModalAnimation>();
			if (animation == null)
			{
				animation = new ModalAnimation { InputContextId = ContextId };
				EntityManager.AddComponent(overlay, animation);
			}
			animation.InputContextId = ContextId;
			return animation;
		}

		private void RequestOpenAnimation()
		{
			var animation = EnsureModalAnimation();
			if (animation == null) return;
			animation.RequestedVisible = true;
			if (IsSnapshotScene())
			{
				animation.Phase = ModalAnimationPhase.Visible;
				animation.ElapsedSeconds = 0f;
			}
			_pendingCloseExitSequence = -1;
			_pendingCloseTransition = false;
			_pendingCloseTransitionScene = SceneId.Climb;
		}

		private bool IsSnapshotScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Snapshot;
		}

		private void RequestCloseAnimation(QuestRewardOverlayState state, bool transitionAfterClose, SceneId transitionScene)
		{
			if (state == null) return;
			var animation = EnsureModalAnimation();
			if (animation == null)
			{
				if (transitionAfterClose)
				{
					EventManager.Publish(new ShowTransition { Scene = transitionScene });
				}
				else
				{
					CloseOverlay(state);
				}
				return;
			}

			state.DismissInProgress = true;
			DisableModalInputs();
			if (animation.Phase == ModalAnimationPhase.Hidden)
			{
				if (transitionAfterClose)
					EventManager.Publish(new ShowTransition { Scene = transitionScene });
				else
					CloseOverlay(state);
				return;
			}
			animation.RequestedVisible = false;
			_pendingCloseExitSequence = animation.Phase == ModalAnimationPhase.Exiting
				? animation.ExitSequence
				: animation.ExitSequence + 1;
			_pendingCloseTransition = transitionAfterClose;
			_pendingCloseTransitionScene = transitionScene;
		}

		private void CompletePendingCloseIfReady(Entity overlayEntity, QuestRewardOverlayState state)
		{
			if (_pendingCloseExitSequence < 0 || state == null) return;
			var animation = overlayEntity?.GetComponent<ModalAnimation>();
			if (animation == null || animation.CompletedExitSequence < _pendingCloseExitSequence) return;

			bool transition = _pendingCloseTransition;
			var transitionScene = _pendingCloseTransitionScene;
			_pendingCloseExitSequence = -1;
			_pendingCloseTransition = false;
			_pendingCloseTransitionScene = SceneId.Climb;

			if (transition)
			{
				EventManager.Publish(new ShowTransition { Scene = transitionScene });
				return;
			}

			CloseOverlay(state);
		}

		private bool IsModalAnimationInteractive()
		{
			var animation = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<ModalAnimation>();
			return animation == null || animation.Phase == ModalAnimationPhase.Visible;
		}

		private void DisableModalInputs()
		{
			HideProceedButton();
			foreach (var card in _rewardCardEntities)
			{
				var ui = card?.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.IsInteractable = false;
					ui.IsClicked = false;
				}
				var hk = card?.GetComponent<HotKey>();
				if (hk != null) hk.IsActive = false;
			}
			DisableDeckRewardControls();
		}

		internal readonly struct ClimbResourceRewardPart
		{
			public ClimbResourceRewardPart(string name, int amount)
			{
				Name = name;
				Amount = amount;
			}

			public string Name { get; }
			public int Amount { get; }
		}

		internal static bool HasClimbResourceReward(ClimbResourceSave resources)
		{
			return (resources?.red ?? 0) > 0
				|| (resources?.white ?? 0) > 0
				|| (resources?.black ?? 0) > 0;
		}

		internal static ClimbResourceRewardPart[] GetClimbResourceRewardParts(ClimbResourceSave resources)
		{
			if (resources == null) return System.Array.Empty<ClimbResourceRewardPart>();
			var parts = new List<ClimbResourceRewardPart>(3);
			if (resources.red > 0) parts.Add(new ClimbResourceRewardPart("RED", resources.red));
			if (resources.white > 0) parts.Add(new ClimbResourceRewardPart("WHITE", resources.white));
			if (resources.black > 0) parts.Add(new ClimbResourceRewardPart("BLACK", resources.black));
			return parts.ToArray();
		}

		internal static string BuildClimbResourceRewardText(ClimbResourceSave resources)
		{
			var parts = GetClimbResourceRewardParts(resources);
			if (parts.Length == 0) return string.Empty;
			return string.Join(" | ", parts.Select(p => $"+{p.Amount} {p.Name}"));
		}

		private static Color GetClimbResourceColor(string name)
		{
			return name switch
			{
				"RED" => ClimbRedColor,
				"WHITE" => ClimbWhiteColor,
				"BLACK" => ClimbBlackColor,
				_ => StageLabelColor
			};
		}

		private static List<string> NormalizeRewardCardKeys(ShowQuestRewardOverlay e)
		{
			var keys = new List<string>();
			if (e?.RewardCardKeys != null)
			{
				foreach (var key in e.RewardCardKeys)
				{
					if (!string.IsNullOrWhiteSpace(key)) keys.Add(key);
				}
			}
			if (keys.Count == 0 && !string.IsNullOrWhiteSpace(e?.RewardCardKey))
			{
				keys.Add(e.RewardCardKey);
			}
			return keys
				.Distinct(System.StringComparer.OrdinalIgnoreCase)
				.Take(MaxRewardCardChoices)
				.ToList();
		}

		private static DeckRewardOfferSave CloneDeckRewardOffer(DeckRewardOfferSave offer)
		{
			if (offer == null) return null;
			var clone = new DeckRewardOfferSave
			{
				rewardGold = offer.rewardGold,
				options = new List<DeckRewardOfferOptionSave>()
			};
			if (offer.options == null) return clone;
			foreach (var option in offer.options)
			{
				if (option == null) continue;
				clone.options.Add(new DeckRewardOfferOptionSave
				{
					kind = option.kind ?? string.Empty,
					loadoutIndex = option.loadoutIndex,
					outgoingEntryId = option.outgoingEntryId ?? string.Empty,
					outgoingCardKey = option.outgoingCardKey ?? string.Empty,
					incomingCardKey = option.incomingCardKey ?? string.Empty,
					upgradedCardKey = option.upgradedCardKey ?? string.Empty,
				});
			}
			return clone;
		}

		public void Draw()
		{
			if (_titleFont == null || !IsOverlayOpen(EntityManager)) return;
			var e = EntityManager.GetEntity("QuestRewardOverlay");
			var st = e.GetComponent<QuestRewardOverlayState>();

			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			bool showGold = st.RewardGold > 0;
			int rewardCardCount = _rewardCardEntities.Count;
			bool showCard = st.HasCardReward && rewardCardCount > 0;
			bool showMedal = st.HasMedalReward && !string.IsNullOrEmpty(st.RewardMedalId);
			bool showEquipment = st.HasEquipmentReward && !string.IsNullOrEmpty(st.RewardEquipmentId);
			bool showClimbResources = HasClimbResourceReward(st.ClimbResources);

			if (st.HasDeckRewardOffer)
			{
				var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
				bool canDraw = scene != null
					&& (scene.Current == SceneId.Battle
						|| scene.Current == SceneId.Climb
						|| scene.Current == SceneId.Snapshot);
				if (canDraw)
				{
					DrawDeckRewardOffer(st, vw, vh);
				}
				return;
			}

			if (!_layoutValid || !_textMetricsValid)
			{
				var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
				EnsureLayout(vw, vh, showGold, showCard, showMedal, showEquipment, showClimbResources, st.RewardGold, rewardCardCount, scene);
			}

			if (!_drawInBattleOrSnapshot) return;

			var render = ModalAnimationRenderState.From(e.GetComponent<ModalAnimation>(), _layout.Modal);
			if (!render.ShouldDraw) return;

			ModalOverlayChrome.DrawDim(_spriteBatch, _pixel, vw, vh, (int)System.Math.Round(DimAlpha * render.DimAlphaMultiplier));
			ModalOverlayChrome.DrawDropShadow(_spriteBatch, _pixel, _layout.Modal, DropShadowOffsetY, render.ApplyShadow(ModalOverlayPalette.DropShadow));

			_spriteBatch.Draw(_pixel, render.Transform(_layout.Modal), render.ApplyShell(ModalOverlayPalette.ModalFill));
			_spriteBatch.Draw(_pixel, render.Transform(_layout.LeftColumn), render.ApplyShell(LeftColTint));
			if (_layout.ShowRightColumn)
			{
				_spriteBatch.Draw(_pixel, render.Transform(_layout.Divider), render.ApplyShell(ColumnDivider));
			}
			if (_layout.Footer.Height > 0)
			{
				_spriteBatch.Draw(_pixel, render.Transform(_layout.Footer), render.ApplyShell(ModalOverlayPalette.FooterFill));
				_spriteBatch.Draw(_pixel, render.Transform(new Rectangle(_layout.Footer.X, _layout.Footer.Y, _layout.Footer.Width, 1)), render.ApplyShell(ModalOverlayPalette.FooterBorderTop));
			}
			ModalOverlayChrome.DrawInsetHighlight(_spriteBatch, _pixel, render.Transform(_layout.Content));
			ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, render.Transform(_layout.Modal), render.ApplyShell(ModalOverlayPalette.PanelBorder), BorderThickness);

			// 4. Left column text
			DrawLeftColumn(render);

			// 5. Right column: label above card
			if (_layout.ShowRightColumn)
			{
				DrawStageLabel(render);
				DrawRightColumnCard(showCard, render);
				DrawRightColumnMedal(showMedal, st.RewardMedalId, render);
				DrawRightColumnEquipment(showEquipment, st.RewardEquipmentId, render);
			}

			if (!showCard)
			{
				var btn = EntityManager.GetEntity("QuestRewardProceedButton");
				bool hovered = btn?.GetComponent<UIElement>()?.IsHovered ?? false;
				DrawProceedButton(hovered, render);
			}
		}

		private QuestRewardLayout ComputeLayout(int vw, int vh, bool showGold, bool showCard, bool showMedal, bool showClimbResources, int rewardCardCount, LayoutSignature sig)
		{
			int modalW = sig.ModalWidth;
			bool showLeftReward = showGold || showClimbResources;
			if (showLeftReward && !showCard && !showMedal)
			{
				modalW = sig.GoldOnlyModalWidth;
			}
			else if (!showLeftReward && !showCard && !showMedal)
			{
				modalW = System.Math.Max(sig.LeftColWidth + sig.BorderThickness * 2, 320);
			}

			int modalH = System.Math.Max(200, sig.IsTreasureChest ? sig.TreasureChestModalHeight : sig.QuestRewardModalHeight);
			int border = System.Math.Max(1, sig.BorderThickness);
			int modalX = (vw - modalW) / 2;
			int modalY = (vh - modalH) / 2;

			var modal = new Rectangle(modalX, modalY, modalW, modalH);
			var content = new Rectangle(
				modal.X + border,
				modal.Y + border,
				System.Math.Max(1, modal.Width - border * 2),
				System.Math.Max(1, modal.Height - border * 2));

			int footerH = showCard ? 0 : sig.FooterPadding * 2 + System.Math.Max(30, sig.ButtonHeight);
			int bodyH = System.Math.Max(1, content.Height - footerH);
			var footer = new Rectangle(content.X, content.Y + bodyH, content.Width, footerH);
			var body = new Rectangle(content.X, content.Y, content.Width, bodyH);

			bool showRightColumn = showCard || showMedal;
			int leftW = showRightColumn ? System.Math.Min(sig.LeftColWidth, body.Width) : body.Width;
			var leftColumn = new Rectangle(body.X, body.Y, leftW, body.Height);
			var leftInner = new Rectangle(
				leftColumn.X + sig.LeftPaddingX,
				leftColumn.Y + sig.LeftPaddingTop,
				System.Math.Max(1, leftColumn.Width - sig.LeftPaddingX * 2),
				System.Math.Max(1, leftColumn.Height - sig.LeftPaddingTop - sig.LeftPaddingBottom));

			Rectangle divider = Rectangle.Empty;
			Rectangle rightColumn = Rectangle.Empty;
			Rectangle rightInner = Rectangle.Empty;
			Vector2 cardCenter = Vector2.Zero;
			Vector2[] cardChoiceCenters = System.Array.Empty<Vector2>();
			Rectangle medalPreviewRect = Rectangle.Empty;
			float stageLabelH = 0f;

			if (showRightColumn)
			{
				int rightX = body.X + leftW;
				int rightW = System.Math.Max(1, body.Width - leftW);
				divider = new Rectangle(rightX - 1, body.Y, 1, body.Height);
				rightColumn = new Rectangle(rightX, body.Y, rightW, body.Height);
				rightInner = new Rectangle(
					rightColumn.X + sig.RightPaddingX,
					rightColumn.Y + sig.RightPaddingTop,
					System.Math.Max(1, rightColumn.Width - sig.RightPaddingX * 2),
					System.Math.Max(1, rightColumn.Height - sig.RightPaddingTop - sig.RightPaddingBottom));

				float labelTop = rightColumn.Y + sig.RightPaddingTop;
				stageLabelH = _bodyFont != null
					? _bodyFont.MeasureString(StageLabelText).Y * sig.StageLabelScale
					: 16f;
				float labelBottom = labelTop + stageLabelH;
				float cardTop = labelBottom + sig.StageLabelGap;
				float cardHalfH = sig.CardHeight * sig.CardPreviewScale / 2f + sig.CardOffsetYExtra * sig.CardPreviewScale;
				cardCenter = new Vector2(
					rightInner.X + rightInner.Width / 2f + sig.CardPreviewOffsetX,
					cardTop + cardHalfH + sig.CardPreviewOffsetY);
				if (showCard)
				{
					int cardCount = System.Math.Max(1, rewardCardCount);
					cardChoiceCenters = new Vector2[cardCount];
					if (cardCount == 1)
					{
						cardChoiceCenters[0] = cardCenter;
					}
					else
					{
						float cardW = sig.CardWidth * sig.CardPreviewScale;
						float totalW = cardW * cardCount + sig.CardChoiceGap * (cardCount - 1);
						float startX = cardCenter.X - totalW / 2f + cardW / 2f;
						for (int i = 0; i < cardCount; i++)
						{
							cardChoiceCenters[i] = new Vector2(
								startX + i * (cardW + sig.CardChoiceGap),
								cardCenter.Y);
						}
					}
				}

				if (showMedal)
				{
					int medalSize = System.Math.Max(40, MedalPreviewSize);
					int medalX = rightInner.X + (rightInner.Width - medalSize) / 2;
					int medalY = (int)(cardTop + sig.CardPreviewOffsetY);
					medalPreviewRect = new Rectangle(medalX, medalY, medalSize, medalSize);
				}
			}

			var proceedButton = Rectangle.Empty;
			if (!showCard)
			{
				int bw = System.Math.Max(60, sig.ButtonWidth);
				int bh = System.Math.Max(30, sig.ButtonHeight);
				int bx = content.X + (content.Width - bw) / 2;
				int by = footer.Y + sig.FooterPadding;
				proceedButton = new Rectangle(bx, by, bw, bh);
			}

			return new QuestRewardLayout
			{
				Modal = modal,
				Content = content,
				LeftColumn = leftColumn,
				LeftInner = leftInner,
				Divider = divider,
				RightColumn = rightColumn,
				RightInner = rightInner,
				Footer = footer,
				ProceedButton = proceedButton,
				CardCenter = cardCenter,
				CardChoiceCenters = cardChoiceCenters,
				MedalPreviewRect = medalPreviewRect,
				StageLabelHeight = stageLabelH,
				ShowRightColumn = showRightColumn,
				ShowGold = showGold,
				ShowCard = showCard,
				ShowMedal = showMedal
			};
		}

		private void DrawLeftColumn(ModalAnimationRenderState render)
		{
			var m = _textMetrics;
			string line1 = _layoutSignature.TitleLine1;
			string line2 = _layoutSignature.TitleLine2;

			_spriteBatch.DrawString(_titleFont, line1, render.Transform(m.TitleLine1Pos), render.ApplyShell(ModalOverlayPalette.TitleColor), 0f, Vector2.Zero, render.TransformScale(TitleScale), SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_titleFont, line2, render.Transform(m.TitleLine2Pos), render.ApplyShell(ModalOverlayPalette.TitleColor), 0f, Vector2.Zero, render.TransformScale(TitleScale), SpriteEffects.None, 0f);

			Vector2 ruleCenter = render.Transform(new Vector2(_layout.LeftInner.Center.X, m.RuleY));
			_gradientRuleCache.DrawRule(
				_spriteBatch,
				(int)System.Math.Round(ruleCenter.X),
				(int)System.Math.Round(ruleCenter.Y),
				(int)System.Math.Round(RedRuleWidth * render.ShellScale),
				(int)System.Math.Round(RedRuleHeight * render.ShellScale),
				render.ApplyShell(Color.White));

			if (m.HasGoldBlock && _bodyFont != null)
			{
				_spriteBatch.DrawString(_bodyFont, GoldLabelText, render.Transform(m.GoldLabelPos),
					render.ApplyShell(GoldLabelColor), 0f, Vector2.Zero, render.TransformScale(GoldLabelScale), SpriteEffects.None, 0f);
			}

			if (m.HasGoldBlock)
			{
				DrawGoldGlow(m.GoldAmountText, render.Transform(m.GoldAmountPos), render.TransformScale(GoldAmountScale), render);
				_spriteBatch.DrawString(_titleFont, m.GoldAmountText, render.Transform(m.GoldAmountPos), render.ApplyShell(GoldAmountColor), 0f, Vector2.Zero, render.TransformScale(GoldAmountScale), SpriteEffects.None, 0f);
			}

			DrawClimbResourceBlock(render);
		}

		private void DrawClimbResourceBlock(ModalAnimationRenderState render)
		{
			if (_bodyFont == null || !_textMetrics.HasClimbResourceBlock) return;
			var state = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			var parts = GetClimbResourceRewardParts(state?.ClimbResources);
			if (parts.Length == 0) return;

			_spriteBatch.DrawString(
				_bodyFont,
				ClimbResourceLabelText,
				render.Transform(_textMetrics.ClimbResourceLabelPos),
				render.ApplyShell(StageLabelColor),
				0f,
				Vector2.Zero,
				render.TransformScale(ClimbResourceLabelScale),
				SpriteEffects.None,
				0f);

			for (int i = 0; i < parts.Length && i < _textMetrics.ClimbResourceRowPositions.Length; i++)
			{
				var part = parts[i];
				var pos = render.Transform(_textMetrics.ClimbResourceRowPositions[i]);
				int iconSize = System.Math.Max(4, (int)System.Math.Round(ClimbResourceIconSize * render.ShellScale));
				var iconRect = new Rectangle((int)pos.X, (int)pos.Y + 2, iconSize, iconSize);
				_spriteBatch.Draw(_pixel, iconRect, render.ApplyShell(GetClimbResourceColor(part.Name) * 0.90f));
				ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, iconRect, render.ApplyShell(Color.White * 0.22f), 1);

				string text = $"+{part.Amount} {part.Name}";
				_spriteBatch.DrawString(
					_bodyFont,
					text,
					new Vector2(pos.X + iconSize + 10f * render.ShellScale, pos.Y),
					render.ApplyShell(StageLabelColor),
					0f,
					Vector2.Zero,
					render.TransformScale(ClimbResourceTextScale),
					SpriteEffects.None,
					0f);
			}
		}

		private void DrawRightColumnCard(bool showCard, ModalAnimationRenderState render)
		{
			if (!showCard || _rewardCardEntities.Count == 0) return;
			var st = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			for (int i = 0; i < _rewardCardEntities.Count; i++)
			{
				var card = _rewardCardEntities[i];
				if (card == null || !card.IsActive) continue;
				float scale = GetRewardCardDisplayScale(i, st);
				if (scale <= 0.001f) continue;
				var t = card.GetComponent<Transform>();
				var drawCenter = t?.Position ?? GetRewardCardCenter(i);
				EventManager.Publish(new CardRenderScaledRotatedEvent
				{
					Card = card,
					Position = render.Transform(drawCenter),
					Scale = render.TransformScale(scale)
				});
			}
		}

		private void DrawRightColumnMedal(bool showMedal, string medalId, ModalAnimationRenderState render)
		{
			if (!showMedal || string.IsNullOrWhiteSpace(medalId)) return;

			var r = _layout.MedalPreviewRect;
			if (r.Width <= 0 || r.Height <= 0) return;

			var center = render.Transform(new Vector2(r.Center.X, r.Center.Y));
			int iconSize = (int)System.Math.Round(System.Math.Min(r.Width, r.Height) * render.ShellScale);
			MedalIconRenderService.DrawMedalIcon(
				_spriteBatch,
				_graphicsDevice,
				_titleFont,
				center,
				iconSize,
				medalId,
				_imageAssets);
		}

		private void DrawStageLabel(ModalAnimationRenderState render)
		{
			if (_bodyFont == null || !_layout.ShowRightColumn) return;
			_spriteBatch.DrawString(_bodyFont, GetStageLabelText(),
				render.Transform(_textMetrics.StageLabelPos),
				render.ApplyShell(StageLabelColor), 0f, Vector2.Zero, render.TransformScale(StageLabelScale), SpriteEffects.None, 0f);
		}

		private string GetStageLabelText()
		{
			var state = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<QuestRewardOverlayState>();
			return state?.DismissToLocation == true ? QuestStageLabelText : StageLabelText;
		}

		private void DrawProceedButton(bool hovered, ModalAnimationRenderState render)
		{
			ModalOverlayChrome.DrawActionButton(
				_spriteBatch,
				_pixel,
				render.Transform(_layout.ProceedButton),
				hovered,
				BorderThickness,
				_titleFont,
				ProceedLabelText,
				render.Transform(_textMetrics.ProceedTextPos),
				render.TransformScale(ButtonTextScale),
				render.ApplyShell(Color.White));
		}

		private void DrawGoldGlow(string text, Vector2 pos, float scale, ModalAnimationRenderState render)
		{
			var glow = render.ApplyShell(GoldAmountColor * 0.35f);
			_spriteBatch.DrawString(_titleFont, text, pos + new Vector2(-2, 0), glow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_titleFont, text, pos + new Vector2(2, 0), glow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_titleFont, text, pos + new Vector2(0, -2), glow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(_titleFont, text, pos + new Vector2(0, 2), glow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawDeckRewardOffer(QuestRewardOverlayState state, int vw, int vh)
		{
			if (state?.DeckRewardOffer?.options == null) return;
			int colCount = state.DeckRewardOffer.options.Count;
			var isUpgradeFlags = new bool[colCount];
			for (int i = 0; i < colCount; i++)
				isUpgradeFlags[i] = string.Equals(state.DeckRewardOffer.options[i]?.kind, DeckRewardOfferKinds.Upgrade, System.StringComparison.OrdinalIgnoreCase);
			var layout = ComputeDeckRewardOfferLayout(vw, vh, colCount, isUpgradeFlags);
			var render = ModalAnimationRenderState.From(EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<ModalAnimation>(), layout.Modal);
			if (!render.ShouldDraw) return;

			ModalOverlayChrome.DrawDim(_spriteBatch, _pixel, vw, vh, (int)System.Math.Round(DimAlpha * render.DimAlphaMultiplier));
			ModalOverlayChrome.DrawDropShadow(_spriteBatch, _pixel, layout.Modal, DropShadowOffsetY, render.ApplyShadow(ModalOverlayPalette.DropShadow));
			_spriteBatch.Draw(_pixel, render.Transform(layout.Modal), render.ApplyShell(ModalOverlayPalette.ModalFill));
			_spriteBatch.Draw(_pixel, render.Transform(layout.Footer), render.ApplyShell(ModalOverlayPalette.FooterFill));
			_spriteBatch.Draw(_pixel, render.Transform(new Rectangle(layout.Footer.X, layout.Footer.Y, layout.Footer.Width, 1)), render.ApplyShell(ModalOverlayPalette.FooterBorderTop));
			_spriteBatch.Draw(_pixel, render.Transform(new Rectangle(layout.Masthead.X, layout.Masthead.Bottom - 1, layout.Masthead.Width, 1)), render.ApplyShell(new Color(255, 255, 255) * 0.08f));
			ModalOverlayChrome.DrawInsetHighlight(_spriteBatch, _pixel, render.Transform(layout.Content));
			ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, render.Transform(layout.Modal), render.ApplyShell(ModalOverlayPalette.PanelBorder), BorderThickness);

			string title = !string.IsNullOrWhiteSpace(state.TitleLine1) || !string.IsNullOrWhiteSpace(state.TitleLine2)
				? $"{state.TitleLine1} {state.TitleLine2}".Trim()
				: DeckMastheadTitle;
			DrawDeckRewardMasthead(layout, title, render);

			for (int i = 0; i < colCount && i < layout.Columns.Length; i++)
			{
				var option = state.DeckRewardOffer.options[i];
				if (option == null) continue;
				bool isUpgrade = i < layout.IsUpgrade.Length && layout.IsUpgrade[i];
				bool selectionAnimating = state.DeckColumnSelectionInProgress;
				bool hovered = !selectionAnimating
					&& i < _deckRewardOptionViews.Count
					&& ((_deckRewardOptionViews[i].Lane?.GetComponent<UIElement>()?.IsHovered ?? false)
						|| (_deckRewardOptionViews[i].OutgoingCard?.GetComponent<UIElement>()?.IsHovered ?? false)
						|| (_deckRewardOptionViews[i].IncomingCard?.GetComponent<UIElement>()?.IsHovered ?? false));
				float columnAlpha = GetDeckColumnChromeAlpha(i, state, DeckColumnSelectionAnimationSeconds);
				DrawDeckRewardColumn(layout, i, isUpgrade, hovered, columnAlpha, render);

				if (i < _deckRewardOptionViews.Count)
				{
					var view = _deckRewardOptionViews[i];
					DrawDeckRewardPreviewCard(
						view.OutgoingCard,
						layout.OutgoingCardCenters[i],
						i,
						isOutgoing: true,
						state,
						render);
					DrawDeckRewardPreviewCard(
						view.IncomingCard,
						layout.IncomingCardCenters[i],
						i,
						isOutgoing: false,
						state,
						render);
				}
			}

			var skipUi = _deckRewardSkipButton?.GetComponent<UIElement>();
			bool skipHovered = skipUi?.IsHovered ?? false;
			DrawDeckRewardSkipButton(layout, skipHovered, render);
		}

		private void DrawDeckRewardMasthead(DeckRewardOfferLayout layout, string titleText, ModalAnimationRenderState render)
		{
			float titleScale = render.TransformScale(DeckMastheadTitleScale);
			var titleSize = _titleFont.MeasureString(titleText) * titleScale;
			Vector2 titleAnchor = render.Transform(new Vector2(layout.Masthead.Center.X, layout.Masthead.Y + DeckMastheadPadTop));
			float titleX = titleAnchor.X - titleSize.X / 2f;
			_spriteBatch.DrawString(_titleFont, titleText,
				new Vector2(titleX, titleAnchor.Y),
				render.ApplyShell(ModalOverlayPalette.TitleColor), 0f, Vector2.Zero,
				titleScale, SpriteEffects.None, 0f);

			int ruleY = (int)(layout.Masthead.Y + DeckMastheadPadTop + titleSize.Y + 10);
			Vector2 ruleCenterPos = render.Transform(new Vector2(layout.Masthead.Center.X, ruleY));
			int ruleCenterX = (int)System.Math.Round(ruleCenterPos.X);
			int ruleHalfW = (int)System.Math.Round(System.Math.Max(30, RedRuleWidth) * render.ShellScale / 2f);
			int ruleX = ruleCenterX - ruleHalfW;
			var ruleRect = new Rectangle(ruleX, (int)System.Math.Round(ruleCenterPos.Y), ruleHalfW * 2, System.Math.Max(1, (int)System.Math.Round(RedRuleHeight * render.ShellScale)));
			var ruleCenter = new Color(196, 30, 58);
			var ruleEdge = new Color(196, 30, 58) * 0.0f;
			float segmentW = System.Math.Max(1f, ruleRect.Width / 8f);
			for (int seg = 0; seg < 8; seg++)
			{
				float t = seg / 7f;
				float alphaT = t < 0.5f ? t * 2f : (1f - t) * 2f;
				var segColor = Color.Lerp(ruleEdge, ruleCenter, alphaT);
				int segX = (int)(ruleRect.X + seg * segmentW);
				int segW = ((seg == 7) ? ruleRect.Right : (int)(ruleRect.X + (seg + 1) * segmentW)) - segX;
				_spriteBatch.Draw(_pixel, new Rectangle(segX, ruleRect.Y, segW, ruleRect.Height), render.ApplyShell(segColor));
			}
		}

		private void DrawDeckRewardColumn(DeckRewardOfferLayout layout, int index, bool isUpgrade, bool hovered, float columnAlpha, ModalAnimationRenderState render)
		{
			var col = render.Transform(layout.Columns[index]);
			int topBarH = System.Math.Max(1, ColumnTopBarThickness);

			if (isUpgrade)
			{
				var fillTop = render.ApplyShell((hovered ? UpgradeColHoverFillTop : ExchangeColFillTop) * columnAlpha);
				var topBar = render.ApplyShell((hovered ? UpgradeColBorderTopHover : UpgradeColBorderTop) * columnAlpha);
				var sideBorder = render.ApplyShell((hovered ? UpgradeColBorderSideHover : ExchangeColBorderSide) * columnAlpha);

				_spriteBatch.Draw(_pixel, col, fillTop);
				_spriteBatch.Draw(_pixel, new Rectangle(col.X, col.Y, col.Width, topBarH), topBar);
				ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, col, sideBorder, 1);
			}
			else
			{
				var fillTop = render.ApplyShell((hovered ? ExchangeColHoverFillTop : ExchangeColFillTop) * columnAlpha);
				var fillMid = render.ApplyShell((hovered ? ExchangeColHoverFillMid : ExchangeColFillMid) * columnAlpha);
				var topBar = render.ApplyShell((hovered ? ExchangeColBorderTopHover : ExchangeColBorderTop) * columnAlpha);
				var sideBorder = render.ApplyShell((hovered ? ExchangeColBorderSideHover : ExchangeColBorderSide) * columnAlpha);

				int thirdH = col.Height / 3;
				_spriteBatch.Draw(_pixel, new Rectangle(col.X, col.Y, col.Width, thirdH), fillTop);
				_spriteBatch.Draw(_pixel, new Rectangle(col.X, col.Y + thirdH, col.Width, col.Height - thirdH * 2), fillMid);
				_spriteBatch.Draw(_pixel, new Rectangle(col.X, col.Bottom - thirdH, col.Width, thirdH), fillTop);
				_spriteBatch.Draw(_pixel, new Rectangle(col.X, col.Y, col.Width, topBarH), topBar);
				ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, col, sideBorder, 1);
			}

			if (isUpgrade)
				DrawUpgradePlus(render.Transform(layout.ArrowCenters[index]), render.TransformScale(ArrowScale), columnAlpha * render.ShellAlpha);
			else
				DrawTradeArrow(render.Transform(layout.ArrowCenters[index]), render.TransformScale(ArrowScale), columnAlpha * render.ShellAlpha);
		}

		private void DrawDeckRewardPreviewCard(
			Entity card,
			Vector2 position,
			int columnIndex,
			bool isOutgoing,
			QuestRewardOverlayState state,
			ModalAnimationRenderState render)
		{
			if (card == null || !card.IsActive) return;

			float scale = 1f;
			float alpha = 1f;
			if (state != null && state.DeckColumnSelectionInProgress && state.SelectedDeckRewardColumnIndex >= 0)
			{
				ComputeDeckColumnSelectionVisual(
					columnIndex,
					state.SelectedDeckRewardColumnIndex,
					isOutgoing,
					state.DeckColumnSelectionElapsedSeconds,
					DeckColumnSelectionAnimationSeconds,
					DeckColumnPulseScaleAmplitude,
					DeckColumnPulseFrequencyHz,
					out scale,
					out alpha);
			}

			if (scale <= 0.001f || alpha <= 0.001f) return;

			var t = card.GetComponent<Transform>();
			var drawCenter = t?.Position ?? position;
			EventManager.Publish(new CardRenderScaledEvent
			{
				Card = card,
				Position = render.Transform(drawCenter),
				Scale = render.TransformScale(scale),
				Alpha = alpha * render.ShellAlpha
			});
		}

		private void DrawTradeArrow(Vector2 center, float scale, float alpha)
		{
			float h = TradeArrowHeight * scale;
			float w = TradeArrowWidth * scale;
			float halfW = w / 2f;
			float shaftW = System.Math.Max(2f, w * 0.32f);
			float headH = h * 0.28f;
			var color = TradeArrowColor * alpha;

			float top = center.Y - h / 2f;
			float shaftBottom = top + h - headH;
			_spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - shaftW / 2f), (int)top, (int)shaftW, (int)(shaftBottom - top)), color);

			Vector2 pLeft = new Vector2(center.X - halfW, shaftBottom);
			Vector2 pRight = new Vector2(center.X + halfW, shaftBottom);
			Vector2 pTip = new Vector2(center.X, top + h);
			DrawTriangle(_pixel, _spriteBatch, pLeft, pRight, pTip, color);
		}

		private void DrawUpgradePlus(Vector2 center, float scale, float alpha)
		{
			float size = UpgradePlusSize * scale;
			float half = size / 2f;
			float thick = System.Math.Max(2f, size * 0.15f);
			var color = UpgradePlusColor * alpha;
			_spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - half), (int)(center.Y - thick / 2f), (int)size, (int)thick), color);
			_spriteBatch.Draw(_pixel, new Rectangle((int)(center.X - thick / 2f), (int)(center.Y - half), (int)thick, (int)size), color);
		}

		private static void DrawTriangle(Texture2D pixel, SpriteBatch sb, Vector2 a, Vector2 b, Vector2 c, Color color)
		{
			Vector2[] verts = { a, b, c };
			System.Array.Sort(verts, (v1, v2) => v1.Y.CompareTo(v2.Y));
			Vector2 top = verts[0], mid = verts[1], bot = verts[2];
			for (int y = (int)top.Y; y <= (int)bot.Y; y++)
			{
				if (y < 0) continue;
				float tY = (bot.Y - top.Y) <= 0f ? 0f : (y - top.Y) / (bot.Y - top.Y);
				Vector2 leftEdge = Vector2.Lerp(top, bot, tY);
				Vector2 rightEdge;
				float midT;
				if (y <= mid.Y)
				{
					midT = (mid.Y - top.Y) <= 0f ? 0f : (y - top.Y) / (mid.Y - top.Y);
					rightEdge = Vector2.Lerp(top, mid, midT);
				}
				else
				{
					midT = (bot.Y - mid.Y) <= 0f ? 0f : (y - mid.Y) / (bot.Y - mid.Y);
					rightEdge = Vector2.Lerp(mid, bot, midT);
				}
				float left = System.Math.Min(leftEdge.X, rightEdge.X);
				float right = System.Math.Max(leftEdge.X, rightEdge.X);
				sb.Draw(pixel, new Rectangle((int)left, y, (int)(right - left) + 1, 1), color);
			}
		}

		private void DrawDeckRewardSkipButton(DeckRewardOfferLayout layout, bool hovered, ModalAnimationRenderState render)
		{
			var r = render.Transform(layout.SkipButton);
			var fill = render.ApplyShell(hovered ? SkipButtonBgHover : Color.Transparent);
			var border = render.ApplyShell(hovered ? SkipButtonBorderHover : SkipButtonBorderDefault);
			var textColor = render.ApplyShell(hovered ? Color.White : StageLabelColor);
			int borderThick = System.Math.Max(2, BorderThickness);
			_spriteBatch.Draw(_pixel, r, fill);
			ModalOverlayChrome.DrawBorder(_spriteBatch, _pixel, r, border, borderThick);
			if (_bodyFont != null)
			{
				var textScale = render.TransformScale(DeckSkipButtonTextScale);
				var textSize = _bodyFont.MeasureString(SkipRewardLabelText) * textScale;
				var textPos = new Vector2(r.Center.X - textSize.X / 2f, r.Center.Y - textSize.Y / 2f);
				_spriteBatch.DrawString(_bodyFont, SkipRewardLabelText, textPos, textColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
			}
		}

		private void DrawString(SpriteFont font, string text, Vector2 position, float scale, Color color)
		{
			if (font == null || string.IsNullOrEmpty(text)) return;
			_spriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawCenteredString(SpriteFont font, string text, Vector2 centerTop, float scale, Color color)
		{
			if (font == null || string.IsNullOrEmpty(text)) return;
			var size = font.MeasureString(text) * scale;
			var pos = new Vector2(centerTop.X - size.X / 2f, centerTop.Y);
			_spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void CloseOverlay(QuestRewardOverlayState state)
		{
			var animation = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<ModalAnimation>();
			if (animation != null)
			{
				animation.RequestedVisible = false;
				animation.Phase = ModalAnimationPhase.Hidden;
				animation.ElapsedSeconds = 0f;
			}
			_pendingCloseExitSequence = -1;
			_pendingCloseTransition = false;
			_pendingCloseTransitionScene = SceneId.Climb;

			if (state.IsEncounterReward)
			{
				ClimbEncounterService.ResolvePendingEncounterReward(EntityManager);
			}

			state.IsOpen = false;
			state.DismissInProgress = false;
			state.RewardGold = 0;
			state.HasCardReward = false;
			state.RewardCardKey = string.Empty;
			state.RewardCardKeys = new List<string>();
			state.DeckRewardOffer = null;
			state.HasMedalReward = false;
			state.RewardMedalId = string.Empty;
			state.HasEquipmentReward = false;
			state.RewardEquipmentId = string.Empty;
			state.IsEncounterReward = false;
			state.ClimbResources = null;
			state.DismissScene = SceneId.Climb;
			state.CardSelectionInProgress = false;
			state.SelectedRewardCardIndex = -1;
			state.CardSelectionElapsedSeconds = 0f;
			state.DeckColumnSelectionInProgress = false;
			state.SelectedDeckRewardColumnIndex = -1;
			state.DeckColumnSelectionElapsedSeconds = 0f;
			StateSingleton.PreventClicking = false;
			HideProceedButton();
			DestroyRewardCards();
			DestroyDeckRewardControls();
			DestroyRewardMedal();
			DestroyRewardEquipment();
			InvalidateCaches();

		}

		private void HideProceedButton()
		{
			var btn = EntityManager.GetEntity("QuestRewardProceedButton");
			if (btn == null) return;

			var ui = btn.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.Bounds = Rectangle.Empty;
				ui.IsInteractable = false;
				ui.IsHidden = true;
				ui.IsClicked = false;
				ui.LayerType = UILayerType.Overlay;
			}

			var hotKey = btn.GetComponent<HotKey>();
			if (hotKey != null) hotKey.IsActive = false;
		}

		private void DestroyRewardCards()
		{
			foreach (var card in _rewardCardEntities.ToList())
			{
				if (card != null)
				{
					EntityManager.DestroyEntity(card.Id);
				}
			}
			_rewardCardEntities.Clear();
			_deckRewardOptionViews.Clear();
		}

		private void DestroyDeckRewardControls()
		{
			foreach (var lane in _deckRewardLaneEntities.ToList())
			{
				if (lane != null) EntityManager.DestroyEntity(lane.Id);
			}
			_deckRewardLaneEntities.Clear();

			if (_deckRewardSkipButton != null)
			{
				EntityManager.DestroyEntity(_deckRewardSkipButton.Id);
				_deckRewardSkipButton = null;
			}
		}

		private void CreateDeckRewardOfferViews(DeckRewardOfferSave offer)
		{
			if (offer?.options == null) return;
			for (int i = 0; i < offer.options.Count; i++)
			{
				var option = offer.options[i];
				if (option == null) continue;

				string incomingKey = string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, System.StringComparison.OrdinalIgnoreCase)
					? option.upgradedCardKey
					: option.incomingCardKey;
				var outgoing = CreateRewardCard(option.outgoingCardKey);
				var incoming = CreateRewardCard(incomingKey);
				if (outgoing == null || incoming == null)
				{
					if (outgoing != null) EntityManager.DestroyEntity(outgoing.Id);
					if (incoming != null) EntityManager.DestroyEntity(incoming.Id);
					continue;
				}

				ApplyDeckRewardPreviewRestrictions(outgoing, option, forIncomingCard: false);
				ApplyDeckRewardPreviewRestrictions(incoming, option, forIncomingCard: true);
				EntityManager.AddComponent(incoming, new CardSheen());

				_rewardCardEntities.Add(outgoing);
				_rewardCardEntities.Add(incoming);
				var lane = EnsureDeckRewardLaneEntity(i);
				_deckRewardOptionViews.Add(new DeckRewardOptionView
				{
					Lane = lane,
					OutgoingCard = outgoing,
					IncomingCard = incoming
				});
			}
		}

		private Entity EnsureDeckRewardLaneEntity(int index)
		{
			while (_deckRewardLaneEntities.Count <= index)
			{
				int next = _deckRewardLaneEntities.Count;
				var ent = EntityManager.CreateEntity($"QuestRewardDeckLane_{next}");
				EntityManager.AddComponent(ent, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 10 + next });
				EntityManager.AddComponent(ent, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					EventType = UIElementEventType.None,
					LayerType = UILayerType.Overlay
				});
				EntityManager.AddComponent(ent, ParallaxLayer.GetUIParallaxLayer());
				EntityManager.AddComponent(ent, new DontDestroyOnLoad());
				InputContextService.EnsureMember(EntityManager, ent, ContextId);
				_deckRewardLaneEntities.Add(ent);
			}
			return _deckRewardLaneEntities[index];
		}

		private Entity EnsureDeckRewardSkipButton()
		{
			if (_deckRewardSkipButton != null && _deckRewardSkipButton.IsActive) return _deckRewardSkipButton;

			_deckRewardSkipButton = EntityManager.CreateEntity("QuestRewardSkipButton");
			EntityManager.AddComponent(_deckRewardSkipButton, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 4 });
			EntityManager.AddComponent(_deckRewardSkipButton, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = false,
				EventType = UIElementEventType.None,
				LayerType = UILayerType.Overlay
			});
			EntityManager.AddComponent(_deckRewardSkipButton, new HotKey { Button = FaceButton.Y, IsActive = false });
			EntityManager.AddComponent(_deckRewardSkipButton, ParallaxLayer.GetUIParallaxLayer());
			EntityManager.AddComponent(_deckRewardSkipButton, new DontDestroyOnLoad());
			InputContextService.EnsureMember(EntityManager, _deckRewardSkipButton, ContextId);
			return _deckRewardSkipButton;
		}

		private void UpdateDeckRewardOfferControls(QuestRewardOverlayState state, SceneState scene, GameTime gameTime)
		{
			if (state?.DeckRewardOffer?.options == null) return;
			UpdateDeckRewardCardSheens(state);
			int colCount = state.DeckRewardOffer.options.Count;
			var isUpgradeFlags = new bool[colCount];
			for (int i = 0; i < colCount; i++)
				isUpgradeFlags[i] = string.Equals(state.DeckRewardOffer.options[i]?.kind, DeckRewardOfferKinds.Upgrade, System.StringComparison.OrdinalIgnoreCase);
			var layout = ComputeDeckRewardOfferLayout(Game1.VirtualWidth, Game1.VirtualHeight, colCount, isUpgradeFlags);

			if (state.DeckColumnSelectionInProgress)
			{
				state.DeckColumnSelectionElapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
				for (int i = 0; i < _deckRewardOptionViews.Count; i++)
				{
					var view = _deckRewardOptionViews[i];
					PreparePreviewCard(view.OutgoingCard, i * 2, state);
					PreparePreviewCard(view.IncomingCard, i * 2 + 1, state);
					if (i < layout.OutgoingCardCenters.Length)
						SyncCardLayoutAnchor(view.OutgoingCard?.GetComponent<Transform>(), view.OutgoingCard?.GetComponent<PositionTween>(), layout.OutgoingCardCenters[i]);
					if (i < layout.IncomingCardCenters.Length)
						SyncCardLayoutAnchor(view.IncomingCard?.GetComponent<Transform>(), view.IncomingCard?.GetComponent<PositionTween>(), layout.IncomingCardCenters[i]);
					var laneUi = view.Lane?.GetComponent<UIElement>();
					if (laneUi != null)
					{
						laneUi.Bounds = i < layout.Columns.Length ? layout.Columns[i] : Rectangle.Empty;
						laneUi.IsInteractable = false;
						laneUi.IsClicked = false;
						laneUi.LayerType = UILayerType.Overlay;
					}
				}

				var skipDuringAnim = _deckRewardSkipButton?.GetComponent<UIElement>();
				if (skipDuringAnim != null)
				{
					skipDuringAnim.Bounds = layout.SkipButton;
					skipDuringAnim.IsInteractable = false;
					skipDuringAnim.IsClicked = false;
					skipDuringAnim.LayerType = UILayerType.Overlay;
				}
				var hotKeyDuringAnim = _deckRewardSkipButton?.GetComponent<HotKey>();
				if (hotKeyDuringAnim != null) hotKeyDuringAnim.IsActive = false;

				if (state.DeckColumnSelectionElapsedSeconds >= System.Math.Max(0.05f, DeckColumnSelectionAnimationSeconds)
					&& !state.DismissInProgress)
				{
					CompleteDeckRewardOfferResolution(state, scene);
				}
				return;
			}

			bool canInteract = state.IsOpen && !state.DismissInProgress && IsModalAnimationInteractive();

			for (int i = 0; i < _deckRewardOptionViews.Count; i++)
			{
				var view = _deckRewardOptionViews[i];
				var laneUi = view.Lane?.GetComponent<UIElement>();
				if (laneUi != null)
				{
					laneUi.Bounds = i < layout.Columns.Length ? layout.Columns[i] : Rectangle.Empty;
					laneUi.IsInteractable = canInteract;
					laneUi.LayerType = UILayerType.Overlay;
					if (laneUi.IsClicked)
					{
						laneUi.IsClicked = false;
						if (QuestCardRewardService.ApplyPendingOfferOption(i))
						{
							SelectDeckRewardColumn(state, i);
							return;
						}
					}
				}

				PreparePreviewCard(view.OutgoingCard, i * 2, state);
				PreparePreviewCard(view.IncomingCard, i * 2 + 1, state);
				if (i < layout.OutgoingCardCenters.Length)
					SyncCardLayoutAnchor(view.OutgoingCard?.GetComponent<Transform>(), view.OutgoingCard?.GetComponent<PositionTween>(), layout.OutgoingCardCenters[i]);
				if (i < layout.IncomingCardCenters.Length)
					SyncCardLayoutAnchor(view.IncomingCard?.GetComponent<Transform>(), view.IncomingCard?.GetComponent<PositionTween>(), layout.IncomingCardCenters[i]);
			}

			SyncDeckRewardCardHover(layout);

			var skip = EnsureDeckRewardSkipButton();
			var skipUi = skip.GetComponent<UIElement>();
			if (skipUi != null)
			{
				skipUi.Bounds = layout.SkipButton;
				skipUi.IsInteractable = canInteract;
				skipUi.LayerType = UILayerType.Overlay;
				if (skipUi.IsClicked)
				{
					skipUi.IsClicked = false;
					QuestCardRewardService.SkipPendingOffer();
					CompleteDeckRewardOfferResolution(state, scene);
				}
			}
			var hotKey = skip.GetComponent<HotKey>();
			if (hotKey != null) hotKey.IsActive = canInteract;
		}

		private void SelectDeckRewardColumn(QuestRewardOverlayState state, int selectedIndex)
		{
			if (state == null || state.DeckColumnSelectionInProgress) return;

			DisableDeckRewardCardSheens();
			state.DeckColumnSelectionInProgress = true;
			state.SelectedDeckRewardColumnIndex = selectedIndex;
			state.DeckColumnSelectionElapsedSeconds = 0f;

			foreach (var lane in _deckRewardLaneEntities)
			{
				var ui = lane?.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.IsClicked = false;
				ui.IsInteractable = false;
			}

			var skipUi = _deckRewardSkipButton?.GetComponent<UIElement>();
			if (skipUi != null)
			{
				skipUi.IsClicked = false;
				skipUi.IsInteractable = false;
			}
			var hotKey = _deckRewardSkipButton?.GetComponent<HotKey>();
			if (hotKey != null) hotKey.IsActive = false;

			var option = state.DeckRewardOffer?.options != null
				&& selectedIndex >= 0
				&& selectedIndex < state.DeckRewardOffer.options.Count
				? state.DeckRewardOffer.options[selectedIndex]
				: null;
			if (option != null)
			{
				if (string.Equals(option.kind, DeckRewardOfferKinds.Exchange, System.StringComparison.OrdinalIgnoreCase))
				{
					EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.TakeReward, Volume = 0.5f });
				}
				else if (string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, System.StringComparison.OrdinalIgnoreCase))
				{
					EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.UpgradeCard, Volume = 0.5f });
				}
			}
		}

		private void PreparePreviewCard(Entity card, int zOffset, QuestRewardOverlayState state)
		{
			if (card == null) return;
			var ui = card.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.Bounds = Rectangle.Empty;
				ui.IsInteractable = false;
				ui.IsClicked = false;
				ui.LayerType = UILayerType.Overlay;
			}
			var transform = card.GetComponent<Transform>();
			if (transform != null)
			{
				transform.ZOrder = ZOrder + 1 + zOffset;
			}
			InputContextService.EnsureMember(EntityManager, card, ContextId);
		}

		private void SyncDeckRewardCardHover(DeckRewardOfferLayout layout)
		{
			var inputState = EntityManager.GetEntitiesWithComponent<PlayerInputState>()
				.FirstOrDefault()?.GetComponent<PlayerInputState>();
			if (inputState == null) return;
			var cursor = inputState.Frame.PointerPosition;

			var settings = CardGeometryService.GetSettings(EntityManager);

			foreach (var view in _deckRewardOptionViews)
			{
				var outgoingUi = view.OutgoingCard?.GetComponent<UIElement>();
				var incomingUi = view.IncomingCard?.GetComponent<UIElement>();
				if (outgoingUi != null) outgoingUi.IsHovered = false;
				if (incomingUi != null) incomingUi.IsHovered = false;
			}

			for (int i = 0; i < _deckRewardOptionViews.Count && i < layout.OutgoingCardCenters.Length; i++)
			{
				var outgoingRect = CardGeometryService.GetVisualRect(settings, layout.OutgoingCardCenters[i], 1.0f);
				if (outgoingRect.Contains(cursor))
				{
					var ui = _deckRewardOptionViews[i].OutgoingCard?.GetComponent<UIElement>();
					if (ui != null) ui.IsHovered = true;
					return;
				}
			}
			for (int i = 0; i < _deckRewardOptionViews.Count && i < layout.IncomingCardCenters.Length; i++)
			{
				var incomingRect = CardGeometryService.GetVisualRect(settings, layout.IncomingCardCenters[i], 1.0f);
				if (incomingRect.Contains(cursor))
				{
					var ui = _deckRewardOptionViews[i].IncomingCard?.GetComponent<UIElement>();
					if (ui != null) ui.IsHovered = true;
					return;
				}
			}
		}

		private void CompleteDeckRewardOfferResolution(QuestRewardOverlayState state, SceneState scene)
		{
			if (state == null) return;
			if (state.DismissToLocation && scene?.Current == SceneId.Battle)
			{
				DisableDeckRewardControls();
				RequestCloseAnimation(state, transitionAfterClose: true, state.DismissScene);
				return;
			}

			RequestCloseAnimation(state, transitionAfterClose: false, state.DismissScene);
		}

		private BoosterPackOpeningTiming BuildDeckRewardSheenTiming(float enterDurationSeconds)
		{
			return new BoosterPackOpeningTiming(
				enterDurationSeconds,
				0f,
				0f,
				0f,
				0f,
				0f,
				0f,
				SheenStaggerSeconds,
				0f,
				SheenDelaySeconds,
				0f);
		}

		private static float GetDeckRewardSheenElapsedSeconds(ModalAnimation animation)
		{
			if (animation == null) return 0f;
			return animation.Phase switch
			{
				ModalAnimationPhase.Entering => animation.ElapsedSeconds,
				ModalAnimationPhase.Visible => animation.EnterDurationSeconds + animation.ElapsedSeconds,
				_ => 0f,
			};
		}

		private void UpdateDeckRewardCardSheens(QuestRewardOverlayState state)
		{
			if (state == null) return;
			if (state.DeckColumnSelectionInProgress || state.DismissInProgress)
			{
				DisableDeckRewardCardSheens();
				return;
			}

			var animation = EntityManager.GetEntity("QuestRewardOverlay")?.GetComponent<ModalAnimation>();
			if (animation == null
				|| animation.Phase == ModalAnimationPhase.Hidden
				|| animation.Phase == ModalAnimationPhase.Exiting)
			{
				DisableDeckRewardCardSheens();
				return;
			}

			float elapsed = GetDeckRewardSheenElapsedSeconds(animation);
			var timing = BuildDeckRewardSheenTiming(animation.EnterDurationSeconds);
			for (int index = 0; index < _deckRewardOptionViews.Count; index++)
			{
				var sheen = _deckRewardOptionViews[index].IncomingCard?.GetComponent<CardSheen>();
				if (sheen == null) continue;
				sheen.IsActive = BoosterPackOpeningAnimationService.HasSheenStarted(elapsed, index, timing);
			}
		}

		private void DisableDeckRewardCardSheens()
		{
			foreach (var view in _deckRewardOptionViews)
			{
				if (view.IncomingCard?.GetComponent<CardSheen>() is CardSheen sheen)
				{
					sheen.IsActive = false;
				}
			}
		}

		private void DisableDeckRewardControls()
		{
			foreach (var lane in _deckRewardLaneEntities)
			{
				var ui = lane?.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.IsInteractable = false;
				ui.IsClicked = false;
			}
			var skipUi = _deckRewardSkipButton?.GetComponent<UIElement>();
			if (skipUi != null)
			{
				skipUi.IsInteractable = false;
				skipUi.IsClicked = false;
			}
			var hotKey = _deckRewardSkipButton?.GetComponent<HotKey>();
			if (hotKey != null) hotKey.IsActive = false;
		}

		private DeckRewardOfferLayout ComputeDeckRewardOfferLayout(int vw, int vh, int columnCount, bool[] isUpgradeFlags)
		{
			int modalW = System.Math.Max(600, DeckModalWidth);
			int modalH = System.Math.Max(400, DeckModalHeight);
			int modalX = (vw - modalW) / 2;
			int modalY = (vh - modalH) / 2;
			var modal = new Rectangle(modalX, modalY, modalW, modalH);
			var content = new Rectangle(modal.X + BorderThickness, modal.Y + BorderThickness, modal.Width - BorderThickness * 2, modal.Height - BorderThickness * 2);

			int mastheadH = System.Math.Max(40, DeckMastheadHeight);
			var masthead = new Rectangle(content.X, content.Y, content.Width, mastheadH);

			int footerH = System.Math.Max(40, DeckFooterHeight);
			var footer = new Rectangle(content.X, content.Bottom - footerH, content.Width, footerH);

			var exchangeStage = new Rectangle(content.X, masthead.Bottom, content.Width, System.Math.Max(1, footer.Y - masthead.Bottom));

			int colCount = System.Math.Max(0, columnCount);
			var columns = new Rectangle[colCount];
			var outgoing = new Vector2[colCount];
			var incoming = new Vector2[colCount];
			var arrows = new Vector2[colCount];
			var isUpgrade = new bool[colCount];

			if (colCount > 0)
			{
				int stagePadX = System.Math.Max(0, ExchangeStagePadX);
				int stagePadTop = System.Math.Max(0, ExchangeStagePadTop);
				int stagePadBottom = System.Math.Max(0, ExchangeStagePadBottom);
				int areaX = exchangeStage.X + stagePadX;
				int areaY = exchangeStage.Y + stagePadTop;
				int areaW = System.Math.Max(1, exchangeStage.Width - stagePadX * 2);
				int areaH = System.Math.Max(1, exchangeStage.Height - stagePadTop - stagePadBottom);

				int gap = System.Math.Max(0, ColumnGap);
				int maxColW = System.Math.Max(100, ColumnMaxWidth);
				int colPadX = System.Math.Max(0, ColumnPaddingX);
				int colPadTop = System.Math.Max(0, ColumnPaddingTop);
				int colPadBot = System.Math.Max(0, ColumnPaddingBottom);

				int colW = System.Math.Min(maxColW, (areaW - gap * (colCount - 1)) / colCount);
				int totalColW = colW * colCount + gap * (colCount - 1);
				int colStartX = areaX + (areaW - totalColW) / 2;

				var settings = CardGeometryService.GetSettings(EntityManager);
				int cardW = settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
				int cardH = settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
				int offsetY = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;
				float cardHalfVisualTop = cardH / 2f + offsetY;
				float cardHalfVisualBot = cardH / 2f - offsetY;

				for (int i = 0; i < colCount; i++)
				{
					columns[i] = new Rectangle(colStartX + i * (colW + gap), areaY, colW, areaH);
					float colCenterX = columns[i].Center.X;
					float outgoingCY = columns[i].Y + colPadTop + cardHalfVisualTop;
					float incomingCY = columns[i].Bottom - colPadBot - cardHalfVisualBot;
					float outgoingBottom = outgoingCY + cardHalfVisualBot;
					float incomingTop = incomingCY - cardHalfVisualTop;
					float arrowCY = (outgoingBottom + incomingTop) / 2f;
					outgoing[i] = new Vector2(colCenterX, outgoingCY);
					incoming[i] = new Vector2(colCenterX, incomingCY);
					arrows[i] = new Vector2(colCenterX, arrowCY);
					isUpgrade[i] = isUpgradeFlags != null && i < isUpgradeFlags.Length && isUpgradeFlags[i];
				}
			}

			int skipBtnW = System.Math.Max(60, DeckSkipButtonWidth);
			int skipBtnH = System.Math.Max(30, DeckSkipButtonHeight);
			var skipButton = new Rectangle(
				content.Center.X - skipBtnW / 2,
				footer.Y + (footer.Height - skipBtnH) / 2,
				skipBtnW, skipBtnH);

			return new DeckRewardOfferLayout
			{
				Modal = modal,
				Content = content,
				Masthead = masthead,
				ExchangeStage = exchangeStage,
				Footer = footer,
				SkipButton = skipButton,
				Columns = columns,
				OutgoingCardCenters = outgoing,
				IncomingCardCenters = incoming,
				ArrowCenters = arrows,
				IsUpgrade = isUpgrade
			};
		}

		private void DestroyRewardMedal()
		{
			if (_rewardMedalEntity == null)
			{
				var orphan = EntityManager.GetEntity("QuestRewardMedalHitbox");
				if (orphan != null) EntityManager.DestroyEntity(orphan.Id);
				return;
			}
			EntityManager.DestroyEntity(_rewardMedalEntity.Id);
			_rewardMedalEntity = null;
		}

		private void DestroyRewardEquipment()
		{
			if (_rewardEquipmentEntity == null)
			{
				var orphan = EntityManager.GetEntity("QuestRewardEquipmentHitbox");
				if (orphan != null) EntityManager.DestroyEntity(orphan.Id);
				return;
			}
			EntityManager.DestroyEntity(_rewardEquipmentEntity.Id);
			_rewardEquipmentEntity = null;
		}

		private static void SyncCardLayoutAnchor(Transform transform, PositionTween tween, Vector2 layoutCenter)
		{
			if (tween != null)
			{
				bool shouldSnap = !tween.Initialized || tween.Target == Vector2.Zero;
				tween.Target = layoutCenter;
				if (shouldSnap)
				{
					tween.Current = layoutCenter;
					tween.Initialized = true;
					if (transform != null) transform.Position = layoutCenter;
				}
				return;
			}

			if (transform != null)
				transform.Position = layoutCenter;
		}

		private void SyncRewardCardHitboxes(QuestRewardOverlayState state)
		{
			for (int i = 0; i < _rewardCardEntities.Count; i++)
			{
				var card = _rewardCardEntities[i];
				if (card == null) continue;
				var ui = card.GetComponent<UIElement>();
				var t = card.GetComponent<Transform>();
				if (ui == null) continue;
				if (t != null) t.ZOrder = ZOrder + 1 + i;
				var layoutCenter = GetRewardCardCenter(i);
				SyncCardLayoutAnchor(t, card.GetComponent<PositionTween>(), layoutCenter);
				float scale = GetRewardCardDisplayScale(i, state);
				ui.Bounds = scale > 0.001f
					? GetCardVisualRectScaled(layoutCenter, scale)
					: Rectangle.Empty;
				ui.IsInteractable = state != null
					&& state.IsOpen
					&& !state.CardSelectionInProgress
					&& !state.DismissInProgress
					&& IsModalAnimationInteractive();
				ui.LayerType = UILayerType.Overlay;
				InputContextService.EnsureMember(
					EntityManager,
					card,
					ContextId);
			}
		}

		private void UpdateRewardCardSelection(QuestRewardOverlayState state, GameTime gameTime)
		{
			if (state == null) return;

			if (state.CardSelectionInProgress)
			{
				state.CardSelectionElapsedSeconds += (float)gameTime.ElapsedGameTime.TotalSeconds;
				if (state.CardSelectionElapsedSeconds >= System.Math.Max(0.05f, CardSelectionAnimationSeconds) && !state.DismissInProgress)
				{
					if (state.DismissToLocation)
					{
						RequestCloseAnimation(state, transitionAfterClose: true, state.DismissScene);
					}
					else
					{
						RequestCloseAnimation(state, transitionAfterClose: false, state.DismissScene);
					}
				}
				return;
			}

			for (int i = 0; i < _rewardCardEntities.Count; i++)
			{
				var card = _rewardCardEntities[i];
				if (card == null) continue;
				var ui = card.GetComponent<UIElement>();
				if (ui == null) continue;

				if (ui.IsHovered)
				{
					var hk = card.GetComponent<HotKey>();
					if (hk == null)
					{
						EntityManager.AddComponent(card, new HotKey { Button = FaceButton.X, RequiresHold = true, Position = HotKeyPosition.Below });
					}
					else
					{
						hk.Button = FaceButton.X;
						hk.RequiresHold = true;
						hk.Position = HotKeyPosition.Below;
						hk.IsActive = true;
					}
				}
				else
				{
					var hk = card.GetComponent<HotKey>();
					if (hk != null) EntityManager.RemoveComponent<HotKey>(card);
				}

				if (!ui.IsClicked) continue;
				ui.IsClicked = false;
				SelectRewardCard(state, i);
				break;
			}
		}

		private void SelectRewardCard(QuestRewardOverlayState state, int selectedIndex)
		{
			if (state == null || state.CardSelectionInProgress) return;
			if (selectedIndex < 0 || selectedIndex >= state.RewardCardKeys.Count) return;

			string selectedKey = state.RewardCardKeys[selectedIndex];
			var grant = QuestCardRewardService.GrantCard(selectedKey);
			if (!grant.Granted) return;

			state.CardSelectionInProgress = true;
			state.SelectedRewardCardIndex = selectedIndex;
			state.RewardCardKey = selectedKey;
			state.CardSelectionElapsedSeconds = 0f;

			for (int i = 0; i < _rewardCardEntities.Count; i++)
			{
				var card = _rewardCardEntities[i];
				if (card == null) continue;
				var ui = card.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.IsClicked = false;
					ui.IsInteractable = false;
				}
				var hk = card.GetComponent<HotKey>();
				if (hk != null) EntityManager.RemoveComponent<HotKey>(card);
			}

			var selected = _rewardCardEntities.ElementAtOrDefault(selectedIndex);
			if (selected != null)
			{
				EventManager.Publish(new JigglePulseEvent
				{
					Target = selected,
					Config = new JigglePulseConfig
					{
						PulseDurationSeconds = System.Math.Max(0.05f, CardSelectionAnimationSeconds),
						PulseScaleAmplitude = 0.12f,
						JiggleDegrees = 7f,
						PulseFrequencyHz = 6f
					}
				});
			}
		}

		private Vector2 GetRewardCardCenter(int index)
		{
			if (_layout.CardChoiceCenters != null && index >= 0 && index < _layout.CardChoiceCenters.Length)
			{
				return _layout.CardChoiceCenters[index];
			}
			return _layout.CardCenter;
		}

		private float GetRewardCardDisplayScale(int index, QuestRewardOverlayState state)
		{
			float scale = CardPreviewScale;
			if (state != null && state.CardSelectionInProgress && state.SelectedRewardCardIndex >= 0 && index != state.SelectedRewardCardIndex)
			{
				float t = MathHelper.Clamp(state.CardSelectionElapsedSeconds / System.Math.Max(0.05f, CardSelectionAnimationSeconds), 0f, 1f);
				float shrink = 1f - SmoothStep(t);
				scale *= shrink;
			}

			var transform = index >= 0 && index < _rewardCardEntities.Count
				? _rewardCardEntities[index]?.GetComponent<Transform>()
				: null;
			if (transform != null)
			{
				scale *= transform.Scale.X;
			}
			return scale;
		}

		private static float SmoothStep(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		internal static float GetDeckColumnChromeAlpha(int columnIndex, QuestRewardOverlayState state, float durationSeconds)
		{
			if (state == null || !state.DeckColumnSelectionInProgress || state.SelectedDeckRewardColumnIndex < 0)
				return 1f;
			if (columnIndex == state.SelectedDeckRewardColumnIndex)
				return 1f;

			float dur = System.Math.Max(0.05f, durationSeconds);
			float t = MathHelper.Clamp(state.DeckColumnSelectionElapsedSeconds / dur, 0f, 1f);
			return 1f - SmoothStep(t);
		}

		internal static void ComputeDeckColumnSelectionVisual(
			int columnIndex,
			int selectedColumnIndex,
			bool isOutgoing,
			float elapsedSeconds,
			float durationSeconds,
			float pulseAmplitude,
			float pulseFrequencyHz,
			out float scale,
			out float alpha)
		{
			scale = 1f;
			alpha = 1f;
			if (selectedColumnIndex < 0) return;

			float dur = System.Math.Max(0.05f, durationSeconds);
			float t = MathHelper.Clamp(elapsedSeconds / dur, 0f, 1f);

			if (columnIndex != selectedColumnIndex)
			{
				alpha = 1f - SmoothStep(t);
				return;
			}

			if (isOutgoing)
			{
				scale = 1f - SmoothStep(t);
				return;
			}

			float env = (1f - t) * (1f - t);
			float phase = MathHelper.TwoPi * pulseFrequencyHz * elapsedSeconds;
			scale = 1f + pulseAmplitude * env * (float)System.Math.Sin(phase);
		}

		private Rectangle GetCardVisualRectScaled(Vector2 position, float scale)
		{
			return CardGeometryService.GetVisualRect(EntityManager, position, scale);
		}

		internal static void ApplyDeckRewardPreviewRestrictions(
			EntityManager entityManager,
			Entity card,
			DeckRewardOfferOptionSave option,
			bool forIncomingCard)
		{
			if (entityManager == null || card == null || option == null) return;
			if (string.IsNullOrWhiteSpace(option.outgoingEntryId)) return;
			RunScopedStateService.ApplySavedRestrictionsToCard(entityManager, card, option.outgoingEntryId);
		}

		private void ApplyDeckRewardPreviewRestrictions(
			Entity card,
			DeckRewardOfferOptionSave option,
			bool forIncomingCard)
		{
			ApplyDeckRewardPreviewRestrictions(EntityManager, card, option, forIncomingCard);
		}

		private Entity CreateRewardCard(string cardKey)
		{
			if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out var color, out var isUpgraded)) return null;
			var created = EntityFactory.CreateCardFromDefinition(
				EntityManager,
				cardId,
				color,
				suppressStatDeltaDisplay: true,
				isUpgraded: isUpgraded);
			if (created == null) return null;

			var ui = created.GetComponent<UIElement>();
			var t = created.GetComponent<Transform>();
			if (ui != null)
			{
				ui.IsInteractable = true;
				ui.EventType = UIElementEventType.None;
				ui.LayerType = UILayerType.Overlay;
			}
			if (t != null) t.ZOrder = ZOrder + 1;
			if (!created.HasComponent<ParallaxLayer>())
				EntityManager.AddComponent(created, ParallaxLayer.GetUIParallaxLayer());
			return created;
		}

		private Entity CreateRewardMedalHitbox(string medalId)
		{
			var medal = MedalFactory.Create(medalId);
			if (medal == null) return null;

			var ent = EntityManager.CreateEntity("QuestRewardMedalHitbox");
			EntityManager.AddComponent(ent, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 1 });
			EntityManager.AddComponent(ent, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = false,
				Tooltip = $"{medal.Name}\n\n{medal.Text}",
				TooltipPosition = TooltipPosition.Below,
				LayerType = UILayerType.Overlay
			});
			EntityManager.AddComponent(ent, ParallaxLayer.GetUIParallaxLayer());
			EntityManager.AddComponent(ent, new DontDestroyOnLoad());
			InputContextService.EnsureMember(
				EntityManager,
				ent,
				ContextId);
			return ent;
		}

		private Entity CreateRewardEquipmentHitbox(string equipmentId)
		{
			var equipment = EquipmentFactory.Create(equipmentId);
			if (equipment == null) return null;

			var ent = EntityManager.CreateEntity("QuestRewardEquipmentHitbox");
			EntityManager.AddComponent(ent, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 1 });
			EntityManager.AddComponent(ent, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = true,
				Tooltip = EquipmentService.GetTooltipText(equipment, EquipmentTooltipType.Shop),
				TooltipPosition = TooltipPosition.Below,
				LayerType = UILayerType.Overlay
			});
			EntityManager.AddComponent(ent, ParallaxLayer.GetUIParallaxLayer());
			EntityManager.AddComponent(ent, new DontDestroyOnLoad());
			InputContextService.EnsureMember(
				EntityManager,
				ent,
				ContextId);
			return ent;
		}

		private void DrawRightColumnEquipment(bool showEquipment, string equipmentId, ModalAnimationRenderState render)
		{
			if (!showEquipment || string.IsNullOrWhiteSpace(equipmentId)) return;

			var equipment = EquipmentFactory.Create(equipmentId);
			if (equipment == null) return;

			var r = _layout.MedalPreviewRect;
			if (r.Width <= 0 || r.Height <= 0) return;

			var icon = GetEquipmentSlotIcon(equipment.Slot);
			if (icon != null)
			{
				int iconSize = (int)System.Math.Round(System.Math.Min(r.Width, r.Height) * render.ShellScale);
				Vector2 topCenter = render.Transform(new Vector2(r.X + r.Width / 2f, r.Y));
				var iconRect = new Rectangle(
					(int)System.Math.Round(topCenter.X - iconSize / 2f),
					(int)System.Math.Round(topCenter.Y),
					iconSize,
					iconSize);
				_spriteBatch.Draw(icon, iconRect, render.ApplyShell(Color.White));
			}

			if (_bodyFont == null || string.IsNullOrWhiteSpace(equipment.Name)) return;

			float nameScale = render.TransformScale(EquipmentNameScale);
			Vector2 nameSize = _bodyFont.MeasureString(equipment.Name) * nameScale;
			Vector2 nameAnchor = render.Transform(new Vector2(r.X + r.Width / 2f, r.Bottom + 8f));
			float nameX = nameAnchor.X - nameSize.X / 2f;
			float nameY = nameAnchor.Y;
			_spriteBatch.DrawString(
				_bodyFont,
				equipment.Name,
				new Vector2(nameX, nameY),
				render.ApplyShell(Color.White),
				0f,
				Vector2.Zero,
				nameScale,
				SpriteEffects.None,
				0f);
		}

		private Texture2D GetEquipmentSlotIcon(EquipmentSlot slot)
		{
			string key = slot.ToString().ToLowerInvariant();
			return _imageAssets.TryGetTexture(key);
		}

		private static CardData.CardColor ParseColor(string color)
		{
			if (string.IsNullOrEmpty(color)) return CardData.CardColor.White;
			switch (color.Trim().ToLowerInvariant())
			{
				case "red": return CardData.CardColor.Red;
				case "black": return CardData.CardColor.Black;
				default: return CardData.CardColor.White;
			}
		}

		private void EnsureOverlayEntity()
		{
			var e = EntityManager.GetEntity("QuestRewardOverlay");
			if (e == null)
			{
				e = EntityManager.CreateEntity("QuestRewardOverlay");
				var t = new Transform { Position = Vector2.Zero, ZOrder = ZOrder };
				EntityManager.AddComponent(e, t);
				EntityManager.AddComponent(e, new QuestRewardOverlayState());
				InputContextService.EnsureContext(
					EntityManager,
					e,
					ContextId,
					720,
					false);
				EntityManager.AddComponent(e, new ModalAnimation { InputContextId = ContextId });
				EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
				EntityManager.AddComponent(e, new DontDestroyOnLoad());
			}
			else
			{
				if (e.GetComponent<UIElement>() != null)
				{
					EntityManager.RemoveComponent<UIElement>(e);
				}
				var t = e.GetComponent<Transform>();
				if (t != null) t.ZOrder = ZOrder;
				EnsureModalAnimation();
			}
		}

		private Entity EnsureProceedButton()
		{
			var ent = EntityManager.GetEntity("QuestRewardProceedButton");
			if (ent == null)
			{
				ent = EntityManager.CreateEntity("QuestRewardProceedButton");
				EntityManager.AddComponent(ent, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 2 });
				EntityManager.AddComponent(ent, new UIElement { Bounds = Rectangle.Empty, IsInteractable = false, IsHidden = true, LayerType = UILayerType.Overlay });
				EntityManager.AddComponent(ent, new HotKey { Button = FaceButton.Y, IsActive = false });
				InputContextService.EnsureMember(
					EntityManager,
					ent,
					ContextId);
				EntityManager.AddComponent(ent, ParallaxLayer.GetUIParallaxLayer());
			}
			else
			{
				var t = ent.GetComponent<Transform>();
				if (t != null) t.ZOrder = ZOrder + 2;
				var ui = ent.GetComponent<UIElement>();
				if (ui != null) ui.LayerType = UILayerType.Overlay;
			}
			return ent;
		}
	}
}
