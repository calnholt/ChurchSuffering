namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Published to display a temporary alert notification on screen.
    /// The alert slides in from the right, holds, then slides out.
    /// </summary>
    public class AlertEvent
    {
        /// <summary>
        /// Main display text (larger, shown prominently).
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Subtitle/category text (smaller, bottom-right).
        /// </summary>
        public string Type { get; set; } = string.Empty;
    }
}
