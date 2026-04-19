using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that encodes positive and negative prompts using CLIP.
/// Registers positive_output and negative_output in the node registry.
/// </summary>
public class PromptsFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "prompts",
        Type = FragmentType.Prompts,
        Title = "Prompts",
        Order = 10,
        Collapsible = false,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "positive",
                Label = "Positive Prompt",
                Type = ParameterType.TextArea
            },
            new FragmentParameter
            {
                Name = "negative",
                Label = "Negative Prompt",
                Type = ParameterType.TextArea
            }
        ]
    };

    /// <summary>
    /// Parameters for the prompts fragment.
    /// </summary>
    public class Parameters
    {
        public string Positive { get; set; } = "";
        public string Negative { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var positive = fragment?.GetString("positive", "") ?? "";
        var negative = fragment?.GetString("negative", "") ?? "";

        BuildInternal(builder, registry, positive, negative, scope);
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters fragmentParams,
        string scope = "")
    {
        BuildInternal(builder, registry, fragmentParams.Positive, fragmentParams.Negative, scope);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        string positive,
        string negative,
        string scope)
    {
        // Get clip reference from registry (with scope support)
        var clipRef = registry.GetRef($"{scope}clip_output");

        builder.AddNode("positive_encode", node => node
            .Type("CLIPTextEncode")
            .Title("CLIP Text Encode-Positive")
            .Input("text", positive)
            .InputRef("clip", clipRef));

        builder.AddNode("negative_encode", node => node
            .Type("CLIPTextEncode")
            .Title("CLIP Text Encode-Negative")
            .Input("text", negative)
            .InputRef("clip", clipRef));

        // Register outputs (prompts don't use scope prefix for outputs)
        registry.Register("positive_output", "positive_encode", 0);
        registry.Register("negative_output", "negative_encode", 0);
    }
}
