using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Qwen;

public class QwenCharacterShotFragment : IFragmentBuilder
{
    private readonly QwenImageEditPlusProEncodeFragment _encodeFragment = new();

    public FragmentMetadata Metadata => new()
    {
        Id = "qwen_character_shot",
        Type = FragmentType.Output,
        Title = "Qwen Character Shot",
        IsHidden = true
    };

    public class Parameters
    {
        public required CharacterReferenceSlotState Slot { get; init; }
        public string NodePrefix { get; init; } = "character_slot_";
        public string SourceImageRefKey { get; init; } = "source_image";
        public string GlobalNegativePrompt { get; init; } = CharacterReferenceDefaults.GlobalNegativePrompt;
        public bool UseRtxUpscale { get; init; } = true;
        public bool UseCleanGpu { get; init; }
        public string FilenamePrefix { get; init; } = "Character/Reference";
        public string SageAttention { get; init; } = "auto";
        public bool AllowSageCompile { get; init; }
        public double ModelShift { get; init; } = 3;
        public double CfgNormStrength { get; init; } = 1;
        public string ImageScaleMethod { get; init; } = "lanczos";
        public double ImageScaleMegapixels { get; init; } = 1;
        public int ImageScaleResolutionSteps { get; init; } = 1;
        public int RtxMultiplier { get; init; } = 2;
        public string RtxQuality { get; init; } = "ULTRA";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public CharacterReferenceOutputNode Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        var slot = fragmentParams.Slot;
        var prefix = fragmentParams.NodePrefix;
        var sourceImageRef = registry.GetRef(fragmentParams.SourceImageRefKey);
        var modelRef = registry.GetRef("model_output");
        var vaeRef = registry.GetRef("vae_output");

        var sageNodeId = $"{prefix}sage_attention";
        var modelSamplingNodeId = $"{prefix}model_sampling";
        var cfgNormNodeId = $"{prefix}cfg_norm";
        var imageScaleNodeId = $"{prefix}image_scale";
        var latentNodeId = $"{prefix}latent";
        var positiveNodeId = $"{prefix}encode_positive";
        var negativeNodeId = $"{prefix}encode_negative";
        var samplerNodeId = $"{prefix}sampler";
        var decodeNodeId = $"{prefix}decode";
        var rtxNodeId = $"{prefix}rtx_upscale";
        var cleanNodeId = $"{prefix}clean_gpu";
        var saveNodeId = $"{prefix}save";
        var scaledImageKey = $"{prefix}scaled_image";
        var positiveKey = $"{prefix}positive_output";
        var negativeKey = $"{prefix}negative_output";
        var imageOutputKey = CharacterReferenceWorkflowIds.SlotImageOutputKey(slot.Id);
        var resolvedSeed = slot.Seed < 0
            ? Random.Shared.NextInt64(0, int.MaxValue)
            : slot.Seed;

        builder.AddNode(sageNodeId, node => node
            .Type("PathchSageAttentionKJ")
            .Title($"{slot.Label} Sage Attention")
            .Input("sage_attention", fragmentParams.SageAttention)
            .Input("allow_compile", fragmentParams.AllowSageCompile)
            .InputRef("model", modelRef));

        builder.AddNode(modelSamplingNodeId, node => node
            .Type("ModelSamplingAuraFlow")
            .Title($"{slot.Label} Model Sampling")
            .Input("shift", fragmentParams.ModelShift)
            .InputRef("model", (sageNodeId, 0)));

        builder.AddNode(cfgNormNodeId, node => node
            .Type("CFGNorm")
            .Title($"{slot.Label} CFGNorm")
            .Input("strength", fragmentParams.CfgNormStrength)
            .InputRef("model", (modelSamplingNodeId, 0)));

        builder.AddNode(imageScaleNodeId, node => node
            .Type("ImageScaleToTotalPixels")
            .Title($"{slot.Label} Reference Resize")
            .Input("upscale_method", fragmentParams.ImageScaleMethod)
            .Input("megapixels", fragmentParams.ImageScaleMegapixels)
            .Input("resolution_steps", fragmentParams.ImageScaleResolutionSteps)
            .InputRef("image", sourceImageRef));
        registry.Register(scaledImageKey, imageScaleNodeId, 0);

        builder.AddNode(latentNodeId, node => node
            .Type("EmptyLatentImage")
            .Title($"{slot.Label} Latent")
            .Input("width", slot.Width)
            .Input("height", slot.Height)
            .Input("batch_size", slot.BatchSize));

        _encodeFragment.Build(builder, registry, new QwenImageEditPlusProEncodeFragment.Parameters
        {
            NodeId = positiveNodeId,
            OutputKey = positiveKey,
            Prompt = CharacterReferenceSlotCatalog.ComposePrompt(slot),
            ImageRefKey = scaledImageKey
        });

        _encodeFragment.Build(builder, registry, new QwenImageEditPlusProEncodeFragment.Parameters
        {
            NodeId = negativeNodeId,
            OutputKey = negativeKey,
            Prompt = string.IsNullOrWhiteSpace(slot.NegativePromptOverride)
                ? fragmentParams.GlobalNegativePrompt
                : slot.NegativePromptOverride,
            ImageRefKey = scaledImageKey
        });

        builder.AddNode(samplerNodeId, node => node
            .Type("KSampler")
            .Title($"{slot.Label} Sampler")
            .Input("seed", resolvedSeed)
            .Input("steps", slot.Steps)
            .Input("cfg", slot.Cfg)
            .Input("sampler_name", slot.SamplerName)
            .Input("scheduler", slot.Scheduler)
            .Input("denoise", slot.Denoise)
            .InputRef("model", (cfgNormNodeId, 0))
            .InputRef("positive", registry.GetRef(positiveKey))
            .InputRef("negative", registry.GetRef(negativeKey))
            .InputRef("latent_image", (latentNodeId, 0)));

        builder.AddNode(decodeNodeId, node => node
            .Type("VAEDecode")
            .Title($"{slot.Label} Decode")
            .InputRef("samples", (samplerNodeId, 0))
            .InputRef("vae", vaeRef));

        var finalImageRef = (nodeId: decodeNodeId, index: 0);

        if (fragmentParams.UseRtxUpscale)
        {
            builder.AddNode(rtxNodeId, node => node
                .Type("RTXVideoSuperResolution")
                .Title($"{slot.Label} RTX Upscale")
                .Input("resize_type", "scale by multiplier")
                .Input("resize_type.scale", fragmentParams.RtxMultiplier)
                .Input("quality", fragmentParams.RtxQuality)
                .InputRef("images", finalImageRef));
            finalImageRef = (rtxNodeId, 0);
        }

        if (fragmentParams.UseCleanGpu)
        {
            builder.AddNode(cleanNodeId, node => node
                .Type("easy cleanGpuUsed")
                .Title($"{slot.Label} Clean GPU")
                .InputRef("anything", finalImageRef));
            finalImageRef = (cleanNodeId, 0);
        }

        registry.Register(imageOutputKey, finalImageRef.nodeId, finalImageRef.index);

        builder.AddNode(saveNodeId, node => node
            .Type("SaveImage")
            .Title($"{slot.Label} Save")
            .Input("filename_prefix", fragmentParams.FilenamePrefix)
            .InputRef("images", finalImageRef));

        return new CharacterReferenceOutputNode(slot.Id, slot.Label, saveNodeId, fragmentParams.FilenamePrefix);
    }
}
