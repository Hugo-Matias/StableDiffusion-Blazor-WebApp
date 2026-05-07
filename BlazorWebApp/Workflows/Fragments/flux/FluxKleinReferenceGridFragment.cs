using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Flux;

/// <summary>
/// Builds a 2x2 image grid from four reference images.
/// </summary>
public class FluxKleinReferenceGridFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "grid_reference_grid",
        Type = FragmentType.Utility,
        Title = "Grid Reference Builder",
        IsHidden = true
    };

    public class Parameters
    {
        public IReadOnlyList<string> Images { get; init; } = [];
        public double GridTileMegapixels { get; init; } = 1.0;
        public double GridMegapixels { get; init; } = 4.0;
        public string UpscaleMethod { get; init; } = "lanczos";
        public int ResolutionSteps { get; init; } = 1;
    }

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
        Parameters fragmentParams)
    {
        for (var i = 0; i < 4; i++)
        {
            var imagePath = i < fragmentParams.Images.Count ? fragmentParams.Images[i] : string.Empty;
            var loadId = $"grid_ref_load_{i + 1}";
            var scaleId = $"grid_ref_scale_{i + 1}";

            builder.AddNode(loadId, node => node
                .Type("LoadImage")
                .Title($"Load Grid Reference {i + 1}")
                .Input("image", imagePath));

            builder.AddNode(scaleId, node => node
                .Type("ImageScaleToTotalPixels")
                .Title($"Scale Grid Reference {i + 1}")
                .Input("upscale_method", fragmentParams.UpscaleMethod)
                .Input("megapixels", fragmentParams.GridTileMegapixels)
                .Input("resolution_steps", fragmentParams.ResolutionSteps)
                .InputFromNode("image", loadId, 0));
        }

        Stitch(builder, "grid_ref_row_top", "Grid Reference Top Row", "right", "grid_ref_scale_1", "grid_ref_scale_2");
        Stitch(builder, "grid_ref_row_bottom", "Grid Reference Bottom Row", "right", "grid_ref_scale_3", "grid_ref_scale_4");
        Stitch(builder, "grid_ref_grid", "Grid Reference Grid", "down", "grid_ref_row_top", "grid_ref_row_bottom");

        builder.AddNode("grid_ref_grid_scale", node => node
            .Type("ImageScaleToTotalPixels")
            .Title("Scale Grid Reference Grid")
            .Input("upscale_method", fragmentParams.UpscaleMethod)
            .Input("megapixels", fragmentParams.GridMegapixels)
            .Input("resolution_steps", fragmentParams.ResolutionSteps)
            .InputFromNode("image", "grid_ref_grid", 0));

        registry.Register("grid_reference_image", "grid_ref_grid_scale", 0);
    }

    private static void Stitch(
        ComfyWorkflowBuilder builder,
        string nodeId,
        string title,
        string direction,
        string image1NodeId,
        string image2NodeId)
    {
        builder.AddNode(nodeId, node => node
            .Type("ImageStitch")
            .Title(title)
            .Input("direction", direction)
            .Input("match_image_size", true)
            .Input("spacing_width", 0)
            .Input("spacing_color", "white")
            .InputFromNode("image1", image1NodeId, 0)
            .InputFromNode("image2", image2NodeId, 0));
    }
}