using System;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.Diagnostics;
using System.Linq;
using ChurchSuffering.ECS.Data.Locations;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
        /// Renders a battle background with cover scaling (no stretch). Anchors to bottom; centers horizontally.
    /// </summary>
    [DebugTab("Battle Background")]
    public class BattleBackgroundSystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private Texture2D _background;

        // Debug-adjustable vertical offset for background positioning
        [DebugEditable(DisplayName = "Offset Y", Step = 2, Min = -2000, Max = 2000)]
        public int OffsetY { get; set; } = 0;

        public BattleBackgroundSystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;

            EventManager.Subscribe<ChangeBattleLocationEvent>(OnChangeLocation);
            LoggingService.Append("BattleBackgroundSystem.ctor", new System.Text.Json.Nodes.JsonObject { ["message"] = "subscribed to ChangeBattleLocationEvent" });
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            // No entities needed; system-level rendering
            yield break;
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnChangeLocation(ChangeBattleLocationEvent evt)
        {
            try
            {
                // Ensure a single Battlefield component exists; update to requested location when provided.
                Battlefield battlefield = null;
                foreach (var e in EntityManager.GetEntitiesWithComponent<Battlefield>())
                {
                    battlefield = e.GetComponent<Battlefield>();
                    if (battlefield != null) break;
                }
                if (battlefield == null)
                {
                    var worldEntity = EntityManager.CreateEntity("Battlefield");
                    battlefield = new Battlefield();
                    EntityManager.AddComponent(worldEntity, battlefield);
                }

                if (evt.Location.HasValue)
                {
                    battlefield.Location = evt.Location.Value;
                }

				// Load the background texture, preferring explicit override then POI background, else default per location.
                string path = ResolveBackgroundPath(evt, battlefield);
                if (!string.IsNullOrWhiteSpace(path))
                {
					_background = _imageAssets.TryGetTexture(path);
                }
            }
            catch (Exception ex)
            {
                LoggingService.Append("BattleBackgroundSystem.OnChangeLocation.error", new System.Text.Json.Nodes.JsonObject { ["message"] = ex.Message });
            }
        }

		private static string RemoveExtension(string p)
		{
			if (string.IsNullOrWhiteSpace(p)) return p;
			try { return System.IO.Path.ChangeExtension(p, null); }
			catch { return p; }
		}

		private string ResolveBackgroundPath(ChangeBattleLocationEvent evt, Battlefield battlefield)
		{
			// 1) Manual override
			if (!string.IsNullOrWhiteSpace(evt.TexturePath))
			{
				return RemoveExtension(evt.TexturePath);
			}

			// 2) Selected quest's POI background (via QueuedEvents context)
			try
			{
				var qeEntity = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
				var qe = qeEntity?.GetComponent<QueuedEvents>();
				if (!string.IsNullOrEmpty(qe?.LocationId) &&
					LocationDefinitionCache.TryGet(qe.LocationId, out var def) &&
					qe.QuestIndex >= 0 && qe.QuestIndex < def.pointsOfInterest.Count)
				{
					var bg = def.pointsOfInterest[qe.QuestIndex].background;
					if (!string.IsNullOrWhiteSpace(bg))
					{
						return RemoveExtension(bg);
					}
				}
			}
			catch { /* ignore and fall back */ }

			// 3) Fallback per battlefield location
			return battlefield.Location switch
			{
				BattleLocation.Desert => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Desert),
				BattleLocation.Tundra => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Tundra),
				BattleLocation.Jungle => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Jungle),
				BattleLocation.Volcano => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Volcano),
				BattleLocation.TheGate => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.TheGate),
				BattleLocation.Forest => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Forest),
				BattleLocation.Cathedral => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Cathedral),
				BattleLocation.Gothic => BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Gothic),
				_ => null
			};
		}

        public void Draw()
        {
            if (_background == null) return;

            int viewportW = Game1.VirtualWidth;
            int viewportH = Game1.VirtualHeight;

            int texW = _background.Width;
            int texH = _background.Height;

            // Compute cover scale (no stretch; scale up until both dimensions cover viewport)
            float scaleX = viewportW / (float)texW;
            float scaleY = viewportH / (float)texH;
            float scale = Math.Max(scaleX, scaleY);

            int drawW = (int)Math.Round(texW * scale);
            int drawH = (int)Math.Round(texH * scale);

            // Position: anchor bottom (prioritize showing bottom), center horizontally
            int x = (viewportW - drawW) / 2;
            int y = viewportH - drawH + OffsetY;

            var dest = new Rectangle(x, y, drawW, drawH);
            _spriteBatch.Draw(_background, dest, Color.White);
        }
    }
}

