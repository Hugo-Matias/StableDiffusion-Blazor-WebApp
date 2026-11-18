using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that upscales images/video frames using NVIDIA RTX Video Super Resolution.
/// Overwrites image_output in the node registry. Conditional: only builds when rtx_upscale.IsActive is true.
/// </summary>
public class RTXUpscaleFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "rtx_upscale",
        Type = FragmentType.Enhancement,
        Title = "RTX Super Resolution",
        Component = "RTXUpscaleForm",
        Icon = "fa-solid fa-microchip",
        Order = 110,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "resize_mode",
                Label = "Resize Mode",
                Type = ParameterType.Select,
                Options = ["multiplier", "dimensions"],
                DefaultValue = "multiplier"
            },
            new FragmentParameter
            {
                Name = "scale",
                Label = "Scale",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 4,
                Step = 0.01,
                DefaultValue = 2.0
            },
            new FragmentParameter
            {
                Name = "width",
                Label = "Target Width",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 8192,
                Step = 8,
                DefaultValue = 1920
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Target Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 8192,
                Step = 8,
                DefaultValue = 1080
            },
            new FragmentParameter
            {
                Name = "quality",
                Label = "Quality",
                Type = ParameterType.Select,
                Options = ["LOW", "MEDIUM", "HIGH", "ULTRA"],
                DefaultValue = "ULTRA"
            }
        ]
    };

    /// <summary>
    /// Parameters for the RTX upscale fragment.
    /// </summary>
    public class Parameters
    {
        public string ResizeMode { get; set; } = "multiplier";
        public double Scale { get; set; } = 2.0;
        public int Width { get; set; } = 1920;
        public int Height { get; set; } = 1080;
        public string Quality { get; set; } = "ULTRA";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        if (fragment?.IsActive != true)
            return;

        var p = new Parameters
        {
            ResizeMode = fragment.GetString("resize_mode", "multiplier"),
            Scale = fragment.GetDouble("scale", 2.0),
            Width = fragment.GetInt("width", 1920),
            Height = fragment.GetInt("height", 1080),
            Quality = fragment.GetString("quality", "ULTRA")
        };

        BuildInternal(builder, registry, p, scope, scopeTitle);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
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
        var imageRef = registry.GetRef($"{scope}image_output");
        var nodeId = $"{scope}rtx_upscale";
        var useDimensions = string.Equals(p.ResizeMode, "dimensions", StringComparison.OrdinalIgnoreCase);

        builder.AddNode(nodeId, node =>
        {
            node.Type("RTXVideoSuperResolution")
                .Title($"{scopeTitle}RTX Super Resolution");

            if (useDimensions)
            {
                node.Input("resize_type", "target dimensions")
                    .Input("resize_type.width", p.Width)
                    .Input("resize_type.height", p.Height);
            }
            else
            {
                node.Input("resize_type", "scale by multiplier")
                    .Input("resize_type.scale", p.Scale);
            }

            node.Input("quality", p.Quality)
                .InputRef("images", imageRef);
        });

        registry.Register($"{scope}image_output", nodeId, 0);
    }
}
