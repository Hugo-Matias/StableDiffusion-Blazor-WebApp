using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxLoadOptionalImageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_optional_image",
        Type = FragmentType.Input,
        Title = "LTX Optional Image",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var loadId = $"{scope}{p.NodePrefix}_load_image";
        var resizeId = $"{scope}{p.NodePrefix}_resize_image";
        var preprocessId = $"{scope}{p.NodePrefix}_preprocess";

        builder.AddNode(loadId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}{p.Title} Load Image")
            .Input("image", p.ImagePath));

        builder.AddNode(resizeId, node => node
            .Type("ResizeImageMaskNode")
            .Title($"{scopeTitle}{p.Title} Resize")
            .InputFromNode("input", loadId, 0)
            .Input("resize_type", "scale dimensions")
            .Input("resize_type.width", p.Width)
            .Input("resize_type.height", p.Height)
            .Input("resize_type.crop", "center")
            .Input("scale_method", "lanczos"));

        builder.AddNode(preprocessId, node => node
            .Type("LTXVPreprocess")
            .Title($"{scopeTitle}{p.Title} LTXVPreprocess")
            .InputFromNode("image", resizeId, 0)
            .Input("img_compression", p.ImgCompression));

        registry.Register($"{scope}{p.OutputName}", preprocessId, 0);
    }

    public class Parameters
    {
        public string NodePrefix { get; set; } = "ltx_image";
        public string Title { get; set; } = "Image";
        public string ImagePath { get; set; } = "";
        public string OutputName { get; set; } = "ltx_frame_image";
        public int Width { get; set; } = 1728;
        public int Height { get; set; } = 1152;
        public int ImgCompression { get; set; } = 35;
    }
}