using BlazorWebApp.Data.Entities;
using BlazorWebApp.Data.Repositories;
using BlazorWebApp.Models;
using BlazorWebApp.Models.CharacterCreator;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using ImageEntity = BlazorWebApp.Data.Entities.Image;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services;

public class MediaSendToService : IMediaSendToService
{
    private const string CharacterSourceKey = "character-source-image";

    private static readonly Workflow CharacterWorkflowTarget = new()
    {
        Id = Guid.Parse("5bcf352d-6e4b-4d9f-979c-51e07b5a4da7"),
        Title = "Characters",
        Mode = ModeType.Img2Img
    };

    private readonly IBackendService _backend;
    private readonly IStateService _state;
    private readonly ICharacterRepository _characters;
    private readonly ISessionService _session;
    private readonly IIOService _io;
    private readonly IOrchestratorService _orchestrator;
    private readonly NavigationManager _navManager;
    private readonly ISnackbar _snackbar;

    public MediaSendToService(
        IBackendService backend,
        IStateService state,
        ICharacterRepository characters,
        ISessionService session,
        IIOService io,
        IOrchestratorService orchestrator,
        NavigationManager navManager,
        ISnackbar snackbar)
    {
        _backend = backend;
        _state = state;
        _characters = characters;
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

    public IReadOnlyList<MediaSendToTarget> GetSourceTargets(SendToMediaType mediaType)
    {
        var targets = new List<MediaSendToTarget>();

        if (mediaType == SendToMediaType.Image)
        {
            targets.Add(new MediaSendToTarget(
                CharacterWorkflowTarget,
                CharacterSourceKey,
                "Source Image",
                "image",
                IsNewSlot: false));
        }

        if (!_backend.IsBackendAvailable || _state.State.Generation.Workflows == null)
            return targets;

        var sourceType = ToSourceType(mediaType);
        var currentBase = _state.State.Generation.WorkflowBase;

        targets.AddRange(_state.State.Generation.Workflows
            .Where(w => IsEnabledForCurrentBase(w, currentBase))
            .SelectMany(w => GetTargetsForWorkflow(w, sourceType))
            .OrderBy(t => t.Workflow.Mode)
            .ThenBy(t => t.Workflow.Title)
            .ThenBy(t => t.SourceLabel));

        return targets;
    }

    public List<Workflow> GetParameterWorkflows()
    {
        if (!_backend.IsBackendAvailable || _state.State.Generation.Workflows == null)
            return new List<Workflow>();

        var currentBase = _state.State.Generation.WorkflowBase;
        return _state.State.Generation.Workflows
            .Where(w => IsEnabledForCurrentBase(w, currentBase) && (w.Mode == ModeType.Txt2Img || w.Mode == ModeType.Img2Img))
            .OrderBy(w => w.Mode)
            .ThenBy(w => w.Title)
            .ToList();
    }

    private bool IsEnabledForCurrentBase(Workflow workflow, ModelBase currentBase)
    {
        return workflow.Base == currentBase
            && _state.State.Generation.DisabledWorkflowIds?.Contains(workflow.Id) != true;
    }

    private IEnumerable<MediaSendToTarget> GetTargetsForWorkflow(Workflow workflow, string sourceType)
    {
        var matchingSources = workflow.Sources?
            .Where(s => string.Equals(s.Type, sourceType, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingSources is not { Count: > 0 })
            yield break;

        foreach (var source in matchingSources)
        {
            if (!source.AllowMultiple)
            {
                yield return new MediaSendToTarget(workflow, source.Id, source.Label, source.Type, IsNewSlot: false);
                continue;
            }

            foreach (var existingSlot in GetExistingMultiSourceSlots(workflow, source))
            {
                yield return existingSlot;
            }

            yield return new MediaSendToTarget(workflow, source.Id, source.Label, source.Type, IsNewSlot: true);
        }
    }

    private IEnumerable<MediaSendToTarget> GetExistingMultiSourceSlots(Workflow workflow, WorkflowSource source)
    {
        if (_state.GenerationParameters.WorkflowId != workflow.Id)
        {
            yield return new MediaSendToTarget(workflow, source.Id, source.Label, source.Type, IsNewSlot: false);
            yield break;
        }

        var existingKeys = _state.GenerationParameters.Sources.Keys
            .Where(k => k == source.Id || k.StartsWith($"{source.Id}_", StringComparison.Ordinal))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        if (existingKeys.Count == 0)
        {
            yield return new MediaSendToTarget(workflow, source.Id, source.Label, source.Type, IsNewSlot: false);
            yield break;
        }

        for (var index = 0; index < existingKeys.Count; index++)
        {
            var key = existingKeys[index];
            var label = _state.GenerationParameters.Sources.TryGetValue(key, out var sourceAsset)
                && !string.IsNullOrWhiteSpace(sourceAsset.Label)
                    ? sourceAsset.Label
                    : $"{source.Label} {index + 1}";

            yield return new MediaSendToTarget(workflow, key, label, source.Type, IsNewSlot: false);
        }
    }

    public string GetWorkflowIcon(ModeType mode) => mode switch
    {
        ModeType.Txt2Img => "fa-solid fa-font",
        ModeType.Img2Img => "fa-solid fa-image",
        ModeType.Img2Vid => "fa-solid fa-video",
        ModeType.Txt2Vid => "fa-solid fa-film",
        ModeType.Vid2Vid => "fa-solid fa-clapperboard",
        ModeType.Extras => "fa-solid fa-panorama",
        _ => "fa-solid fa-circle"
    };

    public string GetWorkflowModeClass(ModeType mode) => mode switch
    {
        ModeType.Txt2Img => "mode-txt2img",
        ModeType.Img2Img => "mode-img2img",
        ModeType.Img2Vid => "mode-img2vid",
        ModeType.Txt2Vid => "mode-img2vid",
        ModeType.Vid2Vid => "mode-img2vid",
        ModeType.Extras => "mode-extras",
        _ => ""
    };

    public string GetMediaIcon(SendToMediaType mediaType) => mediaType switch
    {
        SendToMediaType.Video => "fa-solid fa-video",
        _ => "fa-solid fa-image"
    };

    public async Task SendSourceToTargetAsync(ImageEntity asset, MediaSendToTarget target)
    {
        if (asset == null || target == null)
            return;

        if (!IsLocal(asset.Path))
        {
            _snackbar.Add("Save this media locally before sending it to a workflow source.", Severity.Info);
            return;
        }

        var resolvedPath = _io.ResolveFilePath(asset.Path);
        if (string.IsNullOrWhiteSpace(resolvedPath) || !File.Exists(resolvedPath))
        {
            _snackbar.Add($"Unable to find media file: {asset.Path}", Severity.Error);
            return;
        }

        var mediaType = ParseMediaType(target.SourceType);
        string? data = null;

        if (mediaType == SendToMediaType.Image)
        {
            var base64 = _io.GetBase64FromFile(resolvedPath);
            if (string.IsNullOrEmpty(base64))
            {
                _snackbar.Add($"Unable to read image file: {asset.Path}", Severity.Error);
                return;
            }

            data = $"data:image/png;base64,{base64}";
        }

        if (target.SourceKey == CharacterSourceKey)
        {
            await SendCharacterSourceAsync(asset, resolvedPath, data);
            _navManager.NavigateTo("/characters");
            return;
        }

        _session.PendingSourceMedia.Clear();
        _session.PendingSourceMedia.Add(new PendingSourceMedia(
            SourceKey: target.SourceKey,
            SourceType: target.SourceType,
            Data: data,
            FilePath: resolvedPath,
            Label: target.SourceLabel,
            IsNewSlot: target.IsNewSlot));
        _session.NotifyPendingSourceMedia();

        _navManager.NavigateTo($"/generate/{target.Workflow.Id}");
        _snackbar.Add($"Sent {mediaType.ToString().ToLowerInvariant()} to {target.DisplayLabel}", Severity.Success);
    }

    private async Task SendCharacterSourceAsync(ImageEntity asset, string resolvedPath, string? data)
    {
        var label = Path.GetFileName(asset.Path);
        var pending = new CharacterPendingSourceImage
        {
            ImageId = asset.Id,
            ImagePath = resolvedPath,
            ImageDataUri = data,
            SourceLabel = label,
            OriginalFilename = label
        };

        if (_state.State.Character.SelectedCharacterId is not { } characterId)
        {
            _state.State.Character.PendingSourceImage = pending;
            await _state.SaveState();
            _snackbar.Add("Choose or create a character to attach this source image.", Severity.Info);
            return;
        }

        var sourceImage = CharacterReferenceSourceImage.FromImageId(asset.Id, resolvedPath, label, label);
        var sheet = await _characters.AddOrGetReferenceSheetAsync(characterId, sourceImage, label);
        sheet.ApplyToAppState(_state.State.Character);
        _state.State.Character.PendingSourceImage = null;
        _state.State.Character.SourceImage.ImagePath = resolvedPath;
        _state.State.Character.SourceImage.ImageDataUri = null;
        _state.State.Character.SourceImage.SourceLabel = label;
        await _state.SaveState();
        _snackbar.Add("Sent image to the selected character reference sheet.", Severity.Success);
    }

    public async Task SendParametersToWorkflow(ImageEntity asset, Workflow workflow, IReadOnlyCollection<string> selectedParams)
    {
        if (asset == null || selectedParams.Count == 0)
        {
            _snackbar.Add("Select parameters to send by clicking on them", Severity.Warning);
            return;
        }

        var isImg2Img = workflow.Mode == ModeType.Img2Img;
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

    private static string ToSourceType(SendToMediaType mediaType) => mediaType switch
    {
        SendToMediaType.Video => "video",
        _ => "image"
    };

    private static SendToMediaType ParseMediaType(string sourceType) =>
        string.Equals(sourceType, "video", StringComparison.OrdinalIgnoreCase)
            ? SendToMediaType.Video
            : SendToMediaType.Image;
}