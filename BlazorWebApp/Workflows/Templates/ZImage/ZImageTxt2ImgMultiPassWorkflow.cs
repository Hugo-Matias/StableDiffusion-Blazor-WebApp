using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Fragments.zimage;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.ZImage;

/// <summary>
/// Z-Image Base to ZIT multi-pass Txt2Img workflow.
/// </summary>
public class ZImageTxt2ImgMultiPassWorkflow : IWorkflowBuilder
{
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly KSamplerAdvancedFragment _baseSamplerFragment = new()
    {
        Id = "base_sampler",
        Title = "Phase 1 - ZIB",
        Type = FragmentType.Sampler,
        Order = 50,
        Collapsible = false,
        DefaultActive = true,
        Defaults = new KSamplerAdvancedFragment.Parameters
        {
            SamplerName = "res_multistep",
            Scheduler = "simple",
            Steps = 8,
            Cfg = 4.0,
            StartAtStep = 0,
            EndAtStep = 20,
            AddNoise = "enable",
            ReturnWithLeftoverNoise = "disable"
        }
    };
    private readonly SamplerFragment _zitSamplerFragment = new()
    {
        Id = "zit_sampler",
        Title = "Phase 2 - ZIT",
        Type = FragmentType.Settings,
        Order = 55,
        Collapsible = false,
        DefaultActive = true,
        Defaults = new SamplerFragment.Parameters
        {
            SamplerName = "linear/ralston_2s",
            Scheduler = "beta",
            Steps = 10,
            Cfg = 1.0,
            Denoise = 0.56,
            Eta = 0.23
        }
    };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    private readonly SeedVarianceEnhancerFragment _seedVarianceEnhancerFragment = new();
    private readonly TilePreprocessorFragment _tilePreprocessorFragment = new();
    private readonly ModelPatchLoaderFragment _modelPatchLoaderFragment = new();
    private readonly UpscaleModelLoaderFragment _upscaleModelLoaderFragment = new();
    private readonly ZImageUpscaleFragment _zImageUpscaleFragment = new()
    {
        Title = "ZIT Upscale Enhancement",
        Type = FragmentType.Enhancement,
        Order = 90,
        Collapsible = true,
        DefaultCollapsed = true
    };

    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img Multi Pass",
        Base = Data.Enums.ModelBase.ZImage,
        Mode = ModeType.Txt2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Phase 2 - ZIT Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "z_image_turbo_bf16.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "BaseModel",
                Label = "Phase 1 - ZIB Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "z_image_bf16.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen_3_4b.safetensors",
                Order = 3,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "ae.safetensors",
                Order = 4,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "ModelPatch",
                Label = "Phase 2 - ZIT Model Patch",
                Type = AssetType.ModelPatch,
                DefaultValue = "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors",
                Order = 5,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "UpscaleModel",
                Label = "Phase 2 - ZIT Upscale Model",
                Type = AssetType.UpscaleModel,
                DefaultValue = "x1_ITF_SkinDiffDetail_Lite_v1.pth",
                Order = 6,
                ColumnSize = 4
            }
        ],
        CompatibleResourceBaseModels = ["ZImageTurbo", "ZImageBase"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _baseSamplerFragment;
        yield return _zitSamplerFragment;
        yield return _seedVarianceEnhancerFragment;
        yield return _zImageUpscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var latentFragment = parameters.GetFragment("latent");
        var promptsFragment = parameters.GetFragment("prompts");
        var baseSamplerFragment = parameters.GetFragment("base_sampler");
        var zitSamplerFragment = parameters.GetFragment("zit_sampler");

        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "z_image_turbo_bf16.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_4b.safetensors",
            ClipType = "lumina2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "ae.safetensors"
        });

        builder.AddNode("base_unet_loader", node => node
            .Type("UNETLoader")
            .Title("Load Diffusion Model - Z-image Base")
            .Input("unet_name", parameters.Assets?.GetValueOrDefault("BaseModel") ?? "z_image_bf16.safetensors")
            .Input("weight_dtype", "default"));
        registry.Register("base_model_output", "base_unet_loader", 0);

        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = latentFragment?.GetInt("width", 872) ?? 872,
            Height = latentFragment?.GetInt("height", 1248) ?? 1248,
            BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1,
            LatentClass = "EmptySD3LatentImage"
        });

        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        var seedVarianceFragment = parameters.GetFragment("seed_variance_enhancer");
        if (seedVarianceFragment?.IsActive == true)
        {
            _seedVarianceEnhancerFragment.Build(builder, registry, new SeedVarianceEnhancerFragment.Parameters
            {
                RandomizePercent = seedVarianceFragment.GetInt("randomize_percent", 50),
                Strength = seedVarianceFragment.GetInt("strength", 20),
                NoiseInsert = seedVarianceFragment.GetString("noise_insert", "noise on beginning steps"),
                StepsSwitchoverPercent = seedVarianceFragment.GetInt("steps_switchover_percent", 20),
                Seed = seedVarianceFragment.GetLong("seed", 0),
                MaskStartsAt = seedVarianceFragment.GetString("mask_starts_at", "beginning"),
                MaskPercent = seedVarianceFragment.GetInt("mask_percent", 0),
                LogToConsole = seedVarianceFragment.GetBool("log_to_console", false)
            });
        }

        var baseSeed = baseSamplerFragment?.GetLong("seed", -1) ?? -1;
        if (baseSeed < 0) baseSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _baseSamplerFragment.Build(builder, registry, new KSamplerAdvancedFragment.Parameters
        {
            SamplerId = "sampler_base",
            Title = "Phase 1 - ZIB Sampler",
            SamplerName = baseSamplerFragment?.GetString("sampler_name", "res_multistep") ?? "res_multistep",
            Scheduler = baseSamplerFragment?.GetString("scheduler", "simple") ?? "simple",
            Steps = baseSamplerFragment?.GetInt("steps", 8) ?? 8,
            Cfg = baseSamplerFragment?.GetDouble("cfg", 4.0) ?? 4.0,
            Seed = baseSeed,
            AddNoise = "enable",
            StartAtStep = 0,
            EndAtStep = 20,
            ReturnWithLeftoverNoise = "disable",
            ModelOverride = registry.GetRef("base_model_output")
        });

        var zitSeed = zitSamplerFragment?.GetLong("seed", -1) ?? -1;
        if (zitSeed < 0) zitSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _zitSamplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = "sampler_zit",
            Title = "Phase 2 - ZIT Sampler",
            SamplerName = zitSamplerFragment?.GetString("sampler_name", "linear/ralston_2s") ?? "linear/ralston_2s",
            Scheduler = zitSamplerFragment?.GetString("scheduler", "beta") ?? "beta",
            Steps = zitSamplerFragment?.GetInt("steps", 10) ?? 10,
            Cfg = zitSamplerFragment?.GetDouble("cfg", 1.0) ?? 1.0,
            Denoise = zitSamplerFragment?.GetDouble("denoise", 0.56) ?? 0.56,
            Eta = zitSamplerFragment?.GetDouble("eta", 0.23) ?? 0.23,
            Seed = zitSeed,
            ClassType = "ClownsharKSampler_Beta"
        });

        _vaeDecodeFragment.Build(builder, registry);

        var upscaleFragment = parameters.GetFragment("zimage_upscale");
        if (upscaleFragment?.IsActive == true)
        {
            BuildZitUpscale(builder, registry, parameters, upscaleFragment, latentFragment);
        }

        BuildDetailerIfActive(builder, registry, parameters, promptsFragment, latentFragment);

        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }

    private void BuildZitUpscale(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        BlazorWebApp.Models.FragmentParameters upscaleFragment,
        BlazorWebApp.Models.FragmentParameters? latentFragment)
    {
        _tilePreprocessorFragment.Build(builder, registry, new TilePreprocessorFragment.Parameters { Resolution = 1024 });
        _modelPatchLoaderFragment.Build(builder, registry, new ModelPatchLoaderFragment.Parameters
        {
            PatchName = GetModelPatchAsset(parameters)
        });
        _upscaleModelLoaderFragment.Build(builder, registry, new UpscaleModelLoaderFragment.Parameters
        {
            ModelName = parameters.Assets?.GetValueOrDefault("UpscaleModel") ?? "x1_ITF_SkinDiffDetail_Lite_v1.pth"
        });

        var upscaleSeed = upscaleFragment.GetLong("upscale_seed", -1);
        if (upscaleSeed < 0) upscaleSeed = Random.Shared.NextInt64(0, int.MaxValue);

        _zImageUpscaleFragment.Build(builder, registry, new ZImageUpscaleFragment.Parameters
        {
            UpscaleBy = upscaleFragment.GetDouble("upscale_by", 2.0),
            Strength = upscaleFragment.GetDouble("strength", 0.2),
            ControlNetNodeType = upscaleFragment.GetString("controlnet_node_type", "QwenImageDiffsynthControlnet") ?? "QwenImageDiffsynthControlnet",
            SamplerName = upscaleFragment.GetString("upscale_sampler_name", "deis_2m") ?? "deis_2m",
            Scheduler = upscaleFragment.GetString("upscale_scheduler", "beta") ?? "beta",
            Steps = upscaleFragment.GetInt("upscale_steps", 6),
            Cfg = upscaleFragment.GetDouble("upscale_cfg", 1.0),
            Denoise = upscaleFragment.GetDouble("upscale_denoise", 0.21),
            Seed = upscaleSeed,
            ModeType = upscaleFragment.GetString("mode_type", "Linear") ?? "Linear",
            AutoTileSize = upscaleFragment.GetBool("auto_tile_size", true),
            TileWidth = upscaleFragment.GetInt("tile_width", 512),
            TileHeight = upscaleFragment.GetInt("tile_height", 512),
            MaskBlur = upscaleFragment.GetInt("mask_blur", 8),
            TilePadding = upscaleFragment.GetInt("tile_padding", 32),
            SeamFixMode = upscaleFragment.GetString("seam_fix_mode", "None") ?? "None",
            SeamFixDenoise = upscaleFragment.GetDouble("seam_fix_denoise", 1.0),
            SeamFixWidth = upscaleFragment.GetInt("seam_fix_width", 64),
            SeamFixMaskBlur = upscaleFragment.GetInt("seam_fix_mask_blur", 8),
            SeamFixPadding = upscaleFragment.GetInt("seam_fix_padding", 16),
            ForceUniformTiles = upscaleFragment.GetBool("force_uniform_tiles", true),
            TiledDecode = upscaleFragment.GetBool("tiled_decode", false),
            BatchSize = upscaleFragment.GetInt("batch_size", 1),
            BaseWidth = latentFragment?.GetInt("width", 872) ?? 872,
            BaseHeight = latentFragment?.GetInt("height", 1248) ?? 1248
        });
    }

    private static string GetModelPatchAsset(GenerationParameters parameters)
        => parameters.Assets?.GetValueOrDefault("ModelPatch")
           ?? parameters.Assets?.GetValueOrDefault("ControlNet")
           ?? "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors";

    private void BuildDetailerIfActive(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        BlazorWebApp.Models.FragmentParameters? promptsFragment,
        BlazorWebApp.Models.FragmentParameters? latentFragment)
    {
        var detailerFragment = parameters.GetFragment("detailer");
        if (detailerFragment?.IsActive != true)
        {
            return;
        }

        var passCount = Math.Max(1, detailerFragment.GetInt("pass_count", 1));
        for (int i = 0; i < passCount; i++)
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
                           ?? "z_image_turbo_bf16.safetensors",
                ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_4b.safetensors",
                ClipType = "lumina2",
                VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "ae.safetensors",
                Positive = detailerFragment.GetStringOrFallback($"{keyPrefix}detailer_prompt", promptsFragment?.GetString("positive", "") ?? ""),
                Negative = detailerFragment.GetStringOrFallback($"{keyPrefix}detailer_negative_prompt", promptsFragment?.GetString("negative", "") ?? ""),
                Width = latentFragment?.GetInt("width", 872) ?? 872,
                Height = latentFragment?.GetInt("height", 1248) ?? 1248,
                BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1
            }, scope: scope, scopeTitle: scopeTitle);

            _loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);

            _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
            {
                Scope = scope,
                DetectionModel = detailerFragment.GetString($"{keyPrefix}detailer_detection_model", "bbox/face_yolov8m.pt"),
                Sampler = detailerFragment.GetString($"{keyPrefix}detailer_sampler", "dpmpp_2m"),
                Scheduler = detailerFragment.GetString($"{keyPrefix}detailer_scheduler", "beta"),
                Seed = detailerFragment.GetLong($"{keyPrefix}detailer_seed", 42),
                Steps = detailerFragment.GetInt($"{keyPrefix}detailer_steps", 20),
                Cfg = detailerFragment.GetDouble($"{keyPrefix}detailer_cfg", 8.0),
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
}