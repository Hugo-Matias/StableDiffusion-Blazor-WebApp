namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for state changes in the application.
    /// Provides information about what type of state changed and the old/new values.
    /// </summary>
    public class StateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The type of state that changed
        /// </summary>
        public StateChangeType ChangeType { get; init; }

        /// <summary>
        /// The previous value before the change (optional)
        /// </summary>
        public object? OldValue { get; init; }

        /// <summary>
        /// The new value after the change (optional)
        /// </summary>
        public object? NewValue { get; init; }
    }

    /// <summary>
    /// Defines the different types of state changes that can occur
    /// </summary>
    public enum StateChangeType
    {
        /// <summary>
        /// Application-wide state changed
        /// </summary>
        AppState,

        /// <summary>
        /// Generation parameters changed
        /// </summary>
        GenerationParameters,

        /// <summary>
        /// Workflow base model changed
        /// </summary>
        WorkflowBase,

        /// <summary>
        /// Current workflow changed
        /// </summary>
        CurrentWorkflow,

        /// <summary>
        /// LoRAs changed
        /// </summary>
        Loras,

        /// <summary>
        /// Assets changed
        /// </summary>
        Assets
    }
}
