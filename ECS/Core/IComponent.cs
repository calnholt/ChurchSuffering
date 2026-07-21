namespace ChurchSuffering.ECS.Core
{
    /// <summary>
    /// Base interface for all components in the ECS system.
    /// Components hold data and should be lightweight.
    /// </summary>
    public interface IComponent
    {
        /// <summary>
        /// The entity that owns this component
        /// </summary>
        Entity Owner { get; set; }
    }
} 