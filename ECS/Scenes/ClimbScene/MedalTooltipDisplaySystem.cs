using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Medal Tooltip")]
	public class MedalTooltipDisplaySystem : Core.System
	{
		private sealed class FadeState
		{
			public string MedalId { get; set; } = string.Empty;
			public Rectangle IconBounds { get; set; }
			public float Alpha01 { get; set; }
			public bool TargetVisible { get; set; }
		}

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Dictionary<int, FadeState> _fadeByEntityId = new();

		[DebugEditable(DisplayName = "Icon Size", Step = 1, Min = 32, Max = 320)]
		public int IconSize { get; set; } = 119;

		[DebugEditable(DisplayName = "Icon Soften Strength", Step = 0.01f, Min = 0f, Max = 4f)]
		public float IconSoftenStrength { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Fade Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float FadeSeconds { get; set; } = 0.10f;

		public MedalTooltipDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<ClimbMedalTooltipSource>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			foreach (var state in _fadeByEntityId.Values)
			{
				state.TargetVisible = false;
			}

			var hovered = FindHoveredMedal();
			if (hovered != null)
			{
				var source = hovered.GetComponent<ClimbMedalTooltipSource>();
				var ui = hovered.GetComponent<UIElement>();
				Rectangle sourceBounds = TransformResolverService.ResolveUIBounds(EntityManager, hovered, ui);
				Rectangle iconBounds = ComputeIconBounds(sourceBounds, ui.TooltipOffsetPx);
				var anchor = hovered.GetComponent<ClimbMedalTooltipAnchor>();
				if (anchor == null)
				{
					anchor = new ClimbMedalTooltipAnchor();
					EntityManager.AddComponent(hovered, anchor);
				}
				anchor.IconBounds = iconBounds;

				if (!_fadeByEntityId.TryGetValue(hovered.Id, out var fade))
				{
					fade = new FadeState();
					_fadeByEntityId[hovered.Id] = fade;
				}
				fade.MedalId = source.MedalId;
				fade.IconBounds = iconBounds;
				fade.TargetVisible = true;
			}

			float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
			float delta = FadeSeconds <= 0f ? 1f : elapsed / FadeSeconds;
			foreach (int entityId in _fadeByEntityId.Keys.ToList())
			{
				var fade = _fadeByEntityId[entityId];
				fade.Alpha01 = MathHelper.Clamp(
					fade.Alpha01 + (fade.TargetVisible ? delta : -delta),
					0f,
					1f);
				if (fade.Alpha01 <= 0f && !fade.TargetVisible)
				{
					_fadeByEntityId.Remove(entityId);
				}
			}
		}

		public void Draw()
		{
			if (_graphicsDevice == null || _spriteBatch == null) return;

			foreach (var fade in _fadeByEntityId.Values.Where(state => state.Alpha01 > 0f))
			{
				MedalIconRenderService.DrawMedalIcon(
					_spriteBatch,
					_graphicsDevice,
					FontSingleton.ContentFont,
					fade.IconBounds.Center.ToVector2(),
					IconSize,
					fade.MedalId,
					_imageAssets,
					softenStrength: IconSoftenStrength,
					opacity: fade.Alpha01);
			}
		}

		public void Shutdown()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<ClimbMedalTooltipAnchor>().ToList())
			{
				EntityManager.RemoveComponent<ClimbMedalTooltipAnchor>(entity);
			}
			_fadeByEntityId.Clear();
		}

		private Entity FindHoveredMedal()
		{
			return GetRelevantEntities()
				.Where(entity =>
				{
					var source = entity.GetComponent<ClimbMedalTooltipSource>();
					var ui = entity.GetComponent<UIElement>();
					return !string.IsNullOrWhiteSpace(source?.MedalId)
						&& ui?.IsHovered == true
						&& !ui.IsHidden
						&& ui.TooltipType == TooltipType.Text;
				})
				.OrderByDescending(entity => entity.GetComponent<Transform>()?.ZOrder ?? 0)
				.ThenByDescending(entity => entity.Id)
				.FirstOrDefault();
		}

		private Rectangle ComputeIconBounds(Rectangle sourceBounds, int gap)
		{
			int size = Math.Max(1, IconSize);
			int x = sourceBounds.Right + Math.Max(0, gap);
			int y = sourceBounds.Center.Y - size / 2;
			x = Math.Max(0, Math.Min(x, Game1.VirtualWidth - size));
			y = Math.Max(0, Math.Min(y, Game1.VirtualHeight - size));
			return new Rectangle(x, y, size, size);
		}
	}
}
