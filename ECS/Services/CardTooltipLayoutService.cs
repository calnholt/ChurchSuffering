using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Services
{
	internal static class CardTooltipLayoutService
	{
		public sealed class CardTooltipLayout
		{
			public Entity Entity { get; init; }
			public UIElement UI { get; init; }
			public CardTooltip CardTooltip { get; init; }
			public CardData CardData { get; init; }
			public Rectangle AnchorBounds { get; init; }
			public Rectangle TooltipRect { get; init; }
			public Vector2 RenderCenter { get; init; }
		}

		public static bool TryGetTopHoveredLayout(
			EntityManager entityManager,
			CardGeometrySettings settings,
			int screenWidth,
			int screenHeight,
			int gapOverride,
			int screenPadding,
			out CardTooltipLayout layout)
		{
			layout = null;
			if (entityManager == null || settings == null) return false;

			var top = entityManager.GetEntitiesWithComponent<CardTooltip>()
				.Select(e => new
				{
					E = e,
					UI = e.GetComponent<UIElement>(),
					T = e.GetComponent<Transform>(),
					CT = e.GetComponent<CardTooltip>(),
					CD = e.GetComponent<CardData>(),
				})
				.Where(x => x.UI != null
					&& x.UI.IsHovered
					&& x.UI.TooltipType == TooltipType.Card
					&& x.CT != null
					&& !string.IsNullOrWhiteSpace(x.CT.CardId))
				.OrderByDescending(x => x.T?.ZOrder ?? 0)
				.FirstOrDefault();
			if (top == null) return false;

			int width = (int)System.Math.Round(settings.CardWidth * top.CT.TooltipScale);
			int height = (int)System.Math.Round(settings.CardHeight * top.CT.TooltipScale);
			int gap = gapOverride > 0 ? gapOverride : System.Math.Max(0, top.UI.TooltipOffsetPx);
			Rectangle anchorBounds = TransformResolverService.ResolveUIBounds(entityManager, top.E, top.UI);

			int rx = anchorBounds.X;
			int ry = anchorBounds.Y;
			switch (top.UI.TooltipPosition)
			{
				case TooltipPosition.Above:
					rx = anchorBounds.X + (anchorBounds.Width - width) / 2;
					ry = anchorBounds.Y - height - gap;
					break;
				case TooltipPosition.Below:
					rx = anchorBounds.X + (anchorBounds.Width - width) / 2;
					ry = anchorBounds.Bottom + gap;
					break;
				case TooltipPosition.Right:
					rx = anchorBounds.Right + gap;
					ry = anchorBounds.Y + (anchorBounds.Height - height) / 2;
					break;
				case TooltipPosition.Left:
					rx = anchorBounds.X - width - gap;
					ry = anchorBounds.Y + (anchorBounds.Height - height) / 2;
					break;
			}

			int pad = System.Math.Max(0, screenPadding);
			rx = System.Math.Max(pad, System.Math.Min(rx, screenWidth - width - pad));
			ry = System.Math.Max(pad, System.Math.Min(ry, screenHeight - height - pad));

			var tooltipRect = new Rectangle(rx, ry, width, height);
			int offsetY = (int)System.Math.Round(settings.CardOffsetYExtra * top.CT.TooltipScale);
			var center = new Vector2(rx + width / 2f, ry + (height / 2f + offsetY));

			layout = new CardTooltipLayout
			{
				Entity = top.E,
				UI = top.UI,
				CardTooltip = top.CT,
				CardData = top.CD,
				AnchorBounds = anchorBounds,
				TooltipRect = tooltipRect,
				RenderCenter = center,
			};
			return true;
		}
	}
}
