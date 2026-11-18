using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Enhancements;

/// <summary>
/// Fragment that applies conditioning variation for improved generation quality.
/// Overwrites positive_output and negative_output in the node registry.
/// Conditional: Only builds when conditioning_variation.IsActive is true.
/// </summary>
public class ConditioningVariationFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "conditioning_variation",
        Type = FragmentType.Enhancement,
        Title = "Conditioning Variation",
        Component = "ConditioningVariationForm",
        Icon = "fa-solid fa-code-branch",
        Order = 75,
        Collapsible = true,
        DefaultCollapsed = true,
        Parameters =
        [
            new FragmentParameter
            {
                Name = "switch_point",
                Label = "Switch Point",
                Type = ParameterType.Slider,
                Min = 0,
                Max = 1,
                Step = 0.05,
                DefaultValue = 0.2
            }
        ]
    };

    /// <summary>
    /// Parameters for the conditioning variation fragment.
    /// </summary>
    public class Parameters
    {
        public double SwitchPoint { get; set; } = 0.2;
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        Builders.NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        
        // Check if fragment is active
        if (fragment?.IsActive != true)
            return;

        var switchPoint = fragment.GetDouble("switch_point", 0.2);
        BuildInternal(builder, registry, new Parameters { SwitchPoint = switchPoint }, scope);
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
        BuildInternal(builder, registry, fragmentParams, scope);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        Builders.NodeRegistry registry,
        Parameters p,
        string scope)
    {
        // Get references
        var clipRef = registry.GetRef($"{scope}clip_output");
        var positiveRef = registry.GetRef("positive_output");
        var negativeRef = registry.GetRef("negative_output");

        // Create empty encode node
        builder.AddNode("empty_encode", node => node
            .Type("CLIPTextEncode")
            .Title("Empty Encode")
            .Input("text", "")
            .InputRef("clip", clipRef));

        // Create conditioning range 1 (empty, 0 to switch_point)
        builder.AddNode("conditioning_range_1", node => node
            .Type("ConditioningSetTimestepRange")
            .Title("Conditioning - Empty Range")
            .Input("start", 0)
            .Input("end", p.SwitchPoint)
            .InputRef("conditioning", ("empty_encode", 0)));

        // Create conditioning range 2 (prompt, switch_point to 1)
        builder.AddNode("conditioning_range_2", node => node
            .Type("ConditioningSetTimestepRange")
            .Title("Conditioning - Prompt Range")
            .Input("start", p.SwitchPoint)
            .Input("end", 1)
            .InputRef("conditioning", positiveRef));

        // Combine conditioning
        builder.AddNode("combine_conditioning", node => node
            .Type("ConditioningCombine")
            .Title("Combine Conditioning")
            .InputRef("conditioning_1", ("conditioning_range_1", 0))
            .InputRef("conditioning_2", ("conditioning_range_2", 0)));

        // Zero out negative conditioning
        builder.AddNode("conditioning_zero_out", node => node
            .Type("ConditioningZeroOut")
            .Title("Conditioning Zero Out")
            .InputRef("conditioning", negativeRef));

        // Overwrite outputs
        registry.Register("positive_output", "combine_conditioning", 0);
        registry.Register("negative_output", "conditioning_zero_out", 0);
    }
}
