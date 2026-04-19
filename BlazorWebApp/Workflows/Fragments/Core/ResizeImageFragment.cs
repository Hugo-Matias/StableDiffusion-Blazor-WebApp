using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that resizes an image using ImageResizeKJv2.
/// Takes width/height from registry refs (dynamic) or explicit values.
/// Registers resized_image output.
/// </summary>
public class ResizeImageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "resize_image",
        Type = FragmentType.Utility,
        Title = "Resize Image",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "resize_image";
        public string ImageRefName { get; set; } = "image_input";
        public string WidthRefName { get; set; } = "width";
        public string HeightRefName { get; set; } = "height";
        public string UpscaleMethod { get; set; } = "lanczos";
        public string KeepProportion { get; set; } = "crop";
        public string CropPosition { get; set; } = "center";
        public string OutputName { get; set; } = "resized_image";
        public string Title { get; set; } = "Resize Image";
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
        var nodeId = $"{scope}{p.NodeId}";
        var imageRef = registry.GetRef(p.ImageRefName);
        var widthRef = registry.GetRef(p.WidthRefName);
        var heightRef = registry.GetRef(p.HeightRefName);

        builder.AddNode(nodeId, node => node
            .Type("ImageResizeKJv2")
            .Title($"{scopeTitle}{p.Title}")
            .InputRef("width", widthRef)
            .InputRef("height", heightRef)
            .Input("upscale_method", p.UpscaleMethod)
            .Input("keep_proportion", p.KeepProportion)
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", p.CropPosition)
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputRef("image", imageRef));

        registry.Register($"{scope}{p.OutputName}", nodeId, 0);
    }
}
