using BlazorWebApp.Data.Entities;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.MiniMax;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.MiniMax;

/// <summary>
/// MiniMax H3 Image Edit workflow. Edits one or more source images from a text prompt using the
/// MiniMaxH3ImageConditioning model node (raw prompt string, no CLIPTextEncode). Each image is
/// capped to the megapixel budget and snapped to a multiple of 32 before conditioning; width/height
/// come from the first image's GetImageSize outputs. Negative conditioning is derived via
/// ConditioningZeroOut since the model only produces positive conditioning.
/// Pipeline: Load Models -> [Load Image + Prep x N] -> H3 Conditioning (+ ZeroOut) -> KSampler -> VaeDecode -> Save.
/// </summary>
public class MiniMaxH3ImageEditWorkflow : IWorkflowBuilder
{
    private readonly MiniMaxImageLoadModelsFragment _loadModelsFragment = new();
    private readonly MiniMaxLoadImageFragment _loadImageFragment = new();
    private readonly MiniMaxH3ImagePrepFragment _prepFragment = new();
    private readonly MiniMaxH3ImageConditioningFragment _conditioningFragment = new();
    private readonly PromptsFragment _promptsFragment = new();
    private readonly MiniMaxH3ImageSettingsFragment _settingsFragment = new();
    private readonly SamplerStandardFragment _samplerFragment = new()
    {
        Defaults = new() { Steps = 6, Cfg = 1.0, Denoise = 1.0, SamplerName = "euler", Scheduler = "simple" }
    };
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "H3 Image Edit",
        Description = "MiniMax H3 image editing: supply one or more images and a prompt describing the edit. " +
                      "Each image is capped to the megapixel budget (snapped to a multiple of 32) before conditioning, " +
                      "and the output resolution follows the first image's aspect ratio.",
        Base = Data.Enums.ModelBase.MiniMax,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = "UNet",
                Label = "Diffusion Model",
                Type = AssetType.DiffusionModel,
                DefaultValue = "Base/minimax_h3_image_pruned_int8_convrot.safetensors",
                Order = 1,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Clip",
                Label = "CLIP",
                Type = AssetType.Clip,
                DefaultValue = "qwen3vl_32b_minimax_h3_int8_convrot.safetensors",
                Order = 2,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = "Vae",
                Label = "VAE",
                Type = AssetType.Vae,
                DefaultValue = "minimax_h3_image_vae_fp16.safetensors",
                Order = 3,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = "image",
                Label = "Image",
                Type = SourceType.Image,
                Required = true,
                AllowMultiple = true
            }
        ],
        CompatibleResourceBaseModels = ["MiniMax H3"]
    };

    public IEnumerable<IFragmentBuilder> GetFragments()
    {
        yield return _promptsFragment;
        yield return _settingsFragment;
        yield return _samplerFragment;
    }

    public ComfyWorkflow Build(GenerationParameters parameters)
    {
        var builder = new ComfyWorkflowBuilder();
        var registry = new NodeRegistry();

        var settingsFragment = parameters.GetFragment(_settingsFragment.Metadata.Id);
        var megapixels = settingsFragment?.GetDouble("megapixels", _settingsFragment.DefaultValues.Megapixels) ?? _settingsFragment.DefaultValues.Megapixels;
        var prompt = parameters.GetFragment("prompts")?.GetString("positive", "") ?? "";

        // Collect all image sources (image, image_1, image_2, ...) in order.
        var images = parameters.Sources
            .Where(kvp => kvp.Key.StartsWith("image") && kvp.Value.HasData)
            .OrderBy(kvp => kvp.Key)
            .ToList();

        // 1. Load models (UNet, CLIP[minimax], VAE)
        _loadModelsFragment.Build(builder, parameters, registry);

        // 2. Per-image preparation: load -> megapixel cap -> multiple-of-32 snap -> size refs
        var imageInputNames = new List<string>(images.Count);
        for (var i = 0; i < images.Count; i++)
        {
            var nodePrefix = $"minimax_h3_image_{i + 1}";
            var source = images[i].Value;

            _loadImageFragment.Build(builder, registry, new MiniMaxLoadImageFragment.Parameters
            {
                NodePrefix = nodePrefix,
                Title = "Image",
                ImagePath = source.Filename ?? source.FilePath ?? "",
                OutputName = $"{nodePrefix}_image"
            });

            _prepFragment.Build(builder, registry, new MiniMaxH3ImagePrepFragment.Parameters
            {
                NodePrefix = nodePrefix,
                ImageInputName = $"{nodePrefix}_image",
                Megapixels = megapixels
            });

            imageInputNames.Add($"{nodePrefix}_resized");
        }

        // 3. H3 conditioning (raw prompt string) + zero-out negative; width/height from the first image
        _conditioningFragment.Build(builder, registry, new MiniMaxH3ImageConditioningFragment.Parameters
        {
            Prompt = prompt,
            WidthInputName = "minimax_h3_image_1_width",
            HeightInputName = "minimax_h3_image_1_height",
            ImageInputNames = imageInputNames
        });

        // 4. Sample (KSampler) - seed of -1 means random
        var samplerFragment = parameters.GetFragment("main_sampler");
        var resolvedSeed = samplerFragment?.GetLong("seed", -1) ?? -1;
        if (resolvedSeed < 0) resolvedSeed = Random.Shared.NextInt64(0, int.MaxValue);
        _samplerFragment.Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerName = samplerFragment?.GetString("sampler_name", _samplerFragment.Defaults.SamplerName) ?? _samplerFragment.Defaults.SamplerName,
            Scheduler = samplerFragment?.GetString("scheduler", _samplerFragment.Defaults.Scheduler) ?? _samplerFragment.Defaults.Scheduler,
            Steps = samplerFragment?.GetInt("steps", _samplerFragment.Defaults.Steps) ?? _samplerFragment.Defaults.Steps,
            Cfg = samplerFragment?.GetDouble("cfg", _samplerFragment.Defaults.Cfg) ?? _samplerFragment.Defaults.Cfg,
            Denoise = samplerFragment?.GetDouble("denoise", _samplerFragment.Defaults.Denoise) ?? _samplerFragment.Defaults.Denoise,
            Seed = resolvedSeed
        });

        // 5. VAE Decode -> Save
        _vaeDecodeFragment.Build(builder, registry);
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = "tmp/image"
        });

        return builder.ToComfyWorkflow(registry);
    }
}
