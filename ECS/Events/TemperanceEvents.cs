using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Increase or decrease the player's Temperance by Delta.
    /// Triggers ability checks after modification.
    /// </summary>
    public class ModifyTemperanceEvent
    {
        public int Delta { get; set; } = 0;
    }
    public class SetTemperanceEvent
    {
        public int Amount { get; set; } = 0;
    }

    /// <summary>
    /// Emitted when the player's Temperance ability activates.
    /// Used to drive player-focused VFX.
    /// </summary>
    public class TriggerTemperance
    {
        public Entity Owner { get; set; }
        public string AbilityId { get; set; }
    }
}

