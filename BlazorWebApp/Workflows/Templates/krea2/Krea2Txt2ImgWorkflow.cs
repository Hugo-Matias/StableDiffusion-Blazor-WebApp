using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Krea2;

/// <summary>
/// Krea2 Turbo Txt2Img workflow using the fluent builder API.
/// Uses the ClownsharKSampler_Beta (standard sampler) pipeline, aligned to the Chroma baseline.
/// Pipeline: LoadDiffusionWithPrompts -> LoRA -> EmptyLatent -> SamplerFragment -> VaeDecode -> [SeedVR2] -> [UltimateSDUpscale] -> [Detailer] -> Save
/// </summary>
public class Krea2Txt2ImgWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly SamplerFragment _samplerFragment = new()
    {
        Defaults = new() { Steps = 8, Cfg = 1.0, SamplerName = "euler", Scheduler = "simple" }
    };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();
    private readonly UltimateSdUpscaleFragment _ultimateSdUpscaleFragment = new();

    // Detailer fragments
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Krea2 Turbo T2I",
        Description = "Krea 2 Turbo text-to-image. Fast 8-step generation using ClownsharKSampler. Uses the krea2 CLIP encoder and standard latent space. Add SeedVR2 for quality upscaling.",
        Base = Data.Enums.ModelBase.Krea2,
        Mode = ModeType.Txt2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "krea2_turbo_fp8_scaled.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen3vl_4b_fp8_scaled.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "qwen_image_vae.safetensors",
                Order = 3,
                ColumnSize = 4
            }
        ],
        CompatibleResourceBaseModels = ["Krea 2"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerFragment;
        yield return _seedVR2UpscaleFragment;
        yield return _ultimateSdUpscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");
        var latentFragment = parameters.GetFragment("latent");

        var positive = promptsFragment?.GetString("positive", "") ?? "";
        var negative = promptsFragment?.GetString("negative", "") ?? "";

        var width = latentFragment?.GetInt("width", 1024) ?? 1024;
        var height = latentFragment?.GetInt("height", 1024) ?? 1024;
        var batchSize = latentFragment?.GetInt("batch_size", 1) ?? 1;

        // 1. Load models (UNet, CLIP, VAE) with prompt encoding
        _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "krea2_turbo_fp8_scaled.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen3vl_4b_fp8_scaled.safetensors",
            ClipType = "krea2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "qwen_image_vae.safetensors",
            Positive = positive,
            Negative = negative,
            UsePCLazyLoader = false,
            UseConditioningZeroOut = true
        });

        // 2. Load LoRAs (if any) - model-only to avoid patching the 4B Qwen3VL CLIP
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras, modelOnly: true);

        // 3. Create empty latent (standard SD latent space)
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = width,
            Height = height,
            BatchSize = batchSize,
            LatentClass = "EmptyLatentImage"
        });

        // 4. Sample (ClownsharKSampler_Beta)
        var resolvedSeed = samplerFragment?.GetLong("seed", -1) ?? -1;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = "sampler_main",
            Title = "Main Sampler",
            SamplerName = samplerFragment?.GetString("sampler_name", _samplerFragment.Defaults.SamplerName) ?? _samplerFragment.Defaults.SamplerName,
            Scheduler = samplerFragment?.GetString("scheduler", _samplerFragment.Defaults.Scheduler) ?? _samplerFragment.Defaults.Scheduler,
            Steps = samplerFragment?.GetInt("steps", _samplerFragment.Defaults.Steps) ?? _samplerFragment.Defaults.Steps,
            Cfg = samplerFragment?.GetDouble("cfg", _samplerFragment.Defaults.Cfg) ?? _samplerFragment.Defaults.Cfg,
            Denoise = samplerFragment?.GetDouble("denoise", _samplerFragment.Defaults.Denoise) ?? _samplerFragment.Defaults.Denoise,
            Eta = samplerFragment?.GetDouble("eta", _samplerFragment.Defaults.Eta) ?? _samplerFragment.Defaults.Eta,
            Seed = resolvedSeed,
            ClassType = "ClownsharKSampler_Beta",
            Bongmath = false
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

        // 8. Ultimate SD Upscale (conditional)
        _ultimateSdUpscaleFragment.Build(builder, parameters, registry);

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

                var passEnabledKey = i == 0 ? "detailer_enabled" : $"pass_{i}_detailer_enabled";
                if (!detailerFragment.GetBool(passEnabledKey, true))
                    continue;

                _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
                {
                    UnetName = detailerFragment.GetString($"{kp}detailer_checkpoint")
                               ?? parameters.Assets?.GetValueOrDefault("Model")
                               ?? "krea2_turbo_fp8_scaled.safetensors",
                    ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen3vl_4b_fp8_scaled.safetensors",
                    ClipType = "krea2",
                    VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "qwen_image_vae.safetensors",
                    Positive = detailerFragment.GetStringOrFallback($"{kp}detailer_prompt", positive),
                    Negative = detailerFragment.GetStringOrFallback($"{kp}detailer_negative_prompt", negative),
                    Width = width,
                    Height = height,
                    BatchSize = batchSize,
                    UsePCLazyLoader = false,
                    UseConditioningZeroOut = true
                }, scope: scope, scopeTitle: scopeTitle);

                _loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope, modelOnly: true);

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

        // 10. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
