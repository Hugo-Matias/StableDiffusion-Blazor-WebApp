namespace BlazorWebApp.Services
{
    /// <summary>
    /// Implementation of typed event aggregation service.
    /// Provides a centralized event bus for decoupled component communication.
    /// Thread-safe for concurrent publish/subscribe operations.
    /// </summary>
    public class EventService : IEventService
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();
        private readonly object _lock = new();

        /// <inheritdoc/>
        public void Publish<TEvent>(TEvent eventArgs) where TEvent : EventArgs
        {
            if (eventArgs == null)
                throw new ArgumentNullException(nameof(eventArgs));

            List<Delegate>? handlers;
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.TryGetValue(eventType, out handlers))
                    return;

                // Create a copy to avoid modification during iteration
                handlers = handlers.ToList();
            }

            // Invoke outside lock to avoid deadlocks
            foreach (var handler in handlers)
            {
                try
                {
                    ((Action<TEvent>)handler)(eventArgs);
                }
                catch (Exception ex)
                {
                    // Log error but don't stop other handlers
                    Console.Error.WriteLine($"Error in event handler for {typeof(TEvent).Name}: {ex.Message}");
                }
            }
        }

        /// <inheritdoc/>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                    _subscribers[eventType] = new List<Delegate>();

                _subscribers[eventType].Add(handler);
            }
        }

        /// <inheritdoc/>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : EventArgs
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (_subscribers.TryGetValue(eventType, out var handlers))
                {
                    handlers.Remove(handler);

                    // Clean up empty lists
                    if (handlers.Count == 0)
                        _subscribers.Remove(eventType);
                }
            }
        }

        /// <inheritdoc/>
        public int GetSubscriberCount<TEvent>() where TEvent : EventArgs
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                return _subscribers.TryGetValue(eventType, out var handlers) ? handlers.Count : 0;
            }
        }
    }
}
