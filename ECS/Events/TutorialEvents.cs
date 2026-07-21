using ChurchSuffering.ECS.Data.Tutorials;

namespace ChurchSuffering.ECS.Events
{
    /// <summary>
    /// Published when a tutorial starts being displayed.
    /// </summary>
    public class TutorialStartedEvent
    {
        public TutorialDefinition Tutorial { get; set; }
    }

    /// <summary>
    /// Published when a tutorial has been completed (user held continue).
    /// </summary>
    public class TutorialCompletedEvent
    {
        public TutorialDefinition Tutorial { get; set; }
    }

    /// <summary>
    /// Published when all queued tutorials have been shown.
    /// </summary>
    public class AllTutorialsCompletedEvent { }

    /// <summary>
    /// Request to advance to the next tutorial in the queue.
    /// </summary>
    public class AdvanceTutorialEvent { }

    /// <summary>
    /// Request to restart the current guided tutorial section.
    /// </summary>
    public class GuidedTutorialRestartRequested { }

    /// <summary>
    /// Request to skip and complete the guided tutorial from the pause menu.
    /// </summary>
    public class GuidedTutorialSkipRequested { }
}
