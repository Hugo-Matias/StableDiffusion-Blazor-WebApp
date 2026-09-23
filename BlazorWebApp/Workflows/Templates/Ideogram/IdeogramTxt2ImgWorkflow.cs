using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Ideogram;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Ideogram;

/// <summary>
/// Ideogram 4 Txt2Img workflow.
///
/// Two-model asymmetric CFG pipeline:
///   main UNet (CFGOverride applied) + unconditional UNet -> DualModelGuider
///   -> Ideogram4Scheduler (mu/std shape params) -> SamplerCustomAdvanced -> VAEDecode -> SaveImage
///
/// Prompting: the positive prompt should be a structured JSON caption following the Ideogram 4
/// schema. Use the "Ideogram4" LLM template in the Prompt Utils drawer (LLM Enhancer tab) to
/// convert a plain English scene description into the correct JSON format.
/// </summary>
public class IdeogramTxt2ImgWorkflow : IWorkflowBuilder
{
    private readonly LoadIdeogramFragment _loadIdeogramFragment = new();

    private readonly EmptyLatentFragment _emptyLatentFragment = new()
    {
        DefaultValues = new() { Width = 1024, Height = 1024, WidthStep = 16, HeightStep = 16, MinWidth = 256, MinHeight = 256 }
    };

    private readonly PromptsFragment _promptsFragment = new();

    private readonly IdeogramSamplerFragment _samplerFragment = new()
    {
        Defaults = new() { Steps = 20, Guidance = 7.0, Mu = 0.0, Std = 1.75 }
    };

    private readonly VaeDecodeFragment _vaeDecodeFragment = new();

    private readonly SaveFragment _saveFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Txt2Img",
        Description = "Generates images using Ideogram 4, a dual-model architecture with asymmetric CFG guidance and a custom scheduler. " +
                      "For best results, use the \"Ideogram4\" LLM template in the Prompt Utils drawer to convert a plain scene description into the required structured JSON caption format. " +
                      "Negative prompts are not used by this model.",
        Base = Data.Enums.ModelBase.Ideogram,
        Mode = ModeType.Txt2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "Model",
                Label = "Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "Base/ideogram4_fp8_scaled.safetensors",
                Order = 1,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "UnconditionalModel",
                Label = "Unconditional Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "Base/ideogram4_unconditional_fp8_scaled.safetensors",
                Order = 2,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen3vl_8b_fp8_scaled.safetensors",
                Order = 3,
                ColumnSize = 4
            },
            new WorkflowAsset
            {
                Parameter = "VAE",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "flux2-vae.safetensors",
                Order = 4,
                ColumnSize = 12
            }
        ],
        CompatibleResourceBaseModels = []
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _emptyLatentFragment;
        yield return _samplerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var promptsFragment = parameters.GetFragment("prompts");
        var latentFragment = parameters.GetFragment("latent");
        var samplerFragment = parameters.GetFragment("main_sampler");

        var positive = promptsFragment?.GetString("positive", "") ?? "";
        var seed = samplerFragment?.GetLong("seed", _samplerFragment.Defaults.Seed) ?? _samplerFragment.Defaults.Seed;
        if (seed < 0) seed = Random.Shared.NextInt64(0, int.MaxValue);

        var width = latentFragment?.GetInt("width", _emptyLatentFragment.DefaultValues.Width) ?? _emptyLatentFragment.DefaultValues.Width;
        var height = latentFragment?.GetInt("height", _emptyLatentFragment.DefaultValues.Height) ?? _emptyLatentFragment.DefaultValues.Height;
        var batchSize = latentFragment?.GetInt("batch_size", 1) ?? 1;

        // Ideogram4Scheduler requires dimensions aligned to multiples of 16, min 256
        var alignedWidth = Math.Max(((width + 15) / 16) * 16, 256);
        var alignedHeight = Math.Max(((height + 15) / 16) * 16, 256);

        // 1. Load models and encode positive prompt
        _loadIdeogramFragment.Build(builder, registry, new LoadIdeogramFragment.Parameters
        {
            UnetName = parameters.Assets?.GetValueOrDefault("Model") ?? "Base/ideogram4_fp8_scaled.safetensors",
            UnconditionalUnetName = parameters.Assets?.GetValueOrDefault("UnconditionalModel") ?? "Base/ideogram4_unconditional_fp8_scaled.safetensors",
            ClipName = parameters.Assets?.GetValueOrDefault("Clip") ?? "qwen3vl_8b_fp8_scaled.safetensors",
            VaeName = parameters.Assets?.GetValueOrDefault("VAE") ?? "flux2-vae.safetensors",
            Positive = positive
        });

        // 2. Create empty Flux2 latent at aligned resolution
        _emptyLatentFragment.Build(builder, registry, new EmptyLatentFragment.Parameters
        {
            Width = alignedWidth,
            Height = alignedHeight,
            BatchSize = batchSize,
            LatentClass = "EmptyFlux2LatentImage"
        });

        // 3. Sample
        _samplerFragment.Build(builder, registry, new IdeogramSamplerFragment.Parameters
        {
            SamplerId = "sampler_main",
            Steps = samplerFragment?.GetInt("steps", _samplerFragment.Defaults.Steps) ?? _samplerFragment.Defaults.Steps,
            Guidance = samplerFragment?.GetDouble("guidance", _samplerFragment.Defaults.Guidance) ?? _samplerFragment.Defaults.Guidance,
            Mu = samplerFragment?.GetDouble("mu", _samplerFragment.Defaults.Mu) ?? _samplerFragment.Defaults.Mu,
            Std = samplerFragment?.GetDouble("std", _samplerFragment.Defaults.Std) ?? _samplerFragment.Defaults.Std,
            Seed = seed,
            Width = alignedWidth,
            Height = alignedHeight
        });

        // 4. Decode latent to image
        _vaeDecodeFragment.Build(builder, registry);

        // 5. Save
        _saveFragment.Build(builder, parameters, registry);

        return builder.ToComfyWorkflow(registry);
    }
}
