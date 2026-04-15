using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that encodes an image for WanVideo I2V (Image-to-Video).
/// Pipeline: WanVideoClipVisionEncode -> WanVideoImageToVideoEncode
/// Requires registry: clip_vision_output, resized_image, width, height, num_frames, vae_output.
/// Registers image_embeds.
/// </summary>
public class I2VEncodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "i2v_encode",
        Type = FragmentType.Conditioning,
        Title = "WanVideo I2V Encode",
        IsHidden = true
    };

    public class Parameters
    {
        public double ClipStrength { get; set; } = 1;
        public double NoiseAugStrength { get; set; } = 0;
        public double StartLatentStrength { get; set; } = 1;
        public double EndLatentStrength { get; set; } = 1;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, fragmentParams, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope,
        string scopeTitle)
    {
        var clipEncodeId = $"{scope}clip_vision_encode";
        var i2vEncodeId = $"{scope}i2v_encode";

        var clipVisionRef = registry.GetRef($"{scope}clip_vision_output");
        var resizedImageRef = registry.GetRef("resized_image");
        var widthRef = registry.GetRef("width");
        var heightRef = registry.GetRef("height");
        var numFramesRef = registry.GetRef("num_frames");
        var vaeRef = registry.GetRef($"{scope}vae_output");

        // CLIP Vision Encode (Image)
        builder.AddNode(clipEncodeId, node => node
            .Type("WanVideoClipVisionEncode")
            .Title($"{scopeTitle}CLIP Vision Encode (Image)")
            .Input("strength_1", p.ClipStrength)
            .Input("strength_2", 1.0)
            .Input("crop", "center")
            .Input("combine_embeds", "average")
            .Input("force_offload", true)
            .Input("tiles", 0)
            .Input("ratio", 0.2)
            .InputRef("clip_vision", clipVisionRef)
            .InputRef("image_1", resizedImageRef));

        // WanVideo I2V Encode
        builder.AddNode(i2vEncodeId, node => node
            .Type("WanVideoImageToVideoEncode")
            .Title($"{scopeTitle}WanVideo I2V Encode")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .InputRef("num_frames", numFramesRef)
            .Input("noise_aug_strength", p.NoiseAugStrength)
            .Input("start_latent_strength", p.StartLatentStrength)
            .Input("end_latent_strength", p.EndLatentStrength)
            .Input("force_offload", true)
            .Input("fun_or_fl2v_model", true)
            .Input("tiled_vae", false)
            .Input("augment_empty_frames", 0)
            .InputRef("vae", vaeRef)
            .InputFromNode("clip_embeds", clipEncodeId, 0)
            .InputRef("start_image", resizedImageRef));

        registry.Register($"{scope}image_embeds", i2vEncodeId, 0);
    }
}
