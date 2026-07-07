using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Medals;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Objects.Medals;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation Saints Medals")]
	public class WayStationSaintsMedalsModalSystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _titleFont = FontSingleton.TitleFont;
		private readonly SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
		private readonly RasterizerState _scissorRasterizer;

		private static readonly Color PanelFill = new Color(8, 8, 8) * 0.94f;
		private static readonly Color PanelBorder = Color.White * 0.85f;
		private static readonly Color InsetHighlight = Color.White * 0.08f;
		private static readonly Color Divider = Color.White * 0.08f;
		private static readonly Color HeaderDivider = Color.White * 0.10f;
		private static readonly Color FooterFill = Color.Black * 0.25f;
		private static readonly Color BodyText = new Color(240, 236, 230);
		private static readonly Color MutedText = new Color(200, 192, 184);
		private static readonly Color DetailText = new Color(180, 176, 168);
		private static readonly Color KeywordText = new Color(152, 144, 136);
		private static readonly Color KeywordAccent = new Color(212, 168, 74);
		private static readonly Color RedDark = new Color(139, 0, 0);
		private static readonly Color RedBrick = new Color(180, 50, 50);
		private static readonly Color RedAccent = new Color(196, 30, 58);
		private static readonly Color RedGlow = new Color(196, 30, 58) * 0.45f;
		private static readonly Color TileLockedFill = new Color(26, 26, 26);

		[DebugEditable(DisplayName = "Modal Margin", Step = 2, Min = 0, Max = 160)]
		public int ModalMargin { get; set; } = 48;
		[DebugEditable(DisplayName = "Border Thickness", Step = 1, Min = 1, Max = 8)]
		public int BorderThickness { get; set; } = 2;
		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 1, Min = 0, Max = 80)]
		public int ShadowOffsetY { get; set; } = 32;
		[DebugEditable(DisplayName = "Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ShadowAlpha { get; set; } = 0.75f;
		[DebugEditable(DisplayName = "Header Height", Step = 2, Min = 64, Max = 180)]
		public int HeaderHeight { get; set; } = 104;
		[DebugEditable(DisplayName = "Footer Height", Step = 2, Min = 36, Max = 120)]
		public int FooterHeight { get; set; } = 53;
		[DebugEditable(DisplayName = "Left Split Percent", Step = 0.01f, Min = 0.25f, Max = 0.60f)]
		public float LeftSplitPercent { get; set; } = 0.42f;
		[DebugEditable(DisplayName = "Toolbar Height", Step = 2, Min = 24, Max = 100)]
		public int ToolbarHeight { get; set; } = 45;
		[DebugEditable(DisplayName = "List Padding X", Step = 2, Min = 0, Max = 80)]
		public int ListPaddingX { get; set; } = 24;
		[DebugEditable(DisplayName = "List Padding Top", Step = 2, Min = 0, Max = 80)]
		public int ListPaddingTop { get; set; } = 20;
		[DebugEditable(DisplayName = "List Padding Bottom", Step = 2, Min = 0, Max = 120)]
		public int ListPaddingBottom { get; set; } = 28;
		[DebugEditable(DisplayName = "Detail Padding X", Step = 2, Min = 0, Max = 100)]
		public int DetailPaddingX { get; set; } = 36;
		[DebugEditable(DisplayName = "Detail Padding Top", Step = 2, Min = 0, Max = 100)]
		public int DetailPaddingTop { get; set; } = 28;
		[DebugEditable(DisplayName = "Detail Padding Bottom", Step = 2, Min = 0, Max = 120)]
		public int DetailPaddingBottom { get; set; } = 32;
		[DebugEditable(DisplayName = "Icon Size", Step = 2, Min = 40, Max = 180)]
		public int IconSize { get; set; } = 88;
		[DebugEditable(DisplayName = "Icon Gap", Step = 1, Min = 0, Max = 60)]
		public int IconGap { get; set; } = 14;
		[DebugEditable(DisplayName = "Tile Label Gap", Step = 1, Min = 0, Max = 40)]
		public int TileLabelGap { get; set; } = 8;
		[DebugEditable(DisplayName = "Tile Label Height", Step = 1, Min = 0, Max = 80)]
		public int TileLabelHeight { get; set; } = 28;
		[DebugEditable(DisplayName = "Close Button Size", Step = 2, Min = 24, Max = 96)]
		public int CloseButtonSize { get; set; } = 40;
		[DebugEditable(DisplayName = "Header Padding", Step = 2, Min = 0, Max = 100)]
		public int HeaderPadding { get; set; } = 32;
		[DebugEditable(DisplayName = "Footer Padding X", Step = 2, Min = 0, Max = 100)]
		public int FooterPaddingX { get; set; } = 32;
		[DebugEditable(DisplayName = "Meter Width", Step = 2, Min = 120, Max = 900)]
		public int MeterWidth { get; set; } = 520;
		[DebugEditable(DisplayName = "Meter Height", Step = 1, Min = 4, Max = 32)]
		public int MeterHeight { get; set; } = 12;
		[DebugEditable(DisplayName = "Scroll Step", Step = 1, Min = 0, Max = 240)]
		public int ScrollStep { get; set; } = 84;
		[DebugEditable(DisplayName = "Gamepad Scroll Speed", Step = 50, Min = 100, Max = 6000)]
		public float GamepadScrollSpeed { get; set; } = 1300f;
		[DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float TitleScale { get; set; } = 0.34f;
		[DebugEditable(DisplayName = "Close Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float CloseScale { get; set; } = 0.17f;
		[DebugEditable(DisplayName = "Toolbar Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float ToolbarScale { get; set; } = 0.09f;
		[DebugEditable(DisplayName = "Tile Label Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float TileLabelScale { get; set; } = 0.08f;
		[DebugEditable(DisplayName = "Saint Name Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float SaintNameScale { get; set; } = 0.28f;
		[DebugEditable(DisplayName = "Medal Text Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float MedalTextScale { get; set; } = 0.13f;
		[DebugEditable(DisplayName = "Keyword Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float KeywordScale { get; set; } = 0.10f;
		[DebugEditable(DisplayName = "Section Label Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float SectionLabelScale { get; set; } = 0.08f;
		[DebugEditable(DisplayName = "Body Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float BodyScale { get; set; } = 0.12f;
		[DebugEditable(DisplayName = "Footer Label Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float FooterLabelScale { get; set; } = 0.11f;
		[DebugEditable(DisplayName = "Footer Count Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float FooterCountScale { get; set; } = 0.16f;

		private struct SaintsMedalsLayout
		{
			public Rectangle Modal;
			public Rectangle Content;
			public Rectangle Header;
			public Rectangle Body;
			public Rectangle Footer;
			public Rectangle CloseButton;
			public Rectangle ListPanel;
			public Rectangle ListToolbar;
			public Rectangle ListClip;
			public Rectangle DetailPanel;
			public Rectangle DetailClip;
			public Rectangle CollectionMeter;
			public Vector2 TitlePosition;
			public Vector2 ToolbarPosition;
			public Vector2 FooterLabelPosition;
			public Vector2 FooterCountPosition;
		}

		private sealed class MedalEntry
		{
			public string Id { get; init; } = string.Empty;
			public MedalBase Medal { get; init; }
			public SaintBlurbDefinition Saint { get; init; }
			public bool Purchased { get; init; }
		}

		public WayStationSaintsMedalsModalSystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = imageAssets.GetPixel(Color.White);
			_scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
			EventManager.Subscribe<OpenWayStationSaintsMedalsModalEvent>(OnOpenModal);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.WayStation)
			{
				CloseModal(immediate: true);
				SetModalEntitiesInteractable(false);
				return;
			}

			EnsureModalRoot();
			var animation = GetModalAnimation();
			bool modalOpen = animation != null && (animation.RequestedVisible || animation.Phase != ModalAnimationPhase.Hidden);
			bool modalInteractive = animation?.Phase == ModalAnimationPhase.Visible;
			var layout = ComputeLayout(Game1.VirtualWidth, Game1.VirtualHeight);
			var render = ModalAnimationRenderState.From(animation, layout.Modal);
			var entries = GetMedalEntries();
			var rootState = GetRootState();
			EnsureSelection(rootState, entries);

			SyncModalPanel(render.Transform(layout.Modal), modalOpen);
			SyncButton(WayStationSceneConstants.SaintsMedalsModalCloseButtonName, render.Transform(layout.CloseButton), modalInteractive);
			SyncScrollBlocker(WayStationSceneConstants.SaintsMedalsModalListScrollName, render.Transform(layout.ListClip), modalInteractive);
			SyncScrollBlocker(WayStationSceneConstants.SaintsMedalsModalDetailScrollName, render.Transform(layout.DetailClip), modalInteractive && !string.IsNullOrWhiteSpace(rootState?.SelectedMedalId));
			SyncMedalTiles(entries, layout, render, modalInteractive);

			if (!modalInteractive) return;

			if (WasClicked(WayStationSceneConstants.SaintsMedalsModalRootName)
				|| WasClicked(WayStationSceneConstants.SaintsMedalsModalCloseButtonName)
				|| WasCancelPressed())
			{
				CloseModal();
				return;
			}

			foreach (var entry in entries)
			{
				if (!entry.Purchased || !WasClicked(GetTileName(entry.Id))) continue;
				rootState.SelectedMedalId = entry.Id;
				rootState.DetailScrollOffset = 0;
				break;
			}

			UpdateScroll(rootState, entries, layout, gameTime);
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			if (scene == null || (scene.Current != SceneId.WayStation && scene.Current != SceneId.Snapshot)) return;

			var animation = GetModalAnimation();
			var layout = ComputeLayout(Game1.VirtualWidth, Game1.VirtualHeight);
			var render = ModalAnimationRenderState.From(animation, layout.Modal);
			if (!render.ShouldDraw) return;

			var entries = GetMedalEntries();
			var rootState = GetRootState();
			ModalOverlayChrome.DrawDim(_spriteBatch, _pixel, Game1.VirtualWidth, Game1.VirtualHeight, (int)System.Math.Round(173 * render.DimAlphaMultiplier));
			DrawPanel(layout, render);
			DrawHeader(layout, render);
			DrawMedalList(entries, rootState, layout, render);
			DrawDetail(entries, rootState, layout, render);
			DrawFooter(entries, layout, render);
			DrawCloseButton(render.Transform(layout.CloseButton), IsHovered(WayStationSceneConstants.SaintsMedalsModalCloseButtonName), render);
		}

		private void OnLoadScene(LoadSceneEvent e)
		{
			if (e.Scene != SceneId.WayStation) CloseModal(immediate: true);
		}

		private void OnOpenModal(OpenWayStationSaintsMedalsModalEvent e)
		{
			if (!IsWayStationActive()) return;
			OpenModal();
		}

		private SaintsMedalsLayout ComputeLayout(int vw, int vh)
		{
			int margin = System.Math.Max(0, ModalMargin);
			var modal = new Rectangle(margin, margin, vw - margin * 2, vh - margin * 2);
			int border = System.Math.Max(1, BorderThickness);
			var content = new Rectangle(modal.X + border, modal.Y + border, modal.Width - border * 2, modal.Height - border * 2);
			var header = new Rectangle(content.X, content.Y, content.Width, System.Math.Max(1, HeaderHeight));
			var footer = new Rectangle(content.X, content.Bottom - System.Math.Max(1, FooterHeight), content.Width, System.Math.Max(1, FooterHeight));
			var body = new Rectangle(content.X, header.Bottom, content.Width, System.Math.Max(1, footer.Y - header.Bottom));
			int listW = System.Math.Max(160, (int)System.Math.Round(body.Width * MathHelper.Clamp(LeftSplitPercent, 0.25f, 0.60f)));
			var listPanel = new Rectangle(body.X, body.Y, listW, body.Height);
			var detailPanel = new Rectangle(listPanel.Right, body.Y, System.Math.Max(1, body.Right - listPanel.Right), body.Height);
			var toolbar = new Rectangle(listPanel.X, listPanel.Y, listPanel.Width, System.Math.Max(1, ToolbarHeight));
			var listClip = new Rectangle(
				listPanel.X + ListPaddingX,
				toolbar.Bottom + ListPaddingTop,
				System.Math.Max(1, listPanel.Width - ListPaddingX * 2),
				System.Math.Max(1, listPanel.Bottom - toolbar.Bottom - ListPaddingTop - ListPaddingBottom));
			var detailClip = new Rectangle(
				detailPanel.X + DetailPaddingX,
				detailPanel.Y + DetailPaddingTop,
				System.Math.Max(1, detailPanel.Width - DetailPaddingX * 2),
				System.Math.Max(1, detailPanel.Height - DetailPaddingTop - DetailPaddingBottom));
			var close = new Rectangle(
				header.Right - HeaderPadding - CloseButtonSize,
				header.Y + HeaderPadding,
				CloseButtonSize,
				CloseButtonSize);

			var titleSize = Measure(_titleFont, "Communion of Saints", TitleScale);
			var titlePos = new Vector2(header.Center.X - titleSize.X / 2f, header.Center.Y - titleSize.Y / 2f);
			var toolbarPos = new Vector2(toolbar.X + ListPaddingX, toolbar.Y + 18);
			var footerLabelPos = new Vector2(footer.X + FooterPaddingX, footer.Y + footer.Height / 2f - Measure(_bodyFont, "COLLECTION", FooterLabelScale).Y / 2f);
			string countSample = "00 / 00";
			var countSize = Measure(_titleFont, countSample, FooterCountScale);
			var countPos = new Vector2(footer.Right - FooterPaddingX - countSize.X, footer.Y + footer.Height / 2f - countSize.Y / 2f);
			var meter = new Rectangle(
				footer.Center.X - System.Math.Min(MeterWidth, footer.Width - 520) / 2,
				footer.Center.Y - MeterHeight / 2,
				System.Math.Min(MeterWidth, System.Math.Max(1, footer.Width - 520)),
				System.Math.Max(1, MeterHeight));

			return new SaintsMedalsLayout
			{
				Modal = modal,
				Content = content,
				Header = header,
				Body = body,
				Footer = footer,
				CloseButton = close,
				ListPanel = listPanel,
				ListToolbar = toolbar,
				ListClip = listClip,
				DetailPanel = detailPanel,
				DetailClip = detailClip,
				CollectionMeter = meter,
				TitlePosition = titlePos,
				ToolbarPosition = toolbarPos,
				FooterLabelPosition = footerLabelPos,
				FooterCountPosition = countPos,
			};
		}

		private void DrawPanel(SaintsMedalsLayout layout, ModalAnimationRenderState render)
		{
			var modal = render.Transform(layout.Modal);
			var header = render.Transform(layout.Header);
			var footer = render.Transform(layout.Footer);
			var list = render.Transform(layout.ListPanel);
			var shadow = new Rectangle(
				modal.X,
				modal.Y + (int)System.Math.Round(ShadowOffsetY * render.ShellScale),
				modal.Width,
				System.Math.Max(1, modal.Height - (int)System.Math.Round(ShadowOffsetY * render.ShellScale)));

			_spriteBatch.Draw(_pixel, shadow, render.ApplyShadow(Color.Black * MathHelper.Clamp(ShadowAlpha, 0f, 1f)));
			_spriteBatch.Draw(_pixel, modal, render.ApplyShell(PanelFill));
			_spriteBatch.Draw(_pixel, footer, render.ApplyShell(FooterFill));
			DrawBorder(modal, render.ApplyShell(PanelBorder), System.Math.Max(1, (int)System.Math.Round(BorderThickness * render.ShellScale)));
			DrawBorder(new Rectangle(modal.X + 1, modal.Y + 1, modal.Width - 2, modal.Height - 2), render.ApplyShell(InsetHighlight), 1);
			DrawHorizontalLine(header.X, header.Bottom - 1, header.Width, render.ApplyShell(HeaderDivider), 1);
			DrawHorizontalLine(footer.X, footer.Y, footer.Width, render.ApplyShell(Divider), 1);
			DrawVerticalLine(list.Right - 1, list.Y, list.Height, render.ApplyShell(Divider), 1);
		}

		private void DrawHeader(SaintsMedalsLayout layout, ModalAnimationRenderState render)
		{
			DrawStringWithShadow(_titleFont, "Communion of Saints", render.Transform(layout.TitlePosition), render.ApplyShell(Color.White), render.TransformScale(TitleScale));
			_spriteBatch.DrawString(
				_bodyFont,
				"MEDALS ACQUIRED",
				render.Transform(layout.ToolbarPosition),
				render.ApplyShell(MutedText),
				0f,
				Vector2.Zero,
				render.TransformScale(ToolbarScale),
				SpriteEffects.None,
				0f);
			var toolbar = render.Transform(layout.ListToolbar);
			DrawHorizontalLine(toolbar.X, toolbar.Bottom - 1, toolbar.Width, render.ApplyShell(Color.White * 0.06f), 1);
		}

		private void DrawMedalList(IReadOnlyList<MedalEntry> entries, WayStationSaintsMedalsModalRoot rootState, SaintsMedalsLayout layout, ModalAnimationRenderState render)
		{
			if (entries.Count == 0) return;
			var prevScissor = _graphicsDevice.ScissorRectangle;
			_spriteBatch.End();
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, _scissorRasterizer);
			_graphicsDevice.ScissorRectangle = IntersectWithScreen(render.Transform(layout.ListClip));

			int cols = 4;
			int cellW = System.Math.Max(1, (layout.ListClip.Width - IconGap * (cols - 1)) / cols);
			int cellH = IconSize + TileLabelGap + TileLabelHeight;
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				int col = i % cols;
				int row = i / cols;
				var cell = new Rectangle(
					layout.ListClip.X + col * (cellW + IconGap),
					layout.ListClip.Y + row * (cellH + IconGap) - (rootState?.ListScrollOffset ?? 0),
					cellW,
					cellH);
				DrawMedalTile(entry, cell, string.Equals(rootState?.SelectedMedalId, entry.Id, StringComparison.OrdinalIgnoreCase), render);
			}

			_spriteBatch.End();
			_graphicsDevice.ScissorRectangle = prevScissor;
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
		}

		private void DrawMedalTile(MedalEntry entry, Rectangle cell, bool selected, ModalAnimationRenderState render)
		{
			var transformedCell = render.Transform(cell);
			int iconSize = System.Math.Max(1, (int)System.Math.Round(IconSize * render.ShellScale));
			var center = new Vector2(transformedCell.Center.X, transformedCell.Y + iconSize / 2f);
			bool hovered = IsHovered(GetTileName(entry.Id));

			if (selected)
			{
				DrawCircle(center, iconSize + 22, render.ApplyShell(RedGlow));
				DrawRing(center, iconSize + 8, 4f * render.ShellScale, render.ApplyShell(RedAccent));
			}

			if (entry.Purchased)
			{
				MedalIconRenderService.DrawMedalIcon(
					_spriteBatch,
					_graphicsDevice,
					_titleFont,
					center,
					iconSize,
					entry.Id,
					_imageAssets);
				if (hovered && !selected)
				{
					DrawRing(center, iconSize + 6, 2f * render.ShellScale, render.ApplyShell(Color.White * 0.60f));
				}
				DrawCenteredWrappedText(entry.Medal?.Name ?? entry.Id, new Rectangle(transformedCell.X, transformedCell.Y + iconSize + TileLabelGap, transformedCell.Width, TileLabelHeight), selected ? Color.White : MutedText, render.TransformScale(TileLabelScale), _bodyFont);
				return;
			}

			DrawCircle(center, iconSize, render.ApplyShell(TileLockedFill * 0.85f));
			DrawRing(center, iconSize, 2f * render.ShellScale, render.ApplyShell(Color.White * 0.12f));
			DrawCenteredString(_titleFont, "?", new Rectangle((int)center.X - iconSize / 2, (int)center.Y - iconSize / 2, iconSize, iconSize), render.ApplyShell(Color.White * 0.28f), render.TransformScale(0.28f));
		}

		private void DrawDetail(IReadOnlyList<MedalEntry> entries, WayStationSaintsMedalsModalRoot rootState, SaintsMedalsLayout layout, ModalAnimationRenderState render)
		{
			var selected = entries.FirstOrDefault(entry => entry.Purchased && string.Equals(entry.Id, rootState?.SelectedMedalId, StringComparison.OrdinalIgnoreCase));
			if (selected == null)
			{
				DrawEmptyDetail(layout, render);
				return;
			}

			var prevScissor = _graphicsDevice.ScissorRectangle;
			_spriteBatch.End();
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, _scissorRasterizer);
			_graphicsDevice.ScissorRectangle = IntersectWithScreen(render.Transform(layout.DetailClip));

			int y = layout.DetailClip.Y - (rootState?.DetailScrollOffset ?? 0);
			DrawStringWithShadow(_titleFont, selected.Medal?.Name ?? selected.Id, render.Transform(new Vector2(layout.DetailClip.X, y)), render.ApplyShell(Color.White), render.TransformScale(SaintNameScale));
			y += (int)System.Math.Ceiling(_titleFont.LineSpacing * SaintNameScale) + 18;
			DrawMedalEffectBlock(selected, layout.DetailClip.X, ref y, layout.DetailClip.Width, render);
			DrawSaintContent(selected, layout.DetailClip.X, ref y, layout.DetailClip.Width, render);

			_spriteBatch.End();
			_graphicsDevice.ScissorRectangle = prevScissor;
			_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null);
		}

		private void DrawEmptyDetail(SaintsMedalsLayout layout, ModalAnimationRenderState render)
		{
			var clip = render.Transform(layout.DetailClip);
			var icon = new Rectangle(clip.Center.X - 36, clip.Center.Y - 82, 72, 72);
			DrawRing(new Vector2(icon.Center.X, icon.Center.Y), icon.Width, 2f * render.ShellScale, render.ApplyShell(Color.White * 0.15f));
			DrawCenteredString(_titleFont, "+", icon, render.ApplyShell(Color.White * 0.25f), render.TransformScale(0.22f));
			var textRect = new Rectangle(clip.Center.X - 180, icon.Bottom + 16, 360, 90);
			DrawCenteredWrappedText("Select a medal to read the life of the saint whose intercession you carry.", textRect, render.ApplyShell(MutedText), render.TransformScale(0.12f), _bodyFont);
		}

		private void DrawMedalEffectBlock(MedalEntry entry, int x, ref int y, int width, ModalAnimationRenderState render)
		{
			var keywordBlocks = TooltipTextService.GetKeywordTooltipBlocks(entry.Medal?.Text ?? string.Empty);
			int textHeight = MeasureWrappedHeight(entry.Medal?.Text ?? string.Empty, _bodyFont, MedalTextScale, width - 36);
			int keywordHeight = 0;
			foreach (var block in keywordBlocks)
			{
				keywordHeight += MeasureWrappedHeight(block.Text, _bodyFont, KeywordScale, width - 48) + 12;
			}

			int blockH = 16 + textHeight + (keywordBlocks.Count > 0 ? 12 + keywordHeight : 0) + 16;
			var blockRect = render.Transform(new Rectangle(x, y, width, blockH));
			_spriteBatch.Draw(_pixel, blockRect, render.ApplyShell(Color.White * 0.04f));
			DrawBorder(blockRect, render.ApplyShell(Color.White * 0.08f), 1);

			int cursor = y + 16;
			DrawWrappedText(entry.Medal?.Text ?? string.Empty, x + 18, ref cursor, width - 36, _bodyFont, MedalTextScale, render.ApplyShell(BodyText), render);
			if (keywordBlocks.Count > 0) cursor += 12;
			foreach (var keyword in keywordBlocks)
			{
				var lineRect = render.Transform(new Rectangle(x + 18, cursor, 2, MeasureWrappedHeight(keyword.Text, _bodyFont, KeywordScale, width - 48)));
				_spriteBatch.Draw(_pixel, lineRect, render.ApplyShell(KeywordAccent * 0.35f));
				DrawWrappedText(keyword.Text, x + 30, ref cursor, width - 48, _bodyFont, KeywordScale, render.ApplyShell(KeywordText), render);
				cursor += 12;
			}
			y += blockH + 20;
		}

		private void DrawSaintContent(MedalEntry entry, int x, ref int y, int width, ModalAnimationRenderState render)
		{
			var saint = entry.Saint;
			if (saint == null) return;

			DrawSection("YEARS ON EARTH", x, ref y, width, render);
			DrawWrappedText(saint.lifespan, x, ref y, width, _bodyFont, BodyScale, render.ApplyShell(MutedText), render);
			y += 14;

			DrawSection("LIFE", x, ref y, width, render);
			foreach (var paragraph in saint.bioParagraphs ?? new List<string>())
			{
				DrawWrappedText(paragraph, x, ref y, width, _bodyFont, BodyScale, render.ApplyShell(DetailText), render);
				y += 12;
			}

			DrawSection("PATRONAGES", x, ref y, width, render);
			DrawWrappedText(saint.patronages, x, ref y, width, _bodyFont, BodyScale, render.ApplyShell(MutedText), render);
			y += 14;

			DrawSection("PRAYER", x, ref y, width, render);
			var prayerTitle = string.IsNullOrWhiteSpace(saint.prayerTitle) ? "Associated prayer" : saint.prayerTitle;
			DrawWrappedText(prayerTitle, x, ref y, width, _bodyFont, KeywordScale, render.ApplyShell(KeywordAccent), render);
			y += 6;
			int prayerHeight = MeasureWrappedHeight(saint.prayerText, _bodyFont, BodyScale, width - 40) + 36;
			var prayerRect = render.Transform(new Rectangle(x, y, width, prayerHeight));
			_spriteBatch.Draw(_pixel, prayerRect, render.ApplyShell(Color.White * 0.04f));
			var leftBorder = render.Transform(new Rectangle(x, y, 3, prayerHeight));
			_spriteBatch.Draw(_pixel, leftBorder, render.ApplyShell(RedBrick));
			int prayerY = y + 18;
			DrawWrappedText(saint.prayerText, x + 20, ref prayerY, width - 40, _bodyFont, BodyScale, render.ApplyShell(BodyText), render);
			y += prayerHeight + 24;
		}

		private void DrawSection(string label, int x, ref int y, int width, ModalAnimationRenderState render)
		{
			_spriteBatch.DrawString(
				_bodyFont,
				label,
				render.Transform(new Vector2(x, y)),
				render.ApplyShell(MutedText),
				0f,
				Vector2.Zero,
				render.TransformScale(SectionLabelScale),
				SpriteEffects.None,
				0f);
			y += (int)System.Math.Ceiling(_bodyFont.LineSpacing * SectionLabelScale) + 8;
		}

		private void DrawFooter(IReadOnlyList<MedalEntry> entries, SaintsMedalsLayout layout, ModalAnimationRenderState render)
		{
			int acquired = entries.Count(entry => entry.Purchased);
			int total = entries.Count;
			_spriteBatch.DrawString(_bodyFont, "COLLECTION", render.Transform(layout.FooterLabelPosition), render.ApplyShell(MutedText), 0f, Vector2.Zero, render.TransformScale(FooterLabelScale), SpriteEffects.None, 0f);
			var meter = render.Transform(layout.CollectionMeter);
			_spriteBatch.Draw(_pixel, meter, render.ApplyShell(Color.White * 0.08f));
			if (total > 0 && acquired > 0)
			{
				int fillW = (int)System.Math.Round(meter.Width * (acquired / (float)total));
				var fill = new Rectangle(meter.X, meter.Y, System.Math.Max(1, fillW), meter.Height);
				DrawHorizontalGradient(fill, render.ApplyShell(RedDark), render.ApplyShell(RedBrick));
			}

			string count = $"{acquired} / {total}";
			var size = Measure(_titleFont, count, render.TransformScale(FooterCountScale));
			var pos = new Vector2(layout.Footer.Right - FooterPaddingX - size.X, layout.Footer.Y + layout.Footer.Height / 2f - size.Y / 2f);
			_spriteBatch.DrawString(_titleFont, count, render.Transform(pos), render.ApplyShell(Color.White), 0f, Vector2.Zero, render.TransformScale(FooterCountScale), SpriteEffects.None, 0f);
		}

		private void DrawCloseButton(Rectangle rect, bool hovered, ModalAnimationRenderState render)
		{
			var fill = hovered ? RedDark : Color.Black * 0.50f;
			var border = hovered ? RedAccent : Color.White * 0.50f;
			if (hovered)
			{
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X - 4, rect.Y - 4, rect.Width + 8, rect.Height + 8), render.ApplyShell(RedGlow));
			}
			_spriteBatch.Draw(_pixel, rect, render.ApplyShell(fill));
			DrawBorder(rect, render.ApplyShell(border), 2);
			DrawCenteredString(_titleFont, "X", rect, render.ApplyShell(Color.White), render.TransformScale(CloseScale));
		}

		private List<MedalEntry> GetMedalEntries()
		{
			var purchased = new HashSet<string>(SaveCache.GetPurchasedWayStationMedalIds(), StringComparer.OrdinalIgnoreCase);
			return MedalFactory.GetAllMedals()
				.Select(kv =>
				{
					string id = kv.Value?.Id ?? kv.Key.ToKey();
					SaintBlurbDefinitionCache.TryGet(id, out var saint);
					return new MedalEntry
					{
						Id = id,
						Medal = kv.Value,
						Saint = saint,
						Purchased = purchased.Contains(id),
					};
				})
				.OrderBy(entry => entry.Medal?.Name ?? entry.Id, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private void EnsureSelection(WayStationSaintsMedalsModalRoot rootState, IReadOnlyList<MedalEntry> entries)
		{
			if (rootState == null) return;
			if (entries.Any(entry => entry.Purchased && string.Equals(entry.Id, rootState.SelectedMedalId, StringComparison.OrdinalIgnoreCase))) return;
			rootState.SelectedMedalId = entries.FirstOrDefault(entry => entry.Purchased)?.Id ?? string.Empty;
			rootState.DetailScrollOffset = 0;
		}

		private void UpdateScroll(WayStationSaintsMedalsModalRoot rootState, IReadOnlyList<MedalEntry> entries, SaintsMedalsLayout layout, GameTime gameTime)
		{
			if (rootState == null) return;
			PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
			bool hasWheel = System.Math.Abs(input.ScrollDelta) > 0.001f;
			bool hasStick = MathF.Abs(input.RightStick.Y) > 0.15f;
			if (!hasWheel && !hasStick) return;

			bool detailHovered = layout.DetailClip.Contains(input.PointerPosition);
			bool listHovered = layout.ListClip.Contains(input.PointerPosition);
			bool useDetail = detailHovered || (!listHovered && !string.IsNullOrWhiteSpace(rootState.SelectedMedalId));
			int maxScroll = useDetail
				? System.Math.Max(0, CalculateDetailContentHeight(entries, rootState.SelectedMedalId, layout.DetailClip.Width) - layout.DetailClip.Height)
				: System.Math.Max(0, CalculateListContentHeight(entries, layout.ListClip.Width) - layout.ListClip.Height);
			int current = useDetail ? rootState.DetailScrollOffset : rootState.ListScrollOffset;

			if (hasWheel)
			{
				current -= (int)System.Math.Round(input.ScrollDelta) * ScrollStep;
			}
			if (hasStick)
			{
				float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
				current += (int)System.Math.Round(-System.Math.Sign(input.RightStick.Y) * GamepadScrollSpeed * dt);
			}
			current = Math.Clamp(current, 0, maxScroll);
			if (useDetail) rootState.DetailScrollOffset = current;
			else rootState.ListScrollOffset = current;
		}

		private int CalculateListContentHeight(IReadOnlyList<MedalEntry> entries, int width)
		{
			int cols = 4;
			int rows = (int)System.Math.Ceiling(entries.Count / (float)cols);
			int cellH = IconSize + TileLabelGap + TileLabelHeight;
			return rows <= 0 ? 0 : rows * cellH + (rows - 1) * IconGap;
		}

		private int CalculateDetailContentHeight(IReadOnlyList<MedalEntry> entries, string selectedId, int width)
		{
			var entry = entries.FirstOrDefault(candidate => string.Equals(candidate.Id, selectedId, StringComparison.OrdinalIgnoreCase));
			if (entry == null) return 0;
			int y = 0;
			y += (int)System.Math.Ceiling(_titleFont.LineSpacing * SaintNameScale) + 18;
			var keywordBlocks = TooltipTextService.GetKeywordTooltipBlocks(entry.Medal?.Text ?? string.Empty);
			int effectHeight = 16 + MeasureWrappedHeight(entry.Medal?.Text ?? string.Empty, _bodyFont, MedalTextScale, width - 36) + 16;
			if (keywordBlocks.Count > 0)
			{
				effectHeight += 12 + keywordBlocks.Sum(block => MeasureWrappedHeight(block.Text, _bodyFont, KeywordScale, width - 48) + 12);
			}
			y += effectHeight + 20;
			if (entry.Saint == null) return y;
			y += MeasureSectionHeight() + MeasureWrappedHeight(entry.Saint.lifespan, _bodyFont, BodyScale, width) + 14;
			y += MeasureSectionHeight();
			foreach (var paragraph in entry.Saint.bioParagraphs ?? new List<string>())
			{
				y += MeasureWrappedHeight(paragraph, _bodyFont, BodyScale, width) + 12;
			}
			y += MeasureSectionHeight() + MeasureWrappedHeight(entry.Saint.patronages, _bodyFont, BodyScale, width) + 14;
			y += MeasureSectionHeight() + MeasureWrappedHeight(entry.Saint.prayerTitle, _bodyFont, KeywordScale, width) + 6;
			y += MeasureWrappedHeight(entry.Saint.prayerText, _bodyFont, BodyScale, width - 40) + 36 + 24;
			return y;
		}

		private int MeasureSectionHeight()
		{
			return (int)System.Math.Ceiling(_bodyFont.LineSpacing * SectionLabelScale) + 8;
		}

		private int MeasureWrappedHeight(string text, SpriteFont font, float scale, int width)
		{
			if (font == null) return 0;
			return TextUtils.WrapText(font, text ?? string.Empty, scale, System.Math.Max(1, width)).Count * (int)System.Math.Ceiling(font.LineSpacing * scale);
		}

		private void DrawWrappedText(string text, int x, ref int y, int width, SpriteFont font, float scale, Color color, ModalAnimationRenderState render)
		{
			if (font == null) return;
			float drawScale = render.TransformScale(scale);
			foreach (var line in TextUtils.WrapText(font, text ?? string.Empty, scale, System.Math.Max(1, width)))
			{
				string safe = TextUtils.FilterUnsupportedGlyphs(font, line);
				_spriteBatch.DrawString(font, safe, render.Transform(new Vector2(x, y)), color, 0f, Vector2.Zero, drawScale, SpriteEffects.None, 0f);
				y += (int)System.Math.Ceiling(font.LineSpacing * scale);
			}
		}

		private void DrawCenteredWrappedText(string text, Rectangle rect, Color color, float scale, SpriteFont font)
		{
			if (font == null) return;
			var lines = TextUtils.WrapText(font, text ?? string.Empty, scale, rect.Width);
			float lineH = font.LineSpacing * scale;
			float blockH = lines.Count * lineH;
			float y = rect.Center.Y - blockH / 2f;
			foreach (var line in lines)
			{
				string safe = TextUtils.FilterUnsupportedGlyphs(font, line);
				var size = font.MeasureString(safe) * scale;
				_spriteBatch.DrawString(font, safe, new Vector2(rect.Center.X - size.X / 2f, y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
				y += lineH;
			}
		}

		private void DrawCenteredString(SpriteFont font, string text, Rectangle rect, Color color, float scale)
		{
			if (font == null) return;
			string safe = TextUtils.FilterUnsupportedGlyphs(font, text ?? string.Empty);
			var size = font.MeasureString(safe) * scale;
			_spriteBatch.DrawString(font, safe, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawStringWithShadow(SpriteFont font, string text, Vector2 pos, Color color, float scale)
		{
			if (font == null || string.IsNullOrEmpty(text)) return;
			string safe = TextUtils.FilterUnsupportedGlyphs(font, text);
			_spriteBatch.DrawString(font, safe, pos + new Vector2(0, 2), Color.Black * (0.8f * color.A / 255f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(font, safe, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawHorizontalGradient(Rectangle rect, Color left, Color right)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			for (int x = 0; x < rect.Width; x++)
			{
				float t = rect.Width <= 1 ? 1f : x / (float)(rect.Width - 1);
				_spriteBatch.Draw(_pixel, new Rectangle(rect.X + x, rect.Y, 1, rect.Height), Color.Lerp(left, right, t));
			}
		}

		private void DrawCircle(Vector2 center, int diameter, Color color)
		{
			int radius = System.Math.Max(1, diameter / 2);
			var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
			_spriteBatch.Draw(circle, center, null, color, 0f, new Vector2(radius, radius), 1f, SpriteEffects.None, 0f);
		}

		private void DrawRing(Vector2 center, int diameter, float thickness, Color color)
		{
			int size = System.Math.Max(1, diameter);
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, size, size, thickness);
			_spriteBatch.Draw(ring, new Rectangle((int)System.Math.Round(center.X - size / 2f), (int)System.Math.Round(center.Y - size / 2f), size, size), color);
		}

		private void DrawBorder(Rectangle rect, Color color, int thickness)
		{
			if (rect.Width <= 0 || rect.Height <= 0 || thickness <= 0) return;
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private void DrawHorizontalLine(int x, int y, int width, Color color, int thickness)
		{
			_spriteBatch.Draw(_pixel, new Rectangle(x, y, width, System.Math.Max(1, thickness)), color);
		}

		private void DrawVerticalLine(int x, int y, int height, Color color, int thickness)
		{
			_spriteBatch.Draw(_pixel, new Rectangle(x, y, System.Math.Max(1, thickness), height), color);
		}

		private static Vector2 Measure(SpriteFont font, string text, float scale)
		{
			return font == null ? Vector2.Zero : font.MeasureString(TextUtils.FilterUnsupportedGlyphs(font, text ?? string.Empty)) * scale;
		}

		private static Rectangle IntersectWithScreen(Rectangle rect)
		{
			return Rectangle.Intersect(new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), rect);
		}

		private void SyncMedalTiles(IReadOnlyList<MedalEntry> entries, SaintsMedalsLayout layout, ModalAnimationRenderState render, bool interactable)
		{
			int cols = 4;
			int cellW = System.Math.Max(1, (layout.ListClip.Width - IconGap * (cols - 1)) / cols);
			int cellH = IconSize + TileLabelGap + TileLabelHeight;
			for (int i = 0; i < entries.Count; i++)
			{
				var entry = entries[i];
				int col = i % cols;
				int row = i / cols;
				var cell = new Rectangle(
					layout.ListClip.X + col * (cellW + IconGap),
					layout.ListClip.Y + row * (cellH + IconGap) - (GetRootState()?.ListScrollOffset ?? 0),
					cellW,
					cellH);
				var iconBounds = new Rectangle(cell.Center.X - IconSize / 2, cell.Y, IconSize, IconSize);
				SyncMedalTile(entry, render.Transform(iconBounds), interactable && entry.Purchased);
			}
		}

		private void SyncMedalTile(MedalEntry entry, Rectangle bounds, bool interactable)
		{
			var name = GetTileName(entry.Id);
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement { TooltipType = TooltipType.None, ShowHoverHighlight = false });
				EntityManager.AddComponent(entity, new WayStationSaintsMedalsModalTile { MedalId = entry.Id });
				InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.SaintsMedalsModalContextId);
			}
			var transform = entity.GetComponent<Transform>();
			transform.Position = new Vector2(bounds.X, bounds.Y);
			transform.ZOrder = 10002;
			var ui = entity.GetComponent<UIElement>();
			ui.Bounds = bounds;
			ui.IsInteractable = interactable;
			ui.IsHidden = !interactable;
			ui.LayerType = UILayerType.Overlay;
			ui.TooltipType = TooltipType.None;
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.SaintsMedalsModalContextId);
		}

		private void EnsureModalRoot()
		{
			var root = EntityManager.GetEntity(WayStationSceneConstants.SaintsMedalsModalRootName);
			if (root == null)
			{
				root = EntityManager.CreateEntity(WayStationSceneConstants.SaintsMedalsModalRootName);
				EntityManager.AddComponent(root, new Transform { Position = Vector2.Zero, ZOrder = 10000 });
				EntityManager.AddComponent(root, new UIElement
				{
					Bounds = Rectangle.Empty,
					IsInteractable = false,
					IsHidden = true,
					LayerType = UILayerType.Overlay,
					TooltipType = TooltipType.None,
					ShowHoverHighlight = false
				});
				EntityManager.AddComponent(root, new ModalAnimation { InputContextId = WayStationSceneConstants.SaintsMedalsModalContextId });
				EntityManager.AddComponent(root, new WayStationSaintsMedalsModalRoot());
				InputContextService.EnsureContext(EntityManager, root, WayStationSceneConstants.SaintsMedalsModalContextId, 100, false);
			}

			InputContextService.EnsureContext(
				EntityManager,
				root,
				WayStationSceneConstants.SaintsMedalsModalContextId,
				100,
				root.GetComponent<ModalAnimation>()?.Phase != ModalAnimationPhase.Hidden);
		}

		private void SyncModalPanel(Rectangle bounds, bool visible)
		{
			var panel = EntityManager.GetEntity(WayStationSceneConstants.SaintsMedalsModalPanelName);
			if (panel == null)
			{
				panel = EntityManager.CreateEntity(WayStationSceneConstants.SaintsMedalsModalPanelName);
				EntityManager.AddComponent(panel, new Transform());
				EntityManager.AddComponent(panel, new UIElement { TooltipType = TooltipType.None, ShowHoverHighlight = false });
				EntityManager.AddComponent(panel, new WayStationSaintsMedalsModalPanel());
				InputContextService.EnsureMember(EntityManager, panel, WayStationSceneConstants.SaintsMedalsModalContextId);
			}
			var transform = panel.GetComponent<Transform>();
			transform.Position = new Vector2(bounds.X, bounds.Y);
			transform.ZOrder = 10001;
			var ui = panel.GetComponent<UIElement>();
			ui.Bounds = bounds;
			ui.IsInteractable = visible;
			ui.IsHidden = !visible;
			ui.LayerType = UILayerType.Overlay;
			InputContextService.EnsureMember(EntityManager, panel, WayStationSceneConstants.SaintsMedalsModalContextId);
		}

		private void SyncButton(string name, Rectangle bounds, bool interactable)
		{
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement { TooltipType = TooltipType.None, ShowHoverHighlight = false });
				EntityManager.AddComponent(entity, new WayStationSaintsMedalsModalCloseButton());
				InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.SaintsMedalsModalContextId);
			}
			var transform = entity.GetComponent<Transform>();
			transform.Position = new Vector2(bounds.X, bounds.Y);
			transform.ZOrder = 10003;
			var ui = entity.GetComponent<UIElement>();
			ui.Bounds = bounds;
			ui.IsInteractable = interactable;
			ui.IsHidden = !interactable;
			ui.LayerType = UILayerType.Overlay;
			ui.TooltipType = TooltipType.None;
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.SaintsMedalsModalContextId);
		}

		private void SyncScrollBlocker(string name, Rectangle bounds, bool interactable)
		{
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform());
				EntityManager.AddComponent(entity, new UIElement { TooltipType = TooltipType.None, ShowHoverHighlight = false });
				InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.SaintsMedalsModalContextId);
			}
			var transform = entity.GetComponent<Transform>();
			transform.Position = new Vector2(bounds.X, bounds.Y);
			transform.ZOrder = 10001;
			var ui = entity.GetComponent<UIElement>();
			ui.Bounds = bounds;
			ui.IsInteractable = interactable;
			ui.IsHidden = !interactable;
			ui.LayerType = UILayerType.Overlay;
			ui.TooltipType = TooltipType.None;
			InputContextService.EnsureMember(EntityManager, entity, WayStationSceneConstants.SaintsMedalsModalContextId);
		}

		private void SetModalEntitiesInteractable(bool interactable)
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<InputContextMember>())
			{
				var member = entity.GetComponent<InputContextMember>();
				if (member?.ContextId != WayStationSceneConstants.SaintsMedalsModalContextId) continue;
				var ui = entity.GetComponent<UIElement>();
				if (ui == null) continue;
				ui.IsInteractable = interactable;
				ui.IsHidden = !interactable;
			}
		}

		private void OpenModal()
		{
			EnsureModalRoot();
			var animation = GetModalAnimation();
			if (animation == null) return;
			var root = GetRootState();
			if (root != null)
			{
				root.ListScrollOffset = 0;
				root.DetailScrollOffset = 0;
				EnsureSelection(root, GetMedalEntries());
			}
			animation.RequestedVisible = true;
		}

		private void CloseModal(bool immediate = false)
		{
			var animation = GetModalAnimation();
			if (animation == null) return;
			animation.RequestedVisible = false;
			if (!immediate) return;
			animation.Phase = ModalAnimationPhase.Hidden;
			animation.ElapsedSeconds = 0f;
			var root = EntityManager.GetEntity(WayStationSceneConstants.SaintsMedalsModalRootName);
			var context = root?.GetComponent<InputContext>();
			if (context != null) context.IsActive = false;
			var rootUi = root?.GetComponent<UIElement>();
			if (rootUi != null)
			{
				rootUi.Bounds = Rectangle.Empty;
				rootUi.IsInteractable = false;
				rootUi.IsHidden = true;
				rootUi.IsHovered = false;
				rootUi.IsClicked = false;
			}
		}

		private WayStationSaintsMedalsModalRoot GetRootState()
		{
			return EntityManager.GetEntity(WayStationSceneConstants.SaintsMedalsModalRootName)?.GetComponent<WayStationSaintsMedalsModalRoot>();
		}

		private ModalAnimation GetModalAnimation()
		{
			return EntityManager.GetEntity(WayStationSceneConstants.SaintsMedalsModalRootName)?.GetComponent<ModalAnimation>();
		}

		private bool WasClicked(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsClicked == true;
		}

		private bool IsHovered(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.IsHovered == true;
		}

		private bool WasCancelPressed()
		{
			var frame = PlayerInputService.GetFrame(EntityManager);
			return frame.WasPressed(PlayerButton.Escape) || frame.WasPressed(PlayerButton.Cancel);
		}

		private bool IsWayStationActive()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.WayStation;
		}

		private static string GetTileName(string medalId)
		{
			return WayStationSceneConstants.SaintsMedalsModalTilePrefix + (medalId ?? string.Empty);
		}
	}
}
