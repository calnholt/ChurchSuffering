using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// Base class for all systems in the ECS architecture.
    /// Systems contain the logic that operates on entities with specific components.
    /// </summary>
    public abstract class System
    {
        protected EntityManager EntityManager { get; }
        protected bool IsActive { get; set; } = true;
        
        protected System(EntityManager entityManager)
        {
            EntityManager = entityManager;
        }
        
        /// <summary>
        /// Called every frame to update the system
        /// </summary>
        public virtual void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            
            var entities = GetRelevantEntities();
            foreach (var entity in entities)
            {
                UpdateEntity(entity, gameTime);
            }
        }
        
        /// <summary>
        /// Gets the entities that this system should process
        /// </summary>
        protected abstract IEnumerable<Entity> GetRelevantEntities();
        
        /// <summary>
        /// Updates a single entity
        /// </summary>
        protected abstract void UpdateEntity(Entity entity, GameTime gameTime);
        
        /// <summary>
        /// Enables or disables this system
        /// </summary>
        public virtual void SetActive(bool active)
        {
            IsActive = active;
        }
    }
} 