using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays the currently equipped weapon as a gold circle with the weapon icon centered,
    /// positioned just above the discard pile display. Shows tooltip on hover.
    /// </summary>
    [DebugTab("Equipped Weapon Display")]
    public class EquippedWeaponDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private Texture2D _weaponTex;
        private string _loadedWeaponId;
		private const string RootEntityName = "UI_EquippedWeaponRoot";

        // Layout/debug controls
        [DebugEditable(DisplayName = "Circle Radius", Step = 1, Min = 8, Max = 400)]
		public int CircleRadius { get; set; } = 30;

        [DebugEditable(DisplayName = "Above Discard Offset Y", Step = 1, Min = -400, Max = 400)]
		public int AboveDiscardOffsetY { get; set; } = 16;

        [DebugEditable(DisplayName = "Gold R", Step = 1, Min = 0, Max = 255)]
		public int GoldR { get; set; } = 215;
        [DebugEditable(DisplayName = "Gold G", Step = 1, Min = 0, Max = 255)]
		public int GoldG { get; set; } = 186;
        [DebugEditable(DisplayName = "Gold B", Step = 1, Min = 0, Max = 255)]
		public int GoldB { get; set; } = 147;

        [DebugEditable(DisplayName = "Icon Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
		public float IconScale { get; set; } = 1.2f;

        public EquippedWeaponDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;

            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            LoggingService.Append("EquippedWeaponDisplaySystem.OnChangeBattlePhaseEvent", new JsonObject {
                { "Current", evt.Current.ToString() },
                { "Previous", evt.Previous.ToString() }
            });
            var rootUi = EntityManager.GetEntity(RootEntityName)?.GetComponent<UIElement>();
            if (rootUi != null)
            {
                var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var weapon = player?.GetComponent<EquippedWeapon>()?.SpawnedEntity;
                var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
                bool weaponInHand = weapon != null && deck?.Hand.Contains(weapon) == true;
                rootUi.IsHidden = weaponInHand;
            }
        }
        private void TryLoadWeaponTexture()
        {
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var ew = player?.GetComponent<EquippedWeapon>();
            string weaponId = !string.IsNullOrWhiteSpace(ew?.WeaponId) ? ew.WeaponId : "sword";
            if (_weaponTex != null && weaponId == _loadedWeaponId) return;

            _loadedWeaponId = weaponId;
            _weaponTex = _imageAssets.GetTextureOrFallback(
                CrusaderPortraitAssets.ResolveWeaponCardArtAsset(weaponId),
                CrusaderPortraitAssets.ResolveWeaponCardArtAsset("sword"));
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Player>();
        }

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			TryLoadWeaponTexture();
			EnsureRootEntity();
			var discardRoot = EntityManager.GetEntity("UI_DiscardPileRoot");
			var discardT = discardRoot?.GetComponent<Transform>();
			var tRoot = EntityManager.GetEntity(RootEntityName)?.GetComponent<Transform>();
			if (tRoot != null && discardT != null)
			{
				int r = System.Math.Max(4, CircleRadius);
				// Use Transform.Position (logical, written this frame by DiscardPileDisplaySystem) instead of
				// UIElement.Bounds (stale post-parallax from last frame's Draw) to avoid double-parallax.
				// Bounds.Height is safe to read — it's the fixed panel size, not affected by parallax.
				var discardUI = discardRoot.GetComponent<UIElement>();
				float discardHalfH = discardUI != null && discardUI.Bounds.Height > 0 ? discardUI.Bounds.Height / 2f : 0f;
				tRoot.Position = new Vector2(discardT.Position.X, discardT.Position.Y - discardHalfH - AboveDiscardOffsetY - r);
			}
		}

        public void Draw()
        {
            var player = GetRelevantEntities().FirstOrDefault();
            if (player == null) return;

            TryLoadWeaponTexture();

            int r = System.Math.Max(4, CircleRadius);
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, r);
            EnsureRootEntity();
            var root = EntityManager.GetEntity(RootEntityName);
            var tRoot = root?.GetComponent<Transform>();
            if (tRoot == null) return;
            // ParallaxLayer has adjusted tRoot.Position after Update; use it directly as the draw center
            Vector2 center = tRoot.Position;

            // Draw gold filled circle
            var gold = new Color(GoldR, GoldG, GoldB);
            _spriteBatch.Draw(circle, new Vector2(center.X - r, center.Y - r), gold);

            // Draw weapon icon centered if available
            if (_weaponTex != null)
            {
                // Fit within circle with padding, uniform scale
                float availableDiameter = r * 2f;
                float sx = (availableDiameter / _weaponTex.Width) * System.Math.Max(0.05f, IconScale);
                float sy = (availableDiameter / _weaponTex.Height) * System.Math.Max(0.05f, IconScale);
                float scale = System.Math.Min(sx, sy);
                var origin = new Vector2(_weaponTex.Width / 2f, _weaponTex.Height / 2f);
                _spriteBatch.Draw(_weaponTex, center, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);
            }

			// Use root entity UIElement for hover/tooltip
			int hoverR = (int)System.Math.Ceiling(r * System.Math.Max(1f, IconScale));
			var hitRect = new Rectangle((int)(center.X - hoverR), (int)(center.Y - hoverR), hoverR * 2, hoverR * 2);
			var rootUi = root.GetComponent<UIElement>();
			if (rootUi == null)
			{
				EntityManager.AddComponent(root, new UIElement { Bounds = hitRect, TooltipPosition = TooltipPosition.Right, TooltipType = TooltipType.Card });
                EntityManager.AddComponent(root, new CardTooltip { CardId = player.GetComponent<EquippedWeapon>().WeaponId });
			}
			else
			{
				rootUi.Bounds = hitRect;
				rootUi.TooltipPosition = TooltipPosition.Right;
				rootUi.TooltipType = TooltipType.Card;
				var tooltip = root.GetComponent<CardTooltip>();
				if (tooltip != null)
				{
					tooltip.CardId = player.GetComponent<EquippedWeapon>()?.WeaponId;
				}
			}
        }

	private void EnsureRootEntity()
	{
		var e = EntityManager.GetEntity(RootEntityName);
		if (e == null)
		{
			e = EntityManager.CreateEntity(RootEntityName);
			EntityManager.AddComponent(e, new Transform { Position = new Vector2(100, Game1.VirtualHeight - 200), ZOrder = 10000 });
			EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
            EntityManager.AddComponent(e, new Hint { Text = "Represents your equipped weapon." });
		}
	}
    }
}

