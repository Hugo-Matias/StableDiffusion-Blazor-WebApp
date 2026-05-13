using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Templates.Qwen;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services;

public sealed class CharacterReferenceRunService : ICharacterReferenceRunService
{
    private readonly IComfyUIService _comfyUI;
    private readonly IBackendService _backend;
    private readonly IDatabaseService _database;
    private readonly IStateService _state;
    private readonly MagickService _magick;
    private readonly QwenCharacterReferenceWorkflowComposer _composer;
    private readonly ILogger<CharacterReferenceRunService> _logger;

    public CharacterReferenceRunService(
        IComfyUIService comfyUI,
        IBackendService backend,
        IDatabaseService database,
        IStateService state,
        MagickService magick,
        QwenCharacterReferenceWorkflowComposer composer,
        ILogger<CharacterReferenceRunService> logger)
    {
        _comfyUI = comfyUI;
        _backend = backend;
        _database = database;
        _state = state;
        _magick = magick;
        _composer = composer;
        _logger = logger;
    }

    public async Task<CharacterReferenceRunResult> RunAsync(CharacterReferenceRunRequest? request = null)
    {
        request ??= new CharacterReferenceRunRequest();
        var characterState = _state.State.Character;
        var runSlots = CharacterReferenceSlotPlanner.ResolveRunSlots(characterState, request.SlotIds, request.IncludeDependencies);

        if (runSlots.Count == 0)
        {
            return new CharacterReferenceRunResult(false, null, [], "No character reference slots were selected.");
        }

        if (!_backend.IsBackendAvailable)
        {
            return MarkFailed(runSlots, "ComfyUI backend is not available.", promptId: null);
        }

        try
        {
            return await ExecuteRunAsync(characterState, runSlots, Guid.NewGuid());
        }
        catch (Exception ex) when (ShouldRetryWithoutRtxUpscale(characterState, ex))
        {
            _logger.LogWarning(ex, "Character RTX upscale failed. Retrying once without RTX upscale.");
            characterState.UseRtxUpscale = false;

            try
            {
                var retryResult = await ExecuteRunAsync(characterState, runSlots, Guid.NewGuid());
                return retryResult with
                {
                    ErrorMessage = "RTX upscale failed in ComfyUI, so it was disabled and the run was retried without RTX."
                };
            }
            catch (Exception retryEx)
            {
                _logger.LogError(retryEx, "Character reference generation failed after RTX fallback retry.");
                var result = MarkFailed(runSlots, retryEx.Message, promptId: null);
                await _state.SaveState();
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Character reference generation failed.");
            var result = MarkFailed(runSlots, ex.Message, promptId: null);
            await _state.SaveState();
            return result;
        }
    }

    private async Task<CharacterReferenceRunResult> ExecuteRunAsync(
        AppStateCharacter characterState,
        IReadOnlyList<CharacterReferenceSlotState> runSlots,
        Guid uploadTrackingId)
    {
        var sourceImagePath = await ResolveSourceImagePathAsync(characterState, uploadTrackingId);
        MarkStatus(runSlots, CharacterReferenceSlotStatus.Queued);
        await _state.SaveState();

        var runState = CreateRunState(characterState, runSlots, sourceImagePath);
        var build = _composer.Build(runState);
        var expectedNodeIds = build.Outputs.Select(output => output.NodeId).ToArray();

        MarkStatus(runSlots, CharacterReferenceSlotStatus.Running);
        await _state.SaveState();

        var response = await _comfyUI.PostWorkflowForImageOutputsAsync(
            build.Workflow,
            _backend.ComfyWSClientId,
            expectedNodeIds,
            uploadTrackingId);

        var results = new List<CharacterReferenceSlotRunResult>();
        foreach (var output in build.Outputs)
        {
            var slot = runSlots.First(slot => slot.Id == output.SlotId);
            var files = response.ImagesByNodeId.GetValueOrDefault(output.NodeId) ?? [];
            if (files.Count == 0)
            {
                results.Add(MarkSlotFailed(slot, "ComfyUI did not return an image for this slot."));
                continue;
            }

            var image = await PersistOutputAsync(characterState, slot, files[0]);
            slot.Status = CharacterReferenceSlotStatus.Succeeded;
            slot.LastOutputImageId = image.Id;
            slot.LastOutputPath = image.Path;
            results.Add(new CharacterReferenceSlotRunResult(slot.Id, slot.Status, image));
        }

        await _state.SaveState();
        var success = results.All(result => result.Status == CharacterReferenceSlotStatus.Succeeded);
        return new CharacterReferenceRunResult(success, response.PromptId, results);
    }

    private async Task<string> ResolveSourceImagePathAsync(AppStateCharacter characterState, Guid uploadTrackingId)
    {
        if (!string.IsNullOrWhiteSpace(characterState.SourceImage.ImageDataUri))
        {
            var imagePath = await _comfyUI.UploadImageAsync(characterState.SourceImage.ImageDataUri, uploadTrackingId);
            characterState.SourceImage.ImagePath = null;
            return imagePath;
        }

        if (!string.IsNullOrWhiteSpace(characterState.SourceImage.ImagePath))
        {
            return characterState.SourceImage.ImagePath;
        }

        throw new InvalidOperationException("A source image is required before running character references.");
    }

    private static AppStateCharacter CreateRunState(
        AppStateCharacter source,
        IReadOnlyList<CharacterReferenceSlotState> runSlots,
        string sourceImagePath)
    {
        return new AppStateCharacter
        {
            ActiveTabIndex = source.ActiveTabIndex,
            SidebarCollapsed = source.SidebarCollapsed,
            LoaderMode = source.LoaderMode,
            Assets = source.Assets,
            Loras = source.Loras,
            SourceImage = new CharacterSourceImageState
            {
                ImagePath = sourceImagePath,
                ImageDataUri = source.SourceImage.ImageDataUri,
                SourceLabel = source.SourceImage.SourceLabel
            },
            GlobalNegativePrompt = source.GlobalNegativePrompt,
            SamplerOverrides = source.SamplerOverrides,
            Slots = runSlots.Select(CloneEnabledSlot).ToList(),
            ShowAdvancedSettings = source.ShowAdvancedSettings,
            UseRtxUpscale = source.UseRtxUpscale,
            UseCleanGpu = source.UseCleanGpu
        };
    }

    private static CharacterReferenceSlotState CloneEnabledSlot(CharacterReferenceSlotState slot)
    {
        return new CharacterReferenceSlotState
        {
            Id = slot.Id,
            Label = slot.Label,
            Kind = slot.Kind,
            PresetKey = slot.PresetKey,
            IsBuiltIn = slot.IsBuiltIn,
            Width = slot.Width,
            Height = slot.Height,
            BatchSize = slot.BatchSize,
            Seed = slot.Seed,
            Steps = slot.Steps,
            Cfg = slot.Cfg,
            SamplerName = slot.SamplerName,
            Scheduler = slot.Scheduler,
            Denoise = slot.Denoise,
            PromptTemplate = slot.PromptTemplate,
            PromptExtension = slot.PromptExtension,
            PromptOverride = slot.PromptOverride,
            NegativePromptOverride = slot.NegativePromptOverride,
            IsEnabled = true,
            DependencyPolicy = slot.DependencyPolicy,
            DependencySlotId = slot.DependencySlotId,
            LastOutputImageId = slot.LastOutputImageId,
            LastOutputPath = slot.LastOutputPath,
            Status = slot.Status,
            PromptExpanded = slot.PromptExpanded
        };
    }

    private async Task<Image> PersistOutputAsync(
        AppStateCharacter characterState,
        CharacterReferenceSlotState slot,
        ComfyImageOutputFile file)
    {
        if (!File.Exists(file.FullPath))
        {
            throw new FileNotFoundException("ComfyUI returned an image path that does not exist on disk.", file.FullPath);
        }

        var imageBytes = await File.ReadAllBytesAsync(file.FullPath);
        var imageInfo = _magick.ReadInfoFromBytes(imageBytes);
        var modelFilename = characterState.LoaderMode == CharacterLoaderMode.SplitStack
            ? characterState.Assets.Split.DiffusionModel
            : characterState.Assets.Aio.Checkpoint;

        var image = new Image
        {
            Path = file.FullPath,
            ProjectId = _state.State.Gallery.ProjectId,
            Width = imageInfo.Width > 0 ? (int)imageInfo.Width : slot.Width,
            Height = imageInfo.Height > 0 ? (int)imageInfo.Height : slot.Height,
            Prompt = slot.PromptOverride,
            NegativePrompt = string.IsNullOrWhiteSpace(slot.NegativePromptOverride)
                ? characterState.GlobalNegativePrompt
                : slot.NegativePromptOverride,
            SamplerId = await _database.GetSamplerIdByName(slot.SamplerName),
            Scheduler = slot.Scheduler,
            Steps = slot.Steps,
            Seed = slot.Seed,
            CfgScale = (float)slot.Cfg,
            DenoisingStrength = slot.Denoise,
            Model = await _database.GetResourceByFilename(modelFilename),
            ModeId = await _database.GetMode(ModeType.Img2Img),
            DateCreated = DateTime.Now
        };

        return await _database.AddImage(image);
    }

    private CharacterReferenceRunResult MarkFailed(
        IReadOnlyList<CharacterReferenceSlotState> slots,
        string message,
        string? promptId)
    {
        var results = slots.Select(slot => MarkSlotFailed(slot, message)).ToList();
        return new CharacterReferenceRunResult(false, promptId, results, message);
    }

    private static CharacterReferenceSlotRunResult MarkSlotFailed(CharacterReferenceSlotState slot, string message)
    {
        slot.Status = CharacterReferenceSlotStatus.Failed;
        return new CharacterReferenceSlotRunResult(slot.Id, slot.Status, null, message);
    }

    private static bool ShouldRetryWithoutRtxUpscale(AppStateCharacter characterState, Exception exception)
    {
        return characterState.UseRtxUpscale && IsRtxUpscaleLoadFailure(exception);
    }

    private static bool IsRtxUpscaleLoadFailure(Exception exception)
    {
        var message = exception.ToString();
        return message.Contains("NvVFX_Load failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("nvvfx.NvVFXError", StringComparison.OrdinalIgnoreCase)
            || message.Contains("RTXVideoSuperResolution", StringComparison.OrdinalIgnoreCase);
    }

    private static void MarkStatus(IEnumerable<CharacterReferenceSlotState> slots, CharacterReferenceSlotStatus status)
    {
        foreach (var slot in slots)
        {
            slot.Status = status;
        }
    }
}