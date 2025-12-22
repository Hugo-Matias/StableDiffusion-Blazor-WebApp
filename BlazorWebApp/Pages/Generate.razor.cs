using BlazorWebApp.Components.ImageEditor;
using BlazorWebApp.Components.Shared.Generation;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Events;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.Components;
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
    private bool IsVideoMode => _selectedWorkflow?.Mode == ModeType.Img2Vid;
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

        // Load all workflows
        _workflows = new List<Workflow>();
        foreach (var mode in new[] { ModeType.Txt2Img, ModeType.Img2Img, ModeType.Img2Vid })
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
            // Only reinitialize if workflow changed
            if (_selectedWorkflow?.Id != workflowGuid)
            {
                var workflow = _workflows?.FirstOrDefault(w => w.Id == workflowGuid);
                if (workflow != null)
                {
                    await OnWorkflowSelected(workflow);
                    return;
                }
            }
        }

        // If no URL parameter, try to restore from state
        if (_selectedWorkflow == null && Parameters.WorkflowId.HasValue)
        {
            var restoredWorkflow = _workflows?.FirstOrDefault(w => w.Id == Parameters.WorkflowId.Value);
            if (restoredWorkflow != null)
            {
                await OnWorkflowSelected(restoredWorkflow);
                return;
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

    private async Task OnWorkflowSelected(Workflow workflow)
    {
        if (workflow == null) return;

        _selectedWorkflow = workflow;

        // Update state
        if (State.State?.Generation != null)
        {
            State.State.Generation.CurrentWorkflowId = workflow.Id;
            State.State.Generation.WorkflowBase = workflow.Base;
        }

        // Initialize parameters from workflow
        await ParameterService.InitializeFromWorkflowAsync(workflow);

        // Fragment discovery is now handled by ParameterService.DiscoverFragments()
        // (called automatically during InitializeFromWorkflowAsync)

        // Local state initialization removed - all state is managed by child components or service

        // Update URL without full navigation
        var newUrl = $"/generate/{workflow.Id}";
        NavManager.NavigateTo(newUrl, forceLoad: false, replace: true);

        StateHasChanged();
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

    #region Optional Fragments

    /// <summary>
    /// Gets the optional fragments that should be rendered with CollapsibleFeatureSection.
    /// Uses the service's OptionalFragments discovery instead of manual iteration.
    /// </summary>
    private IEnumerable<(string Id, string Title, string? Icon, FragmentSchema? Schema, FragmentParameters Fragment)> GetOptionalFragments()
    {
        // Use the service's discovered optional fragments
        foreach (var fragmentRef in ParameterService.OptionalFragments)
        {
            yield return (
                fragmentRef.Id,
                fragmentRef.Title,
                fragmentRef.Icon,
                fragmentRef.Schema,
                fragmentRef.Parameters
            );
        }
    }

    private async Task HandleFragmentActiveChanged(string fragmentId, bool active)
    {
        // Notify the parameter service so the change is tracked (this also sets fragment.IsActive)
        ParameterService.SetFragmentActive(fragmentId, active);

        Logger.LogDebug("Fragment '{FragmentId}' IsActive changed to {IsActive}", fragmentId, active);

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Dynamically renders a fragment form using the schema's Component property.
    /// Falls back to a placeholder if no component is registered.
    /// </summary>
    private RenderFragment RenderOptionalFragmentForm(string fragmentId, FragmentParameters fragment, FragmentSchema? schema)
    {
        // Get component name from schema, or derive from fragment file
        var componentName = schema?.Component;

        if (string.IsNullOrEmpty(componentName))
        {
            // Try to derive component name from fragment file: "upscale.sbn" -> "UpscaleForm"
            var baseName = Path.GetFileNameWithoutExtension(fragment.FragmentFile);
            componentName = ToPascalCase(baseName) + "Form";
        }

        // Look up component type in registry
        var componentType = ComponentRegistry.GetComponent(componentName);

        if (componentType != null)
        {
            return builder =>
            {
                builder.OpenComponent(0, componentType);
                builder.AddAttribute(1, "FragmentId", fragmentId);
                builder.CloseComponent();
            };
        }

        // Check if schema has dynamic fields defined
        if (schema?.UsesDynamicFields == true)
        {
            // TODO: Implement dynamic field rendering based on schema.Fields
            return builder => builder.AddContent(0,
                $"Dynamic fields for '{schema.Title}' - field rendering not yet implemented");
        }

        // No component found and no dynamic fields
        Logger.LogWarning("No component found for fragment '{FragmentId}' (tried '{ComponentName}')",
            fragmentId, componentName);

        return builder => builder.AddContent(0,
            $"No form component registered for '{componentName}'");
    }

    /// <summary>
    /// Converts a kebab-case or snake_case string to PascalCase.
    /// </summary>
    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var parts = input.Split(new[] { '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1).ToLowerInvariant() : "")));
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
                    var videos = await ImageService.GenerateVideoAsync(Parameters, _selectedWorkflow);
                    break;

                default: // Txt2Img and Img2Img
                    var images = await ImageService.GenerateImagesAsync(Parameters, _selectedWorkflow);
                    if (images?.Images?.Count > 0)
                    {
                        ImageService.GeneratedImageEntities = images;
                    }
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

    private async Task SkipAsync()
    {
        try
        {
            await API.PostClearQueue();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error skipping generation");
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

    public void Dispose()
    {
        Events.Unsubscribe<StateChangedEventArgs>(OnStateChanged);
        Events.Unsubscribe<GenerationParametersChangedEventArgs>(OnParametersChanged);

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
