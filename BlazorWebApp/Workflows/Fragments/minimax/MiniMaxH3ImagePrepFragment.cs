using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Per-image preparation chain mirroring source workflow nodes 56/48/50: caps the image to a
/// megapixel budget ("scale total pixels"), snaps it to a multiple of 32, and exposes its final
/// width/height via GetImageSize. scale_method is always "area" (implied by the source preset).
/// </summary>
public class MiniMaxH3ImagePrepFragment : IFragmentBuilder
{
    public const int MultipleOf = 32;
    private const string ScaleMethod = "area";

    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_h3_image_prep",
        Type = FragmentType.Utility,
        Title = "H3 Image Prep",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodePrefix { get; set; } = "minimax_h3_image";
        public string ImageInputName { get; set; } = "";
        public double Megapixels { get; set; } = 4.0;
    }

    public void Build(ComfyWorkflowBuilder builder, GenerationParameters parameters, NodeRegistry registry, string scope = "", string scopeTitle = "")
    {
        // UI-less utility fragment; the workflow calls the explicit Parameters overload directly.
    }

    public void Build(ComfyWorkflowBuilder builder, NodeRegistry registry, Parameters p, string scope = "", string scopeTitle = "")
    {
        var megapixelsId = $"{scope}{p.NodePrefix}_resize_megapixels";
        var multipleId = $"{scope}{p.NodePrefix}_resize_multiple";
        var sizeId = $"{scope}{p.NodePrefix}_size";

        builder.AddNode(megapixelsId, node => node
            .Type("ResizeImageMaskNode")
            .Title($"{scopeTitle}Scale to Megapixels")
            .InputFromRegistry("input", registry, p.ImageInputName)
            .Input("resize_type", "scale total pixels")
            .Input("megapixels", p.Megapixels)
            .Input("scale_method", ScaleMethod));

        builder.AddNode(multipleId, node => node
            .Type("ResizeImageMaskNode")
            .Title($"{scopeTitle}Scale to Multiple of {MultipleOf}")
            .InputFromNode("input", megapixelsId, 0)
            .Input("resize_type", "scale to multiple")
            .Input("multiple", MultipleOf)
            .Input("scale_method", ScaleMethod));

        builder.AddNode(sizeId, node => node
            .Type("GetImageSize")
            .Title($"{scopeTitle}Get Image Size")
            .InputFromNode("image", multipleId, 0));

        registry.Register($"{scope}{p.NodePrefix}_resized", multipleId, 0);
        registry.Register($"{scope}{p.NodePrefix}_width", sizeId, 0);
        registry.Register($"{scope}{p.NodePrefix}_height", sizeId, 1);
    }
}
