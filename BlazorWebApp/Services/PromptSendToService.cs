using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorWebApp.Services;

public class PromptSendToService : IPromptSendToService
{
    private readonly IBackendService _backend;
    private readonly IStateService _state;
    private readonly IGenerationParameterService _parameters;
    private readonly NavigationManager _navManager;
    private readonly ISnackbar _snackbar;

    public PromptSendToService(
        IBackendService backend,
        IStateService state,
        IGenerationParameterService parameters,
        NavigationManager navManager,
        ISnackbar snackbar)
    {
        _backend = backend;
        _state = state;
        _parameters = parameters;
        _navManager = navManager;
        _snackbar = snackbar;
    }

    /// <summary>
    /// Workflow IDs explicitly excluded from prompt-targeted send-to lists. Empty by default;
    /// every workflow that exposes a positive-prompt fragment is a valid target. Add IDs here
    /// for special-case workflows that have no prompt input (e.g. pure post-processing /
    /// upscale-only graphs) so they don't appear as send-to options.
    /// </summary>
    private static readonly HashSet<Guid> PromptBlacklist = new();

    public List<Workflow> GetParameterWorkflows()
    {
        if (!_backend.IsBackendAvailable || _state.State.Generation.Workflows == null)
            return new List<Workflow>();

        var currentBase = _state.State.Generation.WorkflowBase;
        // Every supported workflow type (txt2img, img2img, img2vid, etc.) carries a positive
        // prompt as a core fragment, so we no longer filter by Mode. Blacklist remains as the
        // single opt-out mechanism for workflows that genuinely have no prompt input.
        return _state.State.Generation.Workflows
            .Where(w => w.Base == currentBase && !PromptBlacklist.Contains(w.Id))
            .OrderBy(w => w.Mode)
            .ThenBy(w => w.Title)
            .ToList();
    }

    public string GetWorkflowIcon(ModeType mode) => mode switch
    {
        ModeType.Txt2Img => "fa-solid fa-font",
        ModeType.Img2Img => "fa-solid fa-image",
        ModeType.Img2Vid => "fa-solid fa-video",
        ModeType.Extras => "fa-solid fa-panorama",
        _ => "fa-solid fa-circle"
    };

    public string GetWorkflowModeClass(ModeType mode) => mode switch
    {
        ModeType.Txt2Img => "mode-txt2img",
        ModeType.Img2Img => "mode-img2img",
        ModeType.Img2Vid => "mode-img2vid",
        ModeType.Extras => "mode-extras",
        _ => ""
    };

    public void SendPromptToWorkflow(string prompt, Workflow workflow)
    {
        if (workflow == null) return;
        prompt ??= string.Empty;

        // Always queue an override; if the target workflow is already loaded, also
        // notify subscribers immediately so the prompt UI refreshes without a reload.
        _parameters.QueuePendingOverride(FragmentKeys.Fragments.Prompts, FragmentKeys.Params.Positive, prompt);

        var isActive = _state.GenerationParameters.WorkflowId == workflow.Id;
        if (isActive)
        {
            _parameters.SetFragmentValueAndNotify(FragmentKeys.Fragments.Prompts, FragmentKeys.Params.Positive, prompt);
        }

        _navManager.NavigateTo($"/generate/{workflow.Id}");
        _snackbar.Add($"Prompt sent to {workflow.Title}", Severity.Success);
    }
}
