using System;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// Represents a game entity that can have multiple components attached to it.
    /// Entities are just IDs that serve as containers for components.
    /// </summary>
    public class Entity
    {
        public int Id { get; }
        public bool IsActive { get; set; } = true;
        public string Name { get; set; } = "Entity";
        
        private readonly Dictionary<Type, IComponent> _components = new();
        
        public Entity(int id)
        {
            Id = id;
        }
        
        /// <summary>
        /// Adds a component to this entity
        /// </summary>
        public void AddComponent<T>(T component) where T : class, IComponent
        {
            _components[typeof(T)] = component;
        }
        
        /// <summary>
        /// Removes a component from this entity
        /// </summary>
        public void RemoveComponent<T>() where T : class, IComponent
        {
            _components.Remove(typeof(T));
        }
        
        /// <summary>
        /// Gets a component from this entity
        /// </summary>
        public T GetComponent<T>() where T : class, IComponent
        {
            return _components.TryGetValue(typeof(T), out var component) ? component as T : null;
        }
        
        /// <summary>
        /// Checks if this entity has a specific component
        /// </summary>
        public bool HasComponent<T>() where T : class, IComponent
        {
            return _components.ContainsKey(typeof(T));
        }
        
        /// <summary>
        /// Gets all components attached to this entity
        /// </summary>
        public IEnumerable<IComponent> GetAllComponents()
        {
            return _components.Values;
        }
        
        /// <summary>
        /// Gets all component types attached to this entity
        /// </summary>
        public IEnumerable<Type> GetComponentTypes()
        {
            return _components.Keys;
        }
    }
} 