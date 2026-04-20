using BlazorWebApp.Models;

namespace BlazorWebApp.Services;

/// <summary>
/// Scoped service that holds per-circuit resource filter state for the Generation page.
/// Filter state persists across page navigations but resets when the active workflow changes.
/// </summary>
public interface IResourceFilterStateService
{
    /// <summary>
    /// Base models from the current workflow's CompatibleResourceBaseModels that actually have resources in the cache.
    /// Only these are shown as chip toggles in the toolbar.
    /// </summary>
    IReadOnlyList<string> AvailableBaseModels { get; }

    /// <summary>
    /// The subset of AvailableBaseModels currently enabled by the user.
    /// Resources matching these base models pass filtering.
    /// </summary>
    IReadOnlySet<string> EnabledBaseModels { get; }

    /// <summary>
    /// Whether to include resources that have no Resource record or no BaseModel set.
    /// Default: true.
    /// </summary>
    bool IncludeUntracked { get; }

    /// <summary>
    /// When true, all filtering is bypassed (all resources pass through).
    /// Default: false.
    /// </summary>
    bool AllowAll { get; }

    /// <summary>
    /// The workflow ID the filter state is currently initialized for.
    /// </summary>
    Guid? CurrentWorkflowId { get; }

    /// <summary>
    /// Ensures the filter state is initialized for the given workflow.
    /// If the workflow ID matches the current one, state is preserved (no reset).
    /// If different, state resets to all-enabled with available base models recalculated.
    /// </summary>
    Task EnsureInitializedForWorkflowAsync(Workflow workflow);

    /// <summary>
    /// Toggles a specific base model on/off.
    /// </summary>
    void ToggleBaseModel(string baseModel);

    /// <summary>
    /// Enables only the specified base model, disabling all others (solo mode).
    /// If it is already the only enabled model, re-enables all.
    /// </summary>
    void SoloBaseModel(string baseModel);

    /// <summary>
    /// Sets the IncludeUntracked flag.
    /// </summary>
    void SetIncludeUntracked(bool value);

    /// <summary>
    /// Sets the AllowAll flag.
    /// </summary>
    void SetAllowAll(bool value);

    /// <summary>
    /// Clears the current workflow ID so the next EnsureInitializedForWorkflowAsync call
    /// will fully re-initialize (e.g. after resources are enabled/disabled).
    /// </summary>
    void InvalidateCurrentWorkflow();

    /// <summary>
    /// Returns the effective base models to use for filtering: the enabled subset.
    /// Returns null if AllowAll is true or no filter state is initialized.
    /// </summary>
    IReadOnlyList<string>? GetEffectiveBaseModels();
}
