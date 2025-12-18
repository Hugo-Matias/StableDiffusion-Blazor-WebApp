using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing generation parameters.
    /// Provides CRUD operations and workflow initialization.
    /// </summary>
    public interface IGenerationParameterService
    {
        /// <summary>
        /// Event raised when parameters change.
        /// </summary>
        event Action? OnParametersChanged;

        /// <summary>
        /// Gets the current generation parameters.
        /// </summary>
        GenerationParameters Current { get; }

        /// <summary>
        /// Initializes parameters from a workflow template.
        /// Sets up fragments with default values from the pipeline.
        /// </summary>
        void InitializeFromWorkflow(Workflow workflow);

        /// <summary>
        /// Sets a value for a fragment parameter.
        /// </summary>
        void SetFragmentValue(string fragmentId, string parameter, object? value);

        /// <summary>
        /// Gets a value from a fragment parameter.
        /// </summary>
        T? GetFragmentValue<T>(string fragmentId, string parameter);

        /// <summary>
        /// Sets whether a fragment is active.
        /// </summary>
        void SetFragmentActive(string fragmentId, bool isActive);

        /// <summary>
        /// Gets whether a fragment is active.
        /// </summary>
        bool IsFragmentActive(string fragmentId);

        /// <summary>
        /// Adds a new instance of a chainable fragment.
        /// Returns the new fragment parameters with a unique ID.
        /// </summary>
        (string fragmentId, FragmentParameters parameters) AddFragmentInstance(string fragmentFile, string? baseId = null);

        /// <summary>
        /// Removes a fragment instance.
        /// </summary>
        bool RemoveFragmentInstance(string fragmentId);

        /// <summary>
        /// Reorders fragment instances (for chainable fragments).
        /// </summary>
        void ReorderFragments(IEnumerable<string> fragmentIds);

        /// <summary>
        /// Sets an asset value.
        /// </summary>
        void SetAsset(string assetName, string value);

        /// <summary>
        /// Gets an asset value.
        /// </summary>
        string? GetAsset(string assetName);

        /// <summary>
        /// Sets a source asset (input image/video).
        /// </summary>
        void SetSource(string sourceId, SourceAsset source);

        /// <summary>
        /// Gets a source asset.
        /// </summary>
        SourceAsset? GetSource(string sourceId);

        /// <summary>
        /// Clears a source asset.
        /// </summary>
        void ClearSource(string sourceId);

        /// <summary>
        /// Replaces the current parameters with new ones.
        /// Used for loading saved state.
        /// </summary>
        void LoadParameters(GenerationParameters parameters);

        /// <summary>
        /// Creates a snapshot of current parameters.
        /// Used for saving state.
        /// </summary>
        GenerationParameters CreateSnapshot();

        /// <summary>
        /// Notifies listeners that parameters have changed.
        /// Call after batch updates.
        /// </summary>
        void NotifyChanged();
    }
}
