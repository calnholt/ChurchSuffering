using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Root system for the Achievement scene.
    /// Manages lifecycle of child display systems.
    /// </summary>
    [DebugTab("Achievement Scene")]
    public class AchievementSceneSystem : Core.System
    {
        private readonly SystemManager _systemManager;
        private readonly World _world;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ContentManager _content;
        private bool _firstLoad = true;

        private AchievementBackgroundDisplaySystem _backgroundDisplaySystem;
        private AchievementGridDisplaySystem _gridDisplaySystem;
        private AchievementDescriptionDisplaySystem _descriptionDisplaySystem;
        private AchievementMeterDisplaySystem _meterDisplaySystem;
        private AchievementTitleDisplaySystem _titleDisplaySystem;
        private AchievementBackButtonDisplaySystem _backButtonDisplaySystem;
        private AchievementExplosionSystem _explosionSystem;
        private AchievementConfettiDisplaySystem _confettiSystem;

        public AchievementSceneSystem(EntityManager em, SystemManager sm, World world, GraphicsDevice gd, SpriteBatch sb, ContentManager content)
            : base(em)
        {
            _systemManager = sm;
            _world = world;
            _graphicsDevice = gd;
            _spriteBatch = sb;
            _content = content;

			EventManager.Subscribe<PrepareSceneEvent>(evt =>
			{
				if (evt.Scene != SceneId.Achievement) return;
				AddAchievementSystems();
				SetAchievementSystemsActive(false);
			});

            EventManager.Subscribe<LoadSceneEvent>(_ =>
            {
                if (_.Scene != SceneId.Achievement) return;
                AddAchievementSystems();
				SetAchievementSystemsActive(true);
            });

            EventManager.Subscribe<DeleteCachesEvent>(_ =>
            {
				if (_.Scene != SceneId.Achievement) SetAchievementSystemsActive(false);
            });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            yield break;
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public void Draw()
        {
            if (ShaderRuntimeOptions.ShadersEnabled && _backgroundDisplaySystem != null)
                FrameProfiler.Measure("AchievementBackgroundDisplaySystem.Draw", _backgroundDisplaySystem.Draw);
            if (_gridDisplaySystem != null)
                FrameProfiler.Measure("AchievementGridDisplaySystem.Draw", _gridDisplaySystem.Draw);
            if (_descriptionDisplaySystem != null)
                FrameProfiler.Measure("AchievementDescriptionDisplaySystem.Draw", _descriptionDisplaySystem.Draw);
            if (_meterDisplaySystem != null)
                FrameProfiler.Measure("AchievementMeterDisplaySystem.Draw", _meterDisplaySystem.Draw);
            if (_titleDisplaySystem != null)
                FrameProfiler.Measure("AchievementTitleDisplaySystem.Draw", _titleDisplaySystem.Draw);
            if (_backButtonDisplaySystem != null)
                FrameProfiler.Measure("AchievementBackButtonDisplaySystem.Draw", _backButtonDisplaySystem.Draw);
            if (_confettiSystem != null)
                FrameProfiler.Measure("AchievementConfettiDisplaySystem.Draw", _confettiSystem.Draw);
        }

        private void AddAchievementSystems()
        {
            if (!_firstLoad) return;
            _firstLoad = false;

            // Background shader must be added first so it draws behind everything
            if (_backgroundDisplaySystem == null)
                _backgroundDisplaySystem = new AchievementBackgroundDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _content);
            _world.AddSystem(_backgroundDisplaySystem);

            if (_gridDisplaySystem == null)
                _gridDisplaySystem = new AchievementGridDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
            _world.AddSystem(_gridDisplaySystem);

            if (_descriptionDisplaySystem == null)
                _descriptionDisplaySystem = new AchievementDescriptionDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
            _world.AddSystem(_descriptionDisplaySystem);

            if (_meterDisplaySystem == null)
                _meterDisplaySystem = new AchievementMeterDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
            _world.AddSystem(_meterDisplaySystem);

            if (_titleDisplaySystem == null)
                _titleDisplaySystem = new AchievementTitleDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
            _world.AddSystem(_titleDisplaySystem);

            if (_backButtonDisplaySystem == null)
                _backButtonDisplaySystem = new AchievementBackButtonDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
            _world.AddSystem(_backButtonDisplaySystem);

            if (_explosionSystem == null)
                _explosionSystem = new AchievementExplosionSystem(_world.EntityManager);
            _world.AddSystem(_explosionSystem);

            if (_confettiSystem == null)
                _confettiSystem = new AchievementConfettiDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
            _world.AddSystem(_confettiSystem);
        }

		private void SetAchievementSystemsActive(bool active)
		{
			_backgroundDisplaySystem?.SetActive(active);
			_gridDisplaySystem?.SetActive(active);
			_descriptionDisplaySystem?.SetActive(active);
			_meterDisplaySystem?.SetActive(active);
			_titleDisplaySystem?.SetActive(active);
			_backButtonDisplaySystem?.SetActive(active);
			_explosionSystem?.SetActive(active);
			_confettiSystem?.SetActive(active);
		}
    }
}
