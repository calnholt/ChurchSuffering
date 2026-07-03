using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;



namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Quest Tooltip")]
	public class TooltipQuestDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _contentFont = FontSingleton.ContentFont;
		private readonly Dictionary<(int w, int h, int r), Texture2D> _roundedCache = new();
		private readonly Dictionary<(int w, int h, bool right, int border), Texture2D> _triangleCache = new();
	private Texture2D _pixel;
	private Texture2D _chaliceTexture;
	private Texture2D _treasureChestTexture;
	private Texture2D _goldTexture;
	private Texture2D _questIconTexture;
	private Texture2D _hellriftIconTexture;
	private Entity _tooltipEntity;

		[DebugEditable(DisplayName = "Padding", Step = 1, Min = 0, Max = 40)]
		public int Padding { get; set; } = 10;

		[DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int CornerRadius { get; set; } = 8;

		[DebugEditable(DisplayName = "Fade Seconds", Step = 0.05f, Min = 0.05f, Max = 1.5f)]
		public float FadeSeconds { get; set; } = 0.12f;

		[DebugEditable(DisplayName = "Max Alpha", Step = 5, Min = 0, Max = 255)]
		public int MaxAlpha { get; set; } = 220;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.5f, Max = 2.0f)]
		public float TextScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Header Height", Step = 2, Min = 12, Max = 200)]
		public int HeaderHeight { get; set; } = 64;

		[DebugEditable(DisplayName = "Header Left R", Step = 1, Min = 0, Max = 255)]
		public int HeaderLeftR { get; set; } = 20;

		[DebugEditable(DisplayName = "Header Left G", Step = 1, Min = 0, Max = 255)]
		public int HeaderLeftG { get; set; } = 0;

		[DebugEditable(DisplayName = "Header Left B", Step = 1, Min = 0, Max = 255)]
		public int HeaderLeftB { get; set; } = 0;

		[DebugEditable(DisplayName = "Header Right R", Step = 1, Min = 0, Max = 255)]
		public int HeaderRightR { get; set; } = 60;

		[DebugEditable(DisplayName = "Header Right G", Step = 1, Min = 0, Max = 255)]
		public int HeaderRightG { get; set; } = 0;

		[DebugEditable(DisplayName = "Header Right B", Step = 1, Min = 0, Max = 255)]
		public int HeaderRightB { get; set; } = 0;

		[DebugEditable(DisplayName = "Header Stripe Height", Step = 1, Min = 0, Max = 16)]
		public int HeaderStripeHeight { get; set; } = 3;

		[DebugEditable(DisplayName = "Box Width", Step = 10, Min = 100, Max = 1920)]
		public int BoxWidth { get; set; } = 400;

		[DebugEditable(DisplayName = "Box Height", Step = 10, Min = 100, Max = 1080)]
		public int BoxHeight { get; set; } = 260;

		[DebugEditable(DisplayName = "Gap", Step = 1, Min = 0, Max = 120)]
		public int Gap { get; set; } = 60;

		[DebugEditable(DisplayName = "Enemy Scale", Step = 0.05f, Min = 0.1f, Max = 3f)]
		public float EnemyScale { get; set; } = 1.05f;

		[DebugEditable(DisplayName = "Enemy Spacing", Step = 2, Min = 0, Max = 200)]
		public int EnemySpacing { get; set; } = 12;

		[DebugEditable(DisplayName = "Quest Title Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
		public float QuestTitleScale { get; set; } = 0.22f;

		[DebugEditable(DisplayName = "Bottom Bar Height", Step = 2, Min = 16, Max = 200)]
		public int BottomBarHeight { get; set; } = 50;

		// Bottom bar button (LB/RB) controls
		[DebugEditable(DisplayName = "Pill Side Padding", Step = 1, Min = 0, Max = 120)]
		public int PillSidePadding { get; set; } = 5;

		[DebugEditable(DisplayName = "Pill Min Height", Step = 1, Min = 12, Max = 200)]
		public int PillMinHeight { get; set; } = 27;

		[DebugEditable(DisplayName = "Pill InnerPad Min", Step = 1, Min = 0, Max = 40)]
		public int PillInnerPadMin { get; set; } = 5;

		[DebugEditable(DisplayName = "Pill InnerPad Factor (of height)", Step = 0.01f, Min = 0f, Max = 1f)]
		public float PillInnerPadFactor { get; set; } = 0.307f; // ~ 1/6 of height

		[DebugEditable(DisplayName = "Pill Corner Radius Max", Step = 1, Min = 0, Max = 64)]
		public int PillCornerRadiusMax { get; set; } = 0;

		[DebugEditable(DisplayName = "Bottom Label Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float BottomLabelScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Bottom Right Text Margin", Step = 1, Min = 0, Max = 120)]
		public int BottomRightMargin { get; set; } = 10;

		[DebugEditable(DisplayName = "Bottom Right Text Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float BottomRightTextScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Header Image Padding", Step = 1, Min = 0, Max = 40)]
		public int HeaderImagePadding { get; set; } = 4;

		[DebugEditable(DisplayName = "Tribulation Icon Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float TribulationIconScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Tribulation Icon Spacing", Step = 1, Min = 0, Max = 120)]
		public int TribulationIconSpacing { get; set; } = 8;

		[DebugEditable(DisplayName = "Tribulation Vertical Spacing", Step = 1, Min = 0, Max = 120)]
		public int TribulationVerticalSpacing { get; set; } = 40;

		[DebugEditable(DisplayName = "Tribulation Text Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float TribulationTextScale { get; set; } = 0.15f;

		[DebugEditable(DisplayName = "Tribulation Line Spacing", Step = 1, Min = 0, Max = 40)]
		public int TribulationLineSpacing { get; set; } = 4;

		[DebugEditable(DisplayName = "Tribulation Division Line Width (%)", Step = 1, Min = 0, Max = 100)]
		public int TribulationDivisionLineWidthPercent { get; set; } = 60;

		[DebugEditable(DisplayName = "Tribulation Division Line Height", Step = 1, Min = 1, Max = 10)]
		public int TribulationDivisionLineHeight { get; set; } = 2;

		[DebugEditable(DisplayName = "Tribulation Division Line Padding", Step = 1, Min = 0, Max = 60)]
		public int TribulationDivisionLinePadding { get; set; } = 30;

		// Rewards section controls
		[DebugEditable(DisplayName = "Rewards Vertical Spacing", Step = 1, Min = 0, Max = 120)]
		public int RewardsVerticalSpacing { get; set; } = 40;

		[DebugEditable(DisplayName = "Rewards Text Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float RewardsTextScale { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Reward Number-Icon Gap", Step = 1, Min = 0, Max = 120)]
		public int RewardNumberIconGap { get; set; } = 8;

		[DebugEditable(DisplayName = "Rewards Division Line Width (%)", Step = 1, Min = 0, Max = 100)]
		public int RewardsDivisionLineWidthPercent { get; set; } = 60;

		[DebugEditable(DisplayName = "Rewards Division Line Height", Step = 1, Min = 1, Max = 10)]
		public int RewardsDivisionLineHeight { get; set; } = 2;

		[DebugEditable(DisplayName = "Rewards Division Line Padding", Step = 1, Min = 0, Max = 60)]
		public int RewardsDivisionLinePadding { get; set; } = 30;

		[DebugEditable(DisplayName = "Rewards Chest Icon Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float RewardsChestIconScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Reward Coin Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float RewardCoinScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Chevron Width", Step = 1, Min = 2, Max = 100)]
		public float ChevronWidth { get; set; } = 25f;

		[DebugEditable(DisplayName = "Chevron Height", Step = 1, Min = 2, Max = 100)]
		public float ChevronHeight { get; set; } = 11f;

		[DebugEditable(DisplayName = "Chevron Thickness", Step = 0.5f, Min = 1, Max = 20)]
		public float ChevronThickness { get; set; } = 3.5f;

		[DebugEditable(DisplayName = "Chevron Gap", Step = 1, Min = -20, Max = 50)]
		public float ChevronGap { get; set; } = 2f;

		[DebugEditable(DisplayName = "Chevron Scale", Step = 0.1f, Min = 0.1f, Max = 5f)]
		public float ChevronScale { get; set; } = 0.7f;

		[DebugEditable(DisplayName = "Chevron Top Margin", Step = 1, Min = 0, Max = 50)]
		public int ChevronTopMargin { get; set; } = 15;

		private const string TooltipEntityName = "UI_QuestTooltip";

		public TooltipQuestDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
		_pixel = _imageAssets.GetPixel(Color.White);
		_chaliceTexture = _imageAssets.TryGetTexture("chalice");
		_treasureChestTexture = _imageAssets.TryGetTexture("treasure_chest");
		_goldTexture = _imageAssets.TryGetTexture("gold");
		_questIconTexture = _imageAssets.TryGetTexture("Quest_poi");
		_hellriftIconTexture = _imageAssets.TryGetTexture("Hellrift_poi");
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Return empty since we handle logic in Update() once per frame
			return Enumerable.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsActive) return;

			// Only on WorldMap or Location scenes
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || (scene.Current != SceneId.WorldMap && scene.Current != SceneId.Location)) 
			{
				// Clean up tooltip if scene changes
				if (_tooltipEntity != null)
				{
					EntityManager.DestroyEntity(_tooltipEntity.Id);
					_tooltipEntity = null;
				}
				return;
			}

			var hovered = EntityManager.GetEntitiesWithComponent<UIElement>()
				.Select(e => new { E = e, UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>() })
				.Where(x => x.UI != null && x.UI.TooltipType == TooltipType.Quests && x.UI.IsHovered)
				.OrderByDescending(x => x.T?.ZOrder ?? 0)
				.FirstOrDefault();

		// Determine if tooltip should be visible
		bool shouldShowTooltip = false;
		string locationIdTop = null;
		string title = null;
		List<LocationEventDefinition> events = null;
		List<TribulationDefinition> tribulations = null;
		int rewardGold = 0;
		bool isCompleted = false;
		PointOfInterestType poiType = PointOfInterestType.Quest;
		Rectangle? tooltipRect = null;

			if (hovered != null)
			{
			// Case 1: WorldMap location tile
			locationIdTop = ExtractLocationId(hovered.E?.Name);
			if (!string.IsNullOrEmpty(locationIdTop) && !locationIdTop.StartsWith("locked_"))
			{
				var all = LocationDefinitionCache.GetAll();
				if (all.TryGetValue(locationIdTop, out var loc) && loc?.pointsOfInterest != null && loc.pointsOfInterest.Count > 0)
				{
					int completed = SaveCache.GetValueOrDefault(locationIdTop, 0);
					int idx = System.Math.Max(0, System.Math.Min(completed, loc.pointsOfInterest.Count - 1));
					events = loc.pointsOfInterest[idx].events;
					tribulations = loc.pointsOfInterest[idx].tribulations;
					title = "Quest " + (idx + 1);
					// Determine POI type from location definition
					PointOfInterestType poiTypeFromDef = loc.pointsOfInterest[idx].type;
					poiType = poiTypeFromDef;
					shouldShowTooltip = true;
				}
			}
				else
				{
				// Case 2: Location scene run-map POI
				var poi = hovered.E.GetComponent<PointOfInterest>();
				if (poi != null && IsPoiVisible(poi) && SaveCache.TryGetRunNode(poi.Id, out var runNode, out var questIdx))
				{
					locationIdTop = "desert";
					var battleEnemyIds = runNode.ResolveBattleEnemyIds();
					events = battleEnemyIds
						.Select(id => new LocationEventDefinition
						{
							id = id,
							type = "Enemy",
						})
						.ToList();
					tribulations = new List<TribulationDefinition>();
					title = RunMapCombatNodePresentationService.GetTitle(runNode, questIdx);
					rewardGold = RunMapCombatNodePresentationService.GetRewardGold(runNode);
					isCompleted = runNode.isCompleted || poi.IsCompleted;
					poiType = RunMapCombatNodePresentationService.GetPoiType(runNode);
					shouldShowTooltip = true;
				}
				}
				
				// Calculate tooltip rect after we have the content
				if (shouldShowTooltip && !string.IsNullOrEmpty(locationIdTop) && events != null && events.Count > 0)
				{
					Rectangle anchorBounds = TransformResolverService.ResolveUIBounds(
						EntityManager,
						hovered.E,
						hovered.UI);
					tooltipRect = ComputeTooltipRect(anchorBounds, hovered.T, title, events, tribulations, rewardGold, isCompleted);
				}
			}

			// Update or create tooltip entity
			if (shouldShowTooltip && !string.IsNullOrEmpty(locationIdTop) && events != null && events.Count > 0 && tooltipRect.HasValue)
			{
				var rect = tooltipRect.Value;
				
				if (_tooltipEntity == null)
				{
					_tooltipEntity = EntityManager.CreateEntity(TooltipEntityName);
					EntityManager.AddComponent(_tooltipEntity, new Transform { Position = new Vector2(rect.X, rect.Y), ZOrder = 10001 });
					EntityManager.AddComponent(_tooltipEntity, new QuestTooltip { LocationId = locationIdTop, Title = title, Events = events, Tribulations = tribulations, RewardGold = rewardGold, IsCompleted = isCompleted, PoiType = poiType.ToString(), Alpha01 = 0f, TargetVisible = true });
					EntityManager.AddComponent(_tooltipEntity, new UIElement { Bounds = rect, IsInteractable = true });
					EntityManager.AddComponent(_tooltipEntity, new HotKey { Button = FaceButton.X, RequiresHold = true, ParentEntity = hovered.E });
				}
				else
				{
					var transform = _tooltipEntity.GetComponent<Transform>();
					if (transform != null)
					{
						transform.Position = new Vector2(rect.X, rect.Y);
						transform.ZOrder = 10001;
					}

					var questTooltip = _tooltipEntity.GetComponent<QuestTooltip>();
					if (questTooltip != null)
					{
						questTooltip.LocationId = locationIdTop;
						questTooltip.Title = title;
						questTooltip.Events = events;
						questTooltip.Tribulations = tribulations;
						questTooltip.RewardGold = rewardGold;
						questTooltip.IsCompleted = isCompleted;
						questTooltip.PoiType = poiType.ToString();
						questTooltip.TargetVisible = true;
					}

					var ui = _tooltipEntity.GetComponent<UIElement>();
					if (ui != null)
					{
						ui.Bounds = rect;
					}

					// Ensure the tooltip hotkey always targets the currently hovered POI
					var hotKey = _tooltipEntity.GetComponent<HotKey>();
					if (hotKey == null)
					{
						hotKey = new HotKey
						{
							Button = FaceButton.X,
							RequiresHold = true,
							ParentEntity = hovered.E
						};
						EntityManager.AddComponent(_tooltipEntity, hotKey);
					}
					else
					{
						hotKey.Button = FaceButton.X;
						hotKey.RequiresHold = true;
						hotKey.ParentEntity = hovered.E;
					}
				}
			}

			// Update fade state and cleanup if needed
			if (_tooltipEntity != null)
			{
				var questTooltip = _tooltipEntity.GetComponent<QuestTooltip>();
				if (questTooltip != null)
				{
					if (!shouldShowTooltip)
					{
						questTooltip.TargetVisible = false;
					}
					
					float step = (FadeSeconds <= 0f) ? 1f : (1f / (FadeSeconds * 60f));
					questTooltip.Alpha01 = MathHelper.Clamp(questTooltip.Alpha01 + (questTooltip.TargetVisible ? step : -step), 0f, 1f);
					
					if (questTooltip.Alpha01 <= 0f && !questTooltip.TargetVisible)
					{
						EntityManager.DestroyEntity(_tooltipEntity.Id);
						_tooltipEntity = null;
					}
				}
			}
		}

		public void Draw()
		{
			// Only on WorldMap or Location scenes
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || (scene.Current != SceneId.WorldMap && scene.Current != SceneId.Location)) return;
			if (_titleFont == null) return;

			// Find tooltip entity
			if (_tooltipEntity == null)
			{
				_tooltipEntity = EntityManager.GetEntity(TooltipEntityName);
			}

			if (_tooltipEntity == null) return;

			var questTooltip = _tooltipEntity.GetComponent<QuestTooltip>();
			var transform = _tooltipEntity.GetComponent<Transform>();
			var ui = _tooltipEntity.GetComponent<UIElement>();

		if (questTooltip == null || transform == null || ui == null) return;
		if (questTooltip.Alpha01 <= 0f) return;

	var rect = ui.Bounds;
	DrawTooltipBox(rect, questTooltip.Alpha01);
	DrawHeader(questTooltip.LocationId, questTooltip.Title, System.Enum.Parse<PointOfInterestType>(questTooltip.PoiType), rect, questTooltip.Alpha01);
	DrawQuestContent(rect, questTooltip.Alpha01, questTooltip.Title, questTooltip.Events, questTooltip.Tribulations, questTooltip.RewardGold, questTooltip.IsCompleted);
	}


		private Rectangle ComputeTooltipRect(Rectangle anchor, Transform t, string title, List<LocationEventDefinition> events, List<TribulationDefinition> tribulations, int rewardGold, bool isCompleted)
		{
			int w = System.Math.Max(100, BoxWidth);
			int h = CalculateTooltipHeight(w, title, events, tribulations, rewardGold, isCompleted);
			int gap = System.Math.Max(0, Gap);
			int viewportW = Game1.VirtualWidth;
			int viewportH = Game1.VirtualHeight;

			int centerX = (int)System.Math.Round(t?.Position.X ?? (anchor.X + anchor.Width / 2f));
			bool preferRight = centerX < viewportW / 2;
			int rx = preferRight ? (anchor.Right + gap) : (anchor.Left - gap - w);
			int ry = anchor.Top + (anchor.Height - h) / 2;
			var rect = new Rectangle(rx, ry, w, h);
			// clamp to screen
			rect.X = System.Math.Max(0, System.Math.Min(rect.X, viewportW - rect.Width));
			rect.Y = System.Math.Max(0, System.Math.Min(rect.Y, viewportH - rect.Height));
			return rect;
		}

		private int CalculateTooltipHeight(int width, string title, List<LocationEventDefinition> events, List<TribulationDefinition> tribulations, int rewardGold, bool isCompleted)
		{
			int pad = System.Math.Max(0, Padding);
			int headerHeight = System.Math.Max(12, HeaderHeight);
			
		// Header
		int totalHeight = headerHeight;
		
		// Padding after header (title is now in header)
		totalHeight += pad;
			
			// Enemy images height
			if (events != null && events.Count > 0)
			{
				// Calculate enemy images height - match the logic from DrawQuestContent
				var textures = new List<Texture2D>();
				foreach (var q in events)
				{
					var tex = TryLoadEnemyTexture(q.id);
					if (tex != null) textures.Add(tex);
				}
				
				if (textures.Count > 0)
				{
					// Match the actual calculation: use a fixed height based on EnemyScale
					// The actual drawing uses: targetH = Round(maxH * EnemyScale)
					int estimatedEnemyHeight = System.Math.Max(1, (int)System.Math.Round(80 * EnemyScale));
					totalHeight += estimatedEnemyHeight;

					}
				}
			
			// Tribulation section
			if (tribulations != null && tribulations.Count > 0 && _chaliceTexture != null)
			{
				int vertSpacing = System.Math.Max(0, TribulationVerticalSpacing);
				float iconScale = MathHelper.Clamp(TribulationIconScale, 0.1f, 2f);
				float textScale = MathHelper.Clamp(TribulationTextScale, 0.1f, 2f);
				int lineSpacing = System.Math.Max(0, TribulationLineSpacing);
				int divisionLineHeight = System.Math.Max(1, TribulationDivisionLineHeight);
				
				int iconHeight = (int)System.Math.Round(_chaliceTexture.Height * iconScale);
				int maxTextWidth = width - 2 * pad; // Full width for text
				
				int maxTextHeight = 0;
				for (int i = 0; i < tribulations.Count; i++)
				{
					var trib = tribulations[i];
					if (!string.IsNullOrEmpty(trib.text))
					{
						var wrappedLines = TextUtils.WrapText(_contentFont, trib.text, textScale, maxTextWidth);
						maxTextHeight += wrappedLines.Count * (int)System.Math.Ceiling(_contentFont.MeasureString("A").Y * textScale);
						if (i < tribulations.Count - 1)
						{
							maxTextHeight += lineSpacing;
						}
					}
				}
				
				// Vertical spacing before division line
				totalHeight += vertSpacing;
				
				// Division line height (icon is centered on it, so we only need the line height)
				totalHeight += divisionLineHeight;
				
				// Spacing after division line
				int spacingAfterDivisionLine = System.Math.Max(0, TribulationVerticalSpacing / 2);
				totalHeight += spacingAfterDivisionLine;
				
				// Padding before text
				totalHeight += pad;
				
				// Text height
				totalHeight += maxTextHeight;
			}
			
			// Rewards section (only if reward exists and not completed)
			if (rewardGold > 0 && !isCompleted)
			{
				int rewardsVert = System.Math.Max(0, RewardsVerticalSpacing);
				int divisionLineHeight = System.Math.Max(1, RewardsDivisionLineHeight);
				float coinScale = MathHelper.Clamp(RewardCoinScale, 0.1f, 2f);
				float textScale = MathHelper.Clamp(RewardsTextScale, 0.1f, 2f);
				int coinHeight = _goldTexture != null ? (int)System.Math.Round(_goldTexture.Height * coinScale) : (int)System.Math.Ceiling(_contentFont.MeasureString("A").Y * textScale);
				int textHeight = (int)System.Math.Ceiling(_contentFont.MeasureString("A").Y * textScale);
				int rowHeight = System.Math.Max(coinHeight, textHeight);

				// vertical spacing + line + half-spacing + padding + row height
				totalHeight += rewardsVert;
				totalHeight += divisionLineHeight;
				totalHeight += System.Math.Max(0, RewardsVerticalSpacing / 2);
				totalHeight += pad;
				totalHeight += rowHeight;
			}

			// Padding at bottom
			totalHeight += pad;
			
			// Ensure minimum height
			return System.Math.Max(60, totalHeight);
		}

		private void DrawTooltipBox(Rectangle rect, float alpha01)
		{
			int r = System.Math.Max(0, System.Math.Min(CornerRadius, System.Math.Min(rect.Width, rect.Height) / 2));
			if (!_roundedCache.TryGetValue((rect.Width, rect.Height, r), out var tex) || tex == null)
			{
				tex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, rect.Width, rect.Height, r);
				_roundedCache[(rect.Width, rect.Height, r)] = tex;
			}
			int a = (int)System.Math.Round(System.Math.Max(0, System.Math.Min(255, MaxAlpha)) * alpha01);
			var back = new Color(0, 0, 0, System.Math.Clamp(a, 0, 255));
			_spriteBatch.Draw(tex, rect, back);
		}

	private void DrawHeader(string locationId, string title, PointOfInterestType poiType, Rectangle rect, float alpha01)
	{
		int hh = System.Math.Max(12, HeaderHeight);
		int stripe = System.Math.Max(0, System.Math.Min(HeaderStripeHeight, hh));
		var headerRect = new Rectangle(rect.X, rect.Y, rect.Width, System.Math.Min(rect.Height, hh));
		int a = (int)System.Math.Round(System.Math.Max(0, System.Math.Min(255, MaxAlpha)) * alpha01);
		// Darken the left background color a bit more for contrast
		var leftColor = new Color(System.Math.Max(0, HeaderLeftR - 10), System.Math.Max(0, HeaderLeftG), System.Math.Max(0, HeaderLeftB), System.Math.Clamp(a, 0, 255));
		var rightColor = new Color(HeaderRightR, HeaderRightG, HeaderRightB, System.Math.Clamp(a, 0, 255));

		// Top white stripe
		if (stripe > 0)
		{
			var stripeRect = new Rectangle(headerRect.X, headerRect.Y, headerRect.Width, stripe);
			_spriteBatch.Draw(_pixel, stripeRect, Color.White);
		}

		// Split header: left square (image), right area (quest title)
		int pad = System.Math.Max(0, Padding);
		int leftBoxSize = headerRect.Height - stripe; // square inside header below stripe
		var leftRect = new Rectangle(headerRect.X, headerRect.Y + stripe, System.Math.Min(leftBoxSize, headerRect.Width / 2), leftBoxSize);
		var rightRect = new Rectangle(leftRect.Right, headerRect.Y + stripe, System.Math.Max(0, headerRect.Width - leftRect.Width), leftBoxSize);
		_spriteBatch.Draw(_pixel, leftRect, leftColor);
		_spriteBatch.Draw(_pixel, rightRect, rightColor);

		// Draw POI icon centered in left box
		Texture2D iconTexture = null;
		if (poiType == PointOfInterestType.Hellrift && _hellriftIconTexture != null)
		{
			iconTexture = _hellriftIconTexture;
		}
		else if (_questIconTexture != null)
		{
			iconTexture = _questIconTexture;
		}
		
		if (iconTexture != null && leftRect.Width > 0 && leftRect.Height > 0)
		{
			int imgPad = System.Math.Max(0, HeaderImagePadding);
			var imgRect = new Rectangle(leftRect.X + imgPad, leftRect.Y + imgPad, System.Math.Max(1, leftRect.Width - 2 * imgPad), System.Math.Max(1, leftRect.Height - 2 * imgPad));
			float scale = System.Math.Min(imgRect.Width / (float)iconTexture.Width, imgRect.Height / (float)iconTexture.Height);
			int drawW = System.Math.Max(1, (int)System.Math.Round(iconTexture.Width * scale));
			int drawH = System.Math.Max(1, (int)System.Math.Round(iconTexture.Height * scale));
			var dst = new Rectangle(imgRect.X + (imgRect.Width - drawW) / 2, imgRect.Y + (imgRect.Height - drawH) / 2, drawW, drawH);
			_spriteBatch.Draw(iconTexture, dst, Color.White * alpha01);
		}

		// Draw quest title in right area
		if (!string.IsNullOrEmpty(title))
		{
			var size = _titleFont.MeasureString(title) * TextScale;
			var pos = new Vector2(rightRect.X + pad, rightRect.Y + System.Math.Max(0, (rightRect.Height - (int)System.Math.Ceiling(size.Y)) / 2));
			_spriteBatch.DrawString(_titleFont, title, pos, Color.White * alpha01, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
		}
	}

		private void DrawQuestContent(Rectangle rect, float alpha01, string title, List<LocationEventDefinition> questDefs, List<TribulationDefinition> tribulations, int rewardGold, bool isCompleted)
		{
			if (questDefs == null || questDefs.Count == 0) return;

		// inner area below header
		int pad = System.Math.Max(0, Padding);
		int topY = rect.Y + System.Math.Min(rect.Height, System.Math.Max(12, HeaderHeight)) + pad;
		int innerH = System.Math.Max(1, rect.Bottom - topY - pad);
		var inner = new Rectangle(rect.X + pad, topY, System.Math.Max(1, rect.Width - 2 * pad), innerH);

		// Start enemies directly after header padding
		int enemiesTop = inner.Y;
			
			// Calculate space needed for tribulations if present
			int tribulationSpace = 0;
			if (tribulations != null && tribulations.Count > 0 && _chaliceTexture != null)
			{
				int vertSpacing = System.Math.Max(0, TribulationVerticalSpacing);
				float iconScale = MathHelper.Clamp(TribulationIconScale, 0.1f, 2f);
				float textScale = MathHelper.Clamp(TribulationTextScale, 0.1f, 2f);
				int lineSpacing = System.Math.Max(0, TribulationLineSpacing);
				int divisionLineHeight = System.Math.Max(1, TribulationDivisionLineHeight);
				
				int iconHeight = (int)System.Math.Round(_chaliceTexture.Height * iconScale);
				int maxTextWidth = inner.Width; // Text spans full width now
				
				int maxTextHeight = 0;
				for (int i = 0; i < tribulations.Count; i++)
				{
					var trib = tribulations[i];
					if (!string.IsNullOrEmpty(trib.text))
					{
						var wrappedLines = TextUtils.WrapText(_contentFont, trib.text, textScale, maxTextWidth);
						maxTextHeight += wrappedLines.Count * (int)System.Math.Ceiling(_contentFont.MeasureString("A").Y * textScale);
						if (i < tribulations.Count - 1)
						{
							maxTextHeight += lineSpacing;
						}
					}
				}
				// Space calculation: vertical spacing + division line + small spacing after line + padding + text height
				int spacingAfterDivisionLine = System.Math.Max(0, TribulationVerticalSpacing / 2);
				tribulationSpace = vertSpacing + divisionLineHeight + spacingAfterDivisionLine + pad + maxTextHeight;
			}

			// Calculate space needed for rewards if present (so enemies area stays consistent)
			int rewardsSpace = 0;
			if (rewardGold > 0 && !isCompleted)
			{
				int rewardsVert = System.Math.Max(0, RewardsVerticalSpacing);
				int rewardsLineHeight = System.Math.Max(1, RewardsDivisionLineHeight);
				float coinScaleEst = MathHelper.Clamp(RewardCoinScale, 0.1f, 2f);
				float rewardsTextScale = MathHelper.Clamp(RewardsTextScale, 0.1f, 2f);
				int coinHeightEst = _goldTexture != null ? (int)System.Math.Round(_goldTexture.Height * coinScaleEst) : (int)System.Math.Ceiling(_contentFont.MeasureString("A").Y * rewardsTextScale);
				int textHeightEst = (int)System.Math.Ceiling(_contentFont.MeasureString("A").Y * rewardsTextScale);
				int rowHeightEst = System.Math.Max(coinHeightEst, textHeightEst);
				rewardsSpace = rewardsVert + rewardsLineHeight + System.Math.Max(0, RewardsVerticalSpacing / 2) + pad + rowHeightEst;
			}
			
				int enemiesHeight = System.Math.Max(1, inner.Bottom - enemiesTop - tribulationSpace - rewardsSpace);
			var enemiesRect = new Rectangle(inner.X, enemiesTop, inner.Width, enemiesHeight);

			// load enemy textures and their definitions
			var entries = new List<(Texture2D tex, LocationEventDefinition def)>();
			foreach (var q in questDefs)
			{
				var tex = TryLoadEnemyTexture(q.id);
				if (tex != null) entries.Add((tex, q));
			}
			if (entries.Count == 0) return;

			int maxH = enemiesRect.Height;
			int targetH = System.Math.Max(1, (int)System.Math.Round(maxH * EnemyScale));
			var sizes = entries.Select(e => new Point(
				(int)System.Math.Round(e.tex.Width * (targetH / (float)System.Math.Max(1, e.tex.Height))),
				targetH
			)).ToList();
			int totalW = sizes.Sum(s => s.X) + (entries.Count - 1) * System.Math.Max(0, EnemySpacing);
			int startX = enemiesRect.X + (enemiesRect.Width - totalW) / 2;
			for (int i = 0; i < entries.Count; i++)
			{
				int drawX = startX;
				for (int j = 0; j < i; j++) drawX += sizes[j].X + System.Math.Max(0, EnemySpacing);
				int drawY = enemiesRect.Y + (enemiesRect.Height - sizes[i].Y) / 2;
				_spriteBatch.Draw(entries[i].tex, new Rectangle(drawX, drawY, sizes[i].X, sizes[i].Y), Color.White * alpha01);

				}

			// Draw tribulations below enemies (within inner bounds)
			if (tribulations != null && tribulations.Count > 0 && _chaliceTexture != null)
			{
				int vertSpacing = System.Math.Max(0, TribulationVerticalSpacing);
				int iconSpacing = System.Math.Max(0, TribulationIconSpacing);
				int lineSpacing = System.Math.Max(0, TribulationLineSpacing);
				float iconScale = MathHelper.Clamp(TribulationIconScale, 0.1f, 2f);
				float textScale = MathHelper.Clamp(TribulationTextScale, 0.1f, 2f);
				int divisionLineHeight = System.Math.Max(1, TribulationDivisionLineHeight);
				int divisionLineWidthPercent = System.Math.Clamp(TribulationDivisionLineWidthPercent, 0, 100);
				int divisionLinePadding = System.Math.Max(0, TribulationDivisionLinePadding);

				// Calculate icon size
				int iconWidth = (int)System.Math.Round(_chaliceTexture.Width * iconScale);
				int iconHeight = (int)System.Math.Round(_chaliceTexture.Height * iconScale);
				
				// Calculate position below enemies
				int enemiesBottom = enemiesRect.Y + enemiesRect.Height;
				int divisionLineY = enemiesBottom + vertSpacing;
				
				// Ensure tribulation display doesn't exceed inner bottom
				if (divisionLineY >= inner.Bottom) return;

				// Calculate division line layout: two lines with chalice in the middle
				int centerX = inner.X + inner.Width / 2;
				int iconLeft = centerX - iconWidth / 2;
				int iconRight = centerX + iconWidth / 2;
				
				// Left line: from inner.X to iconLeft - padding
				int leftLineRight = System.Math.Max(inner.X, iconLeft - divisionLinePadding);
				int leftLineWidth = System.Math.Max(0, leftLineRight - inner.X);
				
				// Right line: from iconRight + padding to inner.Right
				int rightLineLeft = System.Math.Min(inner.Right, iconRight + divisionLinePadding);
				int rightLineWidth = System.Math.Max(0, inner.Right - rightLineLeft);
				
				// Draw left division line
				if (leftLineWidth > 0)
				{
					var leftLineRect = new Rectangle(inner.X, divisionLineY, leftLineWidth, divisionLineHeight);
					_spriteBatch.Draw(_pixel, leftLineRect, Color.White * alpha01);
				}
				
				// Draw chalice icon centered
				int iconY = divisionLineY + (divisionLineHeight - iconHeight) / 2;
				_spriteBatch.Draw(_chaliceTexture, new Rectangle(iconLeft, iconY, iconWidth, iconHeight), Color.White * alpha01);
				
				// Draw right division line
				if (rightLineWidth > 0)
				{
					var rightLineRect = new Rectangle(rightLineLeft, divisionLineY, rightLineWidth, divisionLineHeight);
					_spriteBatch.Draw(_pixel, rightLineRect, Color.White * alpha01);
				}

				// Calculate position for tribulation text (below division line)
				int tribulationTextTop = divisionLineY + divisionLineHeight + System.Math.Max(0, TribulationVerticalSpacing / 2) + pad;
				int maxTextWidth = inner.Width; // Text spans full width below

				// Draw tribulation text(s) below with wrapping
				int textX = inner.X;
				int currentY = tribulationTextTop;
				float lineHeight = _contentFont.MeasureString("A").Y * textScale;
				
				for (int i = 0; i < tribulations.Count; i++)
				{
					var trib = tribulations[i];
					if (!string.IsNullOrEmpty(trib.text))
					{
						// Wrap text to fit available width
						var wrappedLines = TextUtils.WrapText(_contentFont, trib.text, textScale, maxTextWidth);
						
						foreach (var line in wrappedLines)
						{
							if (string.IsNullOrEmpty(line))
							{
								currentY += (int)System.Math.Ceiling(lineHeight);
								continue;
							}
							
							// Ensure text doesn't exceed inner bounds
							if (currentY + lineHeight > inner.Bottom)
							{
								return; // Stop drawing if we've run out of space
							}
							
							_spriteBatch.DrawString(_contentFont, line, new Vector2(textX, currentY), Color.White * alpha01, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
							currentY += (int)System.Math.Ceiling(lineHeight);
						}
						
						// Add spacing between tribulations
						if (i < tribulations.Count - 1)
						{
							currentY += lineSpacing;
						}
					}
				}
			}

			// Draw rewards divider and centered amount + coin (Location only logic handled by rewardGold/isCompleted)
			if (rewardGold > 0 && !isCompleted)
			{
				int rewardsVert = System.Math.Max(0, RewardsVerticalSpacing);
				int divisionLineHeight = System.Math.Max(1, RewardsDivisionLineHeight);
				int divisionLinePadding = System.Math.Max(0, RewardsDivisionLinePadding);
				float chestScale = MathHelper.Clamp(RewardsChestIconScale, 0.1f, 2f);
				float coinScale = MathHelper.Clamp(RewardCoinScale, 0.1f, 2f);
				float textScale = MathHelper.Clamp(RewardsTextScale, 0.1f, 2f);

				// Top of the rewards section based on reserved rewardsSpace
				int rewardSectionTop = inner.Bottom - rewardsSpace;
				int rewardDivisionLineY = rewardSectionTop + rewardsVert;
				if (rewardDivisionLineY < inner.Bottom)
				{
					// Chest icon size
					int chestW = _treasureChestTexture != null ? (int)System.Math.Round(_treasureChestTexture.Width * chestScale) : 0;
					int chestH = _treasureChestTexture != null ? (int)System.Math.Round(_treasureChestTexture.Height * chestScale) : 0;
					int centerX = inner.X + inner.Width / 2;
					int iconLeft = centerX - chestW / 2;
					int iconRight = centerX + chestW / 2;

					// Left line
					int leftLineRight = System.Math.Max(inner.X, iconLeft - divisionLinePadding);
					int leftLineWidth = System.Math.Max(0, leftLineRight - inner.X);
					if (leftLineWidth > 0)
					{
						var leftLineRect = new Rectangle(inner.X, rewardDivisionLineY, leftLineWidth, divisionLineHeight);
						_spriteBatch.Draw(_pixel, leftLineRect, Color.White * alpha01);
					}

					// Chest icon
					if (_treasureChestTexture != null && chestW > 0 && chestH > 0)
					{
						int iconY = rewardDivisionLineY + (divisionLineHeight - chestH) / 2;
						_spriteBatch.Draw(_treasureChestTexture, new Rectangle(iconLeft, iconY, chestW, chestH), Color.White * alpha01);
					}

					// Right line
					int rightLineLeft = System.Math.Min(inner.Right, iconRight + divisionLinePadding);
					int rightLineWidth = System.Math.Max(0, inner.Right - rightLineLeft);
					if (rightLineWidth > 0)
					{
						var rightLineRect = new Rectangle(rightLineLeft, rewardDivisionLineY, rightLineWidth, divisionLineHeight);
						_spriteBatch.Draw(_pixel, rightLineRect, Color.White * alpha01);
					}

					// Row: amount + coin
					int rowTop = rewardDivisionLineY + divisionLineHeight + System.Math.Max(0, RewardsVerticalSpacing / 2) + pad;
					if (rowTop < inner.Bottom)
					{
						string amountText = rewardGold.ToString();
						var amountSize = _contentFont.MeasureString(amountText) * textScale;
						int coinW = _goldTexture != null ? (int)System.Math.Round(_goldTexture.Width * coinScale) : 0;
						int coinH = _goldTexture != null ? (int)System.Math.Round(_goldTexture.Height * coinScale) : 0;
						int rowH = System.Math.Max((int)System.Math.Ceiling(amountSize.Y), coinH);
						int rewardsTotalW = (int)System.Math.Ceiling(amountSize.X) + (coinW > 0 ? (RewardNumberIconGap + coinW) : 0);
						int rewardsStartX = inner.X + (inner.Width - rewardsTotalW) / 2;
						int textX = rewardsStartX;
						int textY = rowTop + (rowH - (int)System.Math.Ceiling(amountSize.Y)) / 2;
							_spriteBatch.DrawString(_contentFont, amountText, new Vector2(textX, textY), Color.White * alpha01, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);

						if (_goldTexture != null && coinW > 0 && coinH > 0)
						{
							int coinX = textX + (int)System.Math.Ceiling(amountSize.X) + RewardNumberIconGap;
							int coinY = rowTop + (rowH - coinH) / 2;
							_spriteBatch.Draw(_goldTexture, new Rectangle(coinX, coinY, coinW, coinH), Color.White * alpha01);
						}
					}
				}
			}

			// Bottom-right hint: "A - Select"
			// string leftText = "A";
			// string rightText = " - Select";
			// float scale = BottomRightTextScale;
			// var leftSize = _font.MeasureString(leftText) * scale;
			// var rightSize = _font.MeasureString(rightText) * scale;
			// int textPad = System.Math.Max(0, BottomRightMargin);
			// var rightEndX = inner.Right - textPad;
			// var rightPos = new Vector2(rightEndX - rightSize.X, inner.Bottom - rightSize.Y - textPad);
			// var leftPos = new Vector2((int)System.Math.Round(rightPos.X - leftSize.X), inner.Bottom - leftSize.Y - textPad);
			// var green = new Color(0, 200, 0) * alpha01;
			// _spriteBatch.DrawString(_font, leftText, leftPos, green, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			// _spriteBatch.DrawString(_font, rightText, rightPos, Color.White * alpha01, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private Texture2D TryLoadEnemyTexture(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;
			string assetName = EnemyPortraitContent.ToAssetName(id);
			return _imageAssets.GetTextureOrFallback(assetName, id);
		}

		private void DrawPill(Rectangle rect, Color color, int radius)
		{
			int r = System.Math.Max(2, System.Math.Min(radius, System.Math.Min(rect.Width, rect.Height) / 2));
			if (!_roundedCache.TryGetValue((rect.Width, rect.Height, r), out var tex) || tex == null)
			{
				tex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, rect.Width, rect.Height, r);
				_roundedCache[(rect.Width, rect.Height, r)] = tex;
			}
			_spriteBatch.Draw(tex, rect, color);
		}

		private LocationDefinition GetLocationDefinition(string id)
		{
			if (string.IsNullOrEmpty(id)) return null;
			var all = LocationDefinitionCache.GetAll();
			all.TryGetValue(id, out var loc);
			return loc;
		}

		private static string ExtractLocationId(string entityName)
		{
			if (string.IsNullOrEmpty(entityName)) return null;
			const string prefix = "Location_";
			if (!entityName.StartsWith(prefix)) return null;
			return entityName.Substring(prefix.Length);
		}

		private bool IsPoiVisible(PointOfInterest poi)
		{
			if (poi == null) return false;
			return poi.IsRevealed || poi.IsCompleted;
		}

		private bool TryFindLocationByPoiId(string poiId, out string locationId, out int questIndex)
		{
			locationId = null;
			questIndex = -1;
			if (!SaveCache.TryGetRunNode(poiId, out _, out questIndex)) return false;
			locationId = "desert";
			return true;
		}
	}
}
