using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Loaders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Chroma;

/// <summary>
/// Chroma Txt2Img workflow implementation using the fluent builder API.
/// Chroma is similar to Flux but uses a single T5 CLIP (type "chroma") with T5TokenizerOptions wrapping.
/// Pipeline: LoadDiffusionWithPrompts (T5Tokenizer) -> EmptyLatent(SD3) -> Sample -> [Upscale] -> VaeDecode -> [Detailer] -> Save
/// </summary>
public class ChromaTxt2ImgWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly PromptsFragment _promptsFragment = new();
    private readonly LoadDiffusionWithPromptsFragment _loadDiffusionWithPromptsFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly SamplerFragment _samplerFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly UpscaleFragment _upscaleFragment = new();

    // Detailer fragments
    private readonly DetailerFragment _detailerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img",
        Base = Data.Enums.ModelBase.Chroma,
        Mode = ModeType.Txt2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "Chroma1-HD.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip1",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "t5xxl_fp8_e4m3fn_scaled.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "VAE",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "ae.safetensors",
                Order = 3,
                ColumnSize = 4
            }
        ],
        CompatibleResourceBaseModels = ["Chroma"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerFragment;
        yield return _upscaleFragment;
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

        // 1. Load Chroma models (UNet + CLIP chroma + T5TokenizerOptions + VAE) with prompts
        _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "Chroma1-HD.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip1") ?? "t5xxl_fp8_e4m3fn_scaled.safetensors",
            ClipType = "chroma",
            VaeName = parameters.Assets?.GetValueOrDefault("VAE") ?? "ae.safetensors",
            Positive = positive,
            Negative = negative,
            UseT5Tokenizer = true
        });

        // 2. Create empty latent (Chroma uses EmptySD3LatentImage)
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = latentFragment?.GetInt("width", 872) ?? 872,
            Height = latentFragment?.GetInt("height", 1248) ?? 1248,
            BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1,
            LatentClass = "EmptySD3LatentImage"
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
                Cfg = 1, // Chroma uses CFG 1 for upscale
                Seed = resolvedSeed,
                LatentWidth = latentFragment?.GetInt("width", 872) ?? 872,
                LatentHeight = latentFragment?.GetInt("height", 1248) ?? 1248
            });
        }

        // 5. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 6. Detailer (conditional)
        var detailerFragmentData = parameters.GetFragment("detailer");
        if (detailerFragmentData?.IsActive == true)
        {
            // Load separate models for detailer with detailer_ scope
            var detailerPrompt = detailerFragmentData.GetString("detailer_prompt") ?? positive;
            var detailerNegative = detailerFragmentData.GetString("detailer_negative_prompt") ?? negative;

            _loadDiffusionWithPromptsFragment.Build(builder, registry, new LoadDiffusionWithPromptsFragment.Parameters
            {
                UnetName = detailerFragmentData.GetString("detailer_checkpoint")
                           ?? parameters.Assets?.GetValueOrDefault("Model")
                           ?? "Chroma1-HD.safetensors",
                ClipName = parameters.Assets?.GetValueOrDefault("Clip1") ?? "t5xxl_fp8_e4m3fn_scaled.safetensors",
                ClipType = "chroma",
                VaeName = parameters.Assets?.GetValueOrDefault("VAE") ?? "ae.safetensors",
                Positive = detailerPrompt,
                Negative = detailerNegative,
                UseT5Tokenizer = true
            }, scope: "detailer_", scopeTitle: "Detailer ");

            _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
            {
                Scope = "detailer_",
                DetectionModel = detailerFragmentData.GetString("detailer_detection_model", "bbox/face_yolov8m.pt"),
                Sampler = detailerFragmentData.GetString("detailer_sampler", "dpmpp_2m"),
                Scheduler = detailerFragmentData.GetString("detailer_scheduler", "beta"),
                Seed = detailerFragmentData.GetLong("detailer_seed", resolvedSeed),
                Steps = detailerFragmentData.GetInt("detailer_steps", 20),
                Cfg = detailerFragmentData.GetDouble("detailer_cfg", 8.0),
                Denoise = detailerFragmentData.GetDouble("detailer_denoise", 0.65),
                Feather = detailerFragmentData.GetInt("detailer_feather", 5),
                BboxThreshold = detailerFragmentData.GetDouble("detailer_bbox_threshold", 0.7),
                BboxDilation = detailerFragmentData.GetInt("detailer_bbox_dilation", 10),
                BboxCropFactor = detailerFragmentData.GetDouble("detailer_bbox_crop_factor", 3.0),
                DropSize = detailerFragmentData.GetInt("detailer_drop_size", 70),
                GuideSize = detailerFragmentData.GetInt("detailer_guide_size", 512),
                MaxSize = detailerFragmentData.GetInt("detailer_max_size", 1024),
                Cycle = detailerFragmentData.GetInt("detailer_cycle", 1)
            });
        }

        // 7. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
