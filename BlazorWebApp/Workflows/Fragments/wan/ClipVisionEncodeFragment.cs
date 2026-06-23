using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that loads a CLIP Vision model and encodes an image with it.
/// Creates CLIPVisionLoader + CLIPVisionEncode nodes.
/// Uses original_image_output (unresized) from registry for encoding.
/// Registers clip_vision_output.
/// </summary>
public class ClipVisionEncodeFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "clip_vision_encode",
        Type = FragmentType.Conditioning,
        Title = "CLIP Vision Encode",
        IsHidden = true
    };

    public class Parameters
    {
        public string ClipVisionName { get; set; } = "clip_vision_h.safetensors";
        public string ImageInputName { get; set; } = "original_image_output";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters
        {
            ClipVisionName = parameters.Assets?.GetValueOrDefault("ClipVision") ?? "clip_vision_h.safetensors"
        }, scope, scopeTitle);
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
        var loaderId = $"{scope}clip_vision_loader";
        var encodeId = $"{scope}clip_vision_encode";
        var imageRef = registry.GetRef(p.ImageInputName);

        builder.AddNode(loaderId, node => node
            .Type("CLIPVisionLoader")
            .Title($"{scopeTitle}Load CLIP Vision")
            .Input("clip_name", p.ClipVisionName));

        builder.AddNode(encodeId, node => node
            .Type("CLIPVisionEncode")
            .Title($"{scopeTitle}CLIP Vision Encode")
            .Input("crop", "none")
            .InputFromNode("clip_vision", loaderId, 0)
            .InputRef("image", imageRef));

        registry.Register($"{scope}clip_vision_output", encodeId, 0);
    }
}
