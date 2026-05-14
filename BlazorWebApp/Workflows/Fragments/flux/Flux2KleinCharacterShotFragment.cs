using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;

namespace BlazorWebApp.Workflows.Fragments.Flux;

public class Flux2KleinCharacterShotFragment
{
    public CharacterReferenceOutputNode Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        CharacterReferenceSlotState slot,
        (string nodeId, int outputIndex) sourceImageRef,
        string filenamePrefix,
        string nodePrefix,
        string globalPositivePromptExtension,
        string globalNegativePrompt)
    {
        var prompt = CharacterReferenceSlotCatalog.ComposePrompt(slot, globalPositivePromptExtension);
        var negativePrompt = string.IsNullOrWhiteSpace(slot.NegativePromptOverride)
            ? globalNegativePrompt
            : slot.NegativePromptOverride;

        var positiveEncodeNodeId = $"{nodePrefix}positive_encode";
        var negativeEncodeNodeId = $"{nodePrefix}negative_encode";
        var imageScaleNodeId = $"{nodePrefix}reference_scale";
        var getImageSizeNodeId = $"{nodePrefix}reference_size";
        var vaeEncodeNodeId = $"{nodePrefix}reference_vae_encode";
        var positiveReferenceNodeId = $"{nodePrefix}reference_positive";
        var negativeReferenceNodeId = $"{nodePrefix}reference_negative";
        var emptyLatentNodeId = $"{nodePrefix}empty_latent";
        var noiseNodeId = $"{nodePrefix}noise";
        var samplerSelectNodeId = $"{nodePrefix}sampler_select";
        var schedulerNodeId = $"{nodePrefix}flux2_scheduler";
        var guiderNodeId = $"{nodePrefix}cfg_guider";
        var samplerNodeId = $"{nodePrefix}sampler";
        var decodeNodeId = $"{nodePrefix}vae_decode";
        var saveNodeId = $"{nodePrefix}save";
        var noiseSeed = NormalizeNoiseSeed(slot.Seed);

        builder.AddNode(positiveEncodeNodeId, node => node
            .Type("CLIPTextEncode")
            .Title($"{slot.Label} Positive Prompt")
            .Input("text", prompt)
            .InputRef("clip", registry.GetRef("clip_output")));

        builder.AddNode(negativeEncodeNodeId, node => node
            .Type("CLIPTextEncode")
            .Title($"{slot.Label} Negative Prompt")
            .Input("text", negativePrompt ?? string.Empty)
            .InputRef("clip", registry.GetRef("clip_output")));

        builder.AddNode(imageScaleNodeId, node => node
            .Type("ImageScaleToTotalPixels")
            .Title($"{slot.Label} Reference Scale")
            .Input("upscale_method", "lanczos")
            .Input("megapixels", 1.0)
            .Input("resolution_steps", 1)
            .InputRef("image", sourceImageRef));

        builder.AddNode(getImageSizeNodeId, node => node
            .Type("GetImageSize")
            .Title($"{slot.Label} Reference Size")
            .InputFromNode("image", imageScaleNodeId, 0));

        builder.AddNode(vaeEncodeNodeId, node => node
            .Type("VAEEncode")
            .Title($"{slot.Label} Reference VAE Encode")
            .InputFromNode("pixels", imageScaleNodeId, 0)
            .InputRef("vae", registry.GetRef("vae_output")));

        builder.AddNode(positiveReferenceNodeId, node => node
            .Type("ReferenceLatent")
            .Title($"{slot.Label} Positive Reference Latent")
            .InputFromNode("conditioning", positiveEncodeNodeId, 0)
            .InputFromNode("latent", vaeEncodeNodeId, 0));

        builder.AddNode(negativeReferenceNodeId, node => node
            .Type("ReferenceLatent")
            .Title($"{slot.Label} Negative Reference Latent")
            .InputFromNode("conditioning", negativeEncodeNodeId, 0)
            .InputFromNode("latent", vaeEncodeNodeId, 0));

        builder.AddNode(emptyLatentNodeId, node => node
            .Type("EmptyFlux2LatentImage")
            .Title($"{slot.Label} Empty Flux 2 Latent")
            .InputFromNode("width", getImageSizeNodeId, 0)
            .InputFromNode("height", getImageSizeNodeId, 1)
            .Input("batch_size", 1));

        builder.AddNode(noiseNodeId, node => node
            .Type("RandomNoise")
            .Title($"{slot.Label} Noise")
            .Input("noise_seed", noiseSeed));

        builder.AddNode(samplerSelectNodeId, node => node
            .Type("KSamplerSelect")
            .Title($"{slot.Label} Sampler")
            .Input("sampler_name", slot.SamplerName));

        builder.AddNode(schedulerNodeId, node => node
            .Type("Flux2Scheduler")
            .Title($"{slot.Label} Flux 2 Scheduler")
            .Input("steps", slot.Steps)
            .InputFromNode("width", getImageSizeNodeId, 0)
            .InputFromNode("height", getImageSizeNodeId, 1));

        builder.AddNode(guiderNodeId, node => node
            .Type("CFGGuider")
            .Title($"{slot.Label} CFG Guider")
            .Input("cfg", slot.Cfg)
            .InputRef("model", registry.GetRef("model_output"))
            .InputFromNode("positive", positiveReferenceNodeId, 0)
            .InputFromNode("negative", negativeReferenceNodeId, 0));

        builder.AddNode(samplerNodeId, node => node
            .Type("SamplerCustomAdvanced")
            .Title($"{slot.Label} Sampler")
            .InputFromNode("noise", noiseNodeId, 0)
            .InputFromNode("guider", guiderNodeId, 0)
            .InputFromNode("sampler", samplerSelectNodeId, 0)
            .InputFromNode("sigmas", schedulerNodeId, 0)
            .InputFromNode("latent_image", emptyLatentNodeId, 0));

        builder.AddNode(decodeNodeId, node => node
            .Type("VAEDecode")
            .Title($"{slot.Label} Decode")
            .InputFromNode("samples", samplerNodeId, 0)
            .InputRef("vae", registry.GetRef("vae_output")));

        registry.Register(CharacterReferenceWorkflowIds.SlotImageOutputKey(slot.Id), decodeNodeId, 0);

        builder.AddNode(saveNodeId, node => node
            .Type("SaveImage")
            .Title($"{slot.Label} Save")
            .Input("filename_prefix", filenamePrefix)
            .InputFromNode("images", decodeNodeId, 0));

        return new CharacterReferenceOutputNode(slot.Id, slot.Label, saveNodeId, filenamePrefix);
    }

    private static long NormalizeNoiseSeed(long seed)
    {
        return seed < 0 ? Random.Shared.NextInt64(0, long.MaxValue) : seed;
    }
}