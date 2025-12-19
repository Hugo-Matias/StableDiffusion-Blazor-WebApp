using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Provides CRUD operations and workflow initialization.
    /// Subscribe to GenerationParametersChangedEventArgs via IEventService for change notifications.
    /// </summary>
    public interface IGenerationParameterService
    {
        /// <summary>
        /// Gets the current generation parameters.
        /// </summary>
        GenerationParameters Current { get; }

        /// <summary>
        /// Initializes parameters from a workflow template.
        /// Sets up fragments with default values from the pipeline.
        /// Publishes GenerationParametersChangedEventArgs.WorkflowChanged event.
        /// </summary>
        void InitializeFromWorkflow(Workflow workflow);

        /// <summary>
        /// Initializes parameters from a workflow template asynchronously.
        /// Returns the initialized GenerationParameters.
        /// Publishes GenerationParametersChangedEventArgs.WorkflowChanged event.
        /// </summary>
        Task<GenerationParameters> InitializeFromWorkflowAsync(Workflow workflow);

        /// <summary>
        /// Sets a value for a fragment parameter (no event published).
        /// Use for batch updates, call NotifyChanged() when done.
        /// </summary>
        void SetFragmentValue(string fragmentId, string parameter, object? value);

        /// <summary>
        /// Sets a value for a fragment parameter and publishes change event.
        /// </summary>
        void SetFragmentValueAndNotify(string fragmentId, string parameter, object? value);

        /// <summary>
        /// Gets a value from a fragment parameter.
        /// </summary>
        T? GetFragmentValue<T>(string fragmentId, string parameter);

        /// <summary>
        /// Sets whether a fragment is active.
        /// Publishes GenerationParametersChangedEventArgs.FragmentActiveChanged event.
        /// </summary>
        void SetFragmentActive(string fragmentId, bool isActive);

        /// <summary>
        /// Gets whether a fragment is active.
        /// </summary>
        bool IsFragmentActive(string fragmentId);

        /// <summary>
        /// Adds a new instance of a chainable fragment.
        /// Returns the new fragment parameters with a unique ID.
        /// Publishes GenerationParametersChangedEventArgs.FragmentAdded event.
        /// </summary>
        (string fragmentId, FragmentParameters parameters) AddFragmentInstance(string fragmentFile, string? baseId = null);

        /// <summary>
        /// Removes a fragment instance.
        /// Publishes GenerationParametersChangedEventArgs.FragmentRemoved event.
        /// </summary>
        bool RemoveFragmentInstance(string fragmentId);

        /// <summary>
        /// Reorders fragment instances (for chainable fragments).
        /// Publishes GenerationParametersChangedEventArgs with FragmentReordered type.
        /// </summary>
        void ReorderFragments(IEnumerable<string> fragmentIds);

        /// <summary>
        /// Sets an asset value (no event published).
        /// Use for batch updates, call NotifyChanged() when done.
        /// </summary>
        void SetAsset(string assetName, string value);

        /// <summary>
        /// Sets an asset value and publishes change event.
        /// </summary>
        void SetAssetAndNotify(string assetName, string value);

        /// <summary>
        /// Gets an asset value.
        /// </summary>
        string? GetAsset(string assetName);

        /// <summary>
        /// Sets a source asset (input image/video) without publishing event.
        /// </summary>
        void SetSource(string sourceId, SourceAsset source);

        /// <summary>
        /// Sets a source asset and publishes change event.
        /// </summary>
        void SetSourceAndNotify(string sourceId, SourceAsset source);

        /// <summary>
        /// Gets a source asset.
        /// </summary>
        SourceAsset? GetSource(string sourceId);

        /// <summary>
        /// Clears a source asset.
        /// Publishes GenerationParametersChangedEventArgs.SourceChanged event.
        /// </summary>
        void ClearSource(string sourceId);

        /// <summary>
        /// Replaces the current parameters with new ones.
        /// Used for loading saved state.
        /// Publishes GenerationParametersChangedEventArgs.ParametersLoaded event.
        /// </summary>
        void LoadParameters(GenerationParameters parameters);

        /// <summary>
        /// Creates a snapshot of current parameters.
        /// Used for saving state.
        /// </summary>
        GenerationParameters CreateSnapshot();

        /// <summary>
        /// Manually publishes a change notification.
        /// Use after batch updates via Set* methods (without notify).
        /// </summary>
        void NotifyChanged();

        /// <summary>
        /// Resolves a data source to a list of string options.
        /// Uses constraint.Source (node class_type) and constraint.InputName (input field) 
        /// to query ComfyUI's object_info API.
        /// Results are cached and refreshed on workflow change.
        /// </summary>
        /// <param name="constraints">The parameter constraints containing source and input_name</param>
        /// <returns>List of available options, or empty list if source cannot be resolved</returns>
        Task<List<string>> ResolveSourceOptionsAsync(ParameterConstraints constraints);

        /// <summary>
        /// Clears the source options cache.
        /// Called automatically on workflow change.
        /// </summary>
        void ClearSourceCache();
    }
}
