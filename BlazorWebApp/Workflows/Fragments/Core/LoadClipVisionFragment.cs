using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that loads a CLIP Vision model using CLIPVisionLoader.
/// Supports scoping for multiple CLIP vision instances.
/// Registers clip_vision_output.
/// </summary>
public class LoadClipVisionFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "load_clip_vision",
        Type = FragmentType.Loader,
        Title = "Load CLIP Vision",
        IsHidden = true
    };

    public class Parameters
    {
        public string ClipVisionName { get; set; } = "clip_vision_h.safetensors";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        BuildInternal(builder, registry, new Parameters
        {
            ClipVisionName = fragment?.GetString("clip_vision_name", "clip_vision_h.safetensors")
                             ?? "clip_vision_h.safetensors"
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
        var nodeId = $"{scope}clip_vision_loader";

        builder.AddNode(nodeId, node => node
            .Type("CLIPVisionLoader")
            .Title($"{scopeTitle}Load CLIP Vision")
            .Input("clip_name", p.ClipVisionName));

        registry.Register($"{scope}clip_vision_output", nodeId, 0);
    }
}
