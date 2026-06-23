using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

public class WanFunInpaintImagePairFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_fun_inpaint_image_pair",
        Type = FragmentType.Input,
        Title = "Start / End Images",
        IsHidden = true
    };

    public class Parameters
    {
        public string StartImagePath { get; set; } = "";
        public string EndImagePath { get; set; } = "";
        public int MaxLongerEdge { get; set; } = 640;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "",
        string scopeTitle = "")
    {
        var startLoadId = $"{scope}load_start_image";
        var startLongerEdgeId = $"{scope}start_longer_edge";
        var startSizeId = $"{scope}start_ceiling_size";
        var startResizeId = $"{scope}resize_start_image";
        var endLoadId = $"{scope}load_end_image";
        var endResizeId = $"{scope}resize_end_image";

        builder.AddNode(startLoadId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}Load Start Image")
            .Input("image", fragmentParams.StartImagePath));

        builder.AddNode(startLongerEdgeId, node => node
            .Type("ResizeImagesByLongerEdge")
            .Title($"{scopeTitle}Start Longer Edge Ceiling")
            .InputFromNode("images", startLoadId, 0)
            .Input("longer_edge", fragmentParams.MaxLongerEdge));

        builder.AddNode(startSizeId, node => node
            .Type("GetImageSizeAndCount")
            .Title($"{scopeTitle}Start Ceiling Size")
            .InputFromNode("image", startLongerEdgeId, 0));

        builder.AddNode(startResizeId, node => node
            .Type("ImageResizeKJv2")
            .Title($"{scopeTitle}Resize Start Image")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "resize")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", startLongerEdgeId, 0)
            .InputFromNode("width", startSizeId, 1)
            .InputFromNode("height", startSizeId, 2));

        builder.AddNode(endLoadId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}Load End Image")
            .Input("image", fragmentParams.EndImagePath));

        builder.AddNode(endResizeId, node => node
            .Type("ImageResizeKJv2")
            .Title($"{scopeTitle}Resize End Image")
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "crop")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", endLoadId, 0)
            .InputFromNode("width", startResizeId, 1)
            .InputFromNode("height", startResizeId, 2));

        registry.Register("start_image_output", startResizeId, 0);
        registry.Register("end_image_output", endResizeId, 0);
        registry.Register("width", startResizeId, 1);
        registry.Register("height", startResizeId, 2);
    }
}
