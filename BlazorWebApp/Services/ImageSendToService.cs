using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ImageEntity = BlazorWebApp.Data.Entities.Image;

namespace BlazorWebApp.Services;

public class ImageSendToService : IImageSendToService
{
    private readonly IBackendService _backend;
    private readonly IStateService _state;
    private readonly ISessionService _session;
    private readonly IIOService _io;
    private readonly IOrchestratorService _orchestrator;
    private readonly NavigationManager _navManager;
    private readonly ISnackbar _snackbar;

    public ImageSendToService(
        IBackendService backend,
        IStateService state,
        ISessionService session,
        IIOService io,
        IOrchestratorService orchestrator,
        NavigationManager navManager,
        ISnackbar snackbar)
    {
        _backend = backend;
        _state = state;
        _session = session;
        _io = io;
        _orchestrator = orchestrator;
        _navManager = navManager;
        _snackbar = snackbar;
    }

    public bool IsLocal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return !path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            && !path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
    }

    public List<Workflow> GetImageWorkflows()
    {
        if (!_backend.IsBackendAvailable || _state.State.Generation.Workflows == null)
            return new List<Workflow>();

        var currentBase = _state.State.Generation.WorkflowBase;
        return _state.State.Generation.Workflows
            .Where(w => w.Base == currentBase && (w.Mode == ModeType.Img2Img || w.Mode == ModeType.Img2Vid))
            .OrderBy(w => w.Mode)
            .ThenBy(w => w.Title)
            .ToList();
    }

    public List<Workflow> GetParameterWorkflows()
    {
        if (!_backend.IsBackendAvailable || _state.State.Generation.Workflows == null)
            return new List<Workflow>();

        var currentBase = _state.State.Generation.WorkflowBase;
        return _state.State.Generation.Workflows
            .Where(w => w.Base == currentBase && (w.Mode == ModeType.Txt2Img || w.Mode == ModeType.Img2Img))
            .OrderBy(w => w.Mode)
            .ThenBy(w => w.Title)
            .ToList();
    }

    public WorkflowSource? GetMultiSourceDefinition(Workflow workflow)
        => workflow.Sources?.FirstOrDefault(s => s.AllowMultiple);

    public List<SourceSlotInfo> GetSourceSlots(Workflow workflow, WorkflowSource multiSource)
    {
        var slots = new List<SourceSlotInfo>();
        var baseId = multiSource.Id;

        // If this workflow is currently active, read existing slots from state
        if (_state.GenerationParameters.WorkflowId == workflow.Id)
        {
            var existingKeys = _state.GenerationParameters.Sources.Keys
                .Where(k => k == baseId || k.StartsWith($"{baseId}_"))
                .OrderBy(k => k)
                .ToList();

            for (var i = 0; i < existingKeys.Count; i++)
            {
                slots.Add(new SourceSlotInfo(existingKeys[i], $"{workflow.Title} Ref {i + 1}"));
            }
        }

        // Minimum: always show at least the base slot
        if (slots.Count == 0)
        {
            slots.Add(new SourceSlotInfo(baseId, $"{workflow.Title} Ref 1"));
        }

        return slots;
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

    public void SendImageToWorkflow(ImageEntity asset, Workflow workflow)
    {
        if (asset == null) return;

        var base64 = _io.GetBase64FromFile(asset.Path);
        if (string.IsNullOrEmpty(base64))
        {
            _snackbar.Add($"Unable to read image file: {asset.Path}", Severity.Error);
            return;
        }

        var data = $"data:image/png;base64,{base64}";

        switch (workflow.Mode)
        {
            case ModeType.Img2Img:
                _session.Img2ImgInputImage = data;
                break;
            case ModeType.Img2Vid:
                _session.Img2VidInputImage = data;
                break;
        }

        _navManager.NavigateTo($"/generate/{workflow.Id}");
        _snackbar.Add($"Image sent to {workflow.Title}", Severity.Success);
    }

    public void SendImageToSourceSlot(ImageEntity asset, Workflow workflow, string sourceKey)
    {
        if (asset == null) return;

        var base64 = _io.GetBase64FromFile(asset.Path);
        if (string.IsNullOrEmpty(base64))
        {
            _snackbar.Add($"Unable to read image file: {asset.Path}", Severity.Error);
            return;
        }

        var data = $"data:image/png;base64,{base64}";

        _session.PendingSourceImages.Clear();
        _session.PendingSourceImages.Add(new PendingSourceImage(sourceKey, data, asset.Path));
        _session.NotifyPendingSourceImages();

        _navManager.NavigateTo($"/generate/{workflow.Id}");
        _snackbar.Add($"Image sent to {workflow.Title}", Severity.Success);
    }

    public void SendImageToNewSlot(ImageEntity asset, Workflow workflow, WorkflowSource multiSource)
    {
        if (asset == null) return;

        var base64 = _io.GetBase64FromFile(asset.Path);
        if (string.IsNullOrEmpty(base64))
        {
            _snackbar.Add($"Unable to read image file: {asset.Path}", Severity.Error);
            return;
        }

        var data = $"data:image/png;base64,{base64}";

        _session.PendingSourceImages.Clear();
        _session.PendingSourceImages.Add(new PendingSourceImage(
            SourceKey: multiSource.Id,
            Data: data,
            FilePath: asset.Path,
            Label: multiSource.Label,
            IsNewSlot: true));
        _session.NotifyPendingSourceImages();

        _navManager.NavigateTo($"/generate/{workflow.Id}");
        _snackbar.Add($"Image sent to {workflow.Title} (new reference)", Severity.Success);
    }

    public async Task SendParametersToWorkflow(ImageEntity asset, Workflow workflow, IReadOnlyCollection<string> selectedParams)
    {
        if (asset == null || selectedParams.Count == 0)
        {
            _snackbar.Add("Select parameters to send by clicking on them", Severity.Warning);
            return;
        }

        var isImg2Img = workflow.Mode == ModeType.Img2Img;

        // Detect whether the user is already viewing this workflow's Generate page.
        // NavigationManager.Uri is the live browser URL, so this correctly distinguishes
        // "same page" (apply immediately) from "cross-page" (queue + navigate).
        var currentRelative = _navManager.ToBaseRelativePath(_navManager.Uri).TrimEnd('/');
        var targetRelative = $"generate/{workflow.Id}";
        var isAlreadyOnPage = currentRelative.Equals(targetRelative, StringComparison.OrdinalIgnoreCase);

        foreach (var param in selectedParams)
        {
            if (isAlreadyOnPage)
                await _orchestrator.SetGenerationParameter(asset, param, isImg2Img);
            else
                await _orchestrator.QueueGenerationParameter(asset, param, isImg2Img);
        }

        if (!isAlreadyOnPage)
            _navManager.NavigateTo($"/generate/{workflow.Id}");

        _snackbar.Add($"Sent {selectedParams.Count} parameter(s) to {workflow.Title}", Severity.Success);
    }
}
