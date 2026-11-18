using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Fragments.Core;
using BlazorWebApp.Workflows.Fragments.Qwen;
using BlazorWebApp.Workflows.Models;
using AssetType = BlazorWebApp.Workflows.Models.AssetType;
using WorkflowAsset = BlazorWebApp.Workflows.Models.WorkflowAsset;
using WorkflowSource = BlazorWebApp.Workflows.Models.WorkflowSource;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Templates.Qwen;

public class QwenSwapAnythingSam31Workflow : IWorkflowBuilder
{
    private const string TargetImageSourceId = "target_image";
    private const string ReferenceImageSourceId = "reference_image";
    private const string QwenCheckpointAsset = "QwenCheckpoint";
    private const string SamCheckpointAsset = "SamCheckpoint";

    private readonly PromptsFragment _promptsFragment = new()
    {
        Defaults = new()
        {
            Positive = "head_swap: replace the face and hair color from image2. strictly preserving the facial expression and hair-color of the person in image2.",
            Negative = string.Empty
        }
    };

    private readonly QwenSwapAnythingSettingsFragment _settingsFragment = new();
    private readonly SamplerStandardFragment _samplerFragment = new()
    {
        Defaults = new()
        {
            SamplerName = "er_sde",
            Scheduler = "beta",
            Steps = 4,
            Cfg = 1,
            Denoise = 1
        }
    };

    private readonly LoraLoaderFragment _loraLoaderFragment = new();
    private readonly VaeDecodeFragment _vaeDecodeFragment = new();
    private readonly SaveFragment _saveFragment = new();

    public WorkflowMetadata Metadata => new()
    {
        Title = "Swap Anything (SAM 3.1)",
        Description = "Uses SAM 3.1 masks and Qwen image-edit conditioning to transfer a face, hair, outfit, or accessory region from a reference image into a target image.",
        Base = Data.Enums.ModelBase.Qwen,
        Mode = ModeType.Img2Img,
        Assets =
        [
            new WorkflowAsset
            {
                Parameter = QwenCheckpointAsset,
                Label = "Qwen Checkpoint",
                Type = AssetType.CheckpointModel,
                DefaultValue = "Base/Qwen-Rapid-AIO-NSFW-v19.safetensors",
                Order = 1,
                ColumnSize = 6
            },
            new WorkflowAsset
            {
                Parameter = SamCheckpointAsset,
                Label = "SAM 3.1 Checkpoint",
                Type = AssetType.CheckpointModel,
                DefaultValue = "SAM/sam3.1_multiplex_fp16.safetensors",
                Order = 2,
                ColumnSize = 6
            }
        ],
        Sources =
        [
            new WorkflowSource
            {
                Id = TargetImageSourceId,
                Label = "Target Image",
                Type = SourceType.Image,
                Required = true
            },
            new WorkflowSource
            {
                Id = ReferenceImageSourceId,
                Label = "Reference Image",
                Type = SourceType.Image,
                Required = true
            }
        ],
        CompatibleResourceBaseModels = ["Qwen", "Qwen 2"]
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
        var settings = ResolveSettings(parameters);
        var prompts = parameters.GetFragment(_promptsFragment.Metadata.Id);
        var sampler = parameters.GetFragment(_samplerFragment.Metadata.Id);

        var positive = prompts?.GetString("positive", _promptsFragment.Defaults.Positive) ?? _promptsFragment.Defaults.Positive;
        var negative = prompts?.GetString("negative", _promptsFragment.Defaults.Negative) ?? _promptsFragment.Defaults.Negative;
        var targetImage = GetSourcePath(parameters, TargetImageSourceId);
        var referenceImage = GetSourcePath(parameters, ReferenceImageSourceId);
        var qwenCheckpoint = parameters.Assets.GetValueOrDefault(QwenCheckpointAsset) ?? "Base/Qwen-Rapid-AIO-NSFW-v19.safetensors";
        var samCheckpoint = parameters.Assets.GetValueOrDefault(SamCheckpointAsset) ?? "SAM/sam3.1_multiplex_fp16.safetensors";

        BuildSamLoader(builder, samCheckpoint);
        BuildTargetImageBranch(builder, registry, targetImage, settings);
        BuildReferenceImageBranch(builder, registry, referenceImage, settings);
        BuildQwenLoader(builder, registry, parameters, qwenCheckpoint, settings);
        BuildQwenConditioning(builder, registry, positive, negative);
        BuildLatents(builder, registry, settings);
        BuildSampler(builder, registry, sampler);

        _vaeDecodeFragment.Build(builder, registry);
        _saveFragment.Build(builder, registry, new SaveFragment.Parameters
        {
            FilenamePrefix = BuildFilenamePrefix(referenceImage)
        });

        return builder.ToComfyWorkflow(registry);
    }

    private static QwenSwapAnythingSettingsFragment.Parameters ResolveSettings(GenerationParameters parameters)
    {
        var defaults = new QwenSwapAnythingSettingsFragment.Parameters();
        var fragment = parameters.GetFragment("swap_anything_settings");

        return new QwenSwapAnythingSettingsFragment.Parameters
        {
            SamPromptA = fragment.GetString("sam_prompt_a", defaults.SamPromptA),
            SamPromptB = fragment.GetString("sam_prompt_b", defaults.SamPromptB),
            ReferencePrompt = fragment.GetString("reference_prompt", defaults.ReferencePrompt),
            SamThreshold = fragment.GetDouble("sam_threshold", defaults.SamThreshold),
            SamRefineIterations = fragment.GetInt("sam_refine_iterations", defaults.SamRefineIterations),
            GrowMaskExpand = fragment.GetInt("grow_mask_expand", defaults.GrowMaskExpand),
            TargetMegapixels = fragment.GetDouble("target_megapixels", defaults.TargetMegapixels),
            ReferenceMegapixels = fragment.GetDouble("reference_megapixels", defaults.ReferenceMegapixels),
            ReferenceScaleLength = fragment.GetInt("reference_scale_length", defaults.ReferenceScaleLength),
            LatentSource = fragment.GetString("latent_source", defaults.LatentSource),
            EmptyLatentWidth = fragment.GetInt("empty_latent_width", defaults.EmptyLatentWidth),
            EmptyLatentHeight = fragment.GetInt("empty_latent_height", defaults.EmptyLatentHeight),
            EmptyLatentBatchSize = fragment.GetInt("empty_latent_batch_size", defaults.EmptyLatentBatchSize),
            EasyCacheReuseThreshold = fragment.GetDouble("easycache_reuse_threshold", defaults.EasyCacheReuseThreshold),
            EasyCacheStartPercent = fragment.GetDouble("easycache_start_percent", defaults.EasyCacheStartPercent),
            EasyCacheEndPercent = fragment.GetDouble("easycache_end_percent", defaults.EasyCacheEndPercent)
        };
    }

    private static string GetSourcePath(GenerationParameters parameters, string sourceId)
    {
        var source = parameters.Sources.GetValueOrDefault(sourceId);
        return source?.Filename ?? source?.FilePath ?? string.Empty;
    }

    private static string BuildFilenamePrefix(string referenceImage)
    {
        var stem = Path.GetFileNameWithoutExtension(referenceImage);
        return string.IsNullOrWhiteSpace(stem) ? "Qwen/SwapAnything" : $"Qwen/SwapAnything/{stem}";
    }

    private static void BuildSamLoader(ComfyWorkflowBuilder builder, string checkpointName)
    {
        builder.AddNode("sam_checkpoint_loader", node => node
            .Type("CheckpointLoaderSimple")
            .Title("Load SAM 3.1 Checkpoint")
            .Input("ckpt_name", checkpointName));
    }

    private static void BuildTargetImageBranch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string image,
        QwenSwapAnythingSettingsFragment.Parameters settings)
    {
        builder.AddNode("target_image_loader", node => node
            .Type("LoadImage")
            .Title("Target Image")
            .Input("image", image));

        builder.AddNode("target_image_scale", node => node
            .Type("ImageScaleToTotalPixels")
            .Title("Target Image Scale")
            .Input("upscale_method", "nearest-exact")
            .Input("megapixels", settings.TargetMegapixels)
            .Input("resolution_steps", 1)
            .InputFromNode("image", "target_image_loader", 0));

        var maskA = BuildSamDetect(builder, "target_sam_a", "target_image_scale", settings.SamPromptA, settings);
        var maskB = BuildSamDetect(builder, "target_sam_b", "target_image_scale", settings.SamPromptB, settings);

        builder.AddNode("target_mask_composite", node => node
            .Type("MaskComposite")
            .Title("Target Composite Mask")
            .Input("x", 0)
            .Input("y", 0)
            .Input("operation", "add")
            .InputFromNode("destination", maskB, 0)
            .InputFromNode("source", maskA, 0));

        builder.AddNode("target_grow_mask", node => node
            .Type("GrowMask")
            .Title("Target Grow Mask")
            .Input("expand", settings.GrowMaskExpand)
            .Input("tapered_corners", true)
            .InputFromNode("mask", "target_mask_composite", 0));

        builder.AddNode("target_draw_mask", node => node
            .Type("DrawMaskOnImage")
            .Title("Target Draw Mask")
            .Input("color", "0, 255, 60")
            .Input("device", "gpu")
            .InputFromNode("image", "target_image_scale", 0)
            .InputFromNode("mask", "target_grow_mask", 0));

        registry.Register("target_guided_image", "target_draw_mask", 0);
    }

    private static void BuildReferenceImageBranch(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        string image,
        QwenSwapAnythingSettingsFragment.Parameters settings)
    {
        builder.AddNode("reference_image_loader", node => node
            .Type("LoadImage")
            .Title("Reference Image")
            .Input("image", image));

        builder.AddNode("reference_image_scale", node => node
            .Type("ImageScaleToTotalPixels")
            .Title("Reference Image Scale")
            .Input("upscale_method", "nearest-exact")
            .Input("megapixels", settings.ReferenceMegapixels)
            .Input("resolution_steps", 1)
            .InputFromNode("image", "reference_image_loader", 0));

        var referenceMask = BuildSamDetect(builder, "reference_sam", "reference_image_scale", settings.ReferencePrompt, settings);

        builder.AddNode("reference_invert_mask", node => node
            .Type("InvertMask")
            .Title("Reference Invert Mask")
            .InputFromNode("mask", referenceMask, 0));

        builder.AddNode("reference_draw_mask", node => node
            .Type("DrawMaskOnImage")
            .Title("Reference Draw Mask")
            .Input("color", "0,0,0")
            .Input("device", "cpu")
            .InputFromNode("image", "reference_image_scale", 0)
            .InputFromNode("mask", "reference_invert_mask", 0));

        builder.AddNode("reference_cut_by_mask", node => node
            .Type("Cut By Mask")
            .Title("Reference Cut By Mask")
            .Input("force_resize_width", 0)
            .Input("force_resize_height", 0)
            .InputFromNode("image", "reference_draw_mask", 0)
            .InputFromNode("mask", "reference_image_scale", 0));

        builder.AddNode("reference_images_to_rgb", node => node
            .Type("Images to RGB")
            .Title("Reference Images to RGB")
            .InputFromNode("images", "reference_cut_by_mask", 0));

        builder.AddNode("reference_aspect_ratio", node => node
            .Type("LayerUtility: ImageScaleByAspectRatio V2")
            .Title("Reference Aspect Ratio")
            .Input("aspect_ratio", "original")
            .Input("proportional_width", 1)
            .Input("proportional_height", 1)
            .Input("fit", "letterbox")
            .Input("method", "lanczos")
            .Input("round_to_multiple", "8")
            .Input("scale_to_side", "longest")
            .Input("scale_to_length", settings.ReferenceScaleLength)
            .Input("background_color", "#000000")
            .InputFromNode("image", "reference_images_to_rgb", 0));

        registry.Register("reference_guided_image", "reference_aspect_ratio", 0);
    }

    private static string BuildSamDetect(
        ComfyWorkflowBuilder builder,
        string prefix,
        string imageNodeId,
        string prompt,
        QwenSwapAnythingSettingsFragment.Parameters settings)
    {
        var textNodeId = $"{prefix}_text";
        var detectNodeId = $"{prefix}_detect";

        builder.AddNode(textNodeId, node => node
            .Type("CLIPTextEncode")
            .Title($"{prefix} Text")
            .Input("text", prompt)
            .InputFromNode("clip", "sam_checkpoint_loader", 1));

        builder.AddNode(detectNodeId, node => node
            .Type("SAM3_Detect")
            .Title($"{prefix} Detect")
            .Input("threshold", settings.SamThreshold)
            .Input("refine_iterations", settings.SamRefineIterations)
            .Input("individual_masks", false)
            .InputFromNode("model", "sam_checkpoint_loader", 0)
            .InputFromNode("image", imageNodeId, 0)
            .InputFromNode("conditioning", textNodeId, 0));

        return detectNodeId;
    }

    private void BuildQwenLoader(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        GenerationParameters parameters,
        string checkpointName,
        QwenSwapAnythingSettingsFragment.Parameters settings)
    {
        builder.AddNode("qwen_checkpoint_loader", node => node
            .Type("CheckpointLoaderSimple")
            .Title("Load Qwen Checkpoint")
            .Input("ckpt_name", checkpointName));

        registry.Register("model_output", "qwen_checkpoint_loader", 0);
        registry.Register("clip_output", "qwen_checkpoint_loader", 1);
        registry.Register("vae_output", "qwen_checkpoint_loader", 2);

        _loraLoaderFragment.BuildAll(builder, registry, parameters.Loras);

        builder.AddNode("easy_cache", node => node
            .Type("EasyCache")
            .Title("Easy Cache")
            .Input("reuse_threshold", settings.EasyCacheReuseThreshold)
            .Input("start_percent", settings.EasyCacheStartPercent)
            .Input("end_percent", settings.EasyCacheEndPercent)
            .Input("verbose", false)
            .InputRef("model", registry.GetRef("model_output")));

        registry.Register("model_output", "easy_cache", 0);
    }

    private static void BuildQwenConditioning(ComfyWorkflowBuilder builder, NodeRegistry registry, string positive, string negative)
    {
        builder.AddNode("qwen_encode_positive", node => node
            .Type("TextEncodeQwenImageEditPlus")
            .Title("Qwen Positive")
            .Input("prompt", positive)
            .InputRef("clip", registry.GetRef("clip_output"))
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputRef("image1", registry.GetRef("target_guided_image"))
            .InputRef("image2", registry.GetRef("reference_guided_image")));

        builder.AddNode("qwen_encode_negative", node => node
            .Type("TextEncodeQwenImageEditPlus")
            .Title("Qwen Negative")
            .Input("prompt", negative)
            .InputRef("clip", registry.GetRef("clip_output"))
            .InputRef("vae", registry.GetRef("vae_output"))
            .InputRef("image1", registry.GetRef("target_guided_image"))
            .InputRef("image2", registry.GetRef("reference_guided_image")));

        builder.AddNode("reference_vae_encode", node => node
            .Type("VAEEncode")
            .Title("Reference VAE Encode")
            .InputRef("pixels", registry.GetRef("reference_guided_image"))
            .InputRef("vae", registry.GetRef("vae_output")));

        builder.AddNode("positive_reference_latent_1", node => node
            .Type("ReferenceLatent")
            .Title("Positive Reference Latent 1")
            .InputFromNode("conditioning", "qwen_encode_positive", 0)
            .InputFromNode("latent", "reference_vae_encode", 0));

        builder.AddNode("positive_reference_latent_2", node => node
            .Type("ReferenceLatent")
            .Title("Positive Reference Latent 2")
            .InputFromNode("conditioning", "positive_reference_latent_1", 0));

        builder.AddNode("positive_reference_method", node => node
            .Type("FluxKontextMultiReferenceLatentMethod")
            .Title("Positive Reference Method")
            .Input("reference_latents_method", "index_timestep_zero")
            .InputFromNode("conditioning", "positive_reference_latent_2", 0));

        builder.AddNode("negative_reference_latent_1", node => node
            .Type("ReferenceLatent")
            .Title("Negative Reference Latent 1")
            .InputFromNode("conditioning", "qwen_encode_negative", 0)
            .InputFromNode("latent", "reference_vae_encode", 0));

        builder.AddNode("negative_reference_latent_2", node => node
            .Type("ReferenceLatent")
            .Title("Negative Reference Latent 2")
            .InputFromNode("conditioning", "negative_reference_latent_1", 0));

        builder.AddNode("negative_reference_method", node => node
            .Type("FluxKontextMultiReferenceLatentMethod")
            .Title("Negative Reference Method")
            .Input("reference_latents_method", "index_timestep_zero")
            .InputFromNode("conditioning", "negative_reference_latent_2", 0));

        registry.Register("positive_output", "positive_reference_method", 0);
        registry.Register("negative_output", "negative_reference_method", 0);
    }

    private static void BuildLatents(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        QwenSwapAnythingSettingsFragment.Parameters settings)
    {
        builder.AddNode("target_vae_encode", node => node
            .Type("VAEEncode")
            .Title("Target VAE Encode")
            .InputRef("pixels", registry.GetRef("target_guided_image"))
            .InputRef("vae", registry.GetRef("vae_output")));

        builder.AddNode("empty_latent", node => node
            .Type("EmptyLatentImage")
            .Title("Empty Latent")
            .Input("width", settings.EmptyLatentWidth)
            .Input("height", settings.EmptyLatentHeight)
            .Input("batch_size", settings.EmptyLatentBatchSize));

        var latentRef = settings.LatentSource switch
        {
            QwenSwapAnythingSettingsFragment.LatentSourceReference => ("reference_vae_encode", 0),
            QwenSwapAnythingSettingsFragment.LatentSourceEmpty => ("empty_latent", 0),
            _ => ("target_vae_encode", 0)
        };

        registry.Register("latent_output", latentRef.Item1, latentRef.Item2);
    }

    private void BuildSampler(ComfyWorkflowBuilder builder, NodeRegistry registry, FragmentParameters? sampler)
    {
        var seed = sampler.GetLong("seed", -1L);
        if (seed < 0)
        {
            seed = Random.Shared.NextInt64(0, int.MaxValue);
        }

        _samplerFragment.Build(builder, registry, new SamplerStandardFragment.Parameters
        {
            SamplerId = "sampler_main",
            Title = "KSampler",
            SamplerName = sampler.GetString("sampler_name", _samplerFragment.Defaults.SamplerName),
            Scheduler = sampler.GetString("scheduler", _samplerFragment.Defaults.Scheduler),
            Steps = sampler.GetInt("steps", _samplerFragment.Defaults.Steps),
            Cfg = sampler.GetDouble("cfg", _samplerFragment.Defaults.Cfg),
            Denoise = sampler.GetDouble("denoise", _samplerFragment.Defaults.Denoise),
            Seed = seed
        });
    }
}