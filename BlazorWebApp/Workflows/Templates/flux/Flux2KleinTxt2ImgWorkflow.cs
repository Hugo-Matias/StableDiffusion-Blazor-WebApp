using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Flux;

/// <summary>
/// Flux2 Klein Txt2Img workflow implementation using the fluent builder API.
/// Uses the SamplerCustomAdvanced pipeline with Flux2Scheduler, CFGGuider, and RandomNoise.
/// Pipeline: LoadDiffusion -> LoRA -> EmptyLatent -> Prompts -> SamplerCustomAdvanced -> VaeDecode -> [SeedVR2] -> [Detailer] -> Save
/// </summary>
public class Flux2KleinTxt2ImgWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly SamplerCustomAdvancedFragment _samplerCustomAdvancedFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();

    // Detailer fragments
    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Flux2 Klein T2I",
        Base = Data.Enums.ModelBase.Flux,
        Mode = ModeType.Txt2Img,
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
        CompatibleResourceBaseModels = ["Flux.2 D", "Flux.2 Klein 9B", "Flux.2 Klein 9B-base", "Flux.2 Klein 4B", "Flux.2 Klein 4B-base"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerCustomAdvancedFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");
        var latentFragment = parameters.GetFragment("latent");

        var width = latentFragment?.GetInt("width", 1024) ?? 1024;
        var height = latentFragment?.GetInt("height", 1024) ?? 1024;
        var batchSize = latentFragment?.GetInt("batch_size", 1) ?? 1;

        // 1. Load models (UNet, CLIP, VAE)
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "flux-2-klein-base-9b-fp8.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
            ClipType = "flux2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "full_encoder_small_decoder.safetensors"
        });

        // 2. Load LoRAs (if any)
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 3. Create empty latent (Flux2 latent space)
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = width,
            Height = height,
            BatchSize = batchSize,
            LatentClass = "EmptyFlux2LatentImage"
        });

        // 4. Encode prompts
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        // 5. Sample (SamplerCustomAdvanced pipeline)
        var resolvedSeed = samplerFragment?.GetLong("seed", -1) ?? -1;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerCustomAdvancedFragment.Build(builder, registry, new SamplerCustomAdvancedFragment.Parameters
        {
            SamplerId = "sampler_main",
            SamplerName = samplerFragment?.GetString("sampler_name", "euler") ?? "euler",
            Steps = samplerFragment?.GetInt("steps", 20) ?? 20,
            Cfg = samplerFragment?.GetDouble("cfg", 5.0) ?? 5.0,
            Seed = resolvedSeed,
            Width = width,
            Height = height
        });

        // 6. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 7. SeedVR2 Upscale (conditional)
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

        // 8. Detailer (conditional)
        var detailerFragment = parameters.GetFragment("detailer");
        if (detailerFragment?.IsActive == true)
        {
            _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
            {
                UnetName = detailerFragment.GetString("detailer_checkpoint")
                           ?? parameters.Assets?.GetValueOrDefault("Model")
                           ?? "flux-2-klein-base-9b-fp8.safetensors",
                ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_8b_fp8mixed.safetensors",
                ClipType = "flux2",
                VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "full_encoder_small_decoder.safetensors",
                Positive = detailerFragment.GetString("detailer_prompt")
                           ?? promptsFragment?.GetString("positive", "") ?? "",
                Negative = detailerFragment.GetString("detailer_negative_prompt")
                           ?? promptsFragment?.GetString("negative", "") ?? "",
                Width = width,
                Height = height,
                BatchSize = batchSize
            }, scope: "detailer_", scopeTitle: "Detailer ");

            _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
            {
                Scope = "detailer_",
                DetectionModel = detailerFragment.GetString("detailer_detection_model", "bbox/face_yolov8m.pt"),
                Sampler = detailerFragment.GetString("detailer_sampler", "dpmpp_2m"),
                Scheduler = detailerFragment.GetString("detailer_scheduler", "simple"),
                Seed = detailerFragment.GetLong("detailer_seed", resolvedSeed),
                Steps = detailerFragment.GetInt("detailer_steps", 20),
                Cfg = detailerFragment.GetDouble("detailer_cfg", 5.0),
                Denoise = detailerFragment.GetDouble("detailer_denoise", 0.65),
                Feather = detailerFragment.GetInt("detailer_feather", 5),
                BboxThreshold = detailerFragment.GetDouble("detailer_bbox_threshold", 0.7),
                BboxDilation = detailerFragment.GetInt("detailer_bbox_dilation", 10),
                BboxCropFactor = detailerFragment.GetDouble("detailer_bbox_crop_factor", 3.0),
                DropSize = detailerFragment.GetInt("detailer_drop_size", 70),
                GuideSize = detailerFragment.GetInt("detailer_guide_size", 512),
                MaxSize = detailerFragment.GetInt("detailer_max_size", 1024),
                Cycle = detailerFragment.GetInt("detailer_cycle", 1)
            });
        }

        // 9. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
