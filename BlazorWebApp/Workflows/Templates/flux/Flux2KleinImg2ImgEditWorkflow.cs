using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Flux;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Flux;

/// <summary>
/// Flux2 Klein Image Edit workflow using the fluent builder API.
/// Supports multiple reference images chained through ReferenceLatent nodes.
/// Uses the SamplerCustomAdvanced pipeline with Flux2Scheduler, CFGGuider, and RandomNoise.
/// Pipeline: LoadDiffusion -> LoRA -> Prompts -> [ReferenceLatent x N] -> GetImageSize -> EmptyLatent(ref dims) -> SamplerCustomAdvanced(ref dims) -> VaeDecode -> [SeedVR2] -> [Detailer] -> Save
/// </summary>
public class Flux2KleinImg2ImgEditWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly ReferenceLatentFragment _referenceLatentFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly SamplerCustomAdvancedFragment _samplerCustomAdvancedFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Settings fragments
    private readonly ReferenceLatentSettingsFragment _referenceLatentSettingsFragment = new();

    // Enhancement fragments
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();

    // Detailer fragments
    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Flux2 Klein Edit",
        Base = Data.Enums.ModelBase.Flux,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "flux-2-klein-base-9b-fp8.safetensors",
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
                DefaultValue = "full_encoder_small_decoder.safetensors",
                Order = 3,
                ColumnSize = 4
            }
        ],
        Sources =
        [
            new Workflows.Models.WorkflowSource
            {
                Id = "reference_image",
                Label = "Reference Image",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = true
            }
        ],
        CompatibleResourceBaseModels = ["Flux.2 D", "Flux.2 Klein 9B", "Flux.2 Klein 9B-base", "Flux.2 Klein 4B", "Flux.2 Klein 4B-base"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _samplerCustomAdvancedFragment;
        yield return _referenceLatentSettingsFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");
        var referenceLatentSettings = parameters.GetFragment("reference_latent_settings");

        // Collect all reference image sources (reference_image, reference_image_1, reference_image_2, ...)
        var referenceImages = parameters.Sources
            .Where(kvp => kvp.Key.StartsWith("reference_image") && kvp.Value.HasData)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        // Get first image path for size reference
        var firstSource = referenceImages.FirstOrDefault();
        var firstImagePath = firstSource.Value?.Filename ?? firstSource.Value?.FilePath ?? "";

        // 1. Load models (UNet, CLIP[flux2], VAE)
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "flux-2-klein-base-9b-fp8.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
            ClipType = "flux2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "full_encoder_small_decoder.safetensors"
        });

        // 2. Load LoRAs (if any) - modifies model_output and clip_output
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 3. Encode prompts (reads LoRA-modified clip_output)
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        // 4. Chain reference images through ReferenceLatent
        // Each call wraps positive_output and negative_output with the encoded reference latent
        for (var i = 0; i < referenceImages.Count; i++)
        {
            var source = referenceImages[i].Value;
            var imagePath = source.Filename ?? source.FilePath ?? "";

            _referenceLatentFragment.Build(builder, registry, new ReferenceLatentFragment.Parameters
            {
                Image = imagePath,
                Index = i,
                UpscaleMethod = "lanczos",
                Megapixels = referenceLatentSettings?.GetDouble("megapixels", 1.0) ?? 1.0,
                ResolutionSteps = referenceLatentSettings?.GetInt("resolution_steps", 1) ?? 1
            });
        }

        // 5. Get size refs from first reference image (registered by ReferenceLatentFragment index 0)
        var widthRef = registry.GetRef("image_size_width");
        var heightRef = registry.GetRef("image_size_height");

        // 6. Create empty latent (Flux2 latent space) using reference image dimensions
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            BatchSize = 1,
            LatentClass = "EmptyFlux2LatentImage",
            WidthRef = widthRef,
            HeightRef = heightRef
        });

        // 7. Sample (SamplerCustomAdvanced pipeline) using reference image dimensions
        var resolvedSeed = samplerFragment?.GetLong("seed", -1) ?? -1;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerCustomAdvancedFragment.Build(builder, registry, new SamplerCustomAdvancedFragment.Parameters
        {
            SamplerId = "sampler_main",
            SamplerName = samplerFragment?.GetString("sampler_name", "euler") ?? "euler",
            Steps = samplerFragment?.GetInt("steps", 20) ?? 20,
            Cfg = samplerFragment?.GetDouble("cfg", 5.0) ?? 5.0,
            Seed = resolvedSeed,
            WidthRef = widthRef,
            HeightRef = heightRef
        });

        // 8. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 9. SeedVR2 Upscale (conditional)
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

        // 10. Detailer (conditional) - supports chained passes
        var detailerFragment = parameters.GetFragment("detailer");
        if (detailerFragment?.IsActive == true)
        {
            var passCount = Math.Max(1, detailerFragment.GetInt("pass_count", 1));
            for (int i = 0; i < passCount; i++)
            {
                var scope = i == 0 ? "detailer_" : $"detailer_{i}_";
                var scopeTitle = i == 0 ? "Detailer " : $"Detailer {i + 1} ";
                var kp = i == 0 ? string.Empty : $"pass_{i}_";

                _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
                {
                    UnetName = detailerFragment.GetString($"{kp}detailer_checkpoint")
                               ?? parameters.Assets?.GetValueOrDefault("Model")
                               ?? "flux-2-klein-base-9b-fp8.safetensors",
                    ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
                    ClipType = "flux2",
                    VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "full_encoder_small_decoder.safetensors",
                    Positive = detailerFragment.GetStringOrFallback($"{kp}detailer_prompt",
                                     promptsFragment?.GetString("positive", "") ?? ""),
                    Negative = detailerFragment.GetStringOrFallback($"{kp}detailer_negative_prompt",
                                     promptsFragment?.GetString("negative", "") ?? "")
                }, scope: scope, scopeTitle: scopeTitle);

                _loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);

                _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
                {
                    Scope = scope,
                    DetectionModel = detailerFragment.GetString($"{kp}detailer_detection_model", "bbox/face_yolov8m.pt"),
                    Sampler = detailerFragment.GetString($"{kp}detailer_sampler", "dpmpp_2m"),
                    Scheduler = detailerFragment.GetString($"{kp}detailer_scheduler", "simple"),
                    Seed = detailerFragment.GetLong($"{kp}detailer_seed", resolvedSeed),
                    Steps = detailerFragment.GetInt($"{kp}detailer_steps", 20),
                    Cfg = detailerFragment.GetDouble($"{kp}detailer_cfg", 5.0),
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

        // 11. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
