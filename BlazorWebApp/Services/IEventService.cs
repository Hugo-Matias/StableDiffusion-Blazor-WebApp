namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing typed event publication and subscription.
    /// Provides a centralized event bus for cross-component communication.
    /// </summary>
    public interface IEventService
    {
        /// <summary>
        /// Publishes an event to all subscribers.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to publish</typeparam>
        /// <param name="eventArgs">The event arguments</param>
        void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs;

        /// <summary>
        /// Subscribes a handler to events of a specific type.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to subscribe to</typeparam>
        /// <param name="handler">The handler to invoke when the event is published</param>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;

        /// <summary>
        /// Unsubscribes a handler from events of a specific type.
        /// </summary>
        /// <typeparam name="TEvent">The type of event to unsubscribe from</typeparam>
        /// <param name="handler">The handler to remove</param>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs;

        /// <summary>
        /// Gets the number of subscribers for a specific event type.
        /// Useful for debugging and testing.
        /// </summary>
        /// <typeparam name="TEvent">The event type to check</typeparam>
        /// <returns>The number of subscribers</returns>
        int GetSubscriberCount<TEvent>() where TEvent : EventArgs;
    }
}
