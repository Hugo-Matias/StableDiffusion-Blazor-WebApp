using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Enhancements;
using BlazorWebApp.Workflows.Fragments.Flux;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Flux;

/// <summary>
/// Flux Txt2Img workflow implementation using the fluent builder API.
/// Generates images from text prompts using Flux models with dual CLIP encoding and ReFlux.
/// </summary>
public class FluxTxt2ImgWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadFluxFragment _loadFluxFragment = new();
    private readonly SamplerFragment _samplerFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Enhancement fragments
    private readonly UpscaleFragment _upscaleFragment = new();
    private readonly DetailerFragment _detailerFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();

    // UI fragments (prompts handled internally by LoadFluxFragment for encoding)
    private readonly PromptsFragment _promptsFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img",
        Base = Data.Enums.ModelBase.Flux,
        Mode = ModeType.Txt2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "flux1-krea-dev_fp8_scaled.safetensors",
                Order = 1,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Clip1",
                Label = "CLIP T5",
                Type = AssetType.Clip,
                DefaultValue = "t5xxl_fp8_e4m3fn_scaled.safetensors",
                Order = 2,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "Clip2",
                Label = "CLIP ViT",
                Type = AssetType.Clip,
                DefaultValue = "ViT-L-14-BEST-smooth-GmP-TE-only-HF-format.safetensors",
                Order = 3,
                ColumnSize = 3
            },
            new WorkflowAsset
            {
                Parameter = "VAE",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "ae.safetensors",
                Order = 4,
                ColumnSize = 3
            }
        ],
        CompatibleResourceBaseModels = ["Flux.1 S", "Flux.1 D", "Flux.1 Krea"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        // Return fragments in UI order for display
        // Note: LoadFluxFragment handles prompt encoding internally,
        // but PromptsFragment is needed for the UI to collect prompt text
        yield return _promptsFragment;
        yield return _emptyLatentFragment;  // For resolution UI
        yield return _samplerFragment;
        yield return _upscaleFragment;
        yield return _detailerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Get fragment parameters for reuse
        var latentFragment = parameters.GetFragment("latent");
        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");

        // Extract common values
        var width = latentFragment?.GetInt("width", 872) ?? 872;
        var height = latentFragment?.GetInt("height", 1248) ?? 1248;
        var batchSize = latentFragment?.GetInt("batch_size", 1) ?? 1;
        var positive = promptsFragment?.GetString("positive", "") ?? "";
        var seed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);

        // 1. Load Flux models (UNet, Dual CLIPs, VAE) and encode prompts
        _loadFluxFragment.Build(builder, registry, new LoadFluxFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "flux1-krea-dev_fp8_scaled.safetensors",
            ClipName1 = parameters.Assets?.GetValueOrDefault("Clip1") ?? "t5xxl_fp8_e4m3fn_scaled.safetensors",
            ClipName2 = parameters.Assets?.GetValueOrDefault("Clip2") ?? "ViT-L-14-BEST-smooth-GmP-TE-only-HF-format.safetensors",
            VaeName = parameters.Assets?.GetValueOrDefault("VAE") ?? "ae.safetensors",
            Positive = positive,
            Guidance = samplerFragment?.GetDouble("guidance", 3.5) ?? 3.5,
            RefluxEnabled = false,
            Scaling = "exponential",
            MaxShift = 1.35,
            BaseShift = 0.85,
            Width = width,
            Height = height,
            BatchSize = batchSize
        });

        // 2. Sample
        _samplerFragment.Build(builder, registry, new SamplerFragment.Parameters
        {
            SamplerId = samplerFragment?.GetString("sampler_id", "sampler_main") ?? "sampler_main",
            Title = samplerFragment?.GetString("title", "Main Sampler") ?? "Main Sampler",
            SamplerName = samplerFragment?.GetString("sampler_name", "multistep/res_2m") ?? "multistep/res_2m",
            Scheduler = samplerFragment?.GetString("scheduler", "beta") ?? "beta",
            Steps = samplerFragment?.GetInt("steps", 20) ?? 20,
            Cfg = 1, // Flux uses CFG 1 with guidance in FluxGuidance node
            Denoise = samplerFragment?.GetDouble("denoise", 1.0) ?? 1.0,
            Eta = samplerFragment?.GetDouble("eta", 0.5) ?? 0.5,
            Seed = seed,
            ClassType = "ClownsharKSampler_Beta"
        });

        // 3. Upscale (conditional)
        var upscaleFragmentParams = parameters.GetFragment("upscale");
        if (upscaleFragmentParams?.IsActive == true)
        {
            _upscaleFragment.Build(builder, registry, new UpscaleFragment.Parameters
            {
                UpscaleModel = upscaleFragmentParams.GetString("upscale_model", "4x-UltraSharpV2.safetensors"),
                UpscaleWidth = upscaleFragmentParams.GetInt("upscale_width", 0),
                UpscaleHeight = upscaleFragmentParams.GetInt("upscale_height", 0),
                UpscaleSteps = upscaleFragmentParams.GetInt("upscale_steps", 20),
                UpscaleDenoise = upscaleFragmentParams.GetDouble("upscale_denoise", 1.0),
                Scale = upscaleFragmentParams.GetDouble("upscale_scale", 2.0),
                SamplerName = samplerFragment?.GetString("sampler_name", "multistep/res_2m") ?? "multistep/res_2m",
                Scheduler = samplerFragment?.GetString("scheduler", "beta") ?? "beta",
                Cfg = 1, // Flux uses CFG 1
                Seed = seed,
                Scope = "",
                LatentWidth = width,
                LatentHeight = height
            });
        }

        // 4. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 5. Detailer (conditional) - requires its own model loader. Supports chained passes.
        var detailerFragmentParams = parameters.GetFragment("detailer");
        if (detailerFragmentParams?.IsActive == true)
        {
            var passCount = Math.Max(1, detailerFragmentParams.GetInt("pass_count", 1));
            for (int i = 0; i < passCount; i++)
            {
                var scope = i == 0 ? "detailer_" : $"detailer_{i}_";
                var scopeTitle = i == 0 ? "Detailer " : $"Detailer {i + 1} ";
                var kp = i == 0 ? string.Empty : $"pass_{i}_";

                var detailerPrompt = detailerFragmentParams.GetStringOrFallback($"{kp}detailer_prompt", positive);

                _loadFluxFragment.Build(builder, registry, new LoadFluxFragment.Parameters
                {
                    UnetName = detailerFragmentParams.GetString($"{kp}detailer_checkpoint")
                               ?? parameters.Assets?.GetValueOrDefault("Model")
                               ?? "flux1-krea-dev_fp8_scaled.safetensors",
                    ClipName1 = parameters.Assets?.GetValueOrDefault("Clip1") ?? "t5xxl_fp8_e4m3fn_scaled.safetensors",
                    ClipName2 = parameters.Assets?.GetValueOrDefault("Clip2") ?? "ViT-L-14-BEST-smooth-GmP-TE-only-HF-format.safetensors",
                    VaeName = parameters.Assets?.GetValueOrDefault("VAE") ?? "ae.safetensors",
                    Positive = detailerPrompt,
                    Guidance = samplerFragment?.GetDouble("guidance", 3.5) ?? 3.5,
                    RefluxEnabled = false,
                    Scaling = "exponential",
                    MaxShift = 1.35,
                    BaseShift = 0.85,
                    Width = width,
                    Height = height,
                    BatchSize = batchSize
                }, scope: scope, scopeTitle: scopeTitle);

                _loraLoaderFragment.BuildAll(builder, registry, parameters.GetDetailerLoras(i), scope: scope);

                _detailerFragment.Build(builder, registry, new DetailerFragment.Parameters
                {
                    Scope = scope,
                    DetectionModel = detailerFragmentParams.GetString($"{kp}detailer_detection_model", "bbox/face_yolov8m.pt"),
                    Sampler = detailerFragmentParams.GetString($"{kp}detailer_sampler", "dpmpp_2m"),
                    Scheduler = detailerFragmentParams.GetString($"{kp}detailer_scheduler", "beta"),
                    Seed = detailerFragmentParams.GetLong($"{kp}detailer_seed", seed),
                    Steps = detailerFragmentParams.GetInt($"{kp}detailer_steps", 20),
                    Cfg = detailerFragmentParams.GetDouble($"{kp}detailer_cfg", 1.0),
                    Denoise = detailerFragmentParams.GetDouble($"{kp}detailer_denoise", 0.65),
                    Feather = detailerFragmentParams.GetInt($"{kp}detailer_feather", 5),
                    BboxThreshold = detailerFragmentParams.GetDouble($"{kp}detailer_bbox_threshold", 0.7),
                    BboxDilation = detailerFragmentParams.GetInt($"{kp}detailer_bbox_dilation", 10),
                    BboxCropFactor = detailerFragmentParams.GetDouble($"{kp}detailer_bbox_crop_factor", 3.0),
                    DropSize = detailerFragmentParams.GetInt($"{kp}detailer_drop_size", 70),
                    GuideSize = detailerFragmentParams.GetInt($"{kp}detailer_guide_size", 512),
                    MaxSize = detailerFragmentParams.GetInt($"{kp}detailer_max_size", 1024),
                    Cycle = detailerFragmentParams.GetInt($"{kp}detailer_cycle", 1)
                });
            }
        }

        // 6. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
