using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Wan;

/// <summary>
/// Fragment that applies ModelSamplingSD3 with a shift parameter to a model.
/// Used in Wan Img2Vid to configure sampling for high/low noise models.
/// All node IDs and registry names are fully parameterized.
/// </summary>
public class ModelSamplingSD3Fragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "model_sampling_sd3",
        Type = FragmentType.Loader,
        Title = "Model Sampling SD3",
        IsHidden = true
    };

    public class Parameters
    {
        public string SamplerId { get; set; } = "model_sampling";
        public double Shift { get; set; } = 5;
        public string ModelInputName { get; set; } = "model_output";
        public string ModelOutputName { get; set; } = "sampled_model_output";
        public string Title { get; set; } = "ModelSamplingSD3";
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
        var nodeId = $"{scope}{p.SamplerId}";
        var modelRef = registry.GetRef(p.ModelInputName);

        builder.AddNode(nodeId, node => node
            .Type("ModelSamplingSD3")
            .Title($"{scopeTitle}{p.Title}")
            .Input("shift", p.Shift)
            .InputRef("model", modelRef));

        registry.Register(p.ModelOutputName, nodeId, 0);
    }
}
