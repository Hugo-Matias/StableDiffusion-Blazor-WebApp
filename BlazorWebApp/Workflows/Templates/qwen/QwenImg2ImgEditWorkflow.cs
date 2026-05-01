using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Qwen;

/// <summary>
/// Qwen Img2Img Edit workflow implementation using the fluent builder API.
/// Uses LoadQwenEdit (UNet + LoRA + ModelSampling + CFGNorm) with TextEncodeQwenImageEditPlus encoding.
/// Pipeline: LoadImageScaled -> LoadQwenEdit -> VaeEncode -> EncodeEdit -> SamplerStandard -> VaeDecode -> Save
/// </summary>
public class QwenImg2ImgEditWorkflow : IWorkflowBuilder
{
    // Core fragments
    private readonly PromptsFragment _promptsFragment = new();
    private readonly LoadImageScaledFragment _loadImageScaledFragment = new();
    private readonly LoadQwenEditFragment _loadQwenEditFragment = new();
    private readonly VaeEncodeFragment _vaeEncodeFragment = new();
    private readonly EncodeEditFragment _encodeEditFragment = new();
    private readonly SamplerStandardFragment _samplerStandardFragment = new() { Defaults = new() { Steps = 4, Cfg = 1.0 } };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Img2Img (Edit)",
        Base = Data.Enums.ModelBase.Qwen,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "qwen_image_edit_2509_fp8_e4m3fn.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen_2.5_vl_7b_fp8_scaled.safetensors",
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
        CompatibleResourceBaseModels = ["Qwen", "Qwen 2"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _samplerStandardFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var samplerFragment = parameters.GetFragment("main_sampler");

        // Get source image path
        var source = parameters.Sources?.GetValueOrDefault("source_image");
        var imagePath = source?.Filename ?? source?.FilePath ?? "";

        // 1. Load and scale source image
        _loadImageScaledFragment.Build(builder, registry, new LoadImageScaledFragment.Parameters
        {
            Image = imagePath,
            Megapixels = 1,
            UpscaleMethod = "lanczos"
        });

        // 2. Load Qwen Edit models (UNet + LoRA + ModelSampling + CFGNorm + CLIP + VAE)
        _loadQwenEditFragment.Build(builder, registry, new LoadQwenEditFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "qwen_image_edit_2509_fp8_e4m3fn.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen_2.5_vl_7b_fp8_scaled.safetensors",
            VaeName = parameters.Assets?.GetValueOrDefault("Vae") ?? "qwen_image_vae.safetensors",
            LoraName = samplerFragment?.GetString("lora_name", "Speed/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors")
                       ?? "Speed/Qwen-Image-Edit-2509-Lightning-4steps-V1.0-bf16.safetensors",
            LoraStrength = samplerFragment?.GetDouble("lora_strength", 1) ?? 1,
            ModelShift = samplerFragment?.GetDouble("model_shift", 3) ?? 3,
            CfgNormStrength = samplerFragment?.GetDouble("cfg_norm_strength", 1) ?? 1
        });

        // 3. VAE Encode source image to latent
        _vaeEncodeFragment.Build(builder, registry, new VaeEncodeFragment.Parameters());

        // 4. Encode prompts with image reference (Qwen Edit encoding)
        _encodeEditFragment.Build(builder, registry, new EncodeEditFragment.Parameters
        {
            Positive = promptsFragment?.GetString("positive", "") ?? "",
            Negative = promptsFragment?.GetString("negative", "") ?? "",
            ImageRef = "image_input"
        });

        // 5. Sample
        var resolvedSeed = samplerFragment?.GetLong("seed", 42) ?? 42;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerStandardFragment.Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerId = "sampler_main",
            Title = "KSampler",
            SamplerName = samplerFragment?.GetString("sampler_name", "euler") ?? "euler",
            Scheduler = samplerFragment?.GetString("scheduler", "simple") ?? "simple",
            Steps = samplerFragment?.GetInt("steps", 4) ?? 4,
            Cfg = samplerFragment?.GetDouble("cfg", 1) ?? 1,
            Denoise = samplerFragment?.GetDouble("denoise", 1) ?? 1,
            Seed = resolvedSeed
        });

        // 6. VAE Decode
        _vaeDecodeFragment.Build(builder, registry);

        // 7. Save
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/img"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
