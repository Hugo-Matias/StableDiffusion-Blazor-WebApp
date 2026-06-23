using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenCharacterFaceReplacementFragment : IFragmentBuilder
{
    private const string FaceMaskPrompt = "face and hair";
    private const string PositivePrompt = "Replace only the face and visible hair details in image1 using the same person's identity, facial features, and hair details from image2. Preserve image1 camera angle, head orientation, gaze direction, facial expression, lighting, body, clothing, background, crop framing, and art style. Adapt the source identity to image1 instead of changing image1 to match the source pose.";
    private const string PositivePromptWithNeutral = " Image3 is an optional neutral close reference; use it only for stable facial feature guidance while preserving image1 expression and orientation.";
    private const string NegativePrompt = "changed body, changed clothing, changed camera angle, changed head orientation, changed expression, source pose copied onto target, mismatched lighting, different hairstyle silhouette, face pasted on top";

    private readonly QwenImageEditPlusProEncodeFragment _encodeFragment = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "qwen_character_face_replacement",
        Type = FragmentType.Enhancement,
        Title = "Qwen Character Face Replacement",
        IsHidden = true
    };

    public class Parameters
    {
        public required CharacterReferenceSlotState Slot { get; init; }
        public required (string nodeId, int index) TargetImageRef { get; init; }
        public required (string nodeId, int index) ModelRef { get; init; }
        public required (string nodeId, int index) VaeRef { get; init; }
        public string NodePrefix { get; init; } = "character_slot_face_";
        public string SourceImageRefKey { get; init; } = CharacterReferenceWorkflowIds.SourceImageOutputKey;
        public string? CloseNeutralRefKey { get; init; }
        public string GlobalPositivePromptExtension { get; init; } = string.Empty;
        public string GlobalNegativePrompt { get; init; } = CharacterReferenceDefaults.GlobalNegativePrompt;
        public CharacterFaceReplacementSettings Settings { get; init; } = new();
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public (string nodeId, int index) Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        var slot = fragmentParams.Slot;
        var prefix = fragmentParams.NodePrefix;
        var settings = fragmentParams.Settings;
        var sourceImageRef = registry.GetRef(fragmentParams.SourceImageRefKey);
        var useNeutralReference = !string.IsNullOrWhiteSpace(fragmentParams.CloseNeutralRefKey)
            && registry.HasOutput(fragmentParams.CloseNeutralRefKey);

        var clipSegLoaderNodeId = $"{prefix}clipseg_loader";
        var targetMaskNodeId = $"{prefix}target_mask";
        var targetCropNodeId = $"{prefix}target_crop";
        var targetResizeNodeId = $"{prefix}target_resize";
        var targetCleanNodeId = $"{prefix}target_clean";
        var sourceMaskNodeId = $"{prefix}source_mask";
        var sourceCropNodeId = $"{prefix}source_crop";
        var sourceResizeNodeId = $"{prefix}source_resize";
        var sourceCleanNodeId = $"{prefix}source_clean";
        var neutralResizeNodeId = $"{prefix}neutral_resize";
        var neutralCleanNodeId = $"{prefix}neutral_clean";
        var positiveNodeId = $"{prefix}encode_positive";
        var negativeNodeId = $"{prefix}encode_negative";
        var samplerNodeId = $"{prefix}sampler";
        var decodeNodeId = $"{prefix}decode";
        var compositeMaskNodeId = $"{prefix}composite_mask";
        var growMaskNodeId = $"{prefix}grow_mask";
        var compositeNodeId = $"{prefix}composite";
        var uncropNodeId = $"{prefix}uncrop";
        var targetCleanKey = $"{prefix}target_clean_output";
        var sourceCleanKey = $"{prefix}source_clean_output";
        var neutralCleanKey = $"{prefix}neutral_clean_output";
        var positiveKey = $"{prefix}positive_output";
        var negativeKey = $"{prefix}negative_output";
        var latentKey = $"{prefix}latent_output";
        var seed = slot.Seed < 0 ? Random.Shared.NextInt64(0, int.MaxValue) : slot.Seed;

        builder.AddNode(clipSegLoaderNodeId, node => node
            .Type("DownloadAndLoadCLIPSeg")
            .Title($"{slot.Label} Face CLIPSeg Loader")
            .Input("model", "Kijai/clipseg-rd64-refined-fp16"));

        builder.AddNode(targetMaskNodeId, node => node
            .Type("BatchCLIPSeg")
            .Title($"{slot.Label} Target Face Mask")
            .InputRef("images", fragmentParams.TargetImageRef)
            .Input("text", FaceMaskPrompt)
            .Input("threshold", settings.DetectionThreshold)
            .Input("binary_mask", true)
            .Input("combine_mask", true)
            .Input("use_cuda", true)
            .Input("blur_sigma", 0.0)
            .InputFromNode("opt_model", clipSegLoaderNodeId, 0)
            .Input("image_bg_level", 0.0)
            .Input("invert", false));

        builder.AddNode(targetCropNodeId, node => node
            .Type("BatchCropFromMaskAdvanced")
            .Title($"{slot.Label} Target Face Crop")
            .InputRef("original_images", fragmentParams.TargetImageRef)
            .InputFromNode("masks", targetMaskNodeId, 0)
            .Input("crop_size_mult", settings.TargetCropSizeMultiplier)
            .Input("bbox_smooth_alpha", 1.0));

        builder.AddNode(targetResizeNodeId, node => node
            .Type("ImageResize+")
            .Title($"{slot.Label} Target Face Resize")
            .InputFromNode("image", targetCropNodeId, 1)
            .Input("width", settings.CropResolution)
            .Input("height", settings.CropResolution)
            .Input("interpolation", "lanczos")
            .Input("method", "keep proportion")
            .Input("condition", "always")
            .Input("multiple_of", 0));

        builder.AddNode(targetCleanNodeId, node => node
            .Type("ImageRemoveAlpha+")
            .Title($"{slot.Label} Target Face Clean")
            .InputFromNode("image", targetResizeNodeId, 0));
        registry.Register(targetCleanKey, targetCleanNodeId, 0);

        builder.AddNode(sourceMaskNodeId, node => node
            .Type("BatchCLIPSeg")
            .Title($"{slot.Label} Source Face Mask")
            .InputRef("images", sourceImageRef)
            .Input("text", FaceMaskPrompt)
            .Input("threshold", settings.SourceDetectionThreshold)
            .Input("binary_mask", true)
            .Input("combine_mask", true)
            .Input("use_cuda", true)
            .Input("blur_sigma", 0.0)
            .InputFromNode("opt_model", clipSegLoaderNodeId, 0)
            .Input("image_bg_level", 0.0)
            .Input("invert", false));

        builder.AddNode(sourceCropNodeId, node => node
            .Type("BatchCropFromMaskAdvanced")
            .Title($"{slot.Label} Source Face Crop")
            .InputRef("original_images", sourceImageRef)
            .InputFromNode("masks", sourceMaskNodeId, 0)
            .Input("crop_size_mult", settings.SourceCropSizeMultiplier)
            .Input("bbox_smooth_alpha", 1.0));

        builder.AddNode(sourceResizeNodeId, node => node
            .Type("ImageResize+")
            .Title($"{slot.Label} Source Face Resize")
            .InputFromNode("image", sourceCropNodeId, 1)
            .Input("width", settings.CropResolution)
            .Input("height", settings.CropResolution)
            .Input("interpolation", "lanczos")
            .Input("method", "keep proportion")
            .Input("condition", "always")
            .Input("multiple_of", 0));

        builder.AddNode(sourceCleanNodeId, node => node
            .Type("ImageRemoveAlpha+")
            .Title($"{slot.Label} Source Face Clean")
            .InputFromNode("image", sourceResizeNodeId, 0));
        registry.Register(sourceCleanKey, sourceCleanNodeId, 0);

        if (useNeutralReference)
        {
            builder.AddNode(neutralResizeNodeId, node => node
                .Type("ImageResize+")
                .Title($"{slot.Label} Neutral Reference Resize")
                .InputRef("image", registry.GetRef(fragmentParams.CloseNeutralRefKey!))
                .Input("width", settings.CropResolution)
                .Input("height", settings.CropResolution)
                .Input("interpolation", "lanczos")
                .Input("method", "keep proportion")
                .Input("condition", "always")
                .Input("multiple_of", 0));

            builder.AddNode(neutralCleanNodeId, node => node
                .Type("ImageRemoveAlpha+")
                .Title($"{slot.Label} Neutral Reference Clean")
                .InputFromNode("image", neutralResizeNodeId, 0));
            registry.Register(neutralCleanKey, neutralCleanNodeId, 0);
        }

        var imageRefKeys = new List<string> { targetCleanKey, sourceCleanKey };
        if (useNeutralReference)
        {
            imageRefKeys.Add(neutralCleanKey);
        }

        _encodeFragment.Build(builder, registry, new QwenImageEditPlusProEncodeFragment.Parameters
        {
            NodeId = positiveNodeId,
            OutputKey = positiveKey,
            LatentOutputKey = latentKey,
            Prompt = BuildPositivePrompt(fragmentParams.GlobalPositivePromptExtension, useNeutralReference),
            ImageRefKeys = imageRefKeys,
            MainImageIndex = 1
        });

        _encodeFragment.Build(builder, registry, new QwenImageEditPlusProEncodeFragment.Parameters
        {
            NodeId = negativeNodeId,
            OutputKey = negativeKey,
            Prompt = BuildNegativePrompt(slot, fragmentParams.GlobalNegativePrompt),
            ImageRefKeys = [targetCleanKey],
            MainImageIndex = 1
        });

        builder.AddNode(samplerNodeId, node => node
            .Type("KSampler")
            .Title($"{slot.Label} Face Replacement Sampler")
            .Input("seed", seed)
            .Input("steps", settings.Steps)
            .Input("cfg", settings.Cfg)
            .Input("sampler_name", slot.SamplerName)
            .Input("scheduler", slot.Scheduler)
            .Input("denoise", settings.Denoise)
            .InputRef("model", fragmentParams.ModelRef)
            .InputRef("positive", registry.GetRef(positiveKey))
            .InputRef("negative", registry.GetRef(negativeKey))
            .InputRef("latent_image", registry.GetRef(latentKey)));

        builder.AddNode(decodeNodeId, node => node
            .Type("VAEDecode")
            .Title($"{slot.Label} Face Replacement Decode")
            .InputFromNode("samples", samplerNodeId, 0)
            .InputRef("vae", fragmentParams.VaeRef));

        builder.AddNode(compositeMaskNodeId, node => node
            .Type("BatchCLIPSeg")
            .Title($"{slot.Label} Replacement Composite Mask")
            .InputFromNode("images", targetCleanNodeId, 0)
            .Input("text", FaceMaskPrompt)
            .Input("threshold", settings.DetectionThreshold)
            .Input("binary_mask", true)
            .Input("combine_mask", true)
            .Input("use_cuda", true)
            .Input("blur_sigma", 0.0)
            .InputFromNode("opt_model", clipSegLoaderNodeId, 0)
            .Input("image_bg_level", 0.0)
            .Input("invert", false));

        builder.AddNode(growMaskNodeId, node => node
            .Type("GrowMaskWithBlur")
            .Title($"{slot.Label} Replacement Mask Feather")
            .InputFromNode("mask", compositeMaskNodeId, 0)
            .Input("expand", settings.MaskExpand)
            .Input("incremental_expandrate", 0.0)
            .Input("tapered_corners", true)
            .Input("flip_input", false)
            .Input("blur_radius", settings.MaskBlurRadius)
            .Input("lerp_alpha", 1.0)
            .Input("decay_factor", 1.0)
            .Input("fill_holes", true));

        builder.AddNode(compositeNodeId, node => node
            .Type("ImageCompositeMasked")
            .Title($"{slot.Label} Replacement Composite")
            .InputFromNode("source", decodeNodeId, 0)
            .Input("x", 0)
            .Input("y", 0)
            .Input("resize_source", true)
            .InputFromNode("destination", targetCleanNodeId, 0)
            .InputFromNode("mask", growMaskNodeId, 0));

        builder.AddNode(uncropNodeId, node => node
            .Type("BatchUncropAdvanced")
            .Title($"{slot.Label} Replacement Uncrop")
            .InputFromNode("original_images", targetCropNodeId, 0)
            .InputFromNode("cropped_images", compositeNodeId, 0)
            .InputFromNode("cropped_masks", targetCropNodeId, 2)
            .InputFromNode("combined_crop_mask", targetCropNodeId, 4)
            .InputFromNode("bboxes", targetCropNodeId, 5)
            .Input("border_blending", settings.BorderBlending)
            .Input("crop_rescale", 1.0)
            .Input("use_combined_mask", false)
            .Input("use_square_mask", true));

        return (uncropNodeId, 0);
    }

    private static string BuildPositivePrompt(string globalPositivePromptExtension, bool useNeutralReference)
    {
        var prompt = useNeutralReference ? PositivePrompt + PositivePromptWithNeutral : PositivePrompt;
        var extension = globalPositivePromptExtension.Trim();
        return string.IsNullOrWhiteSpace(extension) ? prompt : $"{prompt} {extension}";
    }

    private static string BuildNegativePrompt(CharacterReferenceSlotState slot, string globalNegativePrompt)
    {
        var configuredNegative = string.IsNullOrWhiteSpace(slot.NegativePromptOverride)
            ? globalNegativePrompt
            : slot.NegativePromptOverride;

        return string.IsNullOrWhiteSpace(configuredNegative)
            ? NegativePrompt
            : $"{NegativePrompt}, {configuredNegative}";
    }
}