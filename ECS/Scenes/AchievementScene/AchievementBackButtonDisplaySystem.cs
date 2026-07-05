using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays the back button in the top-right corner of the Achievement scene.
    /// </summary>
    [DebugTab("Achievement Back Button")]
    public class AchievementBackButtonDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;

        private Entity _buttonEntity;
        private Texture2D _buttonTexture;

        [DebugEditable(DisplayName = "Margin Right", Step = 5, Min = 10, Max = 100)]
        public int MarginRight { get; set; } = 30;

        [DebugEditable(DisplayName = "Margin Top", Step = 5, Min = 10, Max = 100)]
        public int MarginTop { get; set; } = 30;

        public AchievementBackButtonDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb) : base(em)
        {
            _graphicsDevice = gd;
            _spriteBatch = sb;

            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
        }

        private void OnLoadScene(LoadSceneEvent evt)
        {
            if (evt.Scene == SceneId.Achievement)
            {
                EnsureButtonEntity();
            }
        }

        private void EnsureButtonEntity()
        {
            if (_buttonEntity != null && EntityManager.GetEntity(_buttonEntity.Name) != null)
                return;

            EnsureButtonTexture();

            _buttonEntity = EntityManager.CreateEntity("AchievementBackButton");

            var rect = GetButtonRect();
            EntityManager.AddComponent(_buttonEntity, new Transform
            {
                Position = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f),
                ZOrder = 200
            });
            EntityManager.AddComponent(_buttonEntity, new UIElement
            {
                Bounds = rect,
                IsInteractable = true,
                TooltipType = TooltipType.None
            });
            EntityManager.AddComponent(_buttonEntity, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Below, RequiresHold = true });
            EntityManager.AddComponent(_buttonEntity, new AchievementBackButton());
            EntityManager.AddComponent(_buttonEntity, new OwnedByScene { Scene = SceneId.Achievement });
        }

        private void EnsureButtonTexture()
        {
            if (_buttonTexture != null) return;
            _buttonTexture = ButtonTextureFactory.Create(
                _graphicsDevice, "Back", Color.Black, Color.White);
        }

        private Rectangle GetButtonRect()
        {
            int x = Game1.VirtualWidth - _buttonTexture.Width - MarginRight;
            int y = MarginTop;
            return new Rectangle(x, y, _buttonTexture.Width, _buttonTexture.Height);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var scene = entity.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            EnsureButtonEntity();

            // Update button bounds in case of resize
            var ui = _buttonEntity?.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.Bounds = GetButtonRect();
            }

            // Check for click
            if (ui != null && ui.IsClicked)
            {
                EventManager.Publish(new ShowTransition { Scene = SceneId.WayStation, SkipHold = true });
                ui.IsClicked = false;
            }
        }

        public void Draw()
        {
            var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Achievement) return;

            if (_buttonEntity == null || _buttonTexture == null) return;

            var ui = _buttonEntity.GetComponent<UIElement>();
            if (ui == null) return;

            _spriteBatch.Draw(_buttonTexture, new Vector2(ui.Bounds.X, ui.Bounds.Y), Color.White);
        }
    }
}
