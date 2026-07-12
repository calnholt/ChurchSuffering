using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Resolves enemy attack banner UI bounds after the global parallax late update.
	/// </summary>
	public sealed class EnemyAttackBannerLateLayoutSystem : Core.System
	{
		public EnemyAttackBannerLateLayoutSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<EnemyAttackBannerPresentation>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var presentation = entity.GetComponent<EnemyAttackBannerPresentation>();
			var transform = entity.GetComponent<Transform>();
			var anchorUi = entity.GetComponent<UIElement>();
			if (presentation == null || transform == null || anchorUi == null) return;

			if (!presentation.IsVisible)
			{
				presentation.RenderBounds = Rectangle.Empty;
				presentation.TextBounds = Rectangle.Empty;
				presentation.ConfirmBounds = Rectangle.Empty;
				anchorUi.Bounds = new Rectangle(0, 0, 1, 1);
				SetAuxiliaryBounds(Rectangle.Empty, Rectangle.Empty);
				return;
			}

			int width = Math.Max(1, (int)MathF.Round(presentation.LogicalWidth * presentation.PanelScaleX));
			int height = Math.Max(1, (int)MathF.Round(presentation.LogicalHeight * presentation.PanelScaleY));
			var centerTop = transform.Position + presentation.RecoilOffset;
			var renderBounds = new Rectangle(
				(int)MathF.Round(centerTop.X - width * 0.5f),
				(int)MathF.Round(centerTop.Y),
				width,
				height);

			presentation.RenderBounds = renderBounds;
			anchorUi.Bounds = renderBounds;

			var localText = presentation.LocalTextBounds;
			presentation.TextBounds = localText == Rectangle.Empty
				? Rectangle.Empty
				: new Rectangle(
					renderBounds.X + localText.X,
					renderBounds.Y + localText.Y,
					localText.Width,
					localText.Height);

			presentation.ConfirmBounds = presentation.ShowConfirm
				? new Rectangle(
					renderBounds.Center.X - presentation.ConfirmWidth / 2,
					renderBounds.Bottom + presentation.ConfirmOffsetY,
					presentation.ConfirmWidth,
					presentation.ConfirmHeight)
				: Rectangle.Empty;

			SetAuxiliaryBounds(presentation.TextBounds, presentation.ConfirmBounds);
		}

		private void SetAuxiliaryBounds(Rectangle textBounds, Rectangle confirmBounds)
		{
			var tooltip = EntityManager.GetEntity("UI_EnemyAttackTextTooltip");
			var tooltipUi = tooltip?.GetComponent<UIElement>();
			var tooltipTransform = tooltip?.GetComponent<Transform>();
			if (tooltipUi != null) tooltipUi.Bounds = textBounds;
			if (tooltipTransform != null) tooltipTransform.Position = new Vector2(textBounds.X, textBounds.Y);

			var confirm = EntityManager.GetEntity("UIButton_ConfirmEnemyAttack");
			var confirmUi = confirm?.GetComponent<UIElement>();
			var confirmTransform = confirm?.GetComponent<Transform>();
			if (confirmUi != null) confirmUi.Bounds = confirmBounds;
			if (confirmTransform != null) confirmTransform.Position = new Vector2(confirmBounds.X, confirmBounds.Y);
		}
	}
}
