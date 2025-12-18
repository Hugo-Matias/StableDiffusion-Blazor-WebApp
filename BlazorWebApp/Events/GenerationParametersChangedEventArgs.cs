using BlazorWebApp.Models;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments fired when GenerationParameters change (new dynamic parameter system).
    /// Subscribe to this event to refresh UI when generation parameters are modified.
    /// </summary>
    public class GenerationParametersChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The type of change that occurred.
        /// </summary>
        public GenerationParameterChangeType ChangeType { get; set; }

        /// <summary>
        /// Optional: The fragment ID that was affected (for fragment-specific changes).
        /// </summary>
        public string? FragmentId { get; set; }

        /// <summary>
        /// Optional: The parameter name that was affected (for value changes).
        /// </summary>
        public string? ParameterName { get; set; }

        /// <summary>
        /// Optional: Additional context message.
        /// </summary>
        public string? Message { get; set; }

        public GenerationParametersChangedEventArgs(
            GenerationParameterChangeType changeType = GenerationParameterChangeType.Unknown,
            string? fragmentId = null,
            string? parameterName = null,
            string? message = null)
        {
            ChangeType = changeType;
            FragmentId = fragmentId;
            ParameterName = parameterName;
            Message = message;
        }

        /// <summary>
        /// Creates event args for when a workflow is loaded/changed.
        /// </summary>
        public static GenerationParametersChangedEventArgs WorkflowChanged(Guid? workflowId = null)
            => new(GenerationParameterChangeType.WorkflowChanged, message: workflowId?.ToString());

        /// <summary>
        /// Creates event args for when a fragment value changes.
        /// </summary>
        public static GenerationParametersChangedEventArgs FragmentValueChanged(string fragmentId, string parameterName)
            => new(GenerationParameterChangeType.FragmentValueChanged, fragmentId, parameterName);

        /// <summary>
        /// Creates event args for when a fragment is added.
        /// </summary>
        public static GenerationParametersChangedEventArgs FragmentAdded(string fragmentId)
            => new(GenerationParameterChangeType.FragmentAdded, fragmentId);

        /// <summary>
        /// Creates event args for when a fragment is removed.
        /// </summary>
        public static GenerationParametersChangedEventArgs FragmentRemoved(string fragmentId)
            => new(GenerationParameterChangeType.FragmentRemoved, fragmentId);

        /// <summary>
        /// Creates event args for when fragment active state changes.
        /// </summary>
        public static GenerationParametersChangedEventArgs FragmentActiveChanged(string fragmentId, bool isActive)
            => new(GenerationParameterChangeType.FragmentActiveChanged, fragmentId, message: isActive.ToString());

        /// <summary>
        /// Creates event args for when parameters are loaded from saved state.
        /// </summary>
        public static GenerationParametersChangedEventArgs ParametersLoaded()
            => new(GenerationParameterChangeType.ParametersLoaded);

        /// <summary>
        /// Creates event args for when an asset changes.
        /// </summary>
        public static GenerationParametersChangedEventArgs AssetChanged(string assetName)
            => new(GenerationParameterChangeType.AssetChanged, parameterName: assetName);

        /// <summary>
        /// Creates event args for when a source (input image/video) changes.
        /// </summary>
        public static GenerationParametersChangedEventArgs SourceChanged(string sourceId)
            => new(GenerationParameterChangeType.SourceChanged, fragmentId: sourceId);
    }

    /// <summary>
    /// Types of changes that can occur to GenerationParameters.
    /// </summary>
    public enum GenerationParameterChangeType
    {
        Unknown = 0,
        WorkflowChanged,
        FragmentValueChanged,
        FragmentAdded,
        FragmentRemoved,
        FragmentActiveChanged,
        FragmentReordered,
        ParametersLoaded,
        AssetChanged,
        SourceChanged,
        LorasChanged
    }
}
