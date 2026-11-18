using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that configures WanVideo context options for sliding window generation.
/// Registers context_options.
/// </summary>
public class ContextOptionsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "context_options",
        Type = FragmentType.Settings,
        Title = "Context Options",
        IsHidden = true
    };

    public class Parameters
    {
        public string ContextSchedule { get; set; } = "uniform_standard";
        public int ContextFrames { get; set; } = 81;
        public int ContextStride { get; set; } = 4;
        public int ContextOverlap { get; set; } = 16;
        public bool Freenoise { get; set; } = true;
        public string FuseMethod { get; set; } = "linear";
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
        var nodeId = $"{scope}context_opts";

        builder.AddNode(nodeId, node => node
            .Type("WanVideoContextOptions")
            .Title($"{scopeTitle}WanVideo Context Options")
            .Input("context_schedule", p.ContextSchedule)
            .Input("context_frames", p.ContextFrames)
            .Input("context_stride", p.ContextStride)
            .Input("context_overlap", p.ContextOverlap)
            .Input("freenoise", p.Freenoise)
            .Input("verbose", false)
            .Input("fuse_method", p.FuseMethod));

        registry.Register($"{scope}context_options", nodeId, 0);
    }
}
