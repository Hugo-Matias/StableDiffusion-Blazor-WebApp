using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

/// <summary>
/// Fragment that loads and preprocesses a source image for LTX Img2Vid.
/// Pipeline: LoadImage -> ResizeImageMaskNode -> ResizeImagesByLongerEdge -> LTXVPreprocess.
/// Registers: preprocessed_image.
/// </summary>
public class LtxLoadImageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_load_image",
        Type = FragmentType.Input,
        Title = "Resolution",
        Component = "LatentForm",
        Icon = "fa-solid fa-image",
        Order = 20,
        Collapsible = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "width",
                Label = "Width",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 32,
                DefaultValue = 1280
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 32,
                DefaultValue = 720
            },
            new FragmentParameter
            {
                Name = "batch_size",
                Label = "Batch Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 4,
                Step = 1,
                DefaultValue = 1
            }
        ]
    };

    public class Parameters
    {
        public string ImagePath { get; set; } = "";
        public int Width { get; set; } = 1280;
        public int Height { get; set; } = 720;
        public int LongerEdge { get; set; } = 1536;
        public int ImgCompression { get; set; } = 18;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var source = parameters.Sources?.GetValueOrDefault("source_image");
        var imagePath = source?.FilePath ?? source?.Filename ?? "";

        var videoSettings = parameters.GetFragment("ltx_video_settings");
        var imgCompression = videoSettings?.GetInt("img_compression", 18) ?? 18;

        BuildInternal(builder, registry, new Parameters
        {
            ImagePath = imagePath,
            Width = fragment?.GetInt("width", 1280) ?? 1280,
            Height = fragment?.GetInt("height", 720) ?? 720,
            ImgCompression = imgCompression
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
        var loadId = $"{scope}load_image";
        var resizeId = $"{scope}resize_image";
        var longerEdgeId = $"{scope}resize_longer_edge";
        var preprocessId = $"{scope}ltx_preprocess";

        // LoadImage
        builder.AddNode(loadId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}Load Image")
            .Input("image", p.ImagePath));

        // ResizeImageMaskNode - resize to target dimensions
        builder.AddNode(resizeId, node => node
            .Type("ResizeImageMaskNode")
            .Title($"{scopeTitle}Resize Image")
            .Input("resize_type", "scale dimensions")
            .Input("resize_type.width", p.Width)
            .Input("resize_type.height", p.Height)
            .Input("resize_type.crop", "center")
            .Input("scale_method", "lanczos")
            .InputFromNode("input", loadId, 0));

        // ResizeImagesByLongerEdge - cap to max edge
        builder.AddNode(longerEdgeId, node => node
            .Type("ResizeImagesByLongerEdge")
            .Title($"{scopeTitle}Resize Images by Longer Edge")
            .Input("longer_edge", p.LongerEdge)
            .InputFromNode("images", resizeId, 0));

        // LTXVPreprocess - apply compression
        builder.AddNode(preprocessId, node => node
            .Type("LTXVPreprocess")
            .Title($"{scopeTitle}LTXVPreprocess")
            .Input("img_compression", p.ImgCompression)
            .InputFromNode("image", longerEdgeId, 0));

        registry.Register($"{scope}preprocessed_image", preprocessId, 0);
    }
}
