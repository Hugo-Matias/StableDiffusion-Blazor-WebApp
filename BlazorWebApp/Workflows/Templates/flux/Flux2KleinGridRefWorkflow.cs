using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Flux;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Models;
using FragmentParameters = BlazorWebApp.Models.FragmentParameters;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Flux;

/// <summary>
/// Flux2 Klein grid-reference workflow using four reference images and an optional base image.
/// </summary>
public class Flux2KleinGridRefWorkflow : IWorkflowBuilder
{
    private const string BaseImageSource = "base_image";
    private const string GridReference1Source = "grid_reference_1";
    private const string GridReference2Source = "grid_reference_2";
    private const string GridReference3Source = "grid_reference_3";
    private const string GridReference4Source = "grid_reference_4";

    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly BasicSchedulerSamplerCustomAdvancedFragment _samplerFragment = new()
    {
        Defaults = new()
        {
            SamplerName = "euler",
            Scheduler = "simple",
            Steps = 4,
            Cfg = 1.0,
            Denoise = 1.0,
            Seed = -1
        }
    };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();
    private readonly FluxKleinGridRefSettingsFragment _gridRefSettingsFragment = new();
    private readonly FluxKleinReferenceGridFragment _referenceGridFragment = new();
    private readonly FluxKleinGridRefConditioningFragment _conditioningFragment = new();
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();
    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Flux2 Klein Grid Ref",
        Description = "Uses a 2x2 grid built from four reference images to guide an optional base-image edit. " +
                  "When no base image is supplied, it runs in a prompt-driven faux image-to-image mode using the standard resolution controls.",
        Base = Data.Enums.ModelBase.Flux,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "flux-2-klein-9b-fp8.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen_3_8b_fp8mixed.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "flux2-vae.safetensors",
                Order = 3,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = BaseImageSource,
                Label = "Base Image",
                Type = SourceType.Image,
                Required = false,
                AllowMultiple = false
            },
            new WorkflowSource
            {
                Id = GridReference1Source,
                Label = "Grid Reference 1",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = false
            },
            new WorkflowSource
            {
                Id = GridReference2Source,
                Label = "Grid Reference 2",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = false
            },
            new WorkflowSource
            {
                Id = GridReference3Source,
                Label = "Grid Reference 3",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = false
            },
            new WorkflowSource
            {
                Id = GridReference4Source,
                Label = "Grid Reference 4",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = false
            }
        ],
        CompatibleResourceBaseModels =
        [
            "Flux.2 D",
            "Flux.2 Klein 9B",
            "Flux.2 Klein 9B-base",
            "Flux.2 Klein 4B",
            "Flux.2 Klein 4B-base"
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _gridRefSettingsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var latentFragment = parameters.GetFragment("latent");
        var settingsFragment = parameters.GetFragment("grid_ref_settings");

        var settingsDefaults = _gridRefSettingsFragment.Defaults;
        var gridTileMegapixels = settingsFragment?.GetDouble("grid_tile_megapixels", settingsDefaults.GridTileMegapixels)
                                 ?? settingsDefaults.GridTileMegapixels;
        var gridMegapixels = settingsFragment?.GetDouble("grid_megapixels", settingsDefaults.GridMegapixels)
                             ?? settingsDefaults.GridMegapixels;
        var baseMegapixels = settingsFragment?.GetDouble("base_megapixels", settingsDefaults.BaseMegapixels)
                             ?? settingsDefaults.BaseMegapixels;

        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "flux-2-klein-9b-fp8.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
            ClipType = "flux2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "flux2-vae.safetensors"
        });

        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = ""
        });

        _referenceGridFragment.Build(builder, registry, new FluxKleinReferenceGridFragment.Parameters
        {
            Images =
            [
                GetSourcePath(parameters, GridReference1Source),
                GetSourcePath(parameters, GridReference2Source),
                GetSourcePath(parameters, GridReference3Source),
                GetSourcePath(parameters, GridReference4Source)
            ],
            GridTileMegapixels = gridTileMegapixels,
            GridMegapixels = gridMegapixels,
            UpscaleMethod = settingsDefaults.UpscaleMethod,
            ResolutionSteps = settingsDefaults.ResolutionSteps
        });

        var baseImagePath = GetSourcePath(parameters, BaseImageSource);
        var hasBaseImage = _conditioningFragment.Build(builder, registry, new FluxKleinGridRefConditioningFragment.Parameters
        {
            BaseImage = baseImagePath,
            BaseMegapixels = baseMegapixels,
            UpscaleMethod = settingsDefaults.UpscaleMethod,
            ResolutionSteps = settingsDefaults.ResolutionSteps
        });

        if (hasBaseImage)
        {
            _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
            {
                BatchSize = 1,
                LatentClass = "EmptyLatentImage",
                WidthRef = registry.GetRef("base_image_width"),
                HeightRef = registry.GetRef("base_image_height")
            });
        }
        else
        {
            _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
            {
                Width = latentFragment?.GetInt("width", 1024) ?? 1024,
                Height = latentFragment?.GetInt("height", 1024) ?? 1024,
                BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1,
                LatentClass = "EmptyLatentImage"
            });
        }

        _samplerFragment.Build(builder, parameters, registry);
        _vaeDecodeFragment.Build(builder, registry);
        _seedVR2UpscaleFragment.Build(builder, parameters, registry);
        BuildDetailerIfActive(builder, registry, parameters, promptsFragment);
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }

    private void BuildDetailerIfActive(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        FragmentParameters? promptsFragment)
    {
        var detailerFragment = parameters.GetFragment("detailer");
        if (detailerFragment?.IsActive != true)
        {
            return;
        }

        var mainPositive = promptsFragment?.GetString("positive", "") ?? "";
        var mainNegative = promptsFragment?.GetString("negative", "") ?? "";
        var samplerFragment = parameters.GetFragment("main_sampler");
        var resolvedSeed = samplerFragment?.GetLong("seed", -1) ?? -1;
        if (resolvedSeed < 0)
        {
            resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        }

        var passCount = Math.Max(1, detailerFragment.GetInt("pass_count", 1));
        for (var i = 0; i < passCount; i++)
        {
            var scope = i == 0 ? "detailer_" : $"detailer_{i}_";
            var scopeTitle = i == 0 ? "Detailer " : $"Detailer {i + 1} ";
            var keyPrefix = i == 0 ? string.Empty : $"pass_{i}_";
            var passEnabledKey = i == 0 ? "detailer_enabled" : $"pass_{i}_detailer_enabled";

            if (!detailerFragment.GetBool(passEnabledKey, true))
            {
                continue;
            }

            _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
            {
                UnetName = detailerFragment.GetString($"{keyPrefix}detailer_checkpoint")
                           ?? parameters.Assets?.GetValueOrDefault("Model")
                           ?? "flux-2-klein-9b-fp8.safetensors",
                ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
                ClipType = "flux2",
                VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "flux2-vae.safetensors",
                Positive = detailerFragment.GetStringOrFallback($"{keyPrefix}detailer_prompt", mainPositive),
                Negative = detailerFragment.GetStringOrFallback($"{keyPrefix}detailer_negative_prompt", mainNegative)
            }, scope: scope, scopeTitle: scopeTitle);

            _loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);

            _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
            {
                Scope = scope,
                DetectionModel = detailerFragment.GetString($"{keyPrefix}detailer_detection_model", "bbox/face_yolov8m.pt"),
                Sampler = detailerFragment.GetString($"{keyPrefix}detailer_sampler", "dpmpp_2m"),
                Scheduler = detailerFragment.GetString($"{keyPrefix}detailer_scheduler", "simple"),
                Seed = detailerFragment.GetLong($"{keyPrefix}detailer_seed", resolvedSeed),
                Steps = detailerFragment.GetInt($"{keyPrefix}detailer_steps", 20),
                Cfg = detailerFragment.GetDouble($"{keyPrefix}detailer_cfg", 5.0),
                Denoise = detailerFragment.GetDouble($"{keyPrefix}detailer_denoise", 0.65),
                Feather = detailerFragment.GetInt($"{keyPrefix}detailer_feather", 5),
                BboxThreshold = detailerFragment.GetDouble($"{keyPrefix}detailer_bbox_threshold", 0.7),
                BboxDilation = detailerFragment.GetInt($"{keyPrefix}detailer_bbox_dilation", 10),
                BboxCropFactor = detailerFragment.GetDouble($"{keyPrefix}detailer_bbox_crop_factor", 3.0),
                DropSize = detailerFragment.GetInt($"{keyPrefix}detailer_drop_size", 70),
                GuideSize = detailerFragment.GetInt($"{keyPrefix}detailer_guide_size", 512),
                MaxSize = detailerFragment.GetInt($"{keyPrefix}detailer_max_size", 1024),
                Cycle = detailerFragment.GetInt($"{keyPrefix}detailer_cycle", 1)
            });
        }
    }

    private static string GetSourcePath(GenerationParameters parameters, string sourceId)
    {
        if (!parameters.Sources.TryGetValue(sourceId, out var source))
        {
            return string.Empty;
        }

        return source.Filename ?? source.FilePath ?? string.Empty;
    }
}