using BlazorWebApp.Components.ImageEditor;
using BlazorWebApp.Components.Shared.Generation;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using BlazorWebApp.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using static BlazorWebApp.Data.Enums;
using static BlazorWebApp.Models.FragmentKeys;

namespace BlazorWebApp.Pages;

/// <summary>
/// Main generation page for creating images and videos.
/// Refactored in Phase 14 to eliminate shadow state and use service-based architecture.
/// </summary>
public partial class Generate : IDisposable
{
    [Parameter] public string? WorkflowId { get; set; }

    #region State & References

    private List<Workflow>? _workflows;
    private Workflow? _selectedWorkflow;
    private bool _isGenerating = false;

    /// <summary>
    /// Reference to the State.GenerationParameters for convenience.
    /// </summary>
    private GenerationParameters Parameters => State.GenerationParameters;

    // Editor
    private ImageEditorModal? _editorModal;
    private string? _editingSourceId;

    #endregion

    #region Computed Properties

    private bool HasSources => _selectedWorkflow?.Sources?.Count > 0;
    private bool IsVideoMode => _selectedWorkflow?.Mode is ModeType.Img2Vid or ModeType.Txt2Vid or ModeType.Vid2Vid;
    private bool UseGuidance => _selectedWorkflow?.Base == ModelBase.Flux;

    /// <summary>
    /// Gets the current prompt value from the service.
    /// </summary>
    private string GetPromptValue() =>
        ParameterService.GetFragmentProperty(ParameterService.PromptsFragment, Params.Positive, "");

    /// <summary>
    /// Gets the current negative prompt value from the service.
    /// </summary>
    private string GetNegativePromptValue() =>
        ParameterService.GetFragmentProperty(ParameterService.PromptsFragment, Params.Negative, "");

    #endregion

    #region Lifecycle

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to events
        Events.Subscribe<StateChangedEventArgs>(OnStateChanged);
        Events.Subscribe<GenerationParametersChangedEventArgs>(OnParametersChanged);
        Events.Subscribe<PendingSourceImagesChangedEventArgs>(OnPendingSourceImages);
        Events.Subscribe<Img2ImgInputImageChangedEventArgs>(OnInputImageChanged);
        Events.Subscribe<Img2VidInputImageChangedEventArgs>(OnInputVideoChanged);

        // Load all workflows that may appear on the Generate page. The WorkflowStrip
        // (which reads from State.Generation.Workflows) shows tabs for every mode the
        // current base supports, so this list MUST cover the same modes - otherwise
        // navigating to a workflow whose mode is missing here silently no-ops in
        // InitializeFromUrl and the page content stays stuck on the previous workflow.
        _workflows = new List<Workflow>();
        foreach (var mode in new[]
        {
            ModeType.Txt2Img,
            ModeType.Img2Img,
            ModeType.Img2Vid,
            ModeType.Txt2Vid,
            ModeType.Vid2Vid
        })
        {
            var modeWorkflows = Orchestrator.GetWorkflowsForMode(mode);
            if (modeWorkflows != null)
            {
                _workflows.AddRange(modeWorkflows);
            }
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        // Handle URL parameter changes
        await InitializeFromUrl();
    }

    private async Task InitializeFromUrl()
    {
        // Check if we have a workflow ID in the URL
        if (!string.IsNullOrEmpty(WorkflowId) && Guid.TryParse(WorkflowId, out var workflowGuid))
        {
            if (_selectedWorkflow?.Id != workflowGuid)
            {
                var workflow = _workflows?.FirstOrDefault(w => w.Id == workflowGuid);
                if (workflow != null)
                {
                    // Don't update URL - we're already navigating from URL
                    await OnWorkflowSelected(workflow, updateUrl: false);
                    return;
                }
            }
            else if (ParameterService.HasPendingOverrides)
            {
                // Same workflow navigated to again (e.g. cross-page "Send Parameters To").
                // InitializeFromWorkflowAsync is skipped by the guard above, so pending
                // overrides would be silently discarded. Apply them directly.
                ParameterService.FlushPendingOverrides();
            }
        }
        else if (string.IsNullOrEmpty(WorkflowId))
        {
            // Route is /generate (no id). Resolve the best workflow for the current
            // base (last-used preferred, first-available fallback) and redirect so the
            // URL reflects the selected workflow. This is the entry point used by the
            // global Generate nav button.
            var resolvedId = Orchestrator.ResolveWorkflowForBase();
            if (resolvedId.HasValue)
            {
                NavManager.NavigateTo($"/generate/{resolvedId.Value}", forceLoad: false, replace: true);
                return;
            }
        }

        // If no URL parameter, try to restore from state
        if (_selectedWorkflow == null && Parameters.WorkflowId.HasValue)
        {
            var restoredWorkflow = _workflows?.FirstOrDefault(w => w.Id == Parameters.WorkflowId.Value);
            if (restoredWorkflow != null)
            {
                // Update URL to reflect restored workflow
                await OnWorkflowSelected(restoredWorkflow, updateUrl: true);
                return;
            }
        }

        // Fallback: check CurrentWorkflowId set by SetCurrentWorkflow (e.g. from Resources page)
        if (_selectedWorkflow == null)
        {
            var currentId = State.State?.Generation?.CurrentWorkflowId;
            if (currentId.HasValue)
            {
                var fallbackWorkflow = _workflows?.FirstOrDefault(w => w.Id == currentId.Value);
                if (fallbackWorkflow != null)
                {
                    await OnWorkflowSelected(fallbackWorkflow, updateUrl: true);
                    return;
                }
            }
        }

        // If still no workflow and we have some, select the first
        if (_selectedWorkflow == null && _workflows?.Count > 0)
        {
            // Don't auto-select - let user choose from navbar
        }
    }

    #endregion

    #region Workflow Selection

    private async Task OnWorkflowSelected(Workflow workflow, bool updateUrl = true)
    {
        if (workflow == null) return;

        _selectedWorkflow = workflow;

        // Update state
        if (State.State?.Generation != null)
        {
            State.State.Generation.CurrentWorkflowId = workflow.Id;
            State.State.Generation.WorkflowBase = workflow.Base;
            State.State.Generation.LastWorkflowByBase[workflow.Base] = workflow.Id;
        }

        // Initialize parameters from workflow
        await ParameterService.InitializeFromWorkflowAsync(workflow);

        // Load session images into sources if available
        await LoadSessionSourcesAsync(workflow);

        // Push workflow info to the right-side Info drawer (Title + Description +
        // a Sources section listing the slot labels). The drawer auto-shows its
        // toggle when CurrentInfo is non-null.
        PublishWorkflowInfo(workflow);

        // Fragment discovery is now handled by ParameterService.DiscoverFragments()
        // (called automatically during InitializeFromWorkflowAsync)

        // Local state initialization removed - all state is managed by child components or service

        // Update URL without full navigation (only if not already navigating from URL)
        if (updateUrl)
        {
            try
            {
                var newUrl = $"/generate/{workflow.Id}";
                NavManager.NavigateTo(newUrl, forceLoad: false, replace: true);
            }
            catch (Exception ex)
            {
                // Navigation may fail if circuit is disconnecting - log but don't throw
                Logger.LogDebug(ex, "Failed to update URL during workflow selection");
            }
        }

        StateHasChanged();
    }

    /// <summary>
    /// Publishes the active workflow's metadata to the global Info drawer so users
    /// can open the right-side panel for a longer-form description, source-slot guidance,
    /// enhancement documentation, and any future tips. When the workflow has no
    /// <see cref="Workflow.Description"/>, no Sources, and no documented enhancements,
    /// the drawer's info button hides itself (CurrentInfo == null).
    /// </summary>
    private void PublishWorkflowInfo(Workflow workflow)
    {
        var hasDescription = !string.IsNullOrWhiteSpace(workflow.Description);
        var hasSources = workflow.Sources?.Count > 0;

        // Collect optional fragments that carry a description for the info drawer.
        var documentedFragments = ParameterService.OptionalFragments
            .Where(f => !string.IsNullOrWhiteSpace(f.Schema?.Description))
            .ToList();

        if (!hasDescription && !hasSources && documentedFragments.Count == 0)
        {
            InfoService.ClearInfo();
            return;
        }

        var sections = new List<InfoSection>();

        if (hasSources)
        {
            sections.Add(new InfoSection(
                Id: "workflow-sources",
                Title: "Required Inputs",
                Icon: Icons.Material.Filled.Input,
                Items: workflow.Sources!
                    .Select(s => new InfoItem(
                        Label: s.Label + (s.Required ? "" : " (optional)"),
                        Text: $"Type: {s.Type}"))
                    .ToList()));
        }

        if (documentedFragments.Count > 0)
        {
            sections.Add(new InfoSection(
                Id: "workflow-enhancements",
                Title: "Enhancements",
                Icon: Icons.Material.Filled.Tune,
                Items: documentedFragments
                    .Select(f => new InfoItem(
                        Label: f.Schema!.Title,
                        Text: f.Schema.Description!))
                    .ToList()));
        }

        InfoService.SetInfo(new InfoContent(
            Title: workflow.Title,
            Overview: workflow.Description ?? string.Empty,
            Shortcuts: new List<ShortcutInfo>(),
            Tips: new List<string>(),
            Sections: sections.Count > 0 ? sections : null));
    }

    /// <summary>
    /// Returns true if there are any pending session images for the given workflow.
    /// </summary>
    private bool HasPendingSessionImages(Workflow workflow)
    {
        if (Session.PendingSourceImages.Count > 0) return true;
        if (!string.IsNullOrEmpty(Session.Img2ImgInputImage) && workflow.Mode == ModeType.Img2Img) return true;
        if (!string.IsNullOrEmpty(Session.Img2VidInputImage) && workflow.Mode == ModeType.Img2Vid) return true;
        return false;
    }

    /// <summary>
    /// Loads session images into workflow sources if available and clears the session afterwards.
    /// </summary>
    private async Task LoadSessionSourcesAsync(Workflow workflow)
    {
        // Check for Img2Vid input image
        if (!string.IsNullOrEmpty(Session.Img2VidInputImage) && workflow.Mode == ModeType.Img2Vid)
        {
            // Find the first image source
            var imageSource = workflow.Sources?.FirstOrDefault(s => s.Type?.Equals("image", StringComparison.OrdinalIgnoreCase) == true);
            if (imageSource != null && Parameters.Sources.TryGetValue(imageSource.Id, out var source))
            {
                source.Data = Session.Img2VidInputImage;
                Logger.LogDebug("Loaded session image into source '{SourceId}'", imageSource.Id);
            }

            // Clear session after loading
            Session.Img2VidInputImage = null;
        }

        // Check for Img2Img input image
        if (!string.IsNullOrEmpty(Session.Img2ImgInputImage) && workflow.Mode == ModeType.Img2Img)
        {
            // Find the first image source
            var imageSource = workflow.Sources?.FirstOrDefault(s => s.Type?.Equals("image", StringComparison.OrdinalIgnoreCase) == true);
            if (imageSource != null && Parameters.Sources.TryGetValue(imageSource.Id, out var source))
            {
                source.Data = Session.Img2ImgInputImage;
                Logger.LogDebug("Loaded session image into source '{SourceId}'", imageSource.Id);
            }

            // Clear session after loading
            Session.Img2ImgInputImage = null;
        }

        // Check for pending targeted source images (from multi-source Send To)
        if (Session.PendingSourceImages.Count > 0)
        {
            foreach (var pending in Session.PendingSourceImages)
            {
                if (pending.IsNewSlot)
                {
                    // Create a new multi-source slot
                    var baseId = pending.SourceKey;
                    var existingKeys = Parameters.Sources.Keys
                        .Where(k => k == baseId || k.StartsWith($"{baseId}_"))
                        .ToList();

                    var existingIndices = existingKeys
                        .Select(k =>
                        {
                            var suffix = k[baseId.Length..];
                            if (string.IsNullOrEmpty(suffix)) return 0;
                            return suffix.StartsWith("_") && int.TryParse(suffix[1..], out var idx) ? idx : 0;
                        })
                        .ToList();

                    var nextIndex = existingIndices.Count > 0 ? existingIndices.Max() + 1 : 1;
                    var newKey = $"{baseId}_{nextIndex}";

                    Parameters.Sources[newKey] = new SourceAsset
                    {
                        Label = $"{pending.Label} {nextIndex + 1}",
                        Type = "image",
                        Data = pending.Data,
                        FilePath = pending.FilePath
                    };
                    Logger.LogDebug("Created new source slot '{SourceId}' from pending", newKey);
                }
                else
                {
                    // Load into existing slot
                    if (Parameters.Sources.TryGetValue(pending.SourceKey, out var existingSource))
                    {
                        existingSource.Data = pending.Data;
                        existingSource.FilePath = pending.FilePath;
                    }
                    else
                    {
                        Parameters.Sources[pending.SourceKey] = new SourceAsset
                        {
                            Label = pending.Label ?? pending.SourceKey,
                            Type = "image",
                            Data = pending.Data,
                            FilePath = pending.FilePath
                        };
                    }
                    Logger.LogDebug("Loaded pending image into source '{SourceId}'", pending.SourceKey);
                }
            }
            Session.PendingSourceImages.Clear();
        }

        await Task.CompletedTask;
    }

    #endregion

    #region Prompt Handlers

    private async Task HandlePromptChanged(string value)
    {
        // Write TO service (with notification for UI updates)
        ParameterService.SetFragmentProperty(ParameterService.PromptsFragment, Params.Positive, value, notify: true);
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleNegativePromptChanged(string value)
    {
        // Write TO service (with notification for UI updates)
        ParameterService.SetFragmentProperty(ParameterService.PromptsFragment, Params.Negative, value, notify: true);
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandlePromptTagAppended(AppendedTags tags)
    {
        var currentPrompt = GetPromptValue();

        if (tags.IsPrefix)
            currentPrompt = tags.Tags + (string.IsNullOrEmpty(currentPrompt) ? "" : ", " + currentPrompt);
        else
            currentPrompt += (string.IsNullOrEmpty(currentPrompt) ? "" : ", ") + tags.Tags;

        // Write TO service (with notification for UI updates)
        ParameterService.SetFragmentProperty(ParameterService.PromptsFragment, Params.Positive, currentPrompt, notify: true);
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleNegativePromptTagAppended(AppendedTags tags)
    {
        var currentNegativePrompt = GetNegativePromptValue();

        if (tags.IsPrefix)
            currentNegativePrompt = tags.Tags + (string.IsNullOrEmpty(currentNegativePrompt) ? "" : ", " + currentNegativePrompt);
        else
            currentNegativePrompt += (string.IsNullOrEmpty(currentNegativePrompt) ? "" : ", ") + tags.Tags;

        // Write TO service (with notification for UI updates)
        ParameterService.SetFragmentProperty(ParameterService.PromptsFragment, Params.Negative, currentNegativePrompt, notify: true);
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region LoRA Handlers

    private async Task HandleLoraAdded(Lora lora)
    {
        if (!Parameters.Loras.Any(l => l.Name.Equals(lora.Name, StringComparison.OrdinalIgnoreCase)))
        {
            Parameters.Loras.Add(lora);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleLorasUpdated()
    {
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Style Handlers

    private async Task HandleStylesChanged()
    {
        // Styles are managed by PromptsForm via State.State.Generation.Styles
        // Apply style LoRAs to our LoRA list
        foreach (var style in State.State.Generation.Styles ?? Enumerable.Empty<PromptStyle>())
        {
            foreach (var lora in style.Loras ?? Enumerable.Empty<Lora>())
            {
                if (!Parameters.Loras.Any(l => l.Name == lora.Name))
                {
                    Parameters.Loras.Add(lora);
                }
            }
        }
        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Fragment Value Handlers

    private async Task HandleFragmentValueChanged()
    {
        // Generic handler for when any fragment value changes
        await State.SaveState();
    }

    #endregion

    #region Fragment Active Handler

    private async Task HandleFragmentActiveChanged(string fragmentId, bool active)
    {
        // Notify the parameter service so the change is tracked (this also sets fragment.IsActive)
        ParameterService.SetFragmentActive(fragmentId, active);

        Logger.LogDebug("Fragment '{FragmentId}' IsActive changed to {IsActive}", fragmentId, active);

        await InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Source Handlers

    private async Task HandleSourceChanged((string sourceId, SourceAsset source) args)
    {
        if (Parameters.Sources.ContainsKey(args.sourceId))
        {
            Parameters.Sources[args.sourceId] = args.source;
        }
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleSourceAdded((string sourceId, SourceAsset source) args)
    {
        Parameters.Sources[args.sourceId] = args.source;
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleSourceRemoved(string sourceId)
    {
        Parameters.Sources.Remove(sourceId);
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleEditClicked(string sourceId)
    {
        _editingSourceId = sourceId;
        if (_editorModal != null && Parameters.Sources.TryGetValue(sourceId, out var source))
        {
            await _editorModal.Open(source.Data);
        }
    }

    private async Task HandleBlankCanvasClicked(string sourceId)
    {
        _editingSourceId = sourceId;
        if (_editorModal != null)
        {
            // Get current width/height from latent fragment
            var width = ParameterService.GetFragmentProperty(ParameterService.PrimaryLatentFragment, Params.Width, 1024);
            var height = ParameterService.GetFragmentProperty(ParameterService.PrimaryLatentFragment, Params.Height, 1024);
            await _editorModal.OpenBlank(width, height);
        }
    }

    private async Task HandleEditorApply((string image, string? mask) result)
    {
        if (!string.IsNullOrEmpty(_editingSourceId) && Parameters.Sources.TryGetValue(_editingSourceId, out var source))
        {
            source.Data = result.image;
            await InvokeAsync(StateHasChanged);
        }
        _editingSourceId = null;
    }

    private void HandleEditorCancel()
    {
        _editingSourceId = null;
    }

    #endregion

    #region Generation

    private bool CanGenerate()
    {
        if (_selectedWorkflow == null) return false;

        // Check if required sources are filled
        if (_selectedWorkflow.Sources != null)
        {
            foreach (var sourceDefinition in _selectedWorkflow.Sources)
            {
                if (sourceDefinition.Required &&
                    (!Parameters.Sources.TryGetValue(sourceDefinition.Id, out var source) ||
                     !source.HasData))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private async Task GenerateAsync()
    {
        if (_selectedWorkflow == null || !CanGenerate()) return;

        try
        {
            _isGenerating = true;
            StateHasChanged();

            // Save workflow state before generation (persists to WorkflowStates table)
            await ParameterService.SaveCurrentWorkflowStateAsync();

            // Use the new GenerationParameters-based methods
            switch (_selectedWorkflow.Mode)
            {
                case ModeType.Img2Vid:
                case ModeType.Txt2Vid:
                case ModeType.Vid2Vid:
                    var videos = await ImageService.GenerateVideoAsync(Parameters, _selectedWorkflow);
                    break;

                default: // Txt2Img and Img2Img
                    var images = await ImageService.GenerateImagesAsync(Parameters, _selectedWorkflow);
                    // GeneratedImageEntities is owned and accumulated by ImageService
                    // across the queue; no assignment needed here.
                    break;
            }

            // Save workflow state after generation (captures any seed updates, etc.)
            await ParameterService.SaveCurrentWorkflowStateAsync();

            // Also save global app state
            await State.SaveState();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during generation");
        }
        finally
        {
            _isGenerating = false;
            StateHasChanged();
        }
    }

    private async Task ClearQueueAsync()
    {
        try
        {
            await API.PostClearQueue();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error clearing generation queue");
        }
    }

    private async Task InterruptAsync()
    {
        try
        {
            State.State.Generation.IsInterrupted = true;
            await API.PostInterrupt();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error interrupting generation");
        }
    }

    #endregion

    #region Event Handlers

    private void OnStateChanged(StateChangedEventArgs args)
    {
        // If workflow base changed externally (e.g. from navbar), we might need to reload
        if (args.ChangeType == StateChangeType.WorkflowBase)
        {
            var currentWorkflowId = State.State?.Generation?.CurrentWorkflowId;
            if (currentWorkflowId.HasValue && _selectedWorkflow?.Id != currentWorkflowId.Value)
            {
                // Workflow changed due to base change - reload
                var workflow = _workflows?.FirstOrDefault(w => w.Id == currentWorkflowId.Value);
                if (workflow != null)
                {
                    _ = OnWorkflowSelected(workflow);
                    return;
                }
            }
        }

        _ = InvokeAsync(StateHasChanged);
    }

    private void OnParametersChanged(GenerationParametersChangedEventArgs args)
    {
        // Fragment discovery is handled by ParameterService
        // Child components auto-sync in their OnParametersSet()
        // No manual state initialization needed!

        _ = InvokeAsync(StateHasChanged);
    }

    #endregion

    #region Dispose

    private void OnPendingSourceImages(PendingSourceImagesChangedEventArgs args)
    {
        if (_selectedWorkflow == null) return;
        if (!HasPendingSessionImages(_selectedWorkflow)) return;

        _ = InvokeAsync(async () =>
        {
            await LoadSessionSourcesAsync(_selectedWorkflow);
            StateHasChanged();
        });
    }

    private void OnInputImageChanged(Img2ImgInputImageChangedEventArgs args)
    {
        if (_selectedWorkflow == null) return;
        if (!HasPendingSessionImages(_selectedWorkflow)) return;

        _ = InvokeAsync(async () =>
        {
            await LoadSessionSourcesAsync(_selectedWorkflow);
            StateHasChanged();
        });
    }

    private void OnInputVideoChanged(Img2VidInputImageChangedEventArgs args)
    {
        if (_selectedWorkflow == null) return;
        if (!HasPendingSessionImages(_selectedWorkflow)) return;

        _ = InvokeAsync(async () =>
        {
            await LoadSessionSourcesAsync(_selectedWorkflow);
            StateHasChanged();
        });
    }

    public void Dispose()
    {
        Events.Unsubscribe<StateChangedEventArgs>(OnStateChanged);
        Events.Unsubscribe<GenerationParametersChangedEventArgs>(OnParametersChanged);
        Events.Unsubscribe<PendingSourceImagesChangedEventArgs>(OnPendingSourceImages);
        Events.Unsubscribe<Img2ImgInputImageChangedEventArgs>(OnInputImageChanged);
        Events.Unsubscribe<Img2VidInputImageChangedEventArgs>(OnInputVideoChanged);

        // Clear any workflow info we pushed to the global Info drawer.
        InfoService.ClearInfo();

        // Save workflow state and global state on dispose
        _ = Task.Run(async () =>
        {
            try
            {
                // Save per-workflow state
                await ParameterService.SaveCurrentWorkflowStateAsync();

                // Save global app state
                await State.SaveState();
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to save state on dispose");
            }
        });
    }

    #endregion
}
