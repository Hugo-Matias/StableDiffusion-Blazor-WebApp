using BlazorWebApp.Events;
using BlazorWebApp.Models;

namespace BlazorWebApp.Services;

/// <summary>
/// Scoped service holding per-circuit resource filter state.
/// Resets when the active workflow changes; persists across page navigations within the same circuit.
/// </summary>
public class ResourceFilterStateService : IResourceFilterStateService
{
    private readonly IEventService _events;

    private List<string> _availableBaseModels = [];
    private HashSet<string> _enabledBaseModels = new(StringComparer.OrdinalIgnoreCase);
    private bool _includeUntracked = true;
    private bool _allowAll = false;
    private Guid? _currentWorkflowId;

    public ResourceFilterStateService(IResourceCacheService cache, IEventService events)
    {
        _events = events;

        // Self-subscribe to resource changes so the filter state is invalidated
        // even when the toolbar component isn't mounted (e.g. user on Resources page)
        _events.Subscribe<ResourcesChangedEventArgs>(OnResourcesChanged);
    }

    public IReadOnlyList<string> AvailableBaseModels => _availableBaseModels;
    public IReadOnlySet<string> EnabledBaseModels => _enabledBaseModels;
    public bool IncludeUntracked => _includeUntracked;
    public bool AllowAll => _allowAll;
    public Guid? CurrentWorkflowId => _currentWorkflowId;

    public async Task EnsureInitializedForWorkflowAsync(Workflow workflow)
    {
        if (workflow.Id == _currentWorkflowId)
            return;

        _currentWorkflowId = workflow.Id;

        if (workflow.CompatibleResourceBaseModels == null || workflow.CompatibleResourceBaseModels.Count == 0)
        {
            _availableBaseModels = [];
            _enabledBaseModels = new(StringComparer.OrdinalIgnoreCase);
            _allowAll = true;
            _includeUntracked = true;
            return;
        }

        _availableBaseModels = workflow.CompatibleResourceBaseModels
            .Where(model => !string.IsNullOrWhiteSpace(model))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        _enabledBaseModels = new HashSet<string>(_availableBaseModels, StringComparer.OrdinalIgnoreCase);
        _allowAll = false;
        _includeUntracked = true;
    }

    public void ToggleBaseModel(string baseModel)
    {
        if (_enabledBaseModels.Contains(baseModel))
            _enabledBaseModels.Remove(baseModel);
        else
            _enabledBaseModels.Add(baseModel);

        _events.Publish(new ResourceFilterChangedEventArgs());
    }

    public void SoloBaseModel(string baseModel)
    {
        if (_enabledBaseModels.Count == 1 && _enabledBaseModels.Contains(baseModel))
        {
            // Already solo on this model -> re-enable all
            _enabledBaseModels = new HashSet<string>(_availableBaseModels, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            _enabledBaseModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { baseModel };
        }

        _events.Publish(new ResourceFilterChangedEventArgs());
    }

    public void SetIncludeUntracked(bool value)
    {
        if (_includeUntracked == value) return;
        _includeUntracked = value;
        _events.Publish(new ResourceFilterChangedEventArgs());
    }

    public void SetAllowAll(bool value)
    {
        if (_allowAll == value) return;
        _allowAll = value;
        _events.Publish(new ResourceFilterChangedEventArgs());
    }

    public void InvalidateCurrentWorkflow()
    {
        _currentWorkflowId = null;
    }

    private void OnResourcesChanged(ResourcesChangedEventArgs args)
    {
        InvalidateCurrentWorkflow();
    }

    public IReadOnlyList<string>? GetEffectiveBaseModels()
    {
        if (_allowAll || _currentWorkflowId == null)
            return null;

        return _enabledBaseModels.ToList();
    }
}
