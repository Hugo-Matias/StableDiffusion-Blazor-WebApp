using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.MiniMax;

/// <summary>
/// Loads a single optional guiding frame (first or last) for MiniMax H3 Image-to-Video.
/// No resize is applied - the raw image is passed straight to MiniMaxH3ImageToVideo, which
/// resizes internally against the explicit width/height inputs. Reusable for both frame slots.
/// </summary>
public class MiniMaxLoadImageFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "minimax_load_image",
        Type = FragmentType.Input,
        Title = "MiniMax Optional Frame",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodePrefix { get; set; } = "minimax_frame";
        public string Title { get; set; } = "Frame";
        public string ImagePath { get; set; } = "";
        public string OutputName { get; set; } = "minimax_frame_image";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        // UI-less utility fragment; the workflow calls the explicit Parameters overload directly.
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var loadId = $"{scope}{p.NodePrefix}_load_image";

        builder.AddNode(loadId, node => node
            .Type("LoadImage")
            .Title($"{scopeTitle}{p.Title}")
            .Input("image", p.ImagePath));

        registry.Register($"{scope}{p.OutputName}", loadId, 0);
    }
}
