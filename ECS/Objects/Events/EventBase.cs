using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Objects.Events
{
    /// <summary>
    /// Abstract base for quest narrative events with 1-3 player choices.
    /// Option visibility is derived from non-empty OptionNText; callers pass EntityManager into handlers.
    /// </summary>
    public abstract class EventBase
    {
        public string Id { get; protected set; } = string.Empty;
        public string Title { get; protected set; } = string.Empty;
        public string EventText { get; protected set; } = string.Empty;

        public virtual void Initialize(EntityManager entityManager) { }

        public virtual string Option1Text => string.Empty;
        public virtual string Option2Text => string.Empty;
        public virtual string Option3Text => string.Empty;

        public virtual void OnOption1(EntityManager entityManager) { }
        public virtual void OnOption2(EntityManager entityManager) { }
        public virtual void OnOption3(EntityManager entityManager) { }
    }
}
