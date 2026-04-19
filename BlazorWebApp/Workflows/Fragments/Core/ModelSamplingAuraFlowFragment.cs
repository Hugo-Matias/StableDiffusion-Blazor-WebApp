using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Core;

/// <summary>
/// Fragment that applies ModelSamplingAuraFlow to the current model.
/// Overwrites model_output in the node registry with the shifted model.
/// </summary>
public class ModelSamplingAuraFlowFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "model_sampling",
        Type = FragmentType.Loader,
        Title = "Model Sampling (AuraFlow)",
        IsHidden = true
    };

    /// <summary>
    /// Parameters for the model sampling auraflow fragment.
    /// </summary>
    public class Parameters
    {
        public double ModelShift { get; set; } = 3.0;
        public string Scope { get; set; } = "";
        public string ScopeTitle { get; set; } = "";
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        var fragment = parameters.GetFragment(Metadata.Id);
        var modelShift = fragment?.GetDouble("model_shift", 3.0) ?? 3.0;

        BuildInternal(builder, registry, new Parameters
        {
            ModelShift = modelShift,
            Scope = scope,
            ScopeTitle = scopeTitle
        });
    }

    /// <summary>
    /// Builds the fragment with explicit parameters.
    /// </summary>
    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters fragmentParams)
    {
        BuildInternal(builder, registry, fragmentParams);
    }

    private static void BuildInternal(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p)
    {
        var scope = p.Scope;
        var modelRef = registry.GetRef($"{scope}model_output");

        builder.AddNode($"{scope}model_sampler_auraflow", node => node
            .Type("ModelSamplingAuraFlow")
            .Title($"{p.ScopeTitle}Model Sampler (AuraFlow)")
            .InputRef("model", modelRef)
            .Input("shift", p.ModelShift));

        // Overwrite model_output with the shifted model
        registry.Register($"{scope}model_output", $"{scope}model_sampler_auraflow", 0);
    }
}
