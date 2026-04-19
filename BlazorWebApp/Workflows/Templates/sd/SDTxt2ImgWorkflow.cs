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
    private readonly SamplerFragment _samplerFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly UpscaleFragment _upscaleFragment = new();
    private readonly SeedVR2UpscaleFragment _seedVR2UpscaleFragment = new();

    // Detailer fragments
    private readonly DetailerFragment _detailerFragment = new();

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

        // 7. Detailer (conditional) - uses LoadCheckpoint for its own scoped model
        var detailerFragment = parameters.GetFragment("detailer");
        if (detailerFragment?.IsActive == true)
        {
            _loadCheckpointFragment.Build(builder, registry, new LoadCheckpointFragment.Parameters
            {
                LoaderId = "model_loader",
                CheckpointName = detailerFragment.GetString("detailer_checkpoint")
                                 ?? parameters.Assets?.GetValueOrDefault("Model")
                                 ?? "Base/v1-5-pruned-emaonly.safetensors",
                Positive = detailerFragment.GetString("detailer_prompt")
                           ?? promptsFragment?.GetString("positive", "") ?? "",
                Negative = detailerFragment.GetString("detailer_negative_prompt")
                           ?? promptsFragment?.GetString("negative", "") ?? ""
            }, scope: "detailer_", scopeTitle: "Detailer ");

            _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
            {
                Scope = "detailer_",
                DetectionModel = detailerFragment.GetString("detailer_detection_model", "bbox/face_yolov8m.pt"),
                Sampler = detailerFragment.GetString("detailer_sampler", "dpmpp_2m"),
                Scheduler = detailerFragment.GetString("detailer_scheduler", "beta"),
                Seed = detailerFragment.GetLong("detailer_seed", 42),
                Steps = detailerFragment.GetInt("detailer_steps", 20),
                Cfg = detailerFragment.GetDouble("detailer_cfg", 8.0),
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

        // 8. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
