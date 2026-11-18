using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.SD;

/// <summary>
/// StableDiffusion Txt2Img workflow implementation using the fluent builder API.
/// Uses CheckpointLoaderSimple (single file loads model+clip+vae) with PCLazyLoraLoader
/// for LoRA scheduling and PCLazyTextEncode for prompt encoding.
/// Pipeline: LoadCheckpoint -> EmptyLatent -> Sample -> [Upscale] -> VaeDecode -> [SeedVR2] -> [Detailer] -> Save
/// </summary>
public class SDTxt2ImgWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly PromptsFragment _promptsFragment = new();
    private readonly LoadCheckpointFragment _loadCheckpointFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly SamplerFragment _samplerFragment = new() { Defaults = new() { Cfg = 5.5 } };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly UpscaleFragment _upscaleFragment = new();
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();

    // Detailer fragments
    private readonly DetailerFragment _detailerFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img",
        Base = Data.Enums.ModelBase.StableDiffusion,
        Mode = ModeType.Txt2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.CheckpointModel,
                DefaultValue = "Base/v1-5-pruned-emaonly.safetensors",
                Order = 1,
                ColumnSize = 6
            }
        ],
        CompatibleResourceBaseModels =
        [
            "SD 1.4", "SD 1.5", "SD 1.5 LCM", "SD 1.5 Hyper",
            "SD 2.0", "SD 2.0 768", "SD 2.1", "SD 2.1 768", "SD 2.1 Unclip",
            "SDXL 0.9", "SDXL 1.0", "SDXL 1.0 LCM", "SDXL Lightning", "SDXL Hyper", "SDXL Distilled",
            "Pony", "Illustrious", "NoobAI"
        ]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerFragment;
        yield return _upscaleFragment;
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

        // 1. Load checkpoint (model + clip + vae + prompts + LoRA loaders)
        _loadCheckpointFragment.Build(builder, registry, new LoadCheckpointFragment.Parameters
        {
            LoaderId = "model_loader",
            CheckpointName = parameters.Assets?.GetValueOrDefault("Model")
                             ?? "Base/v1-5-pruned-emaonly.safetensors",
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        // 2. Create empty latent (SD uses EmptyLatentImage, not SD3)
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = latentFragment?.GetInt("width", 512) ?? 512,
            Height = latentFragment?.GetInt("height", 768) ?? 768,
            BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1,
            LatentClass = "EmptyLatentImage"
        });

        // 3. Sample
        var resolvedSeed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = "sampler_main",
            Title = "Main Sampler",
            SamplerName = samplerFragment?.GetString("sampler_name", "multistep/res_2m") ?? "multistep/res_2m",
            Scheduler = samplerFragment?.GetString("scheduler", "beta") ?? "beta",
            Steps = samplerFragment?.GetInt("steps", 20) ?? 20,
            Cfg = samplerFragment?.GetDouble("cfg", 5.5) ?? 5.5,
            Denoise = samplerFragment?.GetDouble("denoise", 1.0) ?? 1.0,
            Eta = samplerFragment?.GetDouble("eta", 0.5) ?? 0.5,
            Seed = resolvedSeed,
            ClassType = "ClownsharKSampler_Beta"
        });

        // 4. Upscale (conditional)
        var upscaleFragmentData = parameters.GetFragment("upscale");
        if (upscaleFragmentData?.IsActive == true)
        {
            _upscaleFragment.Build(builder, registry, new UpscaleFragment.Parameters
            {
                UpscaleModel = upscaleFragmentData.GetString("upscale_model", "4x-UltraSharpV2.safetensors"),
                UpscaleWidth = upscaleFragmentData.GetInt("upscale_width", 0),
                UpscaleHeight = upscaleFragmentData.GetInt("upscale_height", 0),
                UpscaleSteps = upscaleFragmentData.GetInt("upscale_steps", 20),
                UpscaleDenoise = upscaleFragmentData.GetDouble("upscale_denoise", 1.0),
                Scale = upscaleFragmentData.GetDouble("upscale_scale", 2.0),
                SamplerName = samplerFragment?.GetString("sampler_name", "multistep/res_2m") ?? "multistep/res_2m",
                Scheduler = samplerFragment?.GetString("scheduler", "beta") ?? "beta",
                Cfg = samplerFragment?.GetDouble("cfg", 5.5) ?? 5.5,
                Seed = resolvedSeed,
                LatentWidth = latentFragment?.GetInt("width", 512) ?? 512,
                LatentHeight = latentFragment?.GetInt("height", 768) ?? 768
            });
        }

        // 5. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 6. SeedVR2 Upscale (conditional)
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

        // 7. Detailer (conditional) - uses LoadCheckpoint for its own scoped model.
        // Supports chained passes: each iteration overwrites image_output so the next pass
        // continues from where the previous one left off.
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

                _loadCheckpointFragment.Build(builder, registry, new LoadCheckpointFragment.Parameters
                {
                    LoaderId = "model_loader",
                    CheckpointName = detailerFragment.GetString($"{kp}detailer_checkpoint")
                                     ?? parameters.Assets?.GetValueOrDefault("Model")
                                     ?? "Base/v1-5-pruned-emaonly.safetensors",
                    Positive = detailerFragment.GetStringOrFallback($"{kp}detailer_prompt",
                                     promptsFragment?.GetString("positive", "") ?? ""),
                    Negative = detailerFragment.GetStringOrFallback($"{kp}detailer_negative_prompt",
                                     promptsFragment?.GetString("negative", "") ?? "")
                }, scope: scope, scopeTitle: scopeTitle);

                // Detailer-scoped LoRAs for this pass (independent of main Loras and other passes)
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

        // 8. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
