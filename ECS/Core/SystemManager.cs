using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;

namespace ChurchSuffering.ECS.Core
{
    public enum SystemUpdatePhase
    {
        Input,
        Interaction,
        Gameplay,
        Presentation,
    }

    /// <summary>
    /// Manages all systems in the ECS architecture.
    /// Handles system registration, ordering, and updates.
    /// </summary>
    public class SystemManager
    {
        private readonly Dictionary<SystemUpdatePhase, List<System>> _systems = new()
        {
            [SystemUpdatePhase.Input] = new(),
            [SystemUpdatePhase.Interaction] = new(),
            [SystemUpdatePhase.Gameplay] = new(),
            [SystemUpdatePhase.Presentation] = new(),
        };
        private readonly List<System> _lateSystems = new();
        private readonly EntityManager _entityManager;

        public SystemManager(EntityManager entityManager)
        {
            _entityManager = entityManager;
        }

        /// <summary>
        /// Adds a system to the manager
        /// </summary>
        public void AddSystem(System system, SystemUpdatePhase phase = SystemUpdatePhase.Gameplay)
        {
            if (!_systems.Values.Any(systems => systems.Contains(system)))
            {
                _systems[phase].Add(system);
            }
        }

        /// <summary>
        /// Adds a system that runs after all normal systems have updated.
        /// Use this for systems that must see final Position values (e.g. parallax).
        /// </summary>
        public void AddLateSystem(System system)
        {
            if (!_lateSystems.Contains(system))
            {
                _lateSystems.Add(system);
            }
        }

        /// <summary>
        /// Removes a system from the manager
        /// </summary>
        public void RemoveSystem(System system)
        {
            foreach (var systems in _systems.Values)
            {
                systems.Remove(system);
            }
            _lateSystems.Remove(system);
        }
        
        /// <summary>
        /// Gets a system of the specified type
        /// </summary>
        public T GetSystem<T>() where T : System
        {
            return _systems.Values.SelectMany(systems => systems).OfType<T>().FirstOrDefault();
        }
        
        /// <summary>
        /// Gets all systems of the specified type
        /// </summary>
        public IEnumerable<T> GetSystems<T>() where T : System
        {
            return _systems.Values.SelectMany(systems => systems).OfType<T>();
        }
        
        /// <summary>
        /// Gets all systems.
        /// </summary>
        public IEnumerable<System> GetAllSystems()
        {
            return _systems.Values.SelectMany(systems => systems).Concat(_lateSystems);
        }
        
        /// <summary>
        /// Updates all systems
        /// </summary>
        public void Update(GameTime gameTime)
        {
            // Iterate over a snapshot to avoid modification during enumeration
            foreach (var phase in global::System.Enum.GetValues<SystemUpdatePhase>())
            {
                var snapshot = _systems[phase].ToArray();
                foreach (var system in snapshot)
                {
#if DEBUG
                    FrameProfiler.Measure(system.GetType().Name + ".Update", () => system.Update(gameTime));
#else
                    system.Update(gameTime);
#endif
                }
            }
        }

        /// <summary>
        /// Updates late systems (runs after all normal systems).
        /// </summary>
        public void LateUpdate(GameTime gameTime)
        {
            var snapshot = _lateSystems.ToArray();
            foreach (var system in snapshot)
            {
#if DEBUG
                FrameProfiler.Measure(system.GetType().Name + ".Update", () => system.Update(gameTime));
#else
                system.Update(gameTime);
#endif
            }
        }
        
        /// <summary>
        /// Enables or disables all systems
        /// </summary>
        public void SetAllSystemsActive(bool active)
        {
            foreach (var system in _systems.Values.SelectMany(systems => systems))
            {
                system.SetActive(active);
            }
            foreach (var system in _lateSystems)
            {
                system.SetActive(active);
            }
        }
        
        /// <summary>
        /// Clears all systems
        /// </summary>
        public void Clear()
        {
            foreach (var systems in _systems.Values)
            {
                systems.Clear();
            }
            _lateSystems.Clear();
        }
        
        /// <summary>
        /// Gets the number of registered systems
        /// </summary>
        public int SystemCount => _systems.Values.Sum(systems => systems.Count);
    }
} 
