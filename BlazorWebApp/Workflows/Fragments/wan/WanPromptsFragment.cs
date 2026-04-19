using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that encodes positive and negative prompts using CLIPTextEncode.
/// Uses clip_output from registry (from LoadClipVaeFragment).
/// Registers positive_output and negative_output.
/// </summary>
public class WanPromptsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "wan_prompts",
        Type = FragmentType.Prompts,
        Title = "Prompts",
        IsHidden = true
    };

    public class Parameters
    {
        public string Positive { get; set; } = "";
        public string Negative { get; set; } = "";
        public string ClipInputName { get; set; } = "clip_output";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment("prompts");

        BuildInternal(builder, registry, new Parameters
        {
            Positive = fragment?.GetString("positive", "") ?? "",
            Negative = fragment?.GetString("negative", "") ?? ""
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
        var positiveId = $"{scope}positive_encode";
        var negativeId = $"{scope}negative_encode";
        var clipRef = registry.GetRef(p.ClipInputName);

        builder.AddNode(positiveId, node => node
            .Type("CLIPTextEncode")
            .Title($"{scopeTitle}Positive")
            .Input("text", p.Positive)
            .InputRef("clip", clipRef));

        builder.AddNode(negativeId, node => node
            .Type("CLIPTextEncode")
            .Title($"{scopeTitle}Negative")
            .Input("text", p.Negative)
            .InputRef("clip", clipRef));

        registry.Register($"{scope}positive_output", positiveId, 0);
        registry.Register($"{scope}negative_output", negativeId, 0);
    }
}
