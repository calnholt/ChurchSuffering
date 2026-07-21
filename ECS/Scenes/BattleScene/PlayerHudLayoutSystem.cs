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
	[DebugTab("Player HUD Layout")]
	public class PlayerHudLayoutSystem : Core.System
	{
		public const string RootEntityName = "UI_PlayerHudRoot";
		public const string HealthEntityName = "UI_PlayerHudHealth";
		public const string CourageEntityName = "UI_PlayerHudCourage";
		public const string TemperanceEntityName = "UI_PlayerHudTemperance";
		public const string ActionPointEntityName = "UI_PlayerHudActionPoint";
		public const string PledgeEntityName = "UI_PlayerHudPledge";

		[DebugEditable(DisplayName = "Portrait Gap", Step = 1, Min = -200, Max = 400)]
		public int PortraitGap { get; set; } = 4;

		[DebugEditable(DisplayName = "Chip Height", Step = 1, Min = 20, Max = 120)]
		public int ChipHeight { get; set; } = 36;

		[DebugEditable(DisplayName = "Chip Slant", Step = 1, Min = 0, Max = 60)]
		public int Slant { get; set; } = 14;

		[DebugEditable(DisplayName = "Chip Overlap", Step = 1, Min = 0, Max = 60)]
		public int RegionOverlap { get; set; } = 14;

		[DebugEditable(DisplayName = "Row Overlap", Step = 1, Min = 0, Max = 10)]
		public int RowOverlap { get; set; } = 1;

		[DebugEditable(DisplayName = "HP Width Extension", Step = 1, Min = 0, Max = 80)]
		public int HpWidthExtension { get; set; } = 14;

		[DebugEditable(DisplayName = "Courage Width", Step = 1, Min = 40, Max = 400)]
		public int CourageWidth { get; set; } = 110;

		[DebugEditable(DisplayName = "Temperance Width", Step = 1, Min = 40, Max = 400)]
		public int TemperanceWidth { get; set; } = 144;

		[DebugEditable(DisplayName = "Action Point Width", Step = 1, Min = 40, Max = 400)]
		public int ActionPointWidth { get; set; } = 85;

		[DebugEditable(DisplayName = "Pledge Width", Step = 1, Min = 80, Max = 500)]
		public int PledgeWidth { get; set; } = 262;

		[DebugEditable(DisplayName = "Pledge Icon Size", Step = 1, Min = 8, Max = 100)]
		public int PledgeIconSize { get; set; } = 36;

		[DebugEditable(DisplayName = "Content Gap", Step = 1, Min = 0, Max = 80)]
		public int ContentGap { get; set; } = 8;

		[DebugEditable(DisplayName = "Label Letter Spacing", Step = 1, Min = 0, Max = 20)]
		public int LabelLetterSpacing { get; set; } = 2;

		[DebugEditable(DisplayName = "Health Padding Left", Step = 1, Min = 0, Max = 80)]
		public int HealthPaddingLeft { get; set; } = 14;

		[DebugEditable(DisplayName = "Health Padding Right", Step = 1, Min = 0, Max = 80)]
		public int HealthPaddingRight { get; set; } = 18;

		[DebugEditable(DisplayName = "Health Padding Vertical", Step = 1, Min = 0, Max = 40)]
		public int HealthPaddingVertical { get; set; } = 0;

		[DebugEditable(DisplayName = "Health Track Height", Step = 1, Min = 4, Max = 80)]
		public int HealthTrackHeight { get; set; } = 26;

		[DebugEditable(DisplayName = "Health Track Border", Step = 1, Min = 0, Max = 12)]
		public int HealthTrackBorderThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "Courage Padding Left", Step = 1, Min = 0, Max = 80)]
		public int CouragePaddingLeft { get; set; } = 4;

		[DebugEditable(DisplayName = "Courage Padding Right", Step = 1, Min = 0, Max = 80)]
		public int CouragePaddingRight { get; set; } = 2;

		[DebugEditable(DisplayName = "Temperance Padding Left", Step = 1, Min = 0, Max = 80)]
		public int TemperancePaddingLeft { get; set; } = 14;

		[DebugEditable(DisplayName = "Temperance Padding Right", Step = 1, Min = 0, Max = 80)]
		public int TemperancePaddingRight { get; set; } = 16;

		[DebugEditable(DisplayName = "Action Point Padding Left", Step = 1, Min = 0, Max = 80)]
		public int ActionPointPaddingLeft { get; set; } = 0;

		[DebugEditable(DisplayName = "Action Point Padding Right", Step = 1, Min = 0, Max = 80)]
		public int ActionPointPaddingRight { get; set; } = 0;

		[DebugEditable(DisplayName = "Pledge Padding Left", Step = 1, Min = 0, Max = 80)]
		public int PledgePaddingLeft { get; set; } = 12;

		[DebugEditable(DisplayName = "Pledge Padding Right", Step = 1, Min = 0, Max = 80)]
		public int PledgePaddingRight { get; set; } = 18;

		[DebugEditable(DisplayName = "Pledge Padding Vertical", Step = 1, Min = 0, Max = 40)]
		public int PledgePaddingVertical { get; set; } = 4;

		[DebugEditable(DisplayName = "Pledge Content Gap", Step = 1, Min = 0, Max = 80)]
		public int PledgeContentGap { get; set; } = 10;

		[DebugEditable(DisplayName = "Temperance Chunk Width", Step = 1, Min = 2, Max = 80)]
		public int TemperanceChunkWidth { get; set; } = 21;

		[DebugEditable(DisplayName = "Temperance Chunk Height", Step = 1, Min = 2, Max = 80)]
		public int TemperanceChunkHeight { get; set; } = 26;

		[DebugEditable(DisplayName = "Temperance Chunk Gap", Step = 1, Min = -20, Max = 40)]
		public int TemperanceChunkGap { get; set; } = -8;

		[DebugEditable(DisplayName = "Label Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float LabelFontScale { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Value Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ValueFontScale { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "HUD Red R", Step = 1, Min = 0, Max = 255)]
		public int HudRedR { get; set; } = 196;

		[DebugEditable(DisplayName = "HUD Red G", Step = 1, Min = 0, Max = 255)]
		public int HudRedG { get; set; } = 30;

		[DebugEditable(DisplayName = "HUD Red B", Step = 1, Min = 0, Max = 255)]
		public int HudRedB { get; set; } = 58;

		[DebugEditable(DisplayName = "HUD Black", Step = 1, Min = 0, Max = 255)]
		public int HudBlackValue { get; set; } = 10;

		[DebugEditable(DisplayName = "Courage Inset Shadow Height", Step = 1, Min = 0, Max = 20)]
		public int CourageInsetShadowHeight { get; set; } = 0;

		[DebugEditable(DisplayName = "Courage Inset Shadow Alpha", Step = 1, Min = 0, Max = 255)]
		public int CourageInsetShadowAlpha { get; set; } = 64;

		[DebugEditable(DisplayName = "Action Point Glow Radius", Step = 1, Min = 0, Max = 40)]
		public int ActionPointGlowRadius { get; set; } = 10;

		[DebugEditable(DisplayName = "Action Point Glow Alpha", Step = 1, Min = 0, Max = 255)]
		public int ActionPointGlowAlpha { get; set; } = 115;

		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 1, Min = -40, Max = 80)]
		public int ShadowOffsetY { get; set; } = 6;

		[DebugEditable(DisplayName = "Shadow Blur Radius", Step = 1, Min = 0, Max = 80)]
		public int ShadowBlurRadius { get; set; } = 20;

		[DebugEditable(DisplayName = "Shadow Alpha", Step = 1, Min = 0, Max = 255)]
		public int ShadowAlpha { get; set; } = 140;

		[DebugEditable(DisplayName = "Resource Pulse Seconds", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float ResourcePulseDurationSeconds { get; set; } = 0.30f;

		[DebugEditable(DisplayName = "Resource Pulse Scale", Step = 0.01f, Min = 1f, Max = 2f)]
		public float ResourcePulseMaxScale { get; set; } = 1.12f;

		public PlayerHudLayoutSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsHudScene(GetCurrentScene()))
			{
				DestroyHudEntities();
				return;
			}

			EnsureHudEntities();
			LayoutHud();
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (IsHudScene(evt.Scene))
			{
				EnsureHudEntities();
				LayoutHud();
			}
			else
			{
				DestroyHudEntities();
			}
		}

		private void EnsureHudEntities()
		{
			var root = EnsureRegion(
				PlayerHudRegionType.Root,
				RootEntityName,
				false,
				string.Empty,
				false,
				null);
			EnsureRegion(PlayerHudRegionType.Health, HealthEntityName, false, string.Empty, false, root);
			EnsureRegion(
				PlayerHudRegionType.Courage,
				CourageEntityName,
				true,
				"Courage\n\nBlocking with red cards increases your courage by 1.",
				true,
				root);
			EnsureRegion(
				PlayerHudRegionType.Temperance,
				TemperanceEntityName,
				true,
				"Temperance",
				true,
				root);
			EnsureRegion(
				PlayerHudRegionType.ActionPoint,
				ActionPointEntityName,
				true,
				"Action Points",
				true,
				root);
			EnsureRegion(PlayerHudRegionType.Pledge, PledgeEntityName, false, string.Empty, false, root);
		}

		private Entity EnsureRegion(
			PlayerHudRegionType type,
			string name,
			bool interactable,
			string tooltip,
			bool hasFeedback,
			Entity parent)
		{
			var matching = EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.Where(entity => entity.GetComponent<PlayerHudRegion>()?.Type == type)
				.OrderBy(entity => entity.Id)
				.ToList();
			var entity = matching.FirstOrDefault();
			foreach (var duplicate in matching.Skip(1))
			{
				EntityManager.DestroyEntity(duplicate.Id);
			}

			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new PlayerHudRegion { Type = type });
			}
			if (!entity.HasComponent<Transform>())
			{
				EntityManager.AddComponent(entity, new Transform());
			}
			if (!entity.HasComponent<UIElement>())
			{
				EntityManager.AddComponent(entity, new UIElement());
			}

			entity.Name = name;
			if (type == PlayerHudRegionType.Root)
			{
				if (!entity.HasComponent<PlayerHudAnchor>())
				{
					EntityManager.AddComponent(entity, new PlayerHudAnchor());
				}
				if (entity.HasComponent<ParentTransform>())
				{
					EntityManager.RemoveComponent<ParentTransform>(entity);
				}

				var defaultParallax = ParallaxLayer.GetUIParallaxLayer();
				var parallax = entity.GetComponent<ParallaxLayer>();
				if (parallax == null)
				{
					EntityManager.AddComponent(entity, defaultParallax);
				}
				else
				{
					parallax.MultiplierX = defaultParallax.MultiplierX;
					parallax.MultiplierY = defaultParallax.MultiplierY;
					parallax.MaxOffset = defaultParallax.MaxOffset;
					parallax.SmoothTime = defaultParallax.SmoothTime;
				}
			}
			else
			{
				var parentTransform = entity.GetComponent<ParentTransform>();
				if (parentTransform == null)
				{
					EntityManager.AddComponent(entity, new ParentTransform { Parent = parent });
				}
				else
				{
					parentTransform.Parent = parent;
				}
				if (entity.HasComponent<ParallaxLayer>())
				{
					EntityManager.RemoveComponent<ParallaxLayer>(entity);
				}
			}
			if (hasFeedback && !entity.HasComponent<PlayerHudFeedbackState>())
			{
				EntityManager.AddComponent(entity, new PlayerHudFeedbackState());
			}

			var ui = entity.GetComponent<UIElement>();
			ui.IsInteractable = interactable;
			ui.Tooltip = tooltip;
			ui.TooltipType = interactable ? TooltipType.Text : TooltipType.None;
			ui.TooltipPosition = TooltipPosition.Above;
			ui.ShowHoverHighlight = false;
			ui.EventType = UIElementEventType.None;
			ui.SecondaryEventType = UIElementEventType.None;
			ui.IsHidden = false;
			return entity;
		}

		private void LayoutHud()
		{
			var root = GetRegionEntity(PlayerHudRegionType.Root);
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			var playerTransform = player?.GetComponent<Transform>();
			var portrait = player?.GetComponent<PortraitInfo>();
			if (root == null || playerTransform == null || portrait == null
				|| portrait.TextureWidth <= 0 || portrait.TextureHeight <= 0)
			{
				HideAndClearBounds();
				return;
			}

			float stableScale = Math.Max(0f, portrait.BaseScale);
			int portraitWidth = (int)Math.Round(portrait.TextureWidth * stableScale);
			int portraitHeight = (int)Math.Round(portrait.TextureHeight * stableScale);
			var portraitBounds = new Rectangle(
				(int)Math.Round(playerTransform.Position.X - portraitWidth / 2f),
				(int)Math.Round(playerTransform.Position.Y - portraitHeight / 2f),
				portraitWidth,
				portraitHeight);

			int chipHeight = Math.Max(1, ChipHeight);
			int overlap = Math.Max(0, RegionOverlap);
			int rowOverlap = Math.Clamp(RowOverlap, 0, chipHeight - 1);
			int resourceWidth = Math.Max(1,
				CourageWidth + TemperanceWidth + ActionPointWidth + PledgeWidth - overlap * 3);
			int rootWidth = resourceWidth + Math.Max(0, HpWidthExtension);
			int rootX = (int)Math.Round(playerTransform.Position.X - rootWidth / 2f);
			int rootY = portraitBounds.Bottom + PortraitGap;
			var rootBounds = new Rectangle(0, 0, rootWidth, chipHeight * 2 - rowOverlap);
			var resourceRow = new Rectangle(0, chipHeight - rowOverlap, resourceWidth, chipHeight);
			var healthRow = new Rectangle(
				Math.Max(0, Slant),
				0,
				Math.Max(1, resourceWidth - Math.Max(0, Slant) + Math.Max(0, HpWidthExtension)),
				chipHeight);

			int x = resourceRow.X;
			SetRegionBounds(
				PlayerHudRegionType.Courage,
				new Rectangle(0, 0, CourageWidth, chipHeight),
				new Vector2(x, resourceRow.Y),
				true);
			x += CourageWidth - overlap;
			SetRegionBounds(
				PlayerHudRegionType.Temperance,
				new Rectangle(0, 0, TemperanceWidth, chipHeight),
				new Vector2(x, resourceRow.Y),
				true);
			x += TemperanceWidth - overlap;
			SetRegionBounds(
				PlayerHudRegionType.ActionPoint,
				new Rectangle(0, 0, ActionPointWidth, chipHeight),
				new Vector2(x, resourceRow.Y),
				true);
			x += ActionPointWidth - overlap;
			SetRegionBounds(
				PlayerHudRegionType.Pledge,
				new Rectangle(0, 0, PledgeWidth, chipHeight),
				new Vector2(x, resourceRow.Y),
				true);
			SetRegionBounds(
				PlayerHudRegionType.Health,
				new Rectangle(0, 0, healthRow.Width, healthRow.Height),
				new Vector2(healthRow.X, healthRow.Y),
				true);
			SetRegionBounds(
				PlayerHudRegionType.Root,
				rootBounds,
				new Vector2(rootX, rootY),
				true,
				new Rectangle(rootX, rootY, rootWidth, chipHeight * 2 - rowOverlap));

			var anchor = root.GetComponent<PlayerHudAnchor>();
			anchor.Bounds = rootBounds;
			anchor.StablePortraitBounds = portraitBounds;
			anchor.HealthRowBounds = healthRow;
			anchor.ResourceRowBounds = resourceRow;
			anchor.ChipHeight = chipHeight;
			anchor.Slant = Math.Max(0, Slant);
			anchor.RegionOverlap = overlap;
			anchor.HpWidthExtension = Math.Max(0, HpWidthExtension);
			anchor.PledgeIconSize = Math.Max(1, PledgeIconSize);
			anchor.ContentGap = Math.Max(0, ContentGap);
			anchor.LabelLetterSpacing = Math.Max(0, LabelLetterSpacing);
			anchor.HealthPaddingLeft = Math.Max(0, HealthPaddingLeft);
			anchor.HealthPaddingRight = Math.Max(0, HealthPaddingRight);
			anchor.HealthPaddingVertical = Math.Max(0, HealthPaddingVertical);
			anchor.HealthTrackHeight = Math.Max(1, HealthTrackHeight);
			anchor.HealthTrackBorderThickness = Math.Max(0, HealthTrackBorderThickness);
			anchor.CouragePaddingLeft = Math.Max(0, CouragePaddingLeft);
			anchor.CouragePaddingRight = Math.Max(0, CouragePaddingRight);
			anchor.TemperancePaddingLeft = Math.Max(0, TemperancePaddingLeft);
			anchor.TemperancePaddingRight = Math.Max(0, TemperancePaddingRight);
			anchor.ActionPointPaddingLeft = Math.Max(0, ActionPointPaddingLeft);
			anchor.ActionPointPaddingRight = Math.Max(0, ActionPointPaddingRight);
			anchor.PledgePaddingLeft = Math.Max(0, PledgePaddingLeft);
			anchor.PledgePaddingRight = Math.Max(0, PledgePaddingRight);
			anchor.PledgePaddingVertical = Math.Max(0, PledgePaddingVertical);
			anchor.PledgeContentGap = Math.Max(0, PledgeContentGap);
			anchor.TemperanceChunkWidth = Math.Max(1, TemperanceChunkWidth);
			anchor.TemperanceChunkHeight = Math.Max(1, TemperanceChunkHeight);
			anchor.TemperanceChunkGap = TemperanceChunkGap;
			anchor.HudRed = new Color(ClampByte(HudRedR), ClampByte(HudRedG), ClampByte(HudRedB));
			byte black = ClampByte(HudBlackValue);
			anchor.HudBlack = new Color(black, black, black);
			anchor.HudWhite = Color.White;
			anchor.LabelFontScale = Math.Max(0.01f, LabelFontScale);
			anchor.ValueFontScale = Math.Max(0.01f, ValueFontScale);
			anchor.CourageInsetShadowHeight = Math.Max(0, CourageInsetShadowHeight);
			anchor.CourageInsetShadowAlpha = ClampByte(CourageInsetShadowAlpha);
			anchor.ActionPointGlowRadius = Math.Max(0, ActionPointGlowRadius);
			anchor.ActionPointGlowAlpha = ClampByte(ActionPointGlowAlpha);
			anchor.ShadowOffsetY = ShadowOffsetY;
			anchor.ShadowBlurRadius = Math.Max(0, ShadowBlurRadius);
			anchor.ShadowAlpha = ClampByte(ShadowAlpha);
			anchor.ResourcePulseDurationSeconds = Math.Max(0.01f, ResourcePulseDurationSeconds);
			anchor.ResourcePulseMaxScale = Math.Max(1f, ResourcePulseMaxScale);
		}

		private void SetRegionBounds(
			PlayerHudRegionType type,
			Rectangle bounds,
			Vector2 position,
			bool inScope,
			Rectangle? uiBounds = null)
		{
			var entity = GetRegionEntity(type);
			if (entity == null) return;
			var region = entity.GetComponent<PlayerHudRegion>();
			var transform = entity.GetComponent<Transform>();
			var ui = entity.GetComponent<UIElement>();
			region.Bounds = bounds;
			transform.Position = position;
			transform.Rotation = 0f;
			transform.Scale = Vector2.One;
			ui.Bounds = uiBounds ?? bounds;
			ui.IsHidden = !inScope;
			if (!inScope)
			{
				ui.IsInteractable = false;
				ui.IsHovered = false;
			}
		}

		private void HideAndClearBounds()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<PlayerHudRegion>().ToList())
			{
				SetRegionBounds(
					entity.GetComponent<PlayerHudRegion>().Type,
					Rectangle.Empty,
					Vector2.Zero,
					false);
			}
		}

		private Entity GetRegionEntity(PlayerHudRegionType type)
		{
			return EntityManager.GetEntitiesWithComponent<PlayerHudRegion>()
				.FirstOrDefault(entity => entity.GetComponent<PlayerHudRegion>()?.Type == type);
		}

		private void DestroyHudEntities()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<PlayerHudRegion>().ToList())
			{
				EntityManager.DestroyEntity(entity.Id);
			}
		}

		private SceneId GetCurrentScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current ?? SceneId.None;
		}

		private static bool IsHudScene(SceneId scene)
		{
			return scene == SceneId.Battle || scene == SceneId.Snapshot;
		}

		private static byte ClampByte(int value)
		{
			return (byte)Math.Clamp(value, 0, 255);
		}
	}
}
