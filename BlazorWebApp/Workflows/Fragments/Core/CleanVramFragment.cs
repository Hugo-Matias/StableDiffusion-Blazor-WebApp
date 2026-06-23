using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that inserts a VRAM cleanup passthrough node.
/// Takes any input, passes it through "easy cleanGpuUsed", registers the output under a new name.
/// </summary>
public class CleanVramFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "clean_vram",
        Type = FragmentType.Utility,
        Title = "Clean VRAM",
        IsHidden = true
    };

    public class Parameters
    {
        public string NodeId { get; set; } = "clean_vram";
        public string InputName { get; set; } = "latent_output";
        public string OutputName { get; set; } = "cleaned_latent_output";
        public string Title { get; set; } = "Clean VRAM Used";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        BuildInternal(builder, registry, new Parameters(), scope, scopeTitle);
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
        var nodeId = $"{scope}{p.NodeId}";
        var inputRef = registry.GetRef(p.InputName);

        builder.AddNode(nodeId, node => node
            .Type("easy cleanGpuUsed")
            .Title($"{scopeTitle}{p.Title}")
            .InputRef("anything", inputRef));

        registry.Register(p.OutputName, nodeId, 0);
    }
}
