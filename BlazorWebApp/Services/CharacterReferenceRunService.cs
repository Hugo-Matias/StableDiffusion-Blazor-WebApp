using BlazorWebApp.Data.Entities;
using BlazorWebApp.Extensions;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using System.Globalization;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services;

public sealed class CharacterReferenceRunService : ICharacterReferenceRunService
{
    private readonly IComfyUIService _comfyUI;
    private readonly IBackendService _backend;
    private readonly IDatabaseService _database;
    private readonly IStateService _state;
    private readonly IIOService _io;
    private readonly MagickService _magick;
    private readonly IReadOnlyDictionary<CharacterReferenceEngine, ICharacterReferenceWorkflowComposer> _composers;
    private readonly ILogger<CharacterReferenceRunService> _logger;

    public CharacterReferenceRunService(
        IComfyUIService comfyUI,
        IBackendService backend,
        IDatabaseService database,
        IStateService state,
        IIOService io,
        MagickService magick,
        IEnumerable<ICharacterReferenceWorkflowComposer> composers,
        ILogger<CharacterReferenceRunService> logger)
    {
        _comfyUI = comfyUI;
        _backend = backend;
        _database = database;
        _state = state;
        _io = io;
        _magick = magick;
        _composers = composers.ToDictionary(composer => composer.Engine);
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
        var reusableDependencyImagePaths = await UploadReusableDependencyImagesAsync(characterState, runSlots, uploadTrackingId);
        MarkStatus(runSlots, CharacterReferenceSlotStatus.Queued);
        await _state.SaveState();

        var runState = CreateRunState(characterState, runSlots, sourceImagePath, reusableDependencyImagePaths);
        var build = GetComposer(runState.Engine).Build(runState);
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

    private async Task<Dictionary<string, string>> UploadReusableDependencyImagesAsync(
        AppStateCharacter characterState,
        IReadOnlyList<CharacterReferenceSlotState> runSlots,
        Guid uploadTrackingId)
    {
        var runSlotIds = runSlots.Select(slot => slot.Id).ToHashSet(StringComparer.Ordinal);
        var reusableImages = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var slot in runSlots)
        {
            var dependencySlotId = CharacterReferenceSlotPlanner.ResolveDependencySlotId(slot);
            if (string.IsNullOrWhiteSpace(dependencySlotId)
                || runSlotIds.Contains(dependencySlotId)
                || reusableImages.ContainsKey(dependencySlotId))
            {
                continue;
            }

            var dependencySlot = characterState.Slots.FirstOrDefault(candidate => candidate.Id == dependencySlotId);
            if (dependencySlot is null || !CharacterReferenceSlotPlanner.HasReusableOutput(dependencySlot))
            {
                continue;
            }

            var imageData = await _io.GetBase64FromFileAsync(dependencySlot.LastOutputPath!);
            if (string.IsNullOrWhiteSpace(imageData))
            {
                throw new InvalidOperationException($"The saved output for '{dependencySlot.Label}' could not be loaded from '{dependencySlot.LastOutputPath}'.");
            }

            reusableImages[dependencySlotId] = await _comfyUI.UploadImageAsync(imageData, uploadTrackingId);
        }

        return reusableImages;
    }

    private static AppStateCharacter CreateRunState(
        AppStateCharacter source,
        IReadOnlyList<CharacterReferenceSlotState> runSlots,
        string sourceImagePath,
        IReadOnlyDictionary<string, string> reusableDependencyImagePaths)
    {
        return new AppStateCharacter
        {
            ActiveTabIndex = source.ActiveTabIndex,
            SidebarCollapsed = source.SidebarCollapsed,
            Engine = source.Engine,
            LoaderMode = source.LoaderMode,
            Assets = source.Assets,
            Loras = source.Loras,
            SourceImage = new CharacterSourceImageState
            {
                ImagePath = sourceImagePath,
                ImageDataUri = source.SourceImage.ImageDataUri,
                SourceLabel = source.SourceImage.SourceLabel
            },
            GlobalPositivePromptExtension = source.GlobalPositivePromptExtension,
            GlobalNegativePrompt = source.GlobalNegativePrompt,
            GlobalSeed = source.GlobalSeed,
            SamplerOverrides = source.SamplerOverrides,
            Slots = runSlots.Select(CloneEnabledSlot).ToList(),
            ShowEngineSettings = source.ShowEngineSettings,
            ShowAdvancedSettings = source.ShowAdvancedSettings,
            UseRtxUpscale = source.UseRtxUpscale,
            UseCleanGpu = source.UseCleanGpu,
            FaceReplacement = CloneFaceReplacementSettings(source.FaceReplacement),
            ReusableDependencyImagePaths = reusableDependencyImagePaths.ToDictionary(
                pair => pair.Key,
                pair => pair.Value,
                StringComparer.Ordinal)
        };
    }

    private static CharacterFaceReplacementSettings CloneFaceReplacementSettings(CharacterFaceReplacementSettings settings)
    {
        return new CharacterFaceReplacementSettings
        {
            Enabled = settings.Enabled,
            UseCloseNeutralReference = settings.UseCloseNeutralReference,
            CropResolution = settings.CropResolution,
            DetectionThreshold = settings.DetectionThreshold,
            SourceDetectionThreshold = settings.SourceDetectionThreshold,
            TargetCropSizeMultiplier = settings.TargetCropSizeMultiplier,
            SourceCropSizeMultiplier = settings.SourceCropSizeMultiplier,
            Steps = settings.Steps,
            Cfg = settings.Cfg,
            Denoise = settings.Denoise,
            MaskExpand = settings.MaskExpand,
            MaskBlurRadius = settings.MaskBlurRadius,
            BorderBlending = settings.BorderBlending
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

        var modelFilename = GetCurrentModelFilename(characterState);
        var persistedOutput = await SaveOutputFileAsync(characterState, slot, file, modelFilename);
        var imageInfo = _magick.ReadInfoFromBytes(persistedOutput.Bytes);

        var image = new Image
        {
            Path = persistedOutput.Path,
            ProjectId = _state.State.Gallery.ProjectId,
            Width = imageInfo.Width > 0 ? (int)imageInfo.Width : slot.Width,
            Height = imageInfo.Height > 0 ? (int)imageInfo.Height : slot.Height,
            Prompt = CharacterReferenceSlotCatalog.ComposePrompt(slot, characterState.GlobalPositivePromptExtension),
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

    private async Task<(string Path, byte[] Bytes)> SaveOutputFileAsync(
        AppStateCharacter characterState,
        CharacterReferenceSlotState slot,
        ComfyImageOutputFile file,
        string modelFilename)
    {
        var imageBytes = await File.ReadAllBytesAsync(file.FullPath);
        var saveDir = _io.CreateDirectory(GetCharacterSaveFolder(characterState, slot, modelFilename));
        var imagePath = GetNextImagePath(saveDir.FullName, slot, file.FullPath);

        if (!IsSamePath(file.FullPath, imagePath))
        {
            await _io.SaveFileToDisk(imagePath, imageBytes);
        }

        return (imagePath, imageBytes);
    }

    private string GetCharacterSaveFolder(
        AppStateCharacter characterState,
        CharacterReferenceSlotState slot,
        string modelFilename)
    {
        var basePath = _backend.GetOutputPath(Outdir.Img2ImgSamples);
        if (string.IsNullOrWhiteSpace(basePath))
        {
            throw new InvalidOperationException("The image output directory is not configured.");
        }

        var dirPattern = _backend.OutputPaths.DirectoryPattern;
        if (!string.IsNullOrWhiteSpace(dirPattern))
        {
            var subPath = ConvertPathPattern(dirPattern, characterState, slot, modelFilename);
            if (!string.IsNullOrWhiteSpace(subPath))
            {
                basePath = Path.Combine(basePath, subPath).Replace('/', Path.DirectorySeparatorChar);
            }
        }

        return basePath;
    }

    private string GetNextImagePath(string saveDir, CharacterReferenceSlotState slot, string sourcePath)
    {
        var extension = GetOutputExtension(sourcePath);
        var fileIndex = GetNextFileIndex(saveDir);
        string imagePath;

        do
        {
            imagePath = Path.Combine(saveDir, GetImageFilename(fileIndex, slot, extension));
            fileIndex++;
        }
        while (File.Exists(imagePath));

        return imagePath;
    }

    private string GetImageFilename(int fileIndex, CharacterReferenceSlotState slot, string extension)
    {
        var filename = fileIndex.ToString().PadLeft(5, '0');
        var pattern = _backend.OutputPaths.FilenamePattern ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(pattern))
        {
            filename += "-" + ConvertPathPattern(pattern, _state.State.Character, slot, GetCurrentModelFilename(_state.State.Character));
        }

        return filename + extension;
    }

    private static int GetNextFileIndex(string saveDir)
    {
        if (!Directory.Exists(saveDir))
        {
            return 1;
        }

        return Directory.EnumerateFiles(saveDir)
            .Select(path => Regex.Match(Path.GetFileName(path), @"^(\d+)"))
            .Where(match => match.Success && int.TryParse(match.Groups[1].Value, out _))
            .Select(match => int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture))
            .DefaultIfEmpty(0)
            .Max() + 1;
    }

    private string GetOutputExtension(string sourcePath)
    {
        var extension = Path.GetExtension(sourcePath);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension.ToLowerInvariant();
        }

        var configuredFormat = _backend.OutputPaths.SamplesFormat;
        return string.IsNullOrWhiteSpace(configuredFormat)
            ? ".png"
            : "." + configuredFormat.Trim().TrimStart('.').ToLowerInvariant();
    }

    private static string ConvertPathPattern(
        string pattern,
        AppStateCharacter characterState,
        CharacterReferenceSlotState slot,
        string modelFilename)
    {
        return Regex.Replace(pattern, @"\[.+?\]", match => ConvertPathTag(match.Value, characterState, slot, modelFilename));
    }

    private static string ConvertPathTag(
        string tag,
        AppStateCharacter characterState,
        CharacterReferenceSlotState slot,
        string modelFilename)
    {
        return tag switch
        {
            "[model_name]" => GetModelPathSegment(modelFilename),
            "[sampler]" => SanitizePathSegment(slot.SamplerName),
            "[seed]" => slot.Seed.ToString(CultureInfo.InvariantCulture),
            "[steps]" => slot.Steps.ToString(CultureInfo.InvariantCulture),
            "[cfg]" => slot.Cfg.ToString(CultureInfo.InvariantCulture),
            _ => string.Empty
        };
    }

    private static string GetCurrentModelFilename(AppStateCharacter characterState)
    {
        if (characterState.Engine == CharacterReferenceEngine.Flux2Klein)
        {
            return characterState.Assets.Flux.DiffusionModel;
        }

        return characterState.LoaderMode == CharacterLoaderMode.SplitStack
            ? characterState.Assets.Split.DiffusionModel
            : characterState.Assets.Aio.Checkpoint;
    }

    private ICharacterReferenceWorkflowComposer GetComposer(CharacterReferenceEngine engine)
    {
        if (_composers.TryGetValue(engine, out var composer))
        {
            return composer;
        }

        throw new InvalidOperationException($"No character reference workflow composer is registered for engine '{engine}'.");
    }

    private static string GetModelPathSegment(string modelFilename)
    {
        if (string.IsNullOrWhiteSpace(modelFilename))
        {
            return "unknown";
        }

        var modelAsPath = modelFilename
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var directoryName = Path.GetDirectoryName(modelAsPath) ?? string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(modelAsPath);

        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "unknown";
        }

        return string.IsNullOrWhiteSpace(directoryName)
            ? SanitizePathSegment(fileName)
            : Path.Combine(directoryName, SanitizePathSegment(fileName));
    }

    private static string SanitizePathSegment(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : value.SanitizePath();
    }

    private static bool IsSamePath(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
        }
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
        return characterState.Engine == CharacterReferenceEngine.Qwen
            && characterState.UseRtxUpscale
            && IsRtxUpscaleLoadFailure(exception);
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