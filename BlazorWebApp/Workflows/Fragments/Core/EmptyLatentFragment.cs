using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that creates an empty latent image for txt2img workflows.
/// Registers latent_output in the node registry.
/// </summary>
public class EmptyLatentFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "latent",
        Type = FragmentType.Latent,
        Title = "Resolution",
        Component = "LatentForm",
        Icon = "fa-solid fa-expand",
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
                Max = 4096,
                Step = 8,
                DefaultValue = 1024
            },
            new FragmentParameter
            {
                Name = "height",
                Label = "Height",
                Type = ParameterType.Slider,
                Min = 64,
                Max = 4096,
                Step = 8,
                DefaultValue = 1024
            },
            new FragmentParameter
            {
                Name = "batch_size",
                Label = "Batch Size",
                Type = ParameterType.Slider,
                Min = 1,
                Max = 16,
                Step = 1,
                DefaultValue = 1
            }
        ]
    };

    /// <summary>
    /// Parameters for the empty latent fragment.
    /// </summary>
    public class Parameters
    {
        public int Width { get; set; } = 1024;
        public int Height { get; set; } = 1024;
        public int BatchSize { get; set; } = 1;
        public string LatentClass { get; set; } = "EmptySD3LatentImage";
        /// <summary>
        /// Optional registry reference for width. When set, uses InputRef instead of scalar Input.
        /// </summary>
        public (string nodeId, int index)? WidthRef { get; set; }
        /// <summary>
        /// Optional registry reference for height. When set, uses InputRef instead of scalar Input.
        /// </summary>
        public (string nodeId, int index)? HeightRef { get; set; }
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);

        var width = fragment?.GetInt("width", 1024) ?? 1024;
        var height = fragment?.GetInt("height", 1024) ?? 1024;
        var batchSize = fragment?.GetInt("batch_size", 1) ?? 1;
        var latentClass = fragment?.GetString("latent_class", "EmptySD3LatentImage") ?? "EmptySD3LatentImage";

        BuildInternal(builder, registry, new Parameters
        {
            Width = width,
            Height = height,
            BatchSize = batchSize,
            LatentClass = latentClass
        }, scope, scopeTitle);
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
        var nodeId = $"{scope}empty_latent";

        builder.AddNode(nodeId, node =>
        {
            node.Type(p.LatentClass)
                .Title($"{scopeTitle}Empty Latent Image")
                .Input("batch_size", p.BatchSize);

            if (p.WidthRef.HasValue)
                node.InputRef("width", p.WidthRef.Value);
            else
                node.Input("width", p.Width);

            if (p.HeightRef.HasValue)
                node.InputRef("height", p.HeightRef.Value);
            else
                node.Input("height", p.Height);
        });

        registry.Register($"{scope}latent_output", nodeId, 0);
    }
}
