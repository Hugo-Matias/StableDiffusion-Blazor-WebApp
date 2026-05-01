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
/// Z-Image to ZIT Txt2Img Upscale workflow implementation using the fluent builder API.
/// This is a two-stage pipeline that chains Z-Image Base generation into Z-Image Turbo upscale
/// with ControlNet tile guidance, producing high-resolution, structurally consistent output.
/// </summary>
public class ZImageTxt2ImgUpscaleWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly LoadDiffusionFragment _loadDiffusionFragment = new();
    private readonly VaeMergeFragment _vaeMergeFragment = new();
    private readonly EmptyLatentFragment _emptyLatentFragment = new();
    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly SamplerFragment _samplerFragment = new() { Defaults = new() { Steps = 9, Cfg = 1.0 } };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    // Stage 2 fragments (Upscale pipeline)
    private readonly TilePreprocessorFragment _tilePreprocessorFragment = new();
    private readonly ModelPatchLoaderFragment _modelPatchLoaderFragment = new();
    private readonly UpscaleModelLoaderFragment _upscaleModelLoaderFragment = new();
    private readonly ZImageUpscaleFragment _zImageUpscaleFragment = new();

    // Enhancement fragments
    private readonly SeedVarianceEnhancerFragment _seedVarianceEnhancerFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img Upscale",
        Base = Data.Enums.ModelBase.ZImage,
        Mode = ModeType.Txt2Img,
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
            },
            new WorkflowAsset
            {
                Parameter = "VaeMergeA",
                Label = "VAE Merge A",
                Type = AssetType.Vae,
                DefaultValue = "Z-image-ae.safetensors",
                Order = 4,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "VaeMergeB",
                Label = "VAE Merge B",
                Type = AssetType.Vae,
                DefaultValue = "Ultra_flux_For_Z-image-vae.safetensors",
                Order = 5,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "ControlNet",
                Label = "ControlNet",
                Type = AssetType.ControlNet,
                DefaultValue = "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors",
                Order = 6,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "UpscaleModel",
                Label = "Upscale Model",
                Type = AssetType.UpscaleModel,
                DefaultValue = "x1_ITF_SkinDiffDetail_Lite_v1.pth",
                Order = 7,
                ColumnSize = 4
            }
        ],
        CompatibleResourceBaseModels = ["ZImageTurbo", "ZImageBase"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        // Return fragments in UI order for display
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerFragment;
        yield return _seedVarianceEnhancerFragment;
        yield return _zImageUpscaleFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        // Get fragment parameters for reuse
        var latentFragment = parameters.GetFragment("latent");
        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");

        // === STAGE 1: Base Generation ===

        // 1. Load models (UNet, CLIP, VAE)
        _loadDiffusionFragment.Build(builder, registry, new LoadDiffusionFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "z_image_turbo_bf16.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_3_4b.safetensors",
            ClipType = "lumina2",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "ae.safetensors"
        });

        // 2. VAE Merge (dual VAE merge - overwrites vae_output)
        _vaeMergeFragment.Build(builder, registry, new VaeMergeFragment.Parameters
        {
            VaeAName = parameters.Assets?.GetValueOrDefault("VaeMergeA") ?? "Z-image-ae.safetensors",
            VaeBName = parameters.Assets?.GetValueOrDefault("VaeMergeB") ?? "Ultra_flux_For_Z-image-vae.safetensors",
            Ratio = 0.3f
        });

        // 3. Create empty latent
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = latentFragment?.GetInt("width", 872) ?? 872,
            Height = latentFragment?.GetInt("height", 1248) ?? 1248,
            BatchSize = latentFragment?.GetInt("batch_size", 1) ?? 1,
            LatentClass = "EmptySD3LatentImage"
        });

        // 4. Load LoRAs (if any)
        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        // 5. Encode prompts
        _promptsFragment.Build(builder, registry, new PromptsFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? ""
        });

        // 6. Seed Variance Enhancer (conditional)
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

        // 7. Sample (Stage 1 - ClownsharKSampler)
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
            Denoise = samplerFragment?.GetDouble("denoise", 1.0) ?? 1.0,
            Eta = samplerFragment?.GetDouble("eta", 0.5) ?? 0.5,
            Seed = resolvedSeed,
            ClassType = "ClownsharKSampler_Beta"
        });

        // 8. VAE Decode (Stage 1 output)
        _vaeDecodeFragment.Build(builder, registry);

        // === STAGE 2: Upscale Pipeline ===

        // 9. Tile Preprocessor (prepare ControlNet input from Stage 1 image)
        _tilePreprocessorFragment.Build(builder, registry, new TilePreprocessorFragment.Parameters
        {
            Resolution = 1024
        });

        // 10. Model Patch Loader (load ControlNet)
        _modelPatchLoaderFragment.Build(builder, registry, new ModelPatchLoaderFragment.Parameters
        {
            PatchName = parameters.Assets?.GetValueOrDefault("ControlNet") ?? "Z-Image-Turbo-Fun-Controlnet-Tile-2.1-8steps.safetensors"
        });

        // 11. Z-Image Turbo Upscale (ControlNet + UltimateSDUpscale merged)
        var upscaleFragment = parameters.GetFragment("zimage_upscale");
        if (upscaleFragment?.IsActive == true)
        {
            _zImageUpscaleFragment.Build(builder, registry, new ZImageUpscaleFragment.Parameters
            {
                UpscaleBy = upscaleFragment?.GetDouble("upscale_by", 1.5) ?? 1.5,
                Strength = upscaleFragment?.GetDouble("strength", 0.2) ?? 0.2,
                Scope = ""
            });
        }

        // 14. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
