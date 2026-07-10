using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Booster Pack Opening")]
	public class BoosterPackOpeningDisplaySystem : Core.System
	{
		private const string OverlayEntityName = "BoosterPackOpeningOverlay";
		private const string BlockerEntityName = "BoosterPackOpeningBlocker";
		private const string ContextId = "overlay.booster-pack-opening";
		private const float TotalSeconds = 5.14f;
		private const float SummonStart = 0f;
		private const float IdleStart = 0.76f;
		private const float ChargeStart = 1.28f;
		private const float CrackStart = 2.13f;
		private const float RuptureStart = 2.78f;
		private const float ShowcaseStart = 3.54f;

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly SpriteFont _titleFont;
		private readonly SpriteFont _bodyFont;
		private readonly Random _rng = new();
		private readonly List<ParticleFx> _particles = new();
		private readonly List<ShardFx> _shards = new();
		private readonly Dictionary<string, Texture2D> _textureCache = new();

		private Texture2D _booster1;
		private Texture2D _booster2;
		private Texture2D _booster3;
		private Texture2D _boosterLeft;
		private Texture2D _boosterRight;

		[DebugEditable(DisplayName = "Z Order", Step = 10, Min = 0, Max = 100000)]
		public int ZOrder { get; set; } = 61000;

		[DebugEditable(DisplayName = "Blackout Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float BlackoutAlpha { get; set; } = 0.72f;

		[DebugEditable(DisplayName = "Stage Center X", Step = 1, Min = 0, Max = 1920)]
		public float StageCenterX { get; set; } = 960f;

		[DebugEditable(DisplayName = "Stage Center Y", Step = 1, Min = 0, Max = 1080)]
		public float StageCenterY { get; set; } = 540f;

		[DebugEditable(DisplayName = "Pack Width", Step = 1, Min = 100, Max = 800)]
		public int PackWidth { get; set; } = 370;

		[DebugEditable(DisplayName = "Pack Height", Step = 1, Min = 100, Max = 1000)]
		public int PackHeight { get; set; } = 620;

		[DebugEditable(DisplayName = "Pack Center Y", Step = 1, Min = 0, Max = 1080)]
		public float PackCenterY { get; set; } = 541f;

		[DebugEditable(DisplayName = "Pack Aura Size", Step = 1, Min = 100, Max = 1000)]
		public int PackAuraSize { get; set; } = 590;

		[DebugEditable(DisplayName = "Floor Glow Width", Step = 1, Min = 100, Max = 1200)]
		public int FloorGlowWidth { get; set; } = 760;

		[DebugEditable(DisplayName = "Floor Glow Height", Step = 1, Min = 40, Max = 400)]
		public int FloorGlowHeight { get; set; } = 150;

		[DebugEditable(DisplayName = "Floor Glow Y", Step = 1, Min = 0, Max = 1080)]
		public int FloorGlowY { get; set; } = 830;

		[DebugEditable(DisplayName = "Loot Gap", Step = 1, Min = 0, Max = 220)]
		public int LootGap { get; set; } = 86;

		[DebugEditable(DisplayName = "Loot Slot Width", Step = 1, Min = 100, Max = 600)]
		public int LootSlotWidth { get; set; } = 340;

		[DebugEditable(DisplayName = "Card Slot Width", Step = 1, Min = 100, Max = 600)]
		public int CardSlotWidth { get; set; } = 360;

		[DebugEditable(DisplayName = "Loot Slot Height", Step = 1, Min = 100, Max = 800)]
		public int LootSlotHeight { get; set; } = 540;

		[DebugEditable(DisplayName = "Loot Center Y", Step = 1, Min = 0, Max = 1080)]
		public int LootCenterY { get; set; } = 540;

		[DebugEditable(DisplayName = "Card Scale", Step = 0.01f, Min = 0.2f, Max = 2f)]
		public float CardScale { get; set; } = 1.09f;

		[DebugEditable(DisplayName = "Medal Size", Step = 1, Min = 40, Max = 320)]
		public int MedalSize { get; set; } = 156;

		[DebugEditable(DisplayName = "Equipment Icon Box", Step = 1, Min = 40, Max = 320)]
		public int EquipmentIconBox { get; set; } = 148;

		[DebugEditable(DisplayName = "Equipment Icon Scale", Step = 0.01f, Min = 0.1f, Max = 3f)]
		public float EquipmentIconScale { get; set; } = 1.55f;

		[DebugEditable(DisplayName = "Plate Size", Step = 1, Min = 80, Max = 600)]
		public int PlateSize { get; set; } = 320;

		[DebugEditable(DisplayName = "Reward Title Y", Step = 1, Min = 0, Max = 300)]
		public int RewardTitleY { get; set; } = 62;

		[DebugEditable(DisplayName = "Reward Kicker Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float RewardKickerScale { get; set; } = 0.09f;

		[DebugEditable(DisplayName = "Reward Headline Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float RewardHeadlineScale { get; set; } = 0.58f;

		[DebugEditable(DisplayName = "Charge Particle Count", Step = 1, Min = 0, Max = 200)]
		public int ChargeParticleCount { get; set; } = 34;

		[DebugEditable(DisplayName = "Charge Repeat Count", Step = 1, Min = 0, Max = 80)]
		public int ChargeRepeatParticleCount { get; set; } = 12;

		[DebugEditable(DisplayName = "Crack Particle Count", Step = 1, Min = 0, Max = 200)]
		public int CrackParticleCount { get; set; } = 24;

		[DebugEditable(DisplayName = "Burst Particle Count", Step = 1, Min = 0, Max = 200)]
		public int BurstParticleCount { get; set; } = 74;

		[DebugEditable(DisplayName = "Showcase Particle Count", Step = 1, Min = 0, Max = 200)]
		public int ShowcaseParticleCount { get; set; } = 42;

		[DebugEditable(DisplayName = "Shard Count", Step = 1, Min = 0, Max = 120)]
		public int ShardCount { get; set; } = 34;

		public BoosterPackOpeningDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
			_titleFont = FontSingleton.TitleFont;
			_bodyFont = FontSingleton.ChakraPetchFont;

			EventManager.Subscribe<ShowBoosterPackOpeningOverlayEvent>(_ => OpenOverlay());
			EventManager.Subscribe<CloseBoosterPackOpeningOverlayEvent>(_ => CloseOverlay());
			EventManager.Subscribe<DeleteCachesEvent>(_ => ClearTextureCache());
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<BoosterPackOpeningOverlayState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			var overlay = GetOverlay();
			var state = overlay?.GetComponent<BoosterPackOpeningOverlayState>();
			if (state?.IsOpen != true)
			{
				SetBlockerActive(false);
				SetLootHitboxes(state, visible: false);
				return;
			}

			float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
			state.ElapsedSeconds += elapsed;
			EnsureBlocker();
			SetBlockerActive(true);
			UpdateOneShotEffects(state);
			UpdateParticles(state.ElapsedSeconds);
			SetLootHitboxes(state, visible: state.RevealTriggered);
		}

		public void Draw()
		{
			var state = GetOverlay()?.GetComponent<BoosterPackOpeningOverlayState>();
			if (state?.IsOpen != true) return;

			LoadTextures();
			float t = state.ElapsedSeconds;
			DrawSceneWash(t);
			DrawStageLighting(t);
			if (!state.RevealTriggered)
			{
				DrawPack(t);
			}
			DrawFx(t);
			if (state.RevealTriggered)
			{
				DrawLoot(state, t);
				DrawRewardTitle(t);
			}
			DrawVignette();
		}

		[DebugAction("Play Booster Pack")]
		public void DebugPlayBoosterPack()
		{
			OpenOverlay();
		}

		[DebugAction("Close Booster Pack")]
		public void DebugCloseBoosterPack()
		{
			CloseOverlay();
		}

		private void OpenOverlay()
		{
			var overlay = EnsureOverlay();
			var state = overlay.GetComponent<BoosterPackOpeningOverlayState>();
			DestroyPreviewEntities(state);
			state.IsOpen = true;
			state.ElapsedSeconds = 0f;
			state.RuptureTriggered = false;
			state.RevealTriggered = false;
			state.ChargeParticlesTriggered = false;
			state.CrackParticlesTriggered = false;
			state.NextChargeParticleSeconds = ChargeStart + 0.26f;
			state.Loot = CreateLootPreviews();
			_particles.Clear();
			_shards.Clear();
			SetBlockerActive(true);
		}

		private void CloseOverlay()
		{
			var state = GetOverlay()?.GetComponent<BoosterPackOpeningOverlayState>();
			if (state == null) return;
			state.IsOpen = false;
			DestroyPreviewEntities(state);
			state.Loot.Clear();
			_particles.Clear();
			_shards.Clear();
			SetBlockerActive(false);
		}

		private Entity EnsureOverlay()
		{
			var overlay = EntityManager.GetEntity(OverlayEntityName);
			if (overlay == null)
			{
				overlay = EntityManager.CreateEntity(OverlayEntityName);
				EntityManager.AddComponent(overlay, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
				EntityManager.AddComponent(overlay, new BoosterPackOpeningOverlayState());
				EntityManager.AddComponent(overlay, new DontDestroyOnLoad());
				InputContextService.EnsureContext(EntityManager, overlay, ContextId, 760, true);
			}

			var transform = overlay.GetComponent<Transform>();
			if (transform != null) transform.ZOrder = ZOrder;
			var context = InputContextService.EnsureContext(EntityManager, overlay, ContextId, 760, true);
			context.IsActive = overlay.GetComponent<BoosterPackOpeningOverlayState>()?.IsOpen == true;
			return overlay;
		}

		private Entity GetOverlay()
		{
			return EntityManager.GetEntity(OverlayEntityName);
		}

		private Entity EnsureBlocker()
		{
			var blocker = EntityManager.GetEntity(BlockerEntityName);
			if (blocker == null)
			{
				blocker = EntityManager.CreateEntity(BlockerEntityName);
				EntityManager.AddComponent(blocker, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
				EntityManager.AddComponent(blocker, new UIElement
				{
					Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
					IsInteractable = true,
					IsHidden = true,
					TooltipType = TooltipType.None,
					LayerType = UILayerType.Overlay,
					ShowHoverHighlight = false,
				});
				EntityManager.AddComponent(blocker, new DontDestroyOnLoad());
				InputContextService.EnsureMember(EntityManager, blocker, ContextId);
			}

			var transform = blocker.GetComponent<Transform>();
			if (transform != null) transform.ZOrder = ZOrder;
			var ui = blocker.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.Bounds = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
				ui.LayerType = UILayerType.Overlay;
				ui.TooltipType = TooltipType.None;
				ui.ShowHoverHighlight = false;
			}
			return blocker;
		}

		private void SetBlockerActive(bool active)
		{
			var blocker = EntityManager.GetEntity(BlockerEntityName);
			if (blocker?.GetComponent<UIElement>() is UIElement ui)
			{
				ui.IsHidden = !active;
				ui.IsInteractable = active;
			}

			if (GetOverlay()?.GetComponent<InputContext>() is InputContext context)
			{
				context.IsActive = active;
			}
		}

		private List<BoosterPackLootPreview> CreateLootPreviews()
		{
			var loot = new List<BoosterPackLootPreview>();
			for (int i = 0; i < 3; i++)
			{
				var kind = (BoosterPackLootKind)_rng.Next(3);
				var preview = new BoosterPackLootPreview
				{
					Kind = kind,
					RevealDelaySeconds = kind switch
					{
						BoosterPackLootKind.Medal => 0.08f,
						BoosterPackLootKind.Card => 0.20f,
						_ => 0.32f,
					}
				};
				switch (kind)
				{
					case BoosterPackLootKind.Card:
						ConfigureCardPreview(preview, i);
						break;
					case BoosterPackLootKind.Medal:
						ConfigureMedalPreview(preview, i);
						break;
					case BoosterPackLootKind.Equipment:
						ConfigureEquipmentPreview(preview, i);
						break;
				}
				loot.Add(preview);
			}
			return loot;
		}

		private void ConfigureCardPreview(BoosterPackLootPreview preview, int index)
		{
			var cards = CardFactory.GetAllCards().Keys.Select(id => id.ToKey()).ToList();
			if (cards.Count == 0) return;
			var colors = new[] { CardData.CardColor.White, CardData.CardColor.Red, CardData.CardColor.Black };
			preview.Id = cards[_rng.Next(cards.Count)];
			preview.CardColor = colors[_rng.Next(colors.Length)];
			preview.PreviewEntity = EntityFactory.CreateCardFromDefinition(
				EntityManager,
				preview.Id,
				preview.CardColor,
				allowWeapons: true,
				index: index);
			if (preview.PreviewEntity == null) return;
			PreparePreviewEntity(preview.PreviewEntity);
			var ui = preview.PreviewEntity.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.TooltipPosition = TooltipPosition.Above;
				ui.TooltipOffsetPx = 18;
			}
			var cardTooltip = preview.PreviewEntity.GetComponent<CardTooltip>();
			if (cardTooltip != null)
			{
				cardTooltip.CardColor = preview.CardColor;
			}
		}

		private void ConfigureMedalPreview(BoosterPackLootPreview preview, int index)
		{
			var medals = MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()).ToList();
			if (medals.Count == 0) return;
			preview.Id = medals[_rng.Next(medals.Count)];
			var medal = MedalFactory.Create(preview.Id);
			if (medal == null) return;
			var entity = EntityManager.CreateEntity($"BoosterPackMedalPreview_{index}");
			EntityManager.AddComponent(entity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 10 + index });
			EntityManager.AddComponent(entity, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = true,
				IsHidden = true,
				Tooltip = $"{medal.Name}\n\n{medal.Text}",
				TooltipType = TooltipType.Text,
				TooltipPosition = TooltipPosition.Below,
				TooltipOffsetPx = 18,
				LayerType = UILayerType.Overlay,
				ShowHoverHighlight = false,
			});
			EntityManager.AddComponent(entity, new EquippedMedal { Medal = medal });
			EntityManager.AddComponent(entity, new DontDestroyOnLoad());
			EntityManager.AddComponent(entity, ParallaxLayer.GetUIParallaxLayer());
			InputContextService.EnsureMember(EntityManager, entity, ContextId);
			medal.Initialize(EntityManager, entity);
			preview.PreviewEntity = entity;
		}

		private void ConfigureEquipmentPreview(BoosterPackLootPreview preview, int index)
		{
			var equipmentIds = EquipmentFactory.GetAllEquipment().Keys.Select(id => id.ToKey()).ToList();
			if (equipmentIds.Count == 0) return;
			preview.Id = equipmentIds[_rng.Next(equipmentIds.Count)];
			var equipment = EquipmentFactory.Create(preview.Id);
			if (equipment == null) return;
			var entity = EntityManager.CreateEntity($"BoosterPackEquipmentPreview_{index}");
			EntityManager.AddComponent(entity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder + 10 + index });
			EntityManager.AddComponent(entity, new UIElement
			{
				Bounds = Rectangle.Empty,
				IsInteractable = true,
				IsHidden = true,
				Tooltip = EquipmentService.GetTooltipText(equipment, EquipmentTooltipType.Shop),
				TooltipType = TooltipType.Text,
				TooltipPosition = TooltipPosition.Below,
				TooltipOffsetPx = 18,
				LayerType = UILayerType.Overlay,
				ShowHoverHighlight = false,
			});
			EntityManager.AddComponent(entity, new EquippedEquipment { Equipment = equipment });
			EntityManager.AddComponent(entity, new EquipmentZone { Zone = EquipmentZoneType.Default });
			EntityManager.AddComponent(entity, new DontDestroyOnLoad());
			EntityManager.AddComponent(entity, ParallaxLayer.GetUIParallaxLayer());
			InputContextService.EnsureMember(EntityManager, entity, ContextId);
			equipment.Initialize(EntityManager, entity);
			preview.PreviewEntity = entity;
		}

		private void PreparePreviewEntity(Entity entity)
		{
			if (entity == null) return;
			if (entity.GetComponent<Transform>() is Transform transform)
			{
				transform.Position = new Vector2(-5000, -5000);
				transform.Scale = Vector2.One;
				transform.Rotation = 0f;
				transform.ZOrder = ZOrder + 12;
			}
			if (entity.GetComponent<UIElement>() is UIElement ui)
			{
				ui.IsInteractable = true;
				ui.IsHidden = true;
				ui.LayerType = UILayerType.Overlay;
				ui.ShowHoverHighlight = false;
			}
			InputContextService.EnsureMember(EntityManager, entity, ContextId);
		}

		private void DestroyPreviewEntities(BoosterPackOpeningOverlayState state)
		{
			if (state?.Loot == null) return;
			foreach (var preview in state.Loot)
			{
				if (preview?.PreviewEntity == null) continue;
				EntityManager.DestroyEntity(preview.PreviewEntity.Id);
				preview.PreviewEntity = null;
			}
		}

		private void SetLootHitboxes(BoosterPackOpeningOverlayState state, bool visible)
		{
			if (state?.Loot == null || state.Loot.Count == 0) return;
			var centers = ComputeLootCenters(state.Loot);
			for (int i = 0; i < state.Loot.Count; i++)
			{
				var preview = state.Loot[i];
				var entity = preview.PreviewEntity;
				var ui = entity?.GetComponent<UIElement>();
				if (ui == null) continue;

				bool revealed = visible && GetRevealProgress(state.ElapsedSeconds, preview.RevealDelaySeconds) >= 1f;
				ui.IsHidden = !revealed;
				ui.IsInteractable = revealed;
				ui.Bounds = revealed ? GetItemHitbox(preview, centers[i]) : Rectangle.Empty;
				if (entity.GetComponent<Transform>() is Transform transform)
				{
					transform.ZOrder = ZOrder + 20 + i;
				}
			}
		}

		private Rectangle GetItemHitbox(BoosterPackLootPreview preview, Vector2 center)
		{
			return preview.Kind switch
			{
				BoosterPackLootKind.Card => CardGeometryService.GetVisualRect(EntityManager, GetCardRenderPosition(center), CardScale),
				BoosterPackLootKind.Medal => CenteredRect(center, MedalSize + 40, MedalSize + 40),
				_ => CenteredRect(center, (int)(EquipmentIconBox * EquipmentIconScale), (int)(EquipmentIconBox * EquipmentIconScale)),
			};
		}

		private void UpdateOneShotEffects(BoosterPackOpeningOverlayState state)
		{
			float t = state.ElapsedSeconds;
			if (!state.ChargeParticlesTriggered && t >= ChargeStart)
			{
				state.ChargeParticlesTriggered = true;
				SpawnParticles(t, charge: true, ChargeParticleCount);
			}
			if (t >= state.NextChargeParticleSeconds && t < CrackStart)
			{
				state.NextChargeParticleSeconds += 0.26f;
				SpawnParticles(t, charge: true, ChargeRepeatParticleCount);
			}
			if (!state.CrackParticlesTriggered && t >= CrackStart)
			{
				state.CrackParticlesTriggered = true;
				SpawnParticles(t, charge: false, CrackParticleCount);
			}
			if (!state.RuptureTriggered && t >= RuptureStart)
			{
				state.RuptureTriggered = true;
				SpawnParticles(t, charge: false, BurstParticleCount);
				SpawnShards(t, ShardCount);
			}
			if (!state.RevealTriggered && t >= ShowcaseStart)
			{
				state.RevealTriggered = true;
				SpawnParticles(t, charge: false, ShowcaseParticleCount);
			}
		}

		private void SpawnParticles(float now, bool charge, int count)
		{
			float cx = StageCenterX;
			float cy = charge ? 500f : 475f;
			for (int i = 0; i < count; i++)
			{
				double angle = _rng.NextDouble() * Math.PI * 2.0;
				float dist = charge
					? 120f + NextFloat() * 230f
					: 180f + NextFloat() * 520f;
				float dx = (float)Math.Cos(angle) * dist;
				float dy = (float)Math.Sin(angle) * dist;
				float size = charge
					? 3f + NextFloat() * 6f
					: 5f + NextFloat() * 14f;
				Color color = PickParticleColor();
				_particles.Add(new ParticleFx
				{
					Start = charge ? new Vector2(cx + dx, cy + dy) : new Vector2(cx, cy),
					Delta = charge ? new Vector2(-dx, -dy) : new Vector2(dx, dy),
					Size = size,
					Color = color,
					StartSeconds = now + NextFloat() * (charge ? 0.18f : 0.10f),
					DurationSeconds = charge ? 0.72f + NextFloat() * 0.28f : 0.90f + NextFloat() * 0.55f,
				});
			}
		}

		private void SpawnShards(float now, int count)
		{
			for (int i = 0; i < count; i++)
			{
				double angle = -Math.PI * 0.92
					+ (Math.PI * 1.84 * i) / Math.Max(1, count - 1)
					+ (NextFloat() - 0.5f) * 0.28f;
				float dist = 240f + NextFloat() * 520f;
				_shards.Add(new ShardFx
				{
					Start = new Vector2(960f, 476f),
					Delta = new Vector2((float)Math.Cos(angle) * dist, (float)Math.Sin(angle) * dist + NextFloat() * 110f),
					Width = 8f + NextFloat() * 14f,
					Height = 20f + NextFloat() * 42f,
					RotationRadians = MathHelper.ToRadians((NextFloat() > 0.5f ? 1f : -1f) * (220f + NextFloat() * 520f)),
					StartSeconds = now + NextFloat() * 0.08f,
					DurationSeconds = 0.76f + NextFloat() * 0.46f,
				});
			}
		}

		private void UpdateParticles(float now)
		{
			_particles.RemoveAll(p => now > p.StartSeconds + p.DurationSeconds + 0.05f);
			_shards.RemoveAll(s => now > s.StartSeconds + s.DurationSeconds + 0.05f);
		}

		private void DrawSceneWash(float t)
		{
			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * MathHelper.Clamp(BlackoutAlpha, 0f, 1f));
			DrawSoftEllipse(new Rectangle(518, 115, 884, 734), new Color(20, 12, 10) * (t >= ShowcaseStart ? 0.34f : 0.05f), 0.05f, 1f);
			_spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.Black * 0.18f);
		}

		private void DrawStageLighting(float t)
		{
			float breathe = 0.5f + 0.5f * (float)Math.Sin(t * MathHelper.TwoPi / 2.8f);
			var floor = new Rectangle(
				(int)(StageCenterX - FloorGlowWidth / 2f),
				FloorGlowY,
				FloorGlowWidth,
				FloorGlowHeight);
			DrawSoftEllipse(floor, Blood * MathHelper.Lerp(0.20f, 0.34f, breathe), 0.05f, 1f);
			DrawSoftEllipse(Inflate(floor, -100, -20), Gold * MathHelper.Lerp(0.10f, 0.18f, breathe), 0.05f, 1f);

			DrawAltarRing(new Vector2(StageCenterX, StageCenterY + 44), 860, 292, t / 18f, 0.18f);
			DrawAltarRing(new Vector2(StageCenterX, StageCenterY + 44), 640, 218, -t / 10f, 0.42f);

			float beamProgress = t < RuptureStart ? 0f : EaseOutCubic(MathHelper.Clamp((t - RuptureStart) / 1.8f, 0f, 1f));
			if (beamProgress > 0f)
			{
				DrawBeams(beamProgress, t);
			}
		}

		private void DrawPack(float t)
		{
			Texture2D pack = t >= CrackStart ? _booster3 : t >= ChargeStart ? _booster2 : _booster1;
			Vector2 center = new(StageCenterX, PackCenterY);
			float holderScale = 1f;
			float holderRot = 0f;
			Vector2 offset = Vector2.Zero;
			float alpha = 1f;

			if (t < IdleStart)
			{
				float p = EaseOutBack(MathHelper.Clamp(t / IdleStart, 0f, 1f));
				alpha = p;
				holderScale = MathHelper.Lerp(0.54f, 1f, p);
				offset.Y = MathHelper.Lerp(-560f, 0f, p);
			}
			else if (t < ChargeStart)
			{
				float p = (float)Math.Sin((t - IdleStart) * MathHelper.TwoPi / 2f);
				offset.Y = -7f + p * 7f;
				holderRot = MathHelper.ToRadians(MathHelper.Lerp(-0.5f, 0.7f, (p + 1f) * 0.5f));
			}
			else if (t < CrackStart)
			{
				float p = (float)Math.Sin(t * MathHelper.TwoPi / 0.13f);
				offset.X = p > 0f ? 5f : 0f;
				offset.Y = p > 0f ? -2f : -6f;
				holderRot = MathHelper.ToRadians(p > 0f ? 1.2f : -1f);
				holderScale = p > 0f ? 1.055f : 1.035f;
			}
			else if (t < RuptureStart)
			{
				float p = (float)Math.Sin(t * MathHelper.TwoPi / 0.08f);
				offset = new Vector2(p > 0f ? 5f : -5f, p > 0f ? 2f : -2f);
				holderRot = MathHelper.ToRadians(p > 0f ? 1.3f : -1.3f);
				holderScale = p > 0f ? 1.08f : 1.06f;
			}

			DrawSoftEllipse(CenteredRect(center, PackAuraSize, PackAuraSize), Gold * GetAuraAlpha(t), 0.05f, 1f);

			if (t < RuptureStart)
			{
				DrawTextureCentered(pack, center + offset, PackWidth, PackHeight, Color.White * alpha, holderRot, holderScale);
				if (t >= CrackStart)
				{
					DrawCrackOverlay(t, center + offset, holderRot, holderScale);
				}
				return;
			}

			float peel = EaseOutCubic(MathHelper.Clamp((t - RuptureStart) / 0.62f, 0f, 1f));
			DrawSoftEllipse(new Rectangle(850, 275, 220, 520), Gold * MathHelper.Lerp(0.86f, 0.18f, peel), 0.02f, 1f);
			DrawPackHalf(_boosterLeft, left: true, peel);
			DrawPackHalf(_boosterRight, left: false, peel);
		}

		private void DrawPackHalf(Texture2D texture, bool left, float peel)
		{
			if (texture == null) return;
			float w = left ? PackWidth * 379f / 786f : PackWidth * 376f / 786f;
			float h = PackHeight;
			float baseX = left ? StageCenterX - w / 2f : StageCenterX + w / 2f;
			float x = MathHelper.Lerp(0f, left ? -680f : 680f, peel);
			float y = MathHelper.Lerp(0f, 128f, peel);
			float rot = MathHelper.ToRadians(MathHelper.Lerp(0f, left ? -38f : 38f, peel));
			float alpha = 1f - EaseInQuad(MathHelper.Clamp((peel - 0.45f) / 0.55f, 0f, 1f));
			var dest = new Rectangle((int)(baseX + x), (int)(PackCenterY + y), (int)w, (int)h);
			var origin = left ? new Vector2(texture.Width, texture.Height / 2f) : new Vector2(0f, texture.Height / 2f);
			_spriteBatch.Draw(texture, dest, null, Color.White * alpha, rot, origin, SpriteEffects.None, 0f);
		}

		private void DrawCrackOverlay(float t, Vector2 center, float rotation, float scale)
		{
			float pulse = 0.85f + 0.60f * (0.5f + 0.5f * (float)Math.Sin(t * MathHelper.TwoPi / 0.55f));
			Color color = GoldHot * MathHelper.Clamp(pulse, 0f, 1f);
			DrawLine(center + new Vector2(0, -185) * scale, 370 * scale, 4 * scale, rotation + MathHelper.ToRadians(1), color);
			DrawLine(center + new Vector2(-34, -95) * scale, 118 * scale, 4 * scale, rotation + MathHelper.ToRadians(-42), color);
			DrawLine(center + new Vector2(30, -70) * scale, 134 * scale, 4 * scale, rotation + MathHelper.ToRadians(41), color);
			DrawLine(center + new Vector2(-22, 56) * scale, 110 * scale, 4 * scale, rotation + MathHelper.ToRadians(52), color);
			DrawLine(center + new Vector2(28, 78) * scale, 126 * scale, 4 * scale, rotation + MathHelper.ToRadians(-38), color);
		}

		private void DrawFx(float t)
		{
			float rupture = MathHelper.Clamp((t - RuptureStart) / 0.82f, 0f, 1f);
			if (rupture > 0f && rupture < 1f)
			{
				float alpha = rupture < 0.11f ? rupture / 0.11f : 1f - (rupture - 0.11f) / 0.89f;
				DrawSoftEllipse(new Rectangle(370, -20, 1180, 1080), GoldHot * MathHelper.Clamp(alpha, 0f, 1f), 0f, 1f);
			}

			DrawShockwave(t, 0f, 0.90f);
			DrawShockwave(t, 0.08f, 1.05f);
			DrawVerticalFlare(t);
			DrawParticles(t);
			DrawShards(t);
		}

		private void DrawShockwave(float t, float delay, float duration)
		{
			float p = MathHelper.Clamp((t - RuptureStart - delay) / duration, 0f, 1f);
			if (p <= 0f || p >= 1f) return;
			float eased = EaseOutCubic(p);
			int size = (int)MathHelper.Lerp(120f * 0.15f, 120f * 13.5f, eased);
			var rect = CenteredRect(new Vector2(StageCenterX, 529f), size, size);
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, size, size, 5f);
			_spriteBatch.Draw(ring, rect, GoldHot * (0.95f * (1f - p)));
		}

		private void DrawVerticalFlare(float t)
		{
			float p = MathHelper.Clamp((t - RuptureStart) / 0.56f, 0f, 1f);
			if (p <= 0f || p >= 1f) return;
			float alpha = p < 0.16f ? p / 0.16f : 1f - (p - 0.16f) / 0.84f;
			float width = MathHelper.Lerp(24f * 0.4f, 24f * 8f, p);
			DrawLine(new Vector2(StageCenterX, 150f), 760f, width, 0f, GoldHot * alpha);
			DrawLine(new Vector2(StageCenterX, 150f), 760f, width * 0.38f, 0f, BloodHot * (alpha * 0.58f));
		}

		private void DrawParticles(float t)
		{
			var circle = _imageAssets.GetAntiAliasedCircle(16);
			foreach (var p in _particles)
			{
				float progress = MathHelper.Clamp((t - p.StartSeconds) / p.DurationSeconds, 0f, 1f);
				if (progress <= 0f || progress >= 1f) continue;
				float alpha = progress < 0.08f ? progress / 0.08f : 1f - progress;
				float scale = MathHelper.Lerp(0.35f, 0.05f, progress);
				Vector2 pos = p.Start + p.Delta * EaseOutCubic(progress);
				float size = Math.Max(1f, p.Size * scale * 2f);
				var rect = CenteredRect(pos, (int)size, (int)size);
				_spriteBatch.Draw(circle, rect, p.Color * alpha);
			}
		}

		private void DrawShards(float t)
		{
			foreach (var shard in _shards)
			{
				float p = MathHelper.Clamp((t - shard.StartSeconds) / shard.DurationSeconds, 0f, 1f);
				if (p <= 0f || p >= 1f) continue;
				float alpha = p < 0.10f ? p / 0.10f : 1f - p;
				Vector2 pos = shard.Start + shard.Delta * EaseOutCubic(p);
				float scale = MathHelper.Lerp(0.5f, 0.15f, p);
				var texture = PrimitiveTextureFactory.GetAntialiasedPolygonMask(
					_graphicsDevice,
					(int)Math.Max(1f, shard.Width),
					(int)Math.Max(1f, shard.Height),
					"booster-shard",
					new[] { new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.76f) });
				_spriteBatch.Draw(
					texture,
					pos,
					null,
					GoldHot * alpha,
					shard.RotationRadians * p,
					new Vector2(texture.Width / 2f, texture.Height / 2f),
					scale,
					SpriteEffects.None,
					0f);
			}
		}

		private void DrawLoot(BoosterPackOpeningOverlayState state, float t)
		{
			var centers = ComputeLootCenters(state.Loot);
			for (int i = 0; i < state.Loot.Count; i++)
			{
				var preview = state.Loot[i];
				float progress = GetRevealProgress(t, preview.RevealDelaySeconds);
				if (progress <= 0f) continue;
				float eased = EaseOutBack(progress);
				float alpha = MathHelper.Clamp(progress / 0.3f, 0f, 1f);
				float startRot = preview.Kind switch
				{
					BoosterPackLootKind.Card => -12f,
					BoosterPackLootKind.Medal => 14f,
					_ => 11f,
				};
				float rotation = MathHelper.ToRadians(MathHelper.Lerp(startRot, 0f, progress));
				float scale = MathHelper.Lerp(0.62f, 1f, eased);
				Vector2 center = centers[i] + new Vector2(0f, MathHelper.Lerp(100f, 0f, eased));
				DrawLootPlate(center, t, progress, alpha);
				DrawItemGlow(center, t, progress, alpha);
				DrawLootItem(preview, center, rotation, scale, alpha, t);
			}
		}

		private void DrawLootPlate(Vector2 center, float t, float progress, float alpha)
		{
			float plateScale = MathHelper.Lerp(0.52f, 1f, EaseOutBack(progress));
			int size = (int)(PlateSize * plateScale);
			DrawSoftEllipse(CenteredRect(center + new Vector2(0, 10), size, size), GoldHot * (0.18f * alpha), 0.05f, 1f);
			DrawAltarRing(center + new Vector2(0, 10), size, size, t / 12f, 0.24f * alpha);
		}

		private void DrawItemGlow(Vector2 center, float t, float progress, float alpha)
		{
			if (progress < 0.22f) return;
			float pulse = 0.55f + 0.35f * (0.5f + 0.5f * (float)Math.Sin((t - ShowcaseStart) * MathHelper.TwoPi / 2.6f));
			DrawSoftEllipse(CenteredRect(center, 280, 280), GoldHot * (pulse * alpha * 0.24f), 0.05f, 1f);
		}

		private void DrawLootItem(BoosterPackLootPreview preview, Vector2 center, float rotation, float scale, float alpha, float t)
		{
			switch (preview.Kind)
			{
				case BoosterPackLootKind.Card:
					DrawCardPreview(preview, center, scale, alpha, t);
					break;
				case BoosterPackLootKind.Medal:
					DrawMedalPreview(preview, center, rotation, scale, alpha);
					break;
				case BoosterPackLootKind.Equipment:
					DrawEquipmentPreview(preview, center, rotation, scale, alpha);
					break;
			}
		}

		private void DrawCardPreview(BoosterPackLootPreview preview, Vector2 center, float scale, float alpha, float t)
		{
			var position = GetCardRenderPosition(center);
			var rect = CardGeometryService.GetVisualRect(EntityManager, position, CardScale * scale);
			DrawSoftEllipse(Inflate(rect, 44, 44), Blood * (0.28f * alpha), 0.05f, 1f);
			DrawSoftEllipse(Inflate(rect, 8, 8), GoldHot * (0.16f * alpha), 0.02f, 1f);
			if (preview.PreviewEntity != null)
			{
				EventManager.Publish(new CardRenderScaledEvent
				{
					Card = preview.PreviewEntity,
					Position = position,
					Scale = CardScale * scale,
					Alpha = alpha,
				});
			}
			float shineP = MathHelper.Clamp((t - ShowcaseStart - 0.66f) / 1.15f, 0f, 1f);
			if (shineP > 0f && shineP < 1f)
			{
				float x = MathHelper.Lerp(rect.X - rect.Width, rect.Right + rect.Width, shineP);
				DrawLine(new Vector2(x, rect.Y - 20), rect.Height + 70, 34f, MathHelper.ToRadians(15f), Color.White * (0.28f * (1f - Math.Abs(shineP - 0.5f) * 2f)));
			}
		}

		private void DrawMedalPreview(BoosterPackLootPreview preview, Vector2 center, float rotation, float scale, float alpha)
		{
			Texture2D texture = GetTexture("Medals/" + preview.Id);
			if (texture == null) return;
			int size = (int)(MedalSize * scale);
			DrawSoftEllipse(CenteredRect(center, size + 86, size + 86), Gold * (0.24f * alpha), 0.05f, 1f);
			_spriteBatch.Draw(
				texture,
				center,
				null,
				Color.White * alpha,
				rotation,
				new Vector2(texture.Width / 2f, texture.Height / 2f),
				size / (float)Math.Max(texture.Width, texture.Height),
				SpriteEffects.None,
				0f);
		}

		private void DrawEquipmentPreview(BoosterPackLootPreview preview, Vector2 center, float rotation, float scale, float alpha)
		{
			var equipment = preview.PreviewEntity?.GetComponent<EquippedEquipment>()?.Equipment;
			if (equipment == null) return;
			Texture2D texture = GetTexture(equipment.Slot.ToString().ToLowerInvariant());
			if (texture == null) return;
			int box = (int)(EquipmentIconBox * EquipmentIconScale * scale);
			DrawSoftEllipse(CenteredRect(center, box + 72, box + 72), Blue * (0.20f * alpha), 0.05f, 1f);
			_spriteBatch.Draw(
				texture,
				CenteredRect(center, box, box),
				null,
				Color.White * alpha,
				rotation,
				Vector2.Zero,
				SpriteEffects.None,
				0f);
		}

		private void DrawRewardTitle(float t)
		{
			float p = EaseOutBack(MathHelper.Clamp((t - ShowcaseStart) / 0.68f, 0f, 1f));
			float alpha = MathHelper.Clamp((t - ShowcaseStart) / 0.32f, 0f, 1f);
			float y = RewardTitleY + MathHelper.Lerp(-18f, 0f, p);
			DrawCenteredString(_bodyFont, "PACK OPENED", new Vector2(StageCenterX, y), GoldHot * (0.72f * alpha), RewardKickerScale * MathHelper.Lerp(0.9f, 1f, p));
			DrawCenteredString(_titleFont, "HOLY SPOILS", new Vector2(StageCenterX, y + 28), new Color(255, 247, 204) * alpha, RewardHeadlineScale * MathHelper.Lerp(0.9f, 1f, p));
		}

		private void DrawVignette()
		{
			DrawSoftEllipse(new Rectangle(0, -10, 1920, 1060), Color.Black * 0.72f, 0.50f, 1f, invert: true);
		}

		private void DrawAltarRing(Vector2 center, int width, int height, float turns, float alpha)
		{
			if (width <= 0 || height <= 0 || alpha <= 0f) return;
			var ring = PrimitiveTextureFactory.GetAntialiasedRingMask(_graphicsDevice, width, width, Math.Max(2f, width * 0.012f));
			_spriteBatch.Draw(
				ring,
				CenteredRect(center, width, height),
				null,
				Gold * alpha,
				turns * MathHelper.TwoPi,
				Vector2.Zero,
				SpriteEffects.None,
				0f);
			int spokes = 22;
			for (int i = 0; i < spokes; i++)
			{
				float a = turns * MathHelper.TwoPi + i * MathHelper.TwoPi / spokes;
				float rx = width * 0.48f;
				float ry = height * 0.48f;
				Vector2 end = center + new Vector2((float)Math.Cos(a) * rx, (float)Math.Sin(a) * ry);
				DrawLine(center + (end - center) * 0.80f, 24f, 3f, a + MathHelper.PiOver2, GoldHot * (alpha * 0.7f));
			}
		}

		private void DrawBeams(float progress, float t)
		{
			float alpha = progress < 0.18f ? progress / 0.18f * 0.88f : MathHelper.Lerp(0.88f, 0.34f, progress);
			for (int i = 0; i < 9; i++)
			{
				float angle = MathHelper.ToRadians(i * 40f + 8f + MathHelper.Lerp(-10f, 18f, progress));
				Color color = i % 3 == 0 ? GoldHot : i % 3 == 1 ? Blue : BloodHot;
				DrawLine(new Vector2(StageCenterX, 464f), 980f * MathHelper.Lerp(0.2f, 1.08f, progress), 26f, angle, color * (alpha * 0.18f));
			}
		}

		private void DrawTextureCentered(Texture2D texture, Vector2 center, int width, int height, Color color, float rotation, float scale)
		{
			if (texture == null) return;
			var dest = new Rectangle(
				(int)Math.Round(center.X),
				(int)Math.Round(center.Y),
				(int)Math.Round(width * scale),
				(int)Math.Round(height * scale));
			_spriteBatch.Draw(texture, dest, null, color, rotation, new Vector2(texture.Width / 2f, texture.Height / 2f), SpriteEffects.None, 0f);
		}

		private void DrawLine(Vector2 topCenter, float length, float thickness, float rotation, Color color)
		{
			if (length <= 0f || thickness <= 0f) return;
			var dest = new Rectangle(
				(int)Math.Round(topCenter.X),
				(int)Math.Round(topCenter.Y),
				(int)Math.Round(thickness),
				(int)Math.Round(length));
			_spriteBatch.Draw(_pixel, dest, null, color, rotation, new Vector2(0.5f, 0f), SpriteEffects.None, 0f);
		}

		private void DrawSoftEllipse(Rectangle rect, Color color, float innerStop, float outerStop, bool invert = false)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			int diameter = Math.Max(rect.Width, rect.Height);
			string key = $"soft:{diameter}:{innerStop:0.000}:{outerStop:0.000}:{invert}";
			if (!_textureCache.TryGetValue(key, out var texture))
			{
				texture = invert
					? CreateInvertedSoftRadial(diameter, innerStop, outerStop)
					: PrimitiveTextureFactory.GetSoftRadialCircle(_graphicsDevice, diameter, innerStop, outerStop);
				_textureCache[key] = texture;
			}
			_spriteBatch.Draw(texture, rect, color);
		}

		private Texture2D CreateInvertedSoftRadial(int diameter, float innerStop, float outerStop)
		{
			diameter = Math.Max(1, diameter);
			var texture = new Texture2D(_graphicsDevice, diameter, diameter);
			var data = new Color[diameter * diameter];
			float radius = diameter * 0.5f;
			for (int y = 0; y < diameter; y++)
			{
				float dy = y + 0.5f - radius;
				for (int x = 0; x < diameter; x++)
				{
					float dx = x + 0.5f - radius;
					float d = (float)Math.Sqrt(dx * dx + dy * dy) / radius;
					float alpha = d <= innerStop
						? 0f
						: d >= outerStop
							? 1f
							: (d - innerStop) / (outerStop - innerStop);
					alpha = alpha * alpha * (3f - 2f * alpha);
					byte a = (byte)MathHelper.Clamp((int)Math.Round(alpha * 255f), 0, 255);
					data[y * diameter + x] = Color.FromNonPremultiplied(255, 255, 255, a);
				}
			}
			texture.SetData(data);
			return texture;
		}

		private void DrawCenteredString(SpriteFont font, string text, Vector2 centerTop, Color color, float scale)
		{
			if (font == null || string.IsNullOrEmpty(text)) return;
			Vector2 size = font.MeasureString(text) * scale;
			_spriteBatch.DrawString(font, text, new Vector2(centerTop.X - size.X / 2f, centerTop.Y), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private Texture2D GetTexture(string assetName)
		{
			if (string.IsNullOrWhiteSpace(assetName)) return null;
			if (_textureCache.TryGetValue(assetName, out var texture)) return texture;
			texture = _imageAssets.TryGetTexture(assetName);
			_textureCache[assetName] = texture;
			return texture;
		}

		private void LoadTextures()
		{
			_booster1 ??= GetTexture("Booster_Pack/booster_1");
			_booster2 ??= GetTexture("Booster_Pack/booster_2");
			_booster3 ??= GetTexture("Booster_Pack/booster_3");
			_boosterLeft ??= GetTexture("Booster_Pack/booster_4_left");
			_boosterRight ??= GetTexture("Booster_Pack/booster_4_right");
		}

		private void ClearTextureCache()
		{
			_textureCache.Clear();
			_booster1 = null;
			_booster2 = null;
			_booster3 = null;
			_boosterLeft = null;
			_boosterRight = null;
		}

		private Vector2[] ComputeLootCenters(List<BoosterPackLootPreview> loot)
		{
			int count = loot?.Count ?? 0;
			if (count <= 0) return Array.Empty<Vector2>();
			float[] widths = new float[count];
			for (int i = 0; i < count; i++)
			{
				widths[i] = loot[i].Kind == BoosterPackLootKind.Card ? CardSlotWidth : LootSlotWidth;
			}
			float total = widths.Sum() + LootGap * (count - 1);
			float x = StageCenterX - total / 2f;
			var centers = new Vector2[count];
			for (int i = 0; i < count; i++)
			{
				centers[i] = new Vector2(x + widths[i] / 2f, LootCenterY);
				x += widths[i] + LootGap;
			}
			return centers;
		}

		private Vector2 GetCardRenderPosition(Vector2 desiredVisualCenter)
		{
			var settings = CardGeometryService.GetSettings(EntityManager);
			int offsetY = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;
			return new Vector2(desiredVisualCenter.X, desiredVisualCenter.Y + offsetY * CardScale);
		}

		private float GetRevealProgress(float t, float delay)
		{
			return MathHelper.Clamp((t - ShowcaseStart - delay) / 0.72f, 0f, 1f);
		}

		private float GetAuraAlpha(float t)
		{
			if (t >= ChargeStart && t < RuptureStart)
			{
				float pulse = 0.5f + 0.5f * (float)Math.Sin(t * MathHelper.TwoPi / 0.5f);
				return MathHelper.Lerp(0.40f, 0.90f, pulse);
			}
			return 0.45f;
		}

		private Color PickParticleColor()
		{
			float roll = NextFloat();
			if (roll > 0.34f) return GoldHot;
			return NextFloat() > 0.45f ? Blue : BloodHot;
		}

		private float NextFloat()
		{
			return (float)_rng.NextDouble();
		}

		private static Rectangle CenteredRect(Vector2 center, int width, int height)
		{
			return new Rectangle(
				(int)Math.Round(center.X - width / 2f),
				(int)Math.Round(center.Y - height / 2f),
				width,
				height);
		}

		private static Rectangle Inflate(Rectangle rect, int x, int y)
		{
			rect.Inflate(x, y);
			return rect;
		}

		private static float EaseOutCubic(float x)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			return 1f - (float)Math.Pow(1f - x, 3f);
		}

		private static float EaseInQuad(float x)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			return x * x;
		}

		private static float EaseOutBack(float x)
		{
			x = MathHelper.Clamp(x, 0f, 1f);
			const float c1 = 1.70158f;
			const float c3 = c1 + 1f;
			return 1f + c3 * (float)Math.Pow(x - 1f, 3f) + c1 * (float)Math.Pow(x - 1f, 2f);
		}

		private static readonly Color Blood = new(197, 31, 51);
		private static readonly Color BloodHot = new(255, 64, 86);
		private static readonly Color Gold = new(233, 199, 85);
		private static readonly Color GoldHot = new(255, 240, 164);
		private static readonly Color Blue = new(101, 209, 255);

		private sealed class ParticleFx
		{
			public Vector2 Start;
			public Vector2 Delta;
			public float Size;
			public Color Color;
			public float StartSeconds;
			public float DurationSeconds;
		}

		private sealed class ShardFx
		{
			public Vector2 Start;
			public Vector2 Delta;
			public float Width;
			public float Height;
			public float RotationRadians;
			public float StartSeconds;
			public float DurationSeconds;
		}
	}
}
