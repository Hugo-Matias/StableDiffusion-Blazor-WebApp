using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that loads a source image and resizes it for Wan Img2Vid.
/// Creates LoadImage + ImageResizeKJv2 nodes.
/// Registers: image_output (resized), image_width_output, image_height_output, original_image_output.
/// </summary>
public class WanLoadImageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_load_image",
        Type = FragmentType.Input,
        Title = "Resolution",
        Component = "LatentForm",
        Icon = "fa-solid fa-image",
        Order = 30,
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
                DefaultValue = 768
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 2048,
                Step = 32,
                DefaultValue = 768
            },
            new FragmentParameter
            {
                Name = "batch_size",
                Label = "Batch Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 8,
                Step = 1,
                DefaultValue = 1
            }
        ]
    };

    public class Parameters
    {
        public string ImagePath { get; set; } = "";
        public int Width { get; set; } = 768;
        public int Height { get; set; } = 768;
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
        var imagePath = source?.Filename ?? source?.FilePath ?? "";

        BuildInternal(builder, registry, new Parameters
        {
            ImagePath = imagePath,
            Width = fragment?.GetInt("width", 768) ?? 768,
            Height = fragment?.GetInt("height", 768) ?? 768
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
        var resizeId = $"{scope}image_resize";

        builder.AddNode(loadId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}Load Image")
            .Input("image", p.ImagePath));

        builder.AddNode(resizeId, node => node
            .Type("ImageResizeKJv2")
            .Title($"{scopeTitle}Resize Image")
            .Input("width", p.Width)
            .Input("height", p.Height)
            .Input("upscale_method", "lanczos")
            .Input("keep_proportion", "resize")
            .Input("pad_color", "0, 0, 0")
            .Input("crop_position", "center")
            .Input("divisible_by", 16)
            .Input("device", "cpu")
            .InputFromNode("image", loadId, 0));

        registry.Register($"{scope}original_image_output", loadId, 0);
        registry.Register($"{scope}image_output", resizeId, 0);
        registry.Register($"{scope}image_width_output", resizeId, 1);
        registry.Register($"{scope}image_height_output", resizeId, 2);
    }
}
