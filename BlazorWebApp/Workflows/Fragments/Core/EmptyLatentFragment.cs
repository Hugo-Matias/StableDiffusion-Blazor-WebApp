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

        BuildInternal(builder, registry, width, height, batchSize, latentClass, scope, scopeTitle);
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
        BuildInternal(builder, registry, fragmentParams.Width, fragmentParams.Height, 
                      fragmentParams.BatchSize, fragmentParams.LatentClass, scope, scopeTitle);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        int width,
        int height,
        int batchSize,
        string latentClass,
        string scope,
        string scopeTitle)
    {
        var nodeId = $"{scope}empty_latent";

        builder.AddNode(nodeId, node => node
            .Type(latentClass)
            .Title($"{scopeTitle}Empty Latent Image")
            .Input("width", width)
            .Input("height", height)
            .Input("batch_size", batchSize));

        registry.Register($"{scope}latent_output", nodeId, 0);
    }
}
