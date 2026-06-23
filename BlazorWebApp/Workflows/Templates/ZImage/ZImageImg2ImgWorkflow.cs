using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.ZImage;

/// <summary>
/// Z-Image Img2Img workflow implementation using the fluent builder API.
/// Generates images from a source image using the Z-Image model architecture.
/// Pipeline: LoadImage+Scale -> LoadDiffusion -> LoRAs -> VaeEncode -> Prompts -> Sample -> VaeDecode -> [SeedVR2] -> [Detailer] -> Save
/// </summary>
public class ZImageImg2ImgWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadImageScaledFragment _loadImageScaledFragment = new();
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly VaeEncodeFragment _vaeEncodeFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly SamplerFragment _samplerFragment = new() { Defaults = new() { Steps = 9, Cfg = 1.0, Denoise = 0.75 } };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();

    // Detailer fragments
    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Img2Img",
        Base = Data.Enums.ModelBase.ZImage,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "z_image_turbo_bf16.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen_3_4b.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "ae.safetensors",
                Order = 3,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "source_image",
                Label = "Source Image",
                Type = SourceType.Image,
                Required = true
            }
        ],
        CompatibleResourceBaseModels = ["ZImageTurbo", "ZImageBase"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _samplerFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Get fragment parameters for reuse
        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");

        // Get source image path
        var source = parameters.Sources?.GetValueOrDefault("source_image");
        var imagePath = source?.Filename ?? source?.FilePath ?? "";

        // 1. Load and scale source image
        _loadImageScaledFragment.Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = imagePath,
            UpscaleMethod = "lanczos",
            Megapixels = 1
        });

        // 2. Load models (UNet, CLIP, VAE)
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "z_image_turbo_bf16.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_4b.safetensors",
            ClipType = "lumina2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "ae.safetensors"
        });

        // 3. Load LoRAs (if any)
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 4. VAE Encode (source image -> latent)
        _vaeEncodeFragment.Build(builder, registry, new VaeEncodeFragment.Parameters());

        // 5. Encode prompts
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        // 6. Sample (denoise < 1 for Img2Img to preserve source structure)
        var resolvedSeed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = samplerFragment?.GetString("sampler_id", "sampler_main") ?? "sampler_main",
            Title = samplerFragment?.GetString("title", "Main Sampler") ?? "Main Sampler",
            SamplerName = samplerFragment?.GetString("sampler_name", "linear/euler") ?? "linear/euler",
            Scheduler = samplerFragment?.GetString("scheduler", "simple") ?? "simple",
            Steps = samplerFragment?.GetInt("steps", 9) ?? 9,
            Cfg = samplerFragment?.GetDouble("cfg", 1) ?? 1,
            Denoise = samplerFragment?.GetDouble("denoise", 0.75) ?? 0.75,
            Eta = samplerFragment?.GetDouble("eta", 0.5) ?? 0.5,
            Seed = resolvedSeed,
            ClassType = "ClownsharKSampler_Beta"
        });

        // 7. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 8. SeedVR2 Upscale (conditional)
        var seedVr2Fragment = parameters.GetFragment("seed_vr2");
        if (seedVr2Fragment?.IsActive == true)
        {
            _seedVR2UpscaleFragment.Build(builder, registry, new SeedVR2UpscaleFragment.Parameters
            {
                Model = seedVr2Fragment.GetString("seedvr2_model", "seedvr2_ema_7b-Q4_K_M.gguf"),
                VaeModel = seedVr2Fragment.GetString("seedvr2_vae_model", "ema_vae_fp16.safetensors"),
                Seed = seedVr2Fragment.GetLong("seedvr2_seed", 42),
                Resolution = seedVr2Fragment.GetInt("seedvr2_resolution", 2048),
                BatchSize = seedVr2Fragment.GetInt("seedvr2_batch_size", 1),
                InputNoiseScale = seedVr2Fragment.GetDouble("seedvr2_input_noise_scale", 0.0),
                LatentNoiseScale = seedVr2Fragment.GetDouble("seedvr2_latent_noise_scale", 0.0),
                BlocksToSwap = seedVr2Fragment.GetInt("blocks_to_swap", 36),
                VaeTileSize = seedVr2Fragment.GetInt("vae_tile_size", 1024),
                VaeTileOverlap = seedVr2Fragment.GetInt("vae_tile_overlap", 128)
            });
        }

        // 9. Detailer (conditional) - supports chained passes
        var detailerFragment = parameters.GetFragment("detailer");
        if (detailerFragment?.IsActive == true)
        {
            var passCount = Math.Max(1, detailerFragment.GetInt("pass_count", 1));
            for (int i = 0; i < passCount; i++)
            {
                var scope = i == 0 ? "detailer_" : $"detailer_{i}_";
                var scopeTitle = i == 0 ? "Detailer " : $"Detailer {i + 1} ";
                var kp = i == 0 ? string.Empty : $"pass_{i}_";

                // Check if this pass is enabled
                var passEnabledKey = i == 0 ? "detailer_enabled" : $"pass_{i}_detailer_enabled";
                if (!detailerFragment.GetBool(passEnabledKey, true))
                    continue;

                _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
                {
                    UnetName = detailerFragment.GetString($"{kp}detailer_checkpoint")
                               ?? parameters.Assets?.GetValueOrDefault("Model")
                               ?? "z_image_turbo_bf16.safetensors",
                    ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_4b.safetensors",
                    ClipType = "lumina2",
                    VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "ae.safetensors",
                    Positive = detailerFragment.GetStringOrFallback($"{kp}detailer_prompt",
                                     promptsFragment?.GetString("positive", "") ?? ""),
                    Negative = detailerFragment.GetStringOrFallback($"{kp}detailer_negative_prompt",
                                     promptsFragment?.GetString("negative", "") ?? ""),
                    Width = 872,
                    Height = 1248,
                    BatchSize = 1
                }, scope: scope, scopeTitle: scopeTitle);

                _loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);

                _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
                {
                    Scope = scope,
                    DetectionModel = detailerFragment.GetString($"{kp}detailer_detection_model", "bbox/face_yolov8m.pt"),
                    Sampler = detailerFragment.GetString($"{kp}detailer_sampler", "dpmpp_2m"),
                    Scheduler = detailerFragment.GetString($"{kp}detailer_scheduler", "beta"),
                    Seed = detailerFragment.GetLong($"{kp}detailer_seed", 42),
                    Steps = detailerFragment.GetInt($"{kp}detailer_steps", 20),
                    Cfg = detailerFragment.GetDouble($"{kp}detailer_cfg", 8.0),
                    Denoise = detailerFragment.GetDouble($"{kp}detailer_denoise", 0.65),
                    Feather = detailerFragment.GetInt($"{kp}detailer_feather", 5),
                    BboxThreshold = detailerFragment.GetDouble($"{kp}detailer_bbox_threshold", 0.7),
                    BboxDilation = detailerFragment.GetInt($"{kp}detailer_bbox_dilation", 10),
                    BboxCropFactor = detailerFragment.GetDouble($"{kp}detailer_bbox_crop_factor", 3.0),
                    DropSize = detailerFragment.GetInt($"{kp}detailer_drop_size", 70),
                    GuideSize = detailerFragment.GetInt($"{kp}detailer_guide_size", 512),
                    MaxSize = detailerFragment.GetInt($"{kp}detailer_max_size", 1024),
                    Cycle = detailerFragment.GetInt($"{kp}detailer_cycle", 1)
                });
            }
        }

        // 10. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
