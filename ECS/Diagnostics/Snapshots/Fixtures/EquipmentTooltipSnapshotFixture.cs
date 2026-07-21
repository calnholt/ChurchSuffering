using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class EquipmentTooltipSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "equipment-tooltip";
		public int WarmupFrames => 8;
		public string OutputFileName => _variant?.FileSlug ?? "active";

		private EquipmentTooltipSnapshotVariant _variant;
		private Texture2D _pixel;
		private Entity _equipmentEntity;
		private EquipmentDisplaySystem _display;
		private EquipmentTooltipDisplaySystem _tooltip;
		private double _elapsedSeconds;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = EquipmentTooltipSnapshotVariant.Parse(args);
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			var player = ctx.World.CreateEntity("Player");
			ctx.World.AddComponent(player, new Player());
			_equipmentEntity = ctx.World.CreateEntity($"Equipment_{_variant.EquipmentId}");
			var equipment = EquipmentFactory.Create(_variant.EquipmentId);
			if (equipment == null)
			{
				throw new DisplaySnapshotSetupException(
					$"Failed to create equipment '{_variant.EquipmentId}'");
			}
			equipment.Initialize(ctx.World.EntityManager, _equipmentEntity);
			if (_variant.IsUsed)
			{
				equipment.MarkUsed();
			}
			ctx.World.AddComponent(_equipmentEntity, new Transform());
			ctx.World.AddComponent(_equipmentEntity, new UIElement { IsInteractable = true });
			ctx.World.AddComponent(_equipmentEntity, new EquippedEquipment
			{
				EquippedOwner = player,
				Equipment = equipment,
			});

			_display = new EquipmentDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				ctx.ImageAssets)
			{
				LeftMargin = 500,
				TopMargin = 420,
			};
			_tooltip = new EquipmentTooltipDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			_display.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1d / 60d)));
			var root = ctx.World.EntityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>()
				.First();
			var parallax = root.GetComponent<ParallaxLayer>();
			parallax.MultiplierX = 0f;
			parallax.MultiplierY = 0f;
			parallax.MaxOffset = 0f;
			parallax.SmoothTime = 0f;
			ctx.World.AddSystem(_display);
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(
				_pixel,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				new Color(64, 38, 26));

			_equipmentEntity.GetComponent<UIElement>().IsHovered = true;
			_elapsedSeconds += 1d / 60d;
			_tooltip.Update(new GameTime(
				TimeSpan.FromSeconds(_elapsedSeconds),
				TimeSpan.FromSeconds(_tooltip.FadeSeconds)));
			_display.Draw();
			_tooltip.Draw();
		}
	}
}
