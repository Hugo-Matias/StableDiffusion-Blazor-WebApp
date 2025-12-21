using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Provides CRUD operations and workflow initialization.
    /// Subscribe to GenerationParametersChangedEventArgs via IEventService for change notifications.
    /// </summary>
    /// <remarks>
    /// <para><strong>Default Value Priority Order:</strong></para>
    /// <para>When initializing fragments from a workflow, values are resolved in this priority order:</para>
    /// <list type="number">
    ///   <item>
    ///     <term>Saved State (Database)</term>
    ///     <description>Previously saved parameters for this specific workflow</description>
    ///   </item>
    ///   <item>
    ///     <term>Step Parameters</term>
    ///     <description>Values from workflow template's Pipeline step parameters (e.g., {{ Param ?? "default" | json }})</description>
    ///   </item>
    ///   <item>
    ///     <term>Schema Defaults</term>
    ///     <description>Values from fragment #meta.ui.parameters.*.default</description>
    ///   </item>
    ///   <item>
    ///     <term>Dynamic Sources</term>
    ///     <description>For fields with dynamic sources (ComfyUI node queries), the first available option 
    ///     is set as default during InitializeFromWorkflowAsync. Pre-resolved options are accessible via 
    ///     GetResolvedOptions for synchronous UI access.</description>
    ///   </item>
    /// </list>
    /// </remarks>
    public interface IGenerationParameterService
    {
        /// <summary>
        /// Gets the current generation parameters.
        /// </summary>
        GenerationParameters Current { get; }

        /// <summary>
        /// Initializes parameters from a workflow template.
        /// Sets up fragments with default values from the pipeline using the priority order
        /// documented in the interface remarks.
        /// NOTE: This synchronous version does NOT load saved state from database.
        /// Use InitializeFromWorkflowAsync for full functionality.
        /// Publishes GenerationParametersChangedEventArgs.WorkflowChanged event.
        /// </summary>
        void InitializeFromWorkflow(Workflow workflow);

        /// <summary>
        /// Initializes parameters from a workflow template asynchronously.
        /// - Saves current workflow state before switching (if different workflow)
        /// - Loads saved state from database if available
        /// - Falls back to template defaults if no saved state
        /// Returns the initialized GenerationParameters.
        /// Publishes GenerationParametersChangedEventArgs.WorkflowChanged event.
        /// </summary>
        Task<GenerationParameters> InitializeFromWorkflowAsync(Workflow workflow);

        /// <summary>
        /// Saves the current workflow's parameters to the database.
        /// Call this before switching workflows, on generation complete, or when explicitly saving.
        /// </summary>
        Task SaveCurrentWorkflowStateAsync();

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

        /// <summary>
        /// Gets pre-resolved options for a fragment parameter.
        /// Options are resolved during InitializeFromWorkflowAsync for all parameters 
        /// with dynamic sources (constraint.Source + constraint.InputName).
        /// This allows UI components to access options synchronously.
        /// </summary>
        /// <param name="fragmentId">The fragment ID</param>
        /// <param name="parameterName">The parameter name</param>
        /// <returns>List of available options, or empty list if not pre-resolved</returns>
        List<string> GetResolvedOptions(string fragmentId, string parameterName);
    }
}
