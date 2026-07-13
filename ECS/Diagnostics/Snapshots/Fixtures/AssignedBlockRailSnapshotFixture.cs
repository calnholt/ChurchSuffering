using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class AssignedBlockRailSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "assigned-block-rail";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant;

		private readonly List<Entity> _assignments = new();
		private string _variant = "single-card";
		private Texture2D _background;
		private Texture2D _pixel;
		private AssignedBlockAnimationSystem _animation;
		private AssignedBlockLateLayoutSystem _lateLayout;
		private AssignedBlockCardsDisplaySystem _display;
		private EquipmentTooltipDisplaySystem _tooltip;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args ?? Array.Empty<string>());
			_background = ctx.Content.Load<Texture2D>("Battle_Backgrounds/gothic-battle-background");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			var phase = ctx.World.CreateEntity("AssignedBlockRailSnapshotPhase");
			ctx.World.AddComponent(phase, new PhaseState { Main = MainPhase.EnemyTurn, Sub = SubPhase.Block });
			var enemy = ctx.World.CreateEntity("AssignedBlockRailSnapshotEnemy");
			ctx.World.AddComponent(enemy, new AttackIntent
			{
				Owner = enemy,
				ActiveAttackSequence = 1,
				Planned = { new PlannedAttack() },
			});

			var anchor = ctx.World.CreateEntity("EnemyAttackBannerAnchor");
			ctx.World.AddComponent(anchor, new EnemyAttackBannerAnchor());
			ctx.World.AddComponent(anchor, new Transform { Position = new Vector2(Game1.VirtualWidth * 0.5f, 405f) });
			ctx.World.AddComponent(anchor, new UIElement { IsInteractable = false });
			ctx.World.AddComponent(anchor, new EnemyAttackBannerPresentation
			{
				IsVisible = true,
				LogicalWidth = 620,
				LogicalHeight = 220,
				PanelScaleX = 1f,
				PanelScaleY = 1f,
				RenderBounds = new Rectangle(Game1.VirtualWidth / 2 - 310, 405, 620, 220),
			});

			_animation = new AssignedBlockAnimationSystem(ctx.World.EntityManager);
			_lateLayout = new AssignedBlockLateLayoutSystem(ctx.World.EntityManager);
			_display = new AssignedBlockCardsDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);
			var tooltipEntity = ctx.World.CreateEntity(EquipmentDisplaySystem.TooltipEntityName);
			ctx.World.AddComponent(tooltipEntity, new EquipmentTooltipState());
			ctx.World.AddComponent(tooltipEntity, new Transform { ZOrder = 10002 });
			ctx.World.AddComponent(tooltipEntity, new UIElement { IsInteractable = false, IsHidden = true });
			_tooltip = new EquipmentTooltipDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);

			BuildVariant(ctx);
			Advance(_variant == "entry-impact" ? 0.245f : 0.55f);

			if (_variant == "hover")
			{
				_assignments[_assignments.Count / 2].GetComponent<UIElement>().IsHovered = true;
				Advance(0.12f);
				_tooltip.Update(new GameTime(TimeSpan.FromSeconds(1d), TimeSpan.FromSeconds(_tooltip.FadeSeconds)));
			}
			else if (_variant == "returning")
			{
				var returning = _assignments.Last();
				var assignment = returning.GetComponent<AssignedBlockCard>();
				var presentation = returning.GetComponent<AssignedBlockPresentation>();
				assignment.ReturnTargetPos = new Vector2(1510f, 760f);
				presentation.StartPos = presentation.RenderPos;
				presentation.CurrentPos = presentation.RenderPos;
				presentation.StartScale = presentation.CurrentScale;
				presentation.Phase = AssignedBlockPresentation.PhaseState.Returning;
				presentation.Elapsed = 0f;
				Advance(0.09f);
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.White);
			ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(9, 5, 8, 100));
			var banner = new Rectangle(Game1.VirtualWidth / 2 - 310, 405, 620, 220);
			ctx.SpriteBatch.Draw(_pixel, banner, new Color(15, 9, 14, 225));
			DrawBorder(ctx.SpriteBatch, banner, new Color(122, 18, 32), 2);
			_display.Draw();
			_tooltip.Draw();
		}

		private void BuildVariant(DisplaySnapshotContext ctx)
		{
			switch (_variant)
			{
				case "single-card":
					AddCard(ctx, "mantlet", CardData.CardColor.White, 4);
					break;
				case "mixed-row":
				case "hover":
				case "entry-impact":
				case "returning":
					AddCard(ctx, "mantlet", CardData.CardColor.White, 2);
					AddEquipment(ctx, "knightly_chest", 3, new Color(117, 27, 35));
					AddCard(ctx, "stalwart", CardData.CardColor.Black, 4);
					AddEquipment(ctx, "knightly_grieves", 2, new Color(185, 180, 161));
					AddCard(ctx, "hold_the_line", CardData.CardColor.Red, 3);
					break;
				case "dense-row":
					AddCard(ctx, "mantlet", CardData.CardColor.White, 2);
					AddEquipment(ctx, "knightly_helm", 2, new Color(185, 180, 161));
					AddCard(ctx, "stalwart", CardData.CardColor.Black, 4);
					AddEquipment(ctx, "pierced_heart_plate", 3, new Color(117, 27, 35));
					AddCard(ctx, "hold_the_line", CardData.CardColor.Red, 3);
					AddEquipment(ctx, "knightly_gauntlets", 2, new Color(64, 56, 66));
					AddEquipment(ctx, "helm_of_seeing", 4, new Color(185, 180, 161));
					AddEquipment(ctx, "fleetfoot_greaves", 3, new Color(117, 27, 35));
					break;
			}
		}

		private void AddCard(DisplaySnapshotContext ctx, string cardId, CardData.CardColor color, int block)
		{
			var entity = EntityFactory.CreateCardFromDefinition(ctx.World.EntityManager, cardId, color);
			if (entity == null) throw new DisplaySnapshotSetupException($"Failed to create card '{cardId}'");
			Color background = color switch
			{
				CardData.CardColor.Red => new Color(130, 23, 32),
				CardData.CardColor.Black => new Color(39, 34, 43),
				_ => new Color(205, 201, 184),
			};
			AddAssignment(ctx, entity, new AssignedBlockCard
			{
				BlockAmount = block,
				IsEquipment = false,
				DisplayBgColor = background,
				DisplayFgColor = color == CardData.CardColor.White ? new Color(34, 29, 31) : Color.White,
			});
		}

		private void AddEquipment(DisplaySnapshotContext ctx, string equipmentId, int block, Color color)
		{
			var entity = ctx.World.CreateEntity($"SnapshotEquipment_{equipmentId}_{_assignments.Count}");
			var equipment = EquipmentFactory.Create(equipmentId);
			if (equipment == null) throw new DisplaySnapshotSetupException($"Failed to create equipment '{equipmentId}'");
			equipment.Initialize(ctx.World.EntityManager, entity);
			ctx.World.AddComponent(entity, new Transform { Position = new Vector2(1540f, 650f), Scale = Vector2.One });
			ctx.World.AddComponent(entity, new UIElement { TooltipType = TooltipType.Equipment });
			ctx.World.AddComponent(entity, new EquippedEquipment { Equipment = equipment });
			ctx.World.AddComponent(entity, new EquipmentZone { Zone = EquipmentZoneType.AssignedBlock });
			AddAssignment(ctx, entity, new AssignedBlockCard
			{
				BlockAmount = block,
				IsEquipment = true,
				EquipmentType = equipment.Slot.ToString(),
				DisplayBgColor = color,
				DisplayFgColor = Color.White,
			});
		}

		private void AddAssignment(DisplaySnapshotContext ctx, Entity entity, AssignedBlockCard assignment)
		{
			assignment.AssignedAtTicks = _assignments.Count + 1;
			ctx.World.AddComponent(entity, assignment);
			var transform = entity.GetComponent<Transform>();
			Vector2 start = transform?.Position ?? new Vector2(960f, 900f);
			ctx.World.AddComponent(entity, new AssignedBlockPresentation
			{
				StartPos = start,
				CurrentPos = start,
				TargetPos = start,
				StartScale = 1f,
				CurrentScale = 1f,
			});
			_assignments.Add(entity);
		}

		private void Advance(float seconds)
		{
			const float step = 1f / 60f;
			float elapsed = 0f;
			while (elapsed + step <= seconds + 0.0001f)
			{
				elapsed += step;
				var gameTime = new GameTime(TimeSpan.FromSeconds(elapsed), TimeSpan.FromSeconds(step));
				_animation.Update(gameTime);
				_lateLayout.Update(gameTime);
			}
		}

		private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
		{
			spriteBatch.Draw(_pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
			spriteBatch.Draw(_pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(_pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
			spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 0) return "single-card";
			if (args.Length == 1 && args[0] is "single-card" or "mixed-row" or "dense-row" or "hover" or "entry-impact" or "returning")
				return args[0];
			throw new DisplaySnapshotSetupException(
				"assigned-block-rail expects one variant: single-card, mixed-row, dense-row, hover, entry-impact, or returning");
		}
	}
}
