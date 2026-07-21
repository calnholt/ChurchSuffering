using Microsoft.Xna.Framework;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;

namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// The main ECS world that manages entities and systems.
    /// This is the primary interface for interacting with the ECS system.
    /// </summary>
    public class World
    {
        public EntityManager EntityManager { get; }
        public SystemManager SystemManager { get; }
        
        public World()
        {
            EntityManager = new EntityManager();
            SystemManager = new SystemManager(EntityManager);
        }
        
        /// <summary>
        /// Updates the entire ECS world
        /// </summary>
        public void Update(GameTime gameTime)
        {
#if DEBUG
            FrameProfiler.MeasureInclusive("ECS.World.Update", () => SystemManager.Update(gameTime));
            FrameProfiler.MeasureInclusive("ECS.World.LateUpdate", () => SystemManager.LateUpdate(gameTime));
#else
            SystemManager.Update(gameTime);
            SystemManager.LateUpdate(gameTime);
#endif
        }
        
        /// <summary>
        /// Creates a new entity
        /// </summary>
        public Entity CreateEntity(string name = "Entity")
        {
            return EntityManager.CreateEntity(name);
        }
        
        /// <summary>
        /// Destroys an entity
        /// </summary>
        public void DestroyEntity(int entityId)
        {
            EntityManager.DestroyEntity(entityId);
        }
        
        /// <summary>
        /// Adds a component to an entity
        /// </summary>
        public void AddComponent<T>(Entity entity, T component) where T : class, IComponent
        {
            EntityManager.AddComponent(entity, component);
        }
        
        /// <summary>
        /// Removes a component from an entity
        /// </summary>
        public void RemoveComponent<T>(Entity entity) where T : class, IComponent
        {
            EntityManager.RemoveComponent<T>(entity);
        }
        
        /// <summary>
        /// Gets entities with a specific component
        /// </summary>
        public IEnumerable<Entity> GetEntitiesWithComponent<T>() where T : class, IComponent
        {
            return EntityManager.GetEntitiesWithComponent<T>();
        }
        
        /// <summary>
        /// Adds a system to the world
        /// </summary>
        public void AddSystem(System system, SystemUpdatePhase phase = SystemUpdatePhase.Gameplay)
        {
            SystemManager.AddSystem(system, phase);
        }

        /// <summary>
        /// Adds a system that runs after all normal systems have updated.
        /// </summary>
        public void AddLateSystem(System system)
        {
            SystemManager.AddLateSystem(system);
        }
        
        /// <summary>
        /// Removes a system from the world
        /// </summary>
        public void RemoveSystem(System system)
        {
            SystemManager.RemoveSystem(system);
        }
        
        /// <summary>
        /// Gets a system of the specified type
        /// </summary>
        public T GetSystem<T>() where T : System
        {
            return SystemManager.GetSystem<T>();
        }
        
        /// <summary>
        /// Clears all entities and systems
        /// </summary>
        public void Clear()
        {
            EntityManager.Clear();
            SystemManager.Clear();
        }
    }
} 
